using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Xunit;
using static Vega.AutoTest.Authored.UniformitySupport;

namespace Vega.AutoTest.Authored;

public class UniformityDataRegression
{
    [Fact]
    public void IndependentBoundariesLegacyRecoveryAndPaddingAreStable()
    {
        var r = new AssertionRecorder();
        try
        {
            var inputs = new[]
            {
                new UniformityMeasurementResult { RppmSizeRadiusAndPeakDataList = Points((2,4),(147,8)), SizeRadiusAndPeakDataList = Points((1,20),(145,30)) },
                new UniformityMeasurementResult { RppmSizeRadiusAndPeakDataList = Points((147,8)) },
                new UniformityMeasurementResult { SizeRadiusAndPeakDataList = Points((147,30)) },
                new UniformityMeasurementResult { RppmSizeRadiusAndPeakDataList = Points((101,8)), SizeRadiusAndPeakDataList = Points((101,30)) },
                new UniformityMeasurementResult { RppmSizeRadiusAndPeakDataList = Points((147,8)), SizeRadiusAndPeakDataList = Points((145,30)) },
                new UniformityMeasurementResult()
            };
            int[] diameters = { 300,300,300,200,0,300 };
            double[] rb = { 147,147,0,101,0,0 }, sb = { 145,0,147,101,0,0 };
            var expectedR = new List<RadiusAndPeakData>[]
            {
                Points((2,4),(147,8)).Concat(Tail(148,151,8)).ToList(),
                Points((147,8)).Concat(Tail(148,151,8)).ToList(), Points(), Points((101,8)), Points((147,8)), Points()
            };
            var expectedS = new List<RadiusAndPeakData>[]
            {
                Points((1,20),(145,30)).Concat(Tail(146,151,30)).ToList(), Points(),
                Points((147,30)).Concat(Tail(148,151,30)).ToList(), Points((101,30)), Points((145,30)), Points()
            };
            for (int i = 0; i < inputs.Length; i++)
            {
                var row = inputs[i];
                Append(row, diameters[i]);
                Check(r,$"tail-{i}-r-boundary",row.RppmSizeMaxMeasuredRadius == rb[i],rb[i],row.RppmSizeMaxMeasuredRadius);
                Check(r,$"tail-{i}-s-boundary",row.SizeMaxMeasuredRadius == sb[i],sb[i],row.SizeMaxMeasuredRadius);
                Check(r,$"tail-{i}-r-points",Snapshot(row.RppmSizeRadiusAndPeakDataList) == Snapshot(expectedR[i]),Snapshot(expectedR[i]),Snapshot(row.RppmSizeRadiusAndPeakDataList));
                Check(r,$"tail-{i}-s-points",Snapshot(row.SizeRadiusAndPeakDataList) == Snapshot(expectedS[i]),Snapshot(expectedS[i]),Snapshot(row.SizeRadiusAndPeakDataList));
                var before = $"{row.RppmSizeMaxMeasuredRadius}/{row.SizeMaxMeasuredRadius}/{Snapshot(row.RppmSizeRadiusAndPeakDataList)}/{Snapshot(row.SizeRadiusAndPeakDataList)}";
                Append(row, diameters[i]);
                var after = $"{row.RppmSizeMaxMeasuredRadius}/{row.SizeMaxMeasuredRadius}/{Snapshot(row.RppmSizeRadiusAndPeakDataList)}/{Snapshot(row.SizeRadiusAndPeakDataList)}";
                Check(r,$"tail-{i}-repeat",before == after,before,after);
            }
            var legacy = Points((146,4),(147,5)).Concat(Tail(148,151,5)).ToList();
            var boundaryInputs = new[] { Points(), Points((100,4),(90,5)), legacy,
                Points((147,5),(149,5),(150,5),(151,5)), Points((150,4),(151,5)), Tail(147,151,5), legacy };
            double[] expected = { 0,100,147,149,151,147,151 };
            for (int i = 0; i < boundaryInputs.Length; i++)
            {
                string before = Snapshot(boundaryInputs[i]);
                double observed = Boundary(boundaryInputs[i], i == 6 ? 200 : 300);
                Check(r,$"legacy-{i}-boundary",observed == expected[i],expected[i],observed);
                Check(r,$"legacy-{i}-pure",before == Snapshot(boundaryInputs[i]),before,Snapshot(boundaryInputs[i]));
            }
            var oldRow = new UniformityMeasurementResult
            {
                RppmSizeRadiusAndPeakDataList = legacy,
                SizeRadiusAndPeakDataList = Points((145,30)).Concat(Tail(146,151,30)).ToList(), SizeMaxMeasuredRadius = 145
            };
            string oldPoints = Snapshot(oldRow.RppmSizeRadiusAndPeakDataList) + Snapshot(oldRow.SizeRadiusAndPeakDataList);
            Append(oldRow,300); Append(oldRow,300);
            Check(r,"legacy-row-boundaries",oldRow.RppmSizeMaxMeasuredRadius == 147 && oldRow.SizeMaxMeasuredRadius == 145,"147/145",$"{oldRow.RppmSizeMaxMeasuredRadius}/{oldRow.SizeMaxMeasuredRadius}");
            Check(r,"legacy-row-no-refill",oldPoints == Snapshot(oldRow.RppmSizeRadiusAndPeakDataList) + Snapshot(oldRow.SizeRadiusAndPeakDataList),oldPoints,Snapshot(oldRow.RppmSizeRadiusAndPeakDataList) + Snapshot(oldRow.SizeRadiusAndPeakDataList));
        }
        finally { r.Flush(); }
    }

    [Fact]
    public void SaveKeepsTheBoundCollectionAndRemovalCannotBeResurrected()
    {
        var r = new AssertionRecorder();
        var type = typeof(UniformityCaliResultHelper);
        string path = (string)type.GetField("s_uniformityJsonFilePath",BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var property = type.GetProperty("UniformityCaliAssociatedResults")!;
        object? originalCollection = property.GetValue(null);
        byte[]? originalFile = File.Exists(path) ? File.ReadAllBytes(path) : null;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var bound = Fixture();
            var cal = bound[0]; var verify = bound[1];
            UniformityCaliResultHelper.SaveAssociatedResult(bound);
            Check(r,"save-content",JArray.Parse(File.ReadAllText(path)).Count == 2,2,JArray.Parse(File.ReadAllText(path)).Count);
            var live = UniformityCaliResultHelper.UniformityCaliAssociatedResults;
            Check(r,"save-reference",ReferenceEquals(bound,live),"same bound instance",ReferenceEquals(bound,live));
            int removals = 0;
            bound.CollectionChanged += (_,e) => { if (e.Action == NotifyCollectionChangedAction.Remove) removals++; };
            Check(r,"live-remove",live.Remove(verify),true,!live.Contains(verify));
            Check(r,"bound-notification",removals == 1,1,removals);
            Check(r,"bound-row-removed",bound.Count == 1 && ReferenceEquals(bound[0],cal),"only original Cal row",bound.Count);
            UniformityCaliResultHelper.SaveAssociatedResult(bound);
            var saved = JArray.Parse(File.ReadAllText(path));
            Check(r,"resave-no-resurrection",saved.Count == 1 && (string?)saved[0]["Key"] == cal.Key,cal.Key,saved.ToString());
        }
        finally
        {
            try
            {
                property.SetValue(null,originalCollection);
                if (originalFile == null) File.Delete(path); else File.WriteAllBytes(path,originalFile);
            }
            finally { r.Flush(); }
        }
    }
}
