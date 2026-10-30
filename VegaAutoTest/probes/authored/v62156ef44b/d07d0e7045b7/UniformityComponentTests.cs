using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Utility.Helper;
using Xunit;
using static Vega.AutoTest.Authored.UniformityTestData;

namespace Vega.AutoTest.Authored;

[Collection("Uniformity regression")]
public sealed class UniformityComponentTests
{
    [Fact]
    public void IndependentTailsPreserveMeasuredPointsAndBoundary()
    {
        Run(nameof(IndependentTailsPreserveMeasuredPointsAndBoundary), a =>
        {
            foreach (var c in new[]
            {
                (Size: WaferSizeType.WS200, Max: 98, Tail: new[] { 99d, 100d, 101d }),
                (Size: WaferSizeType.WS300, Max: 147, Tail: new[] { 148d, 149d, 150d, 151d })
            })
            {
                string id = c.Size.ToString();
                var m = new UniformityMeasurementResult
                {
                    RppmSizeRadiusAndPeakDataList = new() { P(c.Max, 4), P(1, 99) },
                    SizeRadiusAndPeakDataList = new() { P(c.Max - 1, 40), P(2, 999) }
                };
                var rOriginal = Values(m.RppmSizeRadiusAndPeakDataList);
                var sOriginal = Values(m.SizeRadiusAndPeakDataList);
                var tail = UniformityCaliResultHelper.BuildUnscannedTail(m.RppmSizeRadiusAndPeakDataList, c.Size);
                Check(a, id + ".pure-tail", Values(tail).SequenceEqual(c.Tail.Select(r => (r, 4d))),
                    "explicit radii with peak 4", JsonHelper.ToJson(tail));
                Check(a, id + ".pure-input", Values(m.RppmSizeRadiusAndPeakDataList).SequenceEqual(rOriginal),
                    "input unchanged", JsonHelper.ToJson(m));
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, c.Size);
                var expectedR = rOriginal.Concat(c.Tail.Select(r => (r, 4d)));
                var expectedS = sOriginal.Concat(new[] { (double)c.Max }.Concat(c.Tail).Select(r => (r, 40d)));
                Check(a, id + ".rppm", Values(m.RppmSizeRadiusAndPeakDataList).SequenceEqual(expectedR),
                    "original order and holes preserved; independent rppm tail", JsonHelper.ToJson(m));
                Check(a, id + ".size", Values(m.SizeRadiusAndPeakDataList).SequenceEqual(expectedS),
                    "original order and holes preserved; independent size tail", JsonHelper.ToJson(m));
                Check(a, id + ".boundary", m.SizeMaxMeasuredRadius == c.Max - 1,
                    (c.Max - 1).ToString(), m.SizeMaxMeasuredRadius.ToString());
                string enriched = JsonHelper.ToJson(m);
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, c.Size);
                Check(a, id + ".idempotent", JsonHelper.ToJson(m) == enriched,
                    "unchanged data and measured boundary", JsonHelper.ToJson(m));
                var restored = JsonHelper.FromJson<UniformityMeasurementResult>(enriched)!;
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(restored, c.Size);
                Check(a, id + ".roundtrip", JsonHelper.ToJson(restored) == enriched,
                    "persisted boundary prevents a second enrichment", JsonHelper.ToJson(restored));
            }
        });
    }

    [Fact]
    public void EmptyMissingSizeAndAlreadyCoveredEdges()
    {
        Run(nameof(EmptyMissingSizeAndAlreadyCoveredEdges), a =>
        {
            foreach (bool rppmOnly in new[] { true, false })
            {
                var m = new UniformityMeasurementResult();
                var list = rppmOnly ? m.RppmSizeRadiusAndPeakDataList : m.SizeRadiusAndPeakDataList;
                list.Add(P(147, 7));
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, WaferSizeType.WS300);
                string first = JsonHelper.ToJson(m);
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, WaferSizeType.WS300);
                Check(a, "one-list." + rppmOnly, Values(list).SequenceEqual(new[]
                    { (147d, 7d), (148d, 7d), (149d, 7d), (150d, 7d), (151d, 7d) }),
                    "only populated list is padded", JsonHelper.ToJson(m));
                Check(a, "one-list.empty." + rppmOnly,
                    (rppmOnly ? m.SizeRadiusAndPeakDataList : m.RppmSizeRadiusAndPeakDataList).Count == 0,
                    "other list stays empty", JsonHelper.ToJson(m));
                Check(a, "one-list.boundary." + rppmOnly, m.SizeMaxMeasuredRadius == (rppmOnly ? 0 : 147),
                    rppmOnly ? "0" : "147", m.SizeMaxMeasuredRadius.ToString());
                Check(a, "one-list.repeat." + rppmOnly, first == JsonHelper.ToJson(m),
                    "second call unchanged even without Size measurements", JsonHelper.ToJson(m));
            }
            foreach (int radius in new[] { 151, 152 })
            {
                var m = new UniformityMeasurementResult
                {
                    RppmSizeRadiusAndPeakDataList = new() { P(radius, 4) },
                    SizeRadiusAndPeakDataList = new() { P(radius, 40) }
                };
                UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, WaferSizeType.WS300);
                Check(a, "covered." + radius, m.RppmSizeRadiusAndPeakDataList.Count == 1 &&
                    m.SizeRadiusAndPeakDataList.Count == 1 && m.SizeMaxMeasuredRadius == radius,
                    "no truncation or padding; original measured boundary", JsonHelper.ToJson(m));
            }
            var empty = new UniformityMeasurementResult();
            UniformityCaliResultHelper.AppendUnscannedRegionDefaults(empty, WaferSizeType.WS300);
            Check(a, "empty", empty.SizeMaxMeasuredRadius == 0 && empty.SizeRadiusAndPeakDataList.Count == 0 &&
                empty.RppmSizeRadiusAndPeakDataList.Count == 0, "no manufactured measurements", JsonHelper.ToJson(empty));
            var missing = new UniformityMeasurementResult { SizeRadiusAndPeakDataList = new() { P(147, 30) } };
            string before = JsonHelper.ToJson(missing);
            UniformityCaliResultHelper.AppendUnscannedRegionDefaults(missing, (WaferSizeType)0);
            Check(a, "missing-wafer", before == JsonHelper.ToJson(missing), "missing diameter leaves row unchanged", JsonHelper.ToJson(missing));
            missing.SizeMaxMeasuredRadius = 147;
            before = JsonHelper.ToJson(missing);
            UniformityCaliResultHelper.AppendUnscannedRegionDefaults(missing, WaferSizeType.WS300);
            Check(a, "existing-boundary", before == JsonHelper.ToJson(missing), "persisted positive boundary skips enrichment", JsonHelper.ToJson(missing));
        });
    }

    [Fact]
    public void AlgorithmConsumerPadsAndNormalizesWithoutMutatingResults()
    {
        Run(nameof(AlgorithmConsumerPadsAndNormalizesWithoutMutatingResults), a =>
        {
            var original = UniformityCaliResultHelper.UniformityCaliAssociatedResults;
            try
            {
                foreach (var c in new[] { (WaferSizeType.WS200, 98, 101), (WaferSizeType.WS300, 147, 151) })
                foreach (var channel in Channels)
                {
                    var row = Row(CalibrationMode.Cal, c.Item1);
                    var m = new UniformityMeasurementResult
                    {
                        ChannelName = channel, RppmSizeRadiusAndPeakDataList = new() { P(10, 2), P(c.Item2, 4) },
                        SizeRadiusAndPeakDataList = new() { P(10, 900), P(c.Item2, 300) }
                    };
                    row.UniformityMeasurementResults.Add(m);
                    UniformityCaliResultHelper.UniformityCaliAssociatedResults = new() { row };
                    var points = UniformityCaliResultHelper.GetUniformityCaliResult(c.Item1, SpinSpeedType.SpinStd,
                        IncidenceType.Normal, ThroughputType.StandardThroughput, channel);
                    string id = c.Item1 + "." + channel;
                    var expected = new[] { (10d, 1d), ((double)c.Item2, .5d) }
                        .Concat(Enumerable.Range(c.Item2 + 1, c.Item3 - c.Item2).Select(r => ((double)r, .5d)));
                    Check(a, id + ".factor", points.Select(p => (p.X, p.Y)).SequenceEqual(expected),
                        "rppm reference 2 divided by peaks 2,4; tail factor 0.5", JsonHelper.ToJson(points));
                    Check(a, id + ".no-mutation", m.RppmSizeRadiusAndPeakDataList.Count == 2 && m.SizeMaxMeasuredRadius == 0,
                        "consumer fallback is local", JsonHelper.ToJson(m));
                    UniformityCaliResultHelper.AppendUnscannedRegionDefaults(m, c.Item1);
                    var enriched = UniformityCaliResultHelper.GetUniformityCaliResult(c.Item1, SpinSpeedType.SpinStd,
                        IncidenceType.Normal, ThroughputType.StandardThroughput, channel);
                    Check(a, id + ".no-duplicate", enriched.Select(p => (p.X, p.Y)).SequenceEqual(expected),
                        "same curve after persistent enrichment", JsonHelper.ToJson(enriched));
                }
                UniformityCaliResultHelper.UniformityCaliAssociatedResults = new();
                Check(a, "no-result", UniformityCaliResultHelper.GetUniformityCaliResult(WaferSizeType.WS300,
                    SpinSpeedType.SpinStd, IncidenceType.Normal, ThroughputType.StandardThroughput, Channels[0]).Count == 0,
                    "empty curve when no Cal row exists", "returned curve count checked");
            }
            finally { UniformityCaliResultHelper.UniformityCaliAssociatedResults = original; }
        });
    }

    [Fact]
    public void LegacyMigrationUsesEachRowsDiameterAndSurvivesSerialization()
    {
        Run(nameof(LegacyMigrationUsesEachRowsDiameterAndSurvivesSerialization), a =>
        {
            var rows = JsonHelper.FromJson<ObservableCollection<UniformityCaliAssociatedResult>>(LegacyJson("legacy"))!;
            rows[0].WaferSize = WaferSizeType.WS200;
            foreach (var m in rows[0].UniformityMeasurementResults) m.RadiusAndPeakDataList = new() { P(98, .05) };
            UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(rows);
            UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(rows);
            foreach (var row in rows)
            foreach (var m in row.UniformityMeasurementResults)
            {
                bool cal = row.CalibrationMode == CalibrationMode.Cal;
                var target = cal ? m.RppmSizeRadiusAndPeakDataList : m.SizeRadiusAndPeakDataList;
                string id = row.CalibrationMode + "." + m.ChannelName;
                Check(a, id + ".legacy-cleared", m.RadiusAndPeakDataList.Count == 0, "legacy consumed", JsonHelper.ToJson(m));
                Check(a, id + ".tail", target.Last().Radius == (cal ? 101 : 151) && target.Last().PeakData == (cal ? .05 : 30),
                    cal ? "WS200: radius 101, peak .05" : "WS300: radius 151, peak 30", JsonHelper.ToJson(target));
                Check(a, id + ".boundary", m.SizeMaxMeasuredRadius == (cal ? 0 : 147),
                    cal ? "0 (no Size data)" : "147", m.SizeMaxMeasuredRadius.ToString());
            }
            string saved = JsonHelper.ToJson(rows);
            rows = JsonHelper.FromJson<ObservableCollection<UniformityCaliAssociatedResult>>(saved)!;
            UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(rows);
            UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(rows);
            Check(a, "legacy.roundtrip-idempotence", saved == JsonHelper.ToJson(rows),
                "all boundaries and curves survive serialization and repeated migration", JsonHelper.ToJson(rows));
        });
    }
}
