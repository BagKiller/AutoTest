using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Xunit;

namespace Vega.AutoTest.Authored;

internal static class UniformitySupport
{
    internal static void Check(AssertionRecorder r, string id, bool ok, object expected, object actual)
    {
        if (!ok) r.Fail(id, "behavior", id, expected.ToString()!, actual.ToString()!);
        else r.Pass(id, "behavior", id, expected.ToString()!, actual.ToString()!);
        Assert.True(ok, $"{id}: expected {expected}; observed {actual}");
    }
    internal static List<RadiusAndPeakData> Points(params (double radius, double value)[] values) =>
        values.Select(p => new RadiusAndPeakData { Radius = p.radius, PeakData = p.value }).ToList();
    internal static List<RadiusAndPeakData> Tail(int start, int end, double value) =>
        Enumerable.Range(start, Math.Max(0, end - start + 1)).Select(i => new RadiusAndPeakData { Radius = i, PeakData = value }).ToList();
    internal static string Snapshot(IEnumerable<RadiusAndPeakData> points) => string.Join(";", points.Select(p =>
        p.Radius.ToString("R", CultureInfo.InvariantCulture) + ":" + p.PeakData.ToString("R", CultureInfo.InvariantCulture)));
    internal static void Append(UniformityMeasurementResult row, int diameter)
    {
        var method = typeof(UniformityCaliResultHelper).GetMethod("AppendUnscannedRegionDefaults")!;
        method.Invoke(null, new object[] { row, Enum.ToObject(method.GetParameters()[1].ParameterType, diameter) });
    }
    internal static double Boundary(List<RadiusAndPeakData> points, int diameter)
    {
        var method = typeof(UniformityCaliResultHelper).GetMethod("ComputeRppmSizeMeasuredBoundary")!;
        return (double)method.Invoke(null, new object[] { points, Enum.ToObject(method.GetParameters()[1].ParameterType, diameter) })!;
    }
    internal static void Set(object instance, Type declaringType, string name, object value) =>
        declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);
    internal static ObservableCollection<UniformityCaliAssociatedResult> Fixture()
    {
        var defaults = UniformityCaliResultHelper.InitUniformityCaliResult();
        var cal = defaults.First(x => Convert.ToInt32(x.WaferSize) == 300 &&
            x.CalibrationMode.ToString() == "Cal" && x.Incidence.ToString() == "Oblique");
        var verify = defaults.First(x => x.CalibrationMode.ToString() == "Verify" &&
            Equals(x.WaferSize, cal.WaferSize) && Equals(x.SpinSpeed, cal.SpinSpeed) &&
            Equals(x.Incidence, cal.Incidence) && Equals(x.Throughput, cal.Throughput));
        var rows = new ObservableCollection<UniformityCaliAssociatedResult> { cal, verify };
        var channels = new[] { ChannelMode.DN_Oblique, ChannelMode.DW1_Oblique, ChannelMode.DW2_Oblique };
        for (int mode = 0; mode < 2; mode++)
        {
            rows[mode].UniformityMeasurementResults.Clear();
            rows[mode].IsLastShow = false;
            for (int channel = 0; channel < 3; channel++)
            {
                int scale = mode * 3 + channel + 1;
                double mean = scale * 0.001, deviation = scale * 0.00001;
                var measurement = new UniformityMeasurementResult
                {
                    ChannelName = channels[channel],
                    RppmSizeRadiusAndPeakDataList = Points((1, mean - deviation), (2, mean), (147, mean + deviation)),
                    SizeRadiusAndPeakDataList = Points((1, scale * 90), (2, scale * 100), (145, scale * 110))
                };
                Append(measurement, 300);
                rows[mode].UniformityMeasurementResults.Add(measurement);
            }
        }
        return rows;
    }
}
