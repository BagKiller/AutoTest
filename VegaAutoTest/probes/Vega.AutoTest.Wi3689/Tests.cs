using System.Reflection;
using Newtonsoft.Json.Linq;
using Vega.AutoTest.Harness;
using Xunit;
namespace Galaxy.Platform.ILogging { public interface ILogService { void Warning(string component,string text); void Information(string component,string text); void Error(string component,string text); } }
namespace Vega.AutoTest.Wi3689 {
public class WorkItem3689Logic {
 [Theory]
 [InlineData("array-order")][InlineData("last-metadata")][InlineData("nearest-fallback")][InlineData("single-equivalence")][InlineData("invalid-cleanup")]
 public void ExporterContract(string scenario) {
  var r=new AssertionRecorder(); var dir=Path.Combine(Path.GetTempPath(),"wi3689-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
  try {
   var docs=new[]{"{\"defect_results\":[1],\"event_results\":[11],\"fused_defect_results\":[21],\"meta\":1,\"fallback\":1}","{\"defect_results\":[2],\"event_results\":[12],\"fused_defect_results\":[22],\"meta\":2,\"fallback\":2}","{\"defect_results\":[3],\"event_results\":[13],\"fused_defect_results\":[23],\"meta\":3,\"is_final\":true}"};
   if(scenario=="single-equivalence") docs=new[]{docs[0]};
   if(scenario=="invalid-cleanup") docs=new[]{"{invalid"};
   var files=docs.Select((s,i)=>{var p=Path.Combine(dir,$"p{i}.json");File.WriteAllText(p,s);return p;}).ToList(); var output=Path.Combine(dir,"merged.json");
   var method=typeof(Vega.BL.IAnalysisServices.AnalysisResultChunkExporter).GetMethod("MergeChunkFiles",BindingFlags.Static|BindingFlags.NonPublic)!;
   if(scenario=="invalid-cleanup") { Assert.Throws<TargetInvocationException>(()=>method.Invoke(null,new object[]{files,output}));Assert.False(File.Exists(output));Assert.False(File.Exists(output+".merging"));Assert.True(File.Exists(files[0])); }
   else {method.Invoke(null,new object[]{files,output});var o=JObject.Parse(File.ReadAllText(output));
    switch(scenario){case "array-order": foreach(var f in new[]{"defect_results","event_results","fused_defect_results"}) Assert.Equal(3,((JArray)o[f]!).Count);Assert.Equal(new[]{1,2,3},o["defect_results"]!.Values<int>());break;
    case "last-metadata":Assert.Equal(3,(int)o["meta"]!);Assert.True((bool)o["is_final"]!);break;
    case "nearest-fallback":Assert.Equal(2,(int)o["fallback"]!);break;
    case "single-equivalence":Assert.True(JToken.DeepEquals(JObject.Parse(docs[0]),o));break;}
   }
   r.Pass("wi3689."+scenario,"logic",scenario,"export contract","matched");
  } catch(Exception e){r.Fail("wi3689."+scenario,"logic",scenario,"export contract",e.ToString());throw;} finally{r.Flush();}
 }
}
}
