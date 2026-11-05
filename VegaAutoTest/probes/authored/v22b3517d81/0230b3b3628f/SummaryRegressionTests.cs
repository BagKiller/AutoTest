using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Xunit;

namespace Vega.AutoTest.Authored;

internal static class SummaryProbe
{
    internal static readonly MethodInfo Build = typeof(CollectionSummaryContent).GetMethod(
        "BuildOpticalSummaryRows", BindingFlags.Static | BindingFlags.NonPublic)!;
    private static readonly Type Collector = Build.GetParameters()[4].ParameterType.GetGenericArguments()[0];
    internal static object Get(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o)!;
    internal static void Set(object o, string name, object? value)
    {
        var property = o.GetType().GetProperty(name)!;
        if (value != null && !property.PropertyType.IsInstanceOfType(value))
            value = property.PropertyType.IsEnum ? Enum.Parse(property.PropertyType, (string)value)
                : Convert.ChangeType(value, property.PropertyType, CultureInfo.InvariantCulture);
        property.SetValue(o, value);
    }
    internal static object Node(object parent, string name)
    {
        var node = RuntimeHelpers.GetUninitializedObject(parent.GetType().GetProperty(name)!.PropertyType);
        Set(parent, name, node);
        return node;
    }
    internal static void List(object parent, string name, params object[] items)
    {
        var itemType = parent.GetType().GetProperty(name)!.PropertyType.GetGenericArguments()[0];
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;
        foreach (var item in items) list.Add(item);
        Set(parent, name, list);
    }
    internal static object Channel(string mode, bool enabled = true)
    {
        var ctor = Collector.GetConstructors().Single(c => c.GetParameters().Length == 1
            && c.GetParameters()[0].ParameterType.IsEnum);
        var channel = ctor.Invoke(new[] { Enum.Parse(ctor.GetParameters()[0].ParameterType, mode) });
        Set(channel, "IsEnabled", enabled);
        Set(channel, "WMask", "W1W2-X5");
        Set(channel, "NMask", "N-X1");
        Set(channel, "WPolarizer", "POL1");
        Set(channel, "NPolarizer", "P");
        return channel;
    }
    internal static KeyValuePair<string, string> K(string key, string value) => new(key, value);
    internal static List<KeyValuePair<string, string>> Rows(double power = 100, object[]? channels = null,
        string incidence = "Oblique", string? throughput = "HighSensitivity", string polarization = "P")
    {
        var p = Build.GetParameters();
        Array? array = null;
        if (channels != null)
        {
            array = Array.CreateInstance(Collector, channels.Length);
            for (int i = 0; i < channels.Length; i++) array.SetValue(channels[i], i);
        }
        return (List<KeyValuePair<string, string>>)Build.Invoke(null, new object?[] {
            Enum.Parse(p[0].ParameterType, incidence),
            throughput == null ? null : Enum.Parse(Nullable.GetUnderlyingType(p[1].ParameterType)!, throughput),
            Enum.Parse(p[2].ParameterType, polarization), power, array })!;
    }
    internal static void Equal(AssertionRecorder r, string id, object? expected, object? actual)
    {
        string e = JsonSerializer.Serialize(expected), a = JsonSerializer.Serialize(actual);
        if (e != a)
        {
            r.Fail(id, "behavior", id, e, a);
            Assert.Equal(e, a);
        }
        r.Pass(id, "behavior", id, e, a);
    }
    internal static void Run(Action<AssertionRecorder> test)
    {
        var r = new AssertionRecorder();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            test(r);
        }
        finally { CultureInfo.CurrentCulture = previous; r.Flush(); }
    }
}

public class OpticalRowsRegressionTests
{
    [Fact]
    public void OpticalRowsHaveCompactOrderedValuesAndNullFallbacks() => SummaryProbe.Run(r =>
    {
        foreach (string mode in new[] { "DN_Oblique", "DN_Normal", "DW2_Oblique", "DW2_Normal" })
        {
            var channel = SummaryProbe.Channel(mode);
            var rows = SummaryProbe.Rows(channels: new[] { channel });
            SummaryProbe.Equal(r, mode + "-keys", new[] { "Collection Info", "" }, rows.Select(x => x.Key).ToArray());
            SummaryProbe.Equal(r, mode + "-info", "Oblique HighSensitivity 100% P", rows[0].Value);
            string track = mode.StartsWith("DN", StringComparison.Ordinal) ? "DN P N-X1" : "DW2 POL1 W1W2-X5";
            SummaryProbe.Equal(r, mode + "-field-selection", track, rows[1].Value);
        }
        var wide = SummaryProbe.Channel("DW2_Oblique");
        var narrow = SummaryProbe.Channel("DN_Oblique");
        SummaryProbe.Set(wide, "WMask", "本地 Mask DW2-X5");
        var mixed = SummaryProbe.Rows(channels: new[] { wide, SummaryProbe.Channel("DN_Normal", false), narrow });
        SummaryProbe.Equal(r, "mixed-exact-rows", new[] {
            SummaryProbe.K("Collection Info", "Oblique HighSensitivity 100% P"),
            SummaryProbe.K("", "DW2 POL1 本地 Mask DW2-X5\nDN P N-X1") }, mixed.ToArray());
        SummaryProbe.Equal(r, "internal-name-preserved", "DW2", SummaryProbe.Get(wide, "CollectorName"));
        object[]?[] absent = { null, Array.Empty<object>(), new[] {
            SummaryProbe.Channel("DW2_Normal", false), SummaryProbe.Channel("DN_Normal", false) } };
        for (int i = 0; i < absent.Length; i++)
            SummaryProbe.Equal(r, "no-track-" + i, new[] { SummaryProbe.K("Collection Info", "Oblique HighSensitivity 100% P") },
                SummaryProbe.Rows(channels: absent[i]).ToArray());
        var inputs = new (double Power, string Incidence, string? Throughput, string Polarization, string Expected)[] {
            (87.5, "Normal", "UltraHighSensitivity", "C", "Normal UltraHighSensitivity 87.5% C"),
            (0, "Oblique", "HighSensitivity", "P", "Oblique HighSensitivity 0% P"),
            (1, "Oblique", "HighSensitivity", "P", "Oblique HighSensitivity 1% P"),
            (10, "Oblique", "HighSensitivity", "P", "Oblique HighSensitivity 10% P"),
            (100, "Oblique", "HighSensitivity", "P", "Oblique HighSensitivity 100% P"),
            (10, "Normal", null, "P", "Normal  10% P") };
        for (int i = 0; i < inputs.Length; i++)
        {
            var t = inputs[i];
            SummaryProbe.Equal(r, "info-format-" + i, new[] { SummaryProbe.K("Collection Info", t.Expected) },
                SummaryProbe.Rows(t.Power, null, t.Incidence, t.Throughput, t.Polarization).ToArray());
        }
        var format = typeof(CollectionSummaryContent).GetMethod("FormatEdgeExclusion", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var t in new (double? Input, string Expected)[] { (null, ""), (0, "0mm"), (3, "3mm"), (2.5, "2.5mm") })
            SummaryProbe.Equal(r, "edge-" + (t.Input?.ToString() ?? "null"), t.Expected, format.Invoke(null, new object?[] { t.Input }));
    });

    [Fact]
    public void SummaryTokensRespectSingleWideDualWideAndTextBoundaries() => SummaryProbe.Run(r =>
    {
        var replace = typeof(CollectorTypeExtensions).GetMethod("ReplaceWideTokenInText",
            BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string) }, null)!;
        var samples = new (string Input, string Single)[] {
            ("DW2 POL1 Full\nDN P N-X1", "DW POL1 Full\nDN P N-X1"),
            ("DW1 None Full", "DW None Full"), ("DW2 Ideal Gain", "DW Ideal Gain"),
            ("DW1 Ideal Gain", "DW Ideal Gain"), ("MyDW2x Film", "MyDW2x Film"),
            ("DW10 Film", "DW10 Film"), ("Film DW2 Reference", "Film DW Reference"),
            ("DW2A-0", "DWA-0"), ("PMT Wide2", "PMT Wide"), ("", "") };
        for (int i = 0; i < samples.Length; i++)
        {
            var t = samples[i];
            SummaryProbe.Equal(r, "single-token-" + i, t.Single, replace.Invoke(null, new object?[] { t.Input, "DW2" }));
            SummaryProbe.Equal(r, "dual-token-" + i, t.Input, replace.Invoke(null, new object?[] { t.Input, null }));
        }
    });
}

public class SummaryRebuildRegressionTests
{
    private static CollectionContent Collection(string name, bool first)
    {
        var c = (CollectionContent)RuntimeHelpers.GetUninitializedObject(typeof(CollectionContent));
        SummaryProbe.Set(c, "CollectionName", name);
        var scan = SummaryProbe.Node(c, "WaferScan");
        SummaryProbe.Set(scan, "Throughput", first ? "HighSensitivity" : "UltraHighSensitivity");
        SummaryProbe.Set(scan, "InspectionSide", "Back");
        var dark = SummaryProbe.Node(c, "DarkField");
        var sizing = SummaryProbe.Node(dark, "SizingContent");
        SummaryProbe.Set(sizing, "Incidence", first ? "Oblique" : "Normal");
        SummaryProbe.Set(sizing, "Polarization", first ? "P" : "C");
        SummaryProbe.Set(sizing, "LaserPower", first ? 100.0 : 87.5);
        SummaryProbe.Set(sizing, "Thickness", first ? 125.0 : 50.0);
        SummaryProbe.Set(sizing, "FilmName", first ? "MyDW2x Film" : "DW2 Reference");
        SummaryProbe.Set(sizing, "HazeNormStyle", first ? "UnityCurve" : "NormCurve");
        if (first)
        {
            SummaryProbe.Set(SummaryProbe.Node(dark, "InspectionRegionContent"), "EdgeExclusion", 2.5);
            SummaryProbe.List(SummaryProbe.Node(dark, "ChannelSettingContent"), "Collectors",
                SummaryProbe.Channel("DW2_Oblique"), SummaryProbe.Channel("DN_Normal", false), SummaryProbe.Channel("DN_Oblique"));
            var hardware = SummaryProbe.Node(dark, "HardwareParamsContent");
            var property = hardware.GetType().GetProperty("HardwareParamsDic")!;
            var dictionary = (IDictionary)Activator.CreateInstance(property.PropertyType)!;
            var entryType = property.PropertyType.GetGenericArguments()[1];
            foreach (var t in new[] { ("DW2", 12.5), ("DN", 2.25) })
            {
                var entry = RuntimeHelpers.GetUninitializedObject(entryType);
                SummaryProbe.Set(entry, "SystemGain", t.Item2);
                dictionary.Add(t.Item1, entry);
            }
            SummaryProbe.Set(hardware, "HardwareParamsDic", dictionary);
        }
        return c;
    }
    private static KeyValuePair<string, string>[] Expected(bool first, bool updated = false) => new[] {
        SummaryProbe.K("Collection Name", first ? "First" : "Second"),
        SummaryProbe.K("Collection Info", first ? (updated ? "Oblique HighSensitivity 1% C" : "Oblique HighSensitivity 100% P") : "Normal UltraHighSensitivity 87.5% C")
    }.Concat(first ? new[] { SummaryProbe.K("", "DW2 POL1 W1W2-X5\nDN P N-X1") } : Array.Empty<KeyValuePair<string, string>>())
    .Concat(new[] {
        SummaryProbe.K("Edge Exclusion", first ? "2.5mm" : ""), SummaryProbe.K("Film Thickness", first ? "125" : "50"),
        SummaryProbe.K("Film Name", first ? "MyDW2x Film" : "DW2 Reference"), SummaryProbe.K("Haze Curve", first ? "UnityCurve" : "NormCurve"),
        SummaryProbe.K("Wafer CenterX", "0"), SummaryProbe.K("Wafer CenterY", "0") })
    .Concat(first ? new[] { SummaryProbe.K("DW2 Ideal Gain", "12.5"), SummaryProbe.K("DN Ideal Gain", "2.25") }
        : Array.Empty<KeyValuePair<string, string>>()).ToArray();

    [Fact]
    public void ConstructorAndRefreshReplaceLegacyRowsWithoutChangingOtherContent() => SummaryProbe.Run(r =>
    {
        var first = Collection("First", true);
        var second = Collection("Second", false);
        var collections = (CollectionsContent)RuntimeHelpers.GetUninitializedObject(typeof(CollectionsContent));
        SummaryProbe.Set(collections, "WaferSize", "WS200");
        SummaryProbe.List(collections, "Collections", first, second);
        var summary = new WaferSummaryContent();
        summary.BaseSummaryDic["Recipe Name"] = "Preserved recipe";
        var old = new CollectionSummaryContent(first);
        old.CollectionSummaryDic = new() { ["Collection Info"] = "legacy", ["BEA OD Filter"] = "IncidenceOD2",
            ["Laser Power(%)"] = "10", ["Polarization"] = "S", ["DW2 Mask"] = "Old" };
        summary.CollectionsSummary.Add(old);
        summary.RefreshCollectionSummary(collections);
        SummaryProbe.Equal(r, "rebuilt-count", 2, summary.CollectionsSummary.Count);
        SummaryProbe.Equal(r, "first-exact-rows", Expected(true), summary.CollectionsSummary[0].CollectionSummaryDic.ToArray());
        SummaryProbe.Equal(r, "second-null-chain-rows", Expected(false), summary.CollectionsSummary[1].CollectionSummaryDic.ToArray());
        SummaryProbe.Equal(r, "base-preserved", "Preserved recipe", summary.BaseSummaryDic["Recipe Name"]);
        SummaryProbe.Equal(r, "wafer-side", "Back", summary.BaseSummaryDic["Wafer Side"]);
        SummaryProbe.Equal(r, "wafer-size", "WS200", summary.BaseSummaryDic["Wafer Size"]);
        var sizing = SummaryProbe.Get(SummaryProbe.Get(first, "DarkField"), "SizingContent");
        SummaryProbe.Set(sizing, "LaserPower", 1.0);
        SummaryProbe.Set(sizing, "Polarization", "C");
        summary.RefreshCollectionSummary(collections);
        SummaryProbe.Equal(r, "refresh-current-optics", Expected(true, true), summary.CollectionsSummary[0].CollectionSummaryDic.ToArray());
        SummaryProbe.Equal(r, "refresh-order-no-duplicates", new[] { "First", "Second" },
            summary.CollectionsSummary.Select(c => c.CollectionSummaryDic["Collection Name"]).ToArray());
        SummaryProbe.Equal(r, "refresh-base-preserved", "Preserved recipe", summary.BaseSummaryDic["Recipe Name"]);
    });
}
