using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Utility.Helper;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

internal static class UniformityTestData
{
    internal static readonly ChannelMode[] Channels =
        { ChannelMode.DN_Normal, ChannelMode.DW1_Oblique, ChannelMode.DW2_Normal };

    internal static void Check(AssertionRecorder a, string id, bool ok, string expected, string actual)
    {
        if (ok) a.Pass(id, "behavior", id, expected, actual);
        else
        {
            a.Fail(id, "behavior", id, expected, actual);
            throw new XunitException(id + ": expected " + expected + "; actual " + actual);
        }
    }

    internal static void Run(string id, Action<AssertionRecorder> action)
    {
        var a = new AssertionRecorder();
        try { action(a); }
        catch (Exception e)
        {
            a.Fail(id + ".execution", "execution", id, "production behavior completes", e.ToString());
            throw;
        }
        finally { a.Flush(); }
    }

    internal static RadiusAndPeakData P(double r, double y) => new() { Radius = r, PeakData = y };
    internal static (double, double)[] Values(List<RadiusAndPeakData> p) =>
        p.Select(x => (x.Radius, x.PeakData)).ToArray();

    internal static UniformityCaliAssociatedResult Row(CalibrationMode mode, WaferSizeType size)
    {
        return new UniformityCaliAssociatedResult($"{mode}.{size}.SpinStd.Normal.StandardThroughput")
        {
            CalibrationMode = mode, WaferSize = size, SpinSpeed = SpinSpeedType.SpinStd,
            Incidence = IncidenceType.Normal, Throughput = ThroughputType.StandardThroughput
        };
    }

    // Explicit synthetic measurements: sample SD(10,20,30)=10; range/mean=100%.
    internal static string LegacyJson(string marker)
    {
        return JsonHelper.ToJson(new[] { CalibrationMode.Cal, CalibrationMode.Verify }.Select(mode => new
        {
            Key = $"{mode}.WS300.SpinStd.Normal.StandardThroughput",
            CalibrationMode = mode, WaferSize = WaferSizeType.WS300,
            SpinSpeed = SpinSpeedType.SpinStd, Incidence = IncidenceType.Normal,
            Throughput = ThroughputType.StandardThroughput, WaferID = marker,
            RecipePath = "synthetic/" + marker, IsSelected = mode == CalibrationMode.Verify,
            IsLastShow = mode == CalibrationMode.Verify, CalibDate = new DateTime(2026, 1, 2, 3, 4, 5),
            UniformityMeasurementResults = Channels.Select(channel => new
            {
                ChannelName = channel,
                RadiusAndPeakDataList = mode == CalibrationMode.Cal
                    ? new[] { P(10, .02), P(146, .04), P(147, .05) }
                    : new[] { P(10, 10), P(146, 20), P(147, 30) }
            }).ToArray()
        }).ToArray());
    }
}
