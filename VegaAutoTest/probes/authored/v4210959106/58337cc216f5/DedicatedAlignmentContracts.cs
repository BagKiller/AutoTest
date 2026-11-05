#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Vega.BL.IWaferRun;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Recipes;
using Vega.Platform.Database;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("WI3716", DisableParallelization = true)]
public sealed class DedicatedAlignmentCollection { }

internal static class C
{
    internal static void Eq<T>(AssertionRecorder a, string id, T expected, T actual)
    {
        bool equal = System.Collections.Generic.EqualityComparer<T>.Default.Equals(expected, actual);
        string e = Convert.ToString(expected, CultureInfo.InvariantCulture) ?? "<null>";
        string v = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "<null>";
        if (equal) a.Pass(id, "behavior", id, e, v);
        else a.Fail(id, "behavior", id, e, v);
        Assert.True(equal, $"{id}: expected {e}; actual {v}");
    }
    internal static Type Type(string name) => System.Type.GetType(name, throwOnError: true)!;
    internal static FieldInfo Field(Type type, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    internal static object Bare(Type type) => RuntimeHelpers.GetUninitializedObject(type);
    internal static void Put(object target, string name, object? value)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p?.SetMethod != null) { p.SetValue(target, value); return; }
        Field(target.GetType(), p == null ? name : $"<{name}>k__BackingField").SetValue(target, value);
    }
    internal static object? Default(Type type)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return Completed(type.GetGenericArguments()[0], Default(type.GetGenericArguments()[0]));
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
    internal static object Completed(Type type, object? value) => typeof(Task).GetMethods()
        .Single(m => m.Name == "FromResult" && m.IsGenericMethodDefinition)
        .MakeGenericMethod(type).Invoke(null, new[] { value })!;
    internal static object Stub(object target, string field, Func<MethodInfo, object?[], object?> handler)
    {
        var f = Field(target.GetType(), field);
        var proxy = DispatchProxy.Create(f.FieldType, typeof(AlignmentApiProbe));
        ((AlignmentApiProbe)proxy).Handler = handler;
        f.SetValue(target, proxy);
        return proxy;
    }
    internal static MethodInfo Method(object target, string name) => target.GetType().GetMethod(
        name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
}

public class AlignmentApiProbe : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler { get; set; } = (m, _) => C.Default(m.ReturnType);
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args ?? Array.Empty<object?>());
}

[Collection("WI3716")]
public sealed class DedicatedAlignmentContracts
{
    [Fact]
    public void DefaultsParsingAndGeneratedRecipeRespectDedicatedContract()
    {
        var a = new AssertionRecorder();
        var oldCulture = Vega.Utility.Common.Properties.Resources.Culture;
        try
        {
            C.Eq(a, "config.default", "Inline", new EdgeDetectionConfig().NotchScanMode);
            (string? Input, NotchScanMode Expected)[] rows = {
                (null, NotchScanMode.Inline), ("", NotchScanMode.Inline), (" ", NotchScanMode.Inline),
                ("\t", NotchScanMode.Inline), ("Inline", NotchScanMode.Inline), ("inline", NotchScanMode.Inline),
                ("INLINE", NotchScanMode.Inline), ("Dedicated", NotchScanMode.Dedicated),
                ("dedicated", NotchScanMode.Dedicated), ("DeDiCaTeD", NotchScanMode.Dedicated),
                (" Dedicated", NotchScanMode.Inline), ("Dedicated ", NotchScanMode.Inline),
                ("XX", NotchScanMode.Inline), ("1", NotchScanMode.Inline) };
            for (int i = 0; i < rows.Length; i++)
                C.Eq(a, $"parse.{i}", rows[i].Expected, NotchScanModeResolver.ParseMode(rows[i].Input));
            foreach (var language in new[] { "en-US", "zh-Hans" })
            {
                Vega.Utility.Common.Properties.Resources.Culture = CultureInfo.GetCultureInfo(language);
                var folder = (BaseFolder)Activator.CreateInstance(typeof(RecipeFolder))!;
                var recipe = AlignmentScanRecipeGenerator.Generate(folder);
                string p = language + ".202.";
                C.Eq(a, p + "id", 202L, recipe.ID);
                C.Eq(a, p + "folder", BaseFolder.CalibrationFolderID, recipe.FolderID);
                C.Eq(a, p + "path", folder.Path + "/", recipe.FolderPath);
                C.Eq(a, p + "name", language == "en-US" ? "Extra Alignment Scan Recipe" : "额外Alignment扫描配方", recipe.Name);
                C.Eq(a, p + "revision", 0, recipe.Revision);
                C.Eq(a, p + "collections", 1, recipe.CollectionElement.Collections.Count);
                C.Eq(a, p + "size", WaferSizeType.WS200, recipe.CollectionElement.WaferSize);
                var c = recipe.CollectionElement.Collections.Single();
                var d = c.DarkField;
                C.Eq(a, p + "incidence", IncidenceType.Normal, d.SizingContent.Incidence);
                C.Eq(a, p + "throughput", ThroughputType.HighSensitivity, c.WaferScan.Throughput);
                C.Eq(a, p + "polarization", PolarizerType.C, d.SizingContent.Polarization);
                C.Eq(a, p + "powerGear", true, new[] { 100.0, 10.0, 1.0 }.Contains(d.SizingContent.LaserPower));
                C.Eq(a, p + "edgeExclusion", 0.0, d.InspectionRegionContent.EdgeExclusion);
                C.Eq(a, p + "endRadiusMm", 100.0, d.InspectionRegionContent.PriInspRegionEndRadius);
                C.Eq(a, p + "secondary", false, d.InspectionRegionContent.IsSecScanEnable);
                C.Eq(a, p + "dic", false, c.BrightField.IsDICEnabled);
                C.Eq(a, p + "dw1", false, d.ChannelSettingContent.DW1.IsEnabled);
                C.Eq(a, p + "physical", "DN_Normal,DW2_Normal", string.Join(",", c.GetPhysicalChannels()));
                foreach (var mode in new[] { ChannelMode.DN_Normal, ChannelMode.DW2_Normal })
                {
                    var block = d.ChannelSettingContent[mode];
                    C.Eq(a, p + mode + ".exact", mode, block.ChannelMode);
                    C.Eq(a, p + mode + ".enabled", true, block.IsEnabled);
                    C.Eq(a, p + mode + ".od", true, block.OpticalFilter != CollectODFilterType.ODp4);
                    C.Eq(a, p + mode + ".pmt", true, double.IsFinite(block.PMTVolatge) && block.PMTVolatge > 0);
                    C.Eq(a, p + mode + ".masks", true, block.NMask == "Full" && block.WMask == "Full");
                }
                var layout = (ResultsLayoutContent)recipe.RecipeElements[2];
                C.Eq(a, p + "surfCategories", true, layout.GraphicLayout.GraphicItems
                    .Where(g => g.GraphicType == GraphicTypes.SURFImage).All(g => g.CategoryItems.Count == 0));
                var independent = AlignmentScanRecipeGenerator.Generate(folder);
                double original = d.ChannelSettingContent.DN.PMTVolatge;
                independent.CollectionElement.Collections[0].DarkField.ChannelSettingContent.DN.PMTVolatge = -7;
                C.Eq(a, p + "independentOptics", original, d.ChannelSettingContent.DN.PMTVolatge);
            }
            var lookup = typeof(Recipe).Assembly.GetTypes().Single(t => t.Name == "NotchScanRecipeLookup");
            foreach (long id in new[] { 200L, 201L, 202L })
                C.Eq(a, "lookup." + id, id != 202L, (bool)lookup.GetMethod("IsSystemRecipeID")!.Invoke(null, new object[] { id })!);
            var error = Record.Exception(() => { lookup.GetMethod("GetExpectedWaferSize")!.Invoke(null, new object[] { 202L }); });
            C.Eq(a, "lookup.202.noSizeContract", true, error is TargetInvocationException { InnerException: not null });
        }
        finally { Vega.Utility.Common.Properties.Resources.Culture = oldCulture; a.Flush(); }
    }
}
