using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Galaxy.Platform.ILogging;
using Vega.AutoTest.Harness;
using Vega.BL.DataReporting;
using Vega.BL.IAnalysisServices;
using Vega.BL.IRecipeProcessing;
using Vega.BL.WaferRun;
using Vega.Data.Analysis;
using Vega.Data.Commons;
using Vega.Data.Measurements;
using Vega.Utility.Helper;
using Xunit;
using static Vega.AutoTest.Authored.UniformitySupport;

namespace Vega.AutoTest.Authored;

public class UniformityPipelineRegression
{
    public class QuietLog : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => null;
    }
    private sealed class Processing : IRecipeProcessingService
    {
        internal int Calls; internal WaferRunInfo? Seen;
        public void postprocessWaferRun(WaferRunInfo waferRun) { Calls++; Seen = waferRun; }
        public void postprocessImageSelection(WaferRunInfo waferRun) { }
    }
    [Fact]
    public void CalibrationVisibilityPostprocessOptInAndNormalResultParsing()
    {
        var r = new AssertionRecorder();
        try
        {
            string?[] names = { null,"","DFCaliJob_1","HazeCaliJob_1","UniformityCaliJob_1","Uniformity_x","CaliJob_x","Job_xxx","DefaultJob_1" };
            bool[] classified = { false,false,true,true,true,false,false,false,false };
            for (int i = 0; i < names.Length; i++)
                Check(r,$"name-{i}",CaliJobNaming.IsCaliJob(names[i]) == classified[i],classified[i],CaliJobNaming.IsCaliJob(names[i]));
            var reporting = (DataReportingService)RuntimeHelpers.GetUninitializedObject(typeof(DataReportingService));
            var cache = new LRUCache<JobRunInfo>(8);
            string[] jobs = { "Production","UniformityCaliJob_1","DFCaliJob_1","HazeCaliJob_1","Uniformity_x" };
            for (int i = 0; i < jobs.Length; i++) cache.AddLast(new JobRunInfo { ID=i+1, JobName=jobs[i], Status=i == 1 ? RunStatus.Start : RunStatus.End });
            Set(reporting,typeof(DataReportingService),"_cachedJobRuns",cache);
            Set(reporting,typeof(DataReportingService),"_pinnedJobRunInfos",new System.Collections.Generic.List<JobRunInfo>());
            string regular = string.Join(",",reporting.GetJobRunInfos(7,false).Select(x => x.ID));
            string super = string.Join(",",reporting.GetJobRunInfos(7,true).Select(x => x.ID));
            Check(r,"regular-query",regular == "1,5","1,5",regular);
            Check(r,"super-query-running-priority",super == "2,1,3,4,5","2,1,3,4,5",super);
            var sequence = (WaferRunDFSequnce)RuntimeHelpers.GetUninitializedObject(typeof(WaferRunDFSequnce));
            var processing = new Processing();
            Set(sequence,typeof(WaferRunSequenceBase),"_recipeProcessingService",processing);
            Set(sequence,typeof(WaferRunSequenceBase),"_logger",DispatchProxy.Create<ILogService,QuietLog>());
            var wafer = (WaferRunInfo)RuntimeHelpers.GetUninitializedObject(typeof(WaferRunInfo));
            var postprocess = typeof(WaferRunDFSequnce).GetMethod("DoPerformPostprocess",BindingFlags.Instance | BindingFlags.NonPublic)!;
            postprocess.Invoke(sequence,new object[] { wafer });
            Check(r,"legacy-postprocess-disabled",processing.Calls == 0,0,processing.Calls);
            sequence.EnablePostprocess = true;
            postprocess.Invoke(sequence,new object[] { wafer });
            Check(r,"uniformity-postprocess-enabled",processing.Calls == 1,1,processing.Calls);
            Check(r,"postprocess-same-result",ReferenceEquals(wafer,processing.Seen),"original wafer",ReferenceEquals(wafer,processing.Seen));
            var concrete = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory,"Vega.BL.AnalysisServiceSim.dll")).GetType("Vega.BL.AnalysisServiceSim.AnalysisServiceSim",true)!;
            object parser = RuntimeHelpers.GetUninitializedObject(concrete);
            var parse = typeof(AnalysisServiceBase).GetMethod("ParseData",BindingFlags.Instance | BindingFlags.NonPublic)!;
            const string json = "{\"wafer_id\":\"synthetic-wafer\",\"collection_name\":\"oblique\",\"is_final\":true,\"is_overload\":true,\"overload_reason\":\"limit\",\"algo_version\":\"fixture-v1\",\"defect_results\":[],\"event_results\":[],\"fused_defect_results\":[]}";
            foreach (var mode in new[] { AnalysisMode.Default,AnalysisMode.SSMDiag,AnalysisMode.UniformityCal })
            {
                var common = (AnalysisRecipeCommonInfo)RuntimeHelpers.GetUninitializedObject(typeof(AnalysisRecipeCommonInfo));
                common.AnalysisnMode = (int)mode;
                Set(parser,typeof(AnalysisServiceBase),"_recipeCommonInfo",common);
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                var result = (AnalysisResultData)parse.Invoke(parser,new object[] { stream })!;
                Check(r,$"parse-{mode}-type",result.GetType() == typeof(AnalysisResultData),nameof(AnalysisResultData),result.GetType().Name);
                Check(r,$"parse-{mode}-wafer",result.WaferID == "synthetic-wafer","synthetic-wafer",result.WaferID);
                Check(r,$"parse-{mode}-collection",result.CollectionName == "oblique","oblique",result.CollectionName);
                Check(r,$"parse-{mode}-final",result.IsFinal,true,result.IsFinal);
                Check(r,$"parse-{mode}-metadata",result.IsOverload && result.OverloadReason == "limit" && result.AlgoVersion == "fixture-v1","overload/limit/fixture-v1",$"{result.IsOverload}/{result.OverloadReason}/{result.AlgoVersion}");
            }
        }
        finally { r.Flush(); }
    }
}
