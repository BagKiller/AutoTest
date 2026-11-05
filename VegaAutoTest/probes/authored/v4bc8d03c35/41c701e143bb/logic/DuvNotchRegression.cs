using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using OpenCvSharp;
using Vega.Algo.Imaging;
using Vega.AutoTest.Harness;
using Vega.BL.WaferAlignment;
using Vega.Data.Measurements;
using Vega.UI.Diagnostics.SystemDiagnostics.DataCollectionSubsystem;
using Vega.Utility.Common;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

internal sealed class DuvProbe
{
    private readonly AssertionRecorder recorder = new();
    internal static void Run(Action<DuvProbe> body)
    {
        var p = new DuvProbe();
        try { body(p); }
        finally { p.recorder.Flush(); }
    }
    internal void Check(bool ok, string id, object expected, object actual)
    {
        string e = Convert.ToString(expected, CultureInfo.InvariantCulture) ?? "null";
        string a = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "null";
        if (ok) recorder.Pass(id, "behavior", id, e, a);
        else { recorder.Fail(id, "behavior", id, e, a); throw new XunitException($"{id}: expected {e}; actual {a}"); }
    }
    internal void Equal<T>(T actual, T expected, string id)
        => Check(EqualityComparer<T>.Default.Equals(actual, expected), id, expected!, actual!);
    internal void Near(double actual, double expected, string id)
        => Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-6, id, expected, actual);
    internal static T Call<T>(string method, params object?[] args)
        => (T)typeof(WaferAlignmentService).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args)!;
    internal static PointInPolar[] Points(params double[] radii)
        => radii.Select((r, i) => new PointInPolar(r, i * 2.0 * Math.PI / radii.Length)).ToArray();
    internal static string Values(IEnumerable<double> values)
        => string.Join(",", values.Select(v => v.ToString("R", CultureInfo.InvariantCulture)));
    internal void Radii(PointInPolar[] actual, double[] expected, string id)
        => Check(actual.Select(v => v.Radius).SequenceEqual(expected), id, Values(expected), Values(actual.Select(v => v.Radius)));
}

public class DuvSignalRegression
{
    [Fact]
    public void PreprocessingOuterBoundaryAndCoverageContracts()
    {
        DuvProbe.Run(p =>
        {
            var medianRows = new (double[] Input, int Half, double[] Expected, bool Same)[]
            {
                (new double[]{100,100,100,40,100,100,100},1,new double[]{100,100,100,100,100,100,100},false),
                (new double[]{100,100,double.NaN,100,100},1,new double[]{100,100,100,100,100},false),
                (new double[]{100,110,120,130,140,150,160},1,new double[]{105,110,120,130,140,150,155},false),
                (new double[]{double.NaN,double.NaN,double.NaN},1,new double[]{double.NaN,double.NaN,double.NaN},false),
                (new double[]{100,double.PositiveInfinity,300},1,new double[]{100,200,300},false),
                (new double[]{100,200},0,new double[]{100,200},true),
                (Array.Empty<double>(),4,Array.Empty<double>(),true)
            };
            for (int i = 0; i < medianRows.Length; i++)
            {
                var row = medianRows[i]; var input = DuvProbe.Points(row.Input);
                var output = DuvProbe.Call<PointInPolar[]>("MedianFilterProfile", input, row.Half);
                p.Radii(output, row.Expected, $"median.{i}.radii");
                p.Check(output.Select(v => v.Theta).SequenceEqual(input.Select(v => v.Theta)), $"median.{i}.theta", "unchanged", DuvProbe.Values(output.Select(v => v.Theta)));
                p.Radii(input, row.Input, $"median.{i}.immutable");
                p.Equal(ReferenceEquals(input, output), row.Same, $"median.{i}.identity");
            }
            var bridgeRows = new (double[] Input, bool[] Mask, int Limit, double[] Expected, bool[] Valid, bool Same)[]
            {
                (new double[]{100,double.NaN,300},new[]{true,false,true},1,new double[]{100,200,300},new[]{true,true,true},false),
                (new double[]{100,double.NaN,double.NaN,400},new[]{true,false,false,true},2,new double[]{100,200,300,400},new[]{true,true,true,true},false),
                (new double[]{100,double.NaN,double.NaN,400},new[]{true,false,false,true},1,new double[]{100,double.NaN,double.NaN,400},new[]{true,false,false,true},false),
                (new double[]{double.NaN,100,400,double.NaN},new[]{false,true,true,false},2,new double[]{200,100,400,300},new[]{true,true,true,true},false),
                (new double[]{double.NaN,double.NaN},new[]{false,false},4,new double[]{double.NaN,double.NaN},new[]{false,false},true),
                (new double[]{100,200},new[]{true,false,true},4,new double[]{100,200},new[]{true,false,true},true),
                (new double[]{100,200,300},new[]{true,true,true},2,new double[]{100,200,300},new[]{true,true,true},false),
                (Array.Empty<double>(),Array.Empty<bool>(),4,Array.Empty<double>(),Array.Empty<bool>(),true),
                (new double[]{100,double.NaN,300},new[]{true,false,true},0,new double[]{100,double.NaN,300},new[]{true,false,true},false)
            };
            for (int i = 0; i < bridgeRows.Length; i++)
            {
                var row = bridgeRows[i]; var input = DuvProbe.Points(row.Input); var mask = (bool[])row.Mask.Clone();
                var output = DuvProbe.Call<(PointInPolar[], bool[])>("BridgeShortInvalidGaps", input, mask, row.Limit);
                p.Radii(output.Item1, row.Expected, $"bridge.{i}.radii");
                p.Check(output.Item2.SequenceEqual(row.Valid), $"bridge.{i}.valid", string.Join(",",row.Valid), string.Join(",",output.Item2));
                p.Check(output.Item1.Select(v => v.Theta).SequenceEqual(input.Select(v => v.Theta)), $"bridge.{i}.theta", "unchanged", DuvProbe.Values(output.Item1.Select(v => v.Theta)));
                p.Radii(input, row.Input, $"bridge.{i}.immutablePoints");
                p.Check(mask.SequenceEqual(row.Mask), $"bridge.{i}.immutableMask", "unchanged", string.Join(",",mask));
                p.Check(ReferenceEquals(input,output.Item1)==row.Same && ReferenceEquals(mask,output.Item2)==row.Same, $"bridge.{i}.identity", row.Same, ReferenceEquals(input,output.Item1));
            }
            var edgeRows = new (ushort[,] Data, double Expected)[]
            {
                (Column(80,(20,49)),49.5), (Column(80,(0,29),(35,49)),49.5),
                (Column(80,(10,41),(55,62)),62.5), (Column(80,(10,41),(55,61)),41.5),
                (Column(80,(20,49),(60,69)),69.5), (Column(80,(20,49),(55,56)),49.5),
                (Column(80),double.NaN), (Column(80,(0,79)),double.NaN),
                (Column(80,(20,79)),double.NaN), (Column(7,(2,4)),double.NaN)
            };
            for (int i=0;i<edgeRows.Length;i++)
            {
                var row=edgeRows[i]; double actual=DuvProbe.Call<double>("FindDuvNotchOuterEdgeTrack",row.Data,0,row.Data.GetLength(0));
                if (double.IsNaN(row.Expected)) p.Equal(double.IsNaN(actual),true,$"outer.{i}.honestFailure");
                else p.Near(actual,row.Expected,$"outer.{i}.crossing");
            }
            foreach (int finite in new[]{7,8,9})
            {
                var points=DuvProbe.Points(Enumerable.Range(0,10).Select(i=>i<finite?100.0:double.NaN).ToArray());
                p.Equal(DuvProbe.Call<bool>("HasSufficientOuterEdgeCoverage",(object)points),finite>=8,$"coverage.{finite}");
            }
            var badTheta=DuvProbe.Points(100,100,100,100,100,100,100,100,double.NaN,double.NaN);
            badTheta[7]=new PointInPolar(100,double.PositiveInfinity);
            p.Equal(DuvProbe.Call<bool>("HasSufficientOuterEdgeCoverage",(object)badTheta),false,"coverage.nonfiniteTheta");
            badTheta[7]=new PointInPolar(double.PositiveInfinity,0);
            p.Equal(DuvProbe.Call<bool>("HasSufficientOuterEdgeCoverage",(object)badTheta),false,"coverage.nonfiniteRadius");
            p.Equal(new EdgeDetectionConfig().DuvNotchSearchMethod,3,"config.default");
            p.Equal(JsonSerializer.Deserialize<EdgeDetectionConfig>("{}")!.DuvNotchSearchMethod,3,"config.omitted");
            foreach(int mode in new[]{1,2,3})
            {
                var config=new EdgeDetectionConfig{DuvNotchSearchMethod=mode};
                p.Equal(JsonSerializer.Deserialize<EdgeDetectionConfig>(JsonSerializer.Serialize(config))!.DuvNotchSearchMethod,mode,$"config.roundtrip.{mode}");
            }
        });
    }
    private static ushort[,] Column(int count, params (int Start,int End)[] bands)
    {
        var data=new ushort[count,1];
        for(int i=0;i<count;i++) data[i,0]=(ushort)(bands.Any(b=>i>=b.Start && i<=b.End)?900:100);
        return data;
    }
}

public class DuvPolicyAndCandidateRegression
{
    [Fact]
    public void FallbackPolicyAndCandidateArbitrationInputs()
    {
        DuvProbe.Run(p =>
        {
            var fallbackRows=new HashSet<int>{2,3,6,7,14};
            for(int row=0;row<16;row++)
                p.Equal(DuvProbe.Call<bool>("ShouldRunLegacyFallback",(row&8)!=0,(row&4)!=0,(row&2)!=0,(row&1)!=0),fallbackRows.Contains(row),$"fallback.matrix.{row}");
            foreach(double refined in new[]{5.825,double.NaN,double.NegativeInfinity})
            foreach(double legacy in new[]{1.25,double.NaN,double.NegativeInfinity,double.PositiveInfinity})
                p.Equal(DuvProbe.Call<double>("PreferLegacyTheta",refined,legacy),double.IsFinite(legacy)?legacy:refined,$"fallback.prefer.{refined}.{legacy}");
            var points=Valleys(1600,new[]{(300,80,1200.0),(800,80,1000.0),(1300,80,1100.0)});
            Queue(p,points,90,3,new[]{300,1300,800},"candidate.depth");
            Queue(p,points,90,1,new[]{300},"candidate.limit1");
            Queue(p,points,90,2,new[]{300,1300},"candidate.limit2");
            p.Equal(Core(points,90,null,null,false),300,"candidate.firstCore");
            var ties=Valleys(2000,new[]{(300,80,1200.0),(900,100,1200.0),(1500,100,1200.0)});
            Queue(p,ties,80,3,new[]{900,1500,300},"candidate.widthThenIndex");
            var seam=Valleys(1000,new[]{(0,80,1200.0)},true);
            p.Equal(Core(seam,90,null,null,true),0,"candidate.circularSeam");
            var narrow=DuvProbe.Points(16,14,11,8,10,12,15,17);
            p.Equal(DuvProbe.Call<int>("NotchCoarseSearchLegacyCore",new PointInPolar(0,0),narrow,2),3,"legacy.wideDip");
            p.Equal(Core(narrow,2,null,null,false),-1,"refined.erasedNarrowDip");
            var invalidRows=new (PointInPolar[] Points,bool[]? Mask,string Warning)[]
            {
                (narrow,new bool[8],"no valid edge points"),
                (narrow,new bool[3],"does not match"),
                (Array.Empty<PointInPolar>(),null,"")
            };
            for(int i=0;i<invalidRows.Length;i++)
            {
                var row=invalidRows[i];var warnings=new List<string>();
                p.Equal(Core(row.Points,2,row.Mask,warnings.Add,false),-1,$"candidate.invalid.{i}");
                p.Check(row.Warning==""?warnings.Count==0:warnings.Count==1 && warnings[0].Contains(row.Warning),$"candidate.warning.{i}",row.Warning==""?"none":row.Warning,string.Join(";",warnings));
            }
            Queue(p,Valleys(1600,new[]{(800,80,5000.0)}),90,3,new[]{800},"candidate.deepPhysicalNotch");
        });
    }
    private static int Core(PointInPolar[] points,int half,bool[]? mask,Action<string>? warn,bool circular)
        => DuvProbe.Call<int>("NotchCoarseSearchCore",new PointInPolar(0,0),points,half,mask,warn,circular,0);
    private static void Queue(DuvProbe p,PointInPolar[] points,int half,int limit,int[] expected,string id)
    {
        var actual=DuvProbe.Call<int[]>("NotchCoarseSearchCandidatesCore",new PointInPolar(0,0),points,half,null,null,false,0,limit);
        p.Check(actual.SequenceEqual(expected),id,string.Join(",",expected),string.Join(",",actual));
    }
    private static PointInPolar[] Valleys(int count,(int Center,int Width,double Depth)[] wells,bool circular=false)
    {
        var radii=new double[count];
        for(int i=0;i<count;i++)
        {
            double indentation=0;
            foreach(var w in wells)
            {
                int distance=Math.Abs(i-w.Center);if(circular) distance=Math.Min(distance,count-distance);
                indentation=Math.Max(indentation,w.Depth*Math.Clamp((w.Width-distance)/(w.Width-10.0),0,1));
            }
            radii[i]=10000-indentation;
        }
        return DuvProbe.Points(radii);
    }
}

public class DuvDepthAndImageRegression
{
    [Fact]
    public void ShoulderAndAsymmetryGatesAndUnicodeImageOutput()
    {
        DuvProbe.Run(p =>
        {
            foreach(int step in new[]{0,400,500,501})
            {
                var input=Profile(step,0);var result=WECDHelper.FindNotchCenterByDepth(input.Radii,input.Valid);bool accepted=step<=500;
                p.Equal(result.Status,accepted?"OK":"IncompleteShoulders",$"depth.step.{step}.status");
                p.Equal(double.IsNaN(result.CenterIndex),!accepted,$"depth.step.{step}.center");
                if(accepted){p.Near(result.CommonDepth,1000,$"depth.step.{step}.depth");p.Equal(result.Pairs.Count,5,$"depth.step.{step}.pairs");}
            }
            var mild=Profile(0,1);var good=WECDHelper.FindNotchCenterByDepth(mild.Radii,mild.Valid);
            p.Equal(good.Status,"OK","depth.mild.status");p.Near(good.CenterIndex,510,"depth.mild.center");
            p.Near(good.CenterSpread,20,"depth.mild.spread");p.Near(good.SpreadLimit,35.2,"depth.mild.limit");p.Equal(good.Pairs.Count,5,"depth.mild.pairs");
            var severe=Profile(0,2);var bad=WECDHelper.FindNotchCenterByDepth(severe.Radii,severe.Valid);
            p.Equal(bad.Status,"InconsistentDepthCenters","depth.severe.status");p.Equal(double.IsNaN(bad.CenterIndex),true,"depth.severe.center");
            p.Near(bad.CenterSpread,39.8,"depth.severe.spread");p.Near(bad.SpreadLimit,35.804,"depth.severe.limit");p.Equal(bad.Pairs.Count,5,"depth.severe.pairs");
            foreach(var input in new[]{(new double[20],new bool[20]),(new double[21],new bool[20])})
            {
                var result=WECDHelper.FindNotchCenterByDepth(input.Item1,input.Item2);
                p.Check(result.Status=="InsufficientValidPoints" && double.IsNaN(result.CenterIndex),$"depth.invalid.{input.Item1.Length}","InsufficientValidPoints; NaN",$"{result.Status}; {result.CenterIndex}");
            }
            var error=Record.Exception(()=>WECDHelper.SmoothNotchRadius(new double[2],new bool[1]));
            p.Check(error is ArgumentException,"depth.maskContract","ArgumentException",error?.GetType().Name ?? "no exception");
            Images(p);
        });
    }
    private static (double[] Radii,bool[] Valid) Profile(int step,int shape)
    {
        var radii=new double[1001];var valid=Enumerable.Repeat(true,1001).ToArray();
        for(int i=0;i<radii.Length;i++)
        {
            if(i<200) radii[i]=10000;
            else if(i<400) radii[i]=10000-5*(i-200);
            else if(i<=550) radii[i]=9000;
            else if(shape==1) radii[i]=i<710?9000+(i-550)*350.0/160:i<750?9350+(i-710)*10:i<800?9750+(i-750)*5:10000;
            else if(shape==2) radii[i]=i<738?9000:10000;
            else radii[i]=i<800?9000+(i-550)*(1000.0+step)/250:10000+step;
            if(i>=490 && i<=510){radii[i]=double.NaN;valid[i]=false;}
        }
        return (radii,valid);
    }
    private static ushort[] Pixels(Mat mat)
        => Enumerable.Range(0,6).Select(i=>mat.At<ushort>(i/3,i%3)).ToArray();
    private static void Images(DuvProbe p)
    {
        string directory=Path.Combine(Path.GetTempPath(),"Vega3737_"+Guid.NewGuid().ToString("N"),"晶圆_路径_Δ_測試");
        Directory.CreateDirectory(directory);
        try
        {
            ushort[] values={0,1,255,256,4096,65535};
            using var polar=new Mat(2,3,MatType.CV_16UC1);using var mercator=new Mat(2,3,MatType.CV_16UC1);
            for(int i=0;i<6;i++){polar.Set<ushort>(i/3,i%3,values[i]);mercator.Set<ushort>(i/3,i%3,values[5-i]);}
            var converter=new Mercator2Polar(16,30,4,100){PolarMat=polar,MercatorMat=mercator};
            converter.SaveImage(Path.Combine(directory,"试片.bin"));
            foreach(var row in new[]{("_PolarImage.png",values),("_MercatorImage.png",values.Reverse().ToArray())})
            {
                using var decoded=Cv2.ImRead(Path.Combine(directory,"试片"+row.Item1),ImreadModes.Unchanged);
                p.Check(decoded.Rows==2 && decoded.Cols==3,"image.dimensions."+row.Item1,"2x3",$"{decoded.Rows}x{decoded.Cols}");
                p.Equal(decoded.Type(),MatType.CV_16UC1,"image.type."+row.Item1);
                p.Check(Pixels(decoded).SequenceEqual(row.Item2),"image.pixels."+row.Item1,string.Join(",",row.Item2),string.Join(",",Pixels(decoded)));
            }
            var error=Record.Exception(()=>converter.SaveImage(Path.Combine(directory,"不存在","试片.bin")));
            p.Check(error is null,"image.writeFailureContained","no exception",error?.ToString() ?? "no exception");
            p.Check(Pixels(polar).SequenceEqual(values),"image.polarUnmodified",string.Join(",",values),string.Join(",",Pixels(polar)));
            p.Check(Pixels(mercator).SequenceEqual(values.Reverse()),"image.mercatorUnmodified",string.Join(",",values.Reverse()),string.Join(",",Pixels(mercator)));
        }
        finally { Directory.Delete(Path.GetDirectoryName(directory)!,true); }
    }
}
