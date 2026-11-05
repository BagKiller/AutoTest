using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Vega.AutoTest.Harness;
using Vega.Data.Analysis.Helper;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("LPD contracts", DisableParallelization = true)]
public class LpdContractCollection { }

internal static class LpdTest
{
    public static T Empty<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    public static void Equal<T>(AssertionRecorder a, string description, T expected, T actual)
    {
        string e = Convert.ToString(expected, CultureInfo.InvariantCulture) ?? "<null>";
        string v = Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "<null>";
        string id = $"{a.Count:D3}:{description}";
        if (EqualityComparer<T>.Default.Equals(expected, actual)) a.Pass(id, "behavior", description, e, v);
        else a.Fail(id, "behavior", description, e, v);
        Assert.Equal(expected, actual);
    }
    public static DFCollectorSettingContent Collector(ChannelMode mode, double min, double max, double xt, bool enabled = true)
    {
        var c = Empty<DFCollectorSettingContent>();
        c.ChannelMode = mode;
        c.IsEnabled = enabled;
        c.ThresMin = min;
        c.ThresMinSIT = 7777;
        c.ThresMax = max;
        c.ThresMaxXT = xt;
        return c;
    }
    public static CollectionContent Collection(string name, DFSizingUnit unit, params DFCollectorSettingContent[] collectors)
    {
        var c = Empty<CollectionContent>();
        c.CollectionName = name;
        c.DarkField = Empty<DarkFieldSettingContent>();
        c.DarkField.SizingContent = Empty<DFSizingContent>();
        c.DarkField.SizingContent.LPDUnit = unit;
        c.DarkField.ChannelSettingContent = Empty<DFChannelSettingContent>();
        typeof(DFChannelSettingContent).GetProperty("Collectors")!.SetValue(c.DarkField.ChannelSettingContent, new List<DFCollectorSettingContent>(collectors));
        c.WaferScan = Empty<WaferScanSettingContent>();
        return c;
    }
    public static Recipe Recipe(params CollectionContent[] collections)
    {
        var element = Empty<CollectionsContent>();
        element.ElementType = RecipeElementType.Collection;
        element.Collections = new List<CollectionContent>(collections);
        var recipe = Empty<Recipe>();
        recipe.RecipeElements = [new RecipeInfoContent(), element];
        return recipe;
    }
}

[Collection("LPD contracts")]
public class LpdRangeRegressionTests
{
    private static void Range(AssertionRecorder a, Recipe recipe, string attr, ChannelMode mode,
        string collection, string collector, bool expected, double min, double max, string defect = "LPD")
    {
        bool result = LpdBinningRangeResolver.TryGetRange(recipe, defect, attr, mode, collection, collector,
            0.09, true, true, out double actualMin, out double actualMax);
        string id = $"{defect}/{attr}/{mode}/{collection}/{collector}";
        LpdTest.Equal(a, id + " resolved", expected, result);
        LpdTest.Equal(a, id + " minimum", min, actualMin);
        LpdTest.Equal(a, id + " maximum", max, actualMax);
    }

    [Fact]
    public void AttributeUnitsUseRawThresholdsAndRejectUndefinedCrossUnits()
    {
        var a = new AssertionRecorder();
        try
        {
            foreach (bool waferRecipe in new[] { false, true })
            {
                foreach (var unit in new[] { DFSizingUnit.umLSE, DFSizingUnit.nPPM, DFSizingUnit.rPPM })
                {
                    var recipe = LpdTest.Recipe(LpdTest.Collection("A", unit,
                        LpdTest.Collector(ChannelMode.DW2_Normal, 125, 675, 975)));
                    recipe.IsWaferRunRecipe = waferRecipe;
                    foreach (string defect in new[] { "LPD", "LPD_N", "LPD_S", "LPD_E" })
                    {
                        Range(a, recipe, "Size", ChannelMode.DW2_Normal, "A", "DW2", true, 125, 675, defect);
                        if (unit != DFSizingUnit.umLSE)
                            Range(a, recipe, unit == DFSizingUnit.nPPM ? "NppmSize" : "RppmSize",
                                ChannelMode.DW2_Normal, "A", "DW2", true, 125, 675, defect);
                    }
                }
                foreach (var row in new[] { (DFSizingUnit.nPPM, "RppmSize"), (DFSizingUnit.rPPM, "NppmSize") })
                {
                    var recipe = LpdTest.Recipe(LpdTest.Collection("A", row.Item1,
                        LpdTest.Collector(ChannelMode.DW2_Normal, 125, 675, 975)));
                    recipe.IsWaferRunRecipe = waferRecipe;
                    Range(a, recipe, row.Item2, ChannelMode.DW2_Normal, "A", "DW2", false, 0, 0);
                }
            }
            foreach (var row in new[] { ("Scratch", "Size"), ("LPD", "ADC"), ("lpd", "Size") })
                Range(a, LpdTest.Recipe(), row.Item2, ChannelMode.DW2_Normal, "A", "DW2", false, 0, 0, row.Item1);
        }
        finally { a.Flush(); }
    }

    [Fact]
    public void ParticipationExtendedThresholdsAndMixedCollectionsRetainTheirMeaning()
    {
        var a = new AssertionRecorder();
        try
        {
            var dw = LpdTest.Collector(ChannelMode.DW2_Normal, 20, 80, 120);
            var dn = LpdTest.Collector(ChannelMode.DN_Normal, 10, 90, 110);
            var disabled = LpdTest.Collector(ChannelMode.DW1_Oblique, -100, 9000, 9000, false);
            var first = LpdTest.Collection("A", DFSizingUnit.nPPM, dw, dn, disabled);
            var second = LpdTest.Collection("B", DFSizingUnit.rPPM, LpdTest.Collector(ChannelMode.DW2_Normal, 5, 200, 300));
            first.DarkField.IsExtendedEnabled = second.DarkField.IsExtendedEnabled = true;
            var recipe = LpdTest.Recipe(first, second, LpdTest.Collection("Empty", DFSizingUnit.umLSE));
            Range(a, recipe, "Size", ChannelMode.DC, "ignored", "ignored", true, 5, 300);
            Range(a, recipe, "Size", ChannelMode.DC_Normal, "A", "ignored", true, 10, 120);
            Range(a, recipe, "NppmSize", ChannelMode.DC_Normal, "A", "ignored", true, 10, 120);
            Range(a, recipe, "NppmSize", ChannelMode.DC, "ignored", "ignored", false, 0, 0);
            Range(a, recipe, "Size", ChannelMode.DW1_Oblique, "A", "DW1", true, -100, 9000);
            Range(a, recipe, "NppmSize", ChannelMode.DW1_Oblique, "A", "DW1", false, 0, 0);
            Range(a, recipe, "Size", ChannelMode.DW2_Normal, "missing", "DW2", false, 0, 0);
            Range(a, recipe, "Size", ChannelMode.DW2_Normal, "A", "missing", false, 0, 0);
            Range(a, recipe, "Size", ChannelMode.DC_Normal, "Empty", "ignored", false, 0, 0);
            first.DarkField.IsExtendedEnabled = false;
            Range(a, recipe, "NppmSize", ChannelMode.DW2_Normal, "A", "DW2", true, 20, 80);
            first.DarkField.IsExtendedEnabled = true;
            Range(a, recipe, "NppmSize", ChannelMode.DW2_Normal, "A", "DW2", true, 20, 120);
            dw.ThresMaxXT = 60;
            Range(a, recipe, "NppmSize", ChannelMode.DW2_Normal, "A", "DW2", true, 20, 80);
            dw.IsEnabled = dn.IsEnabled = false;
            Range(a, recipe, "Size", ChannelMode.DC_Normal, "A", "ignored", false, 0, 0);
        }
        finally { a.Flush(); }
    }

    [Fact]
    public void NonpositiveMaximumDefaultsButInvalidPositiveIntervalsAreRejected()
    {
        var a = new AssertionRecorder();
        try
        {
            foreach (var attr in new[] { "Size", "NppmSize", "RppmSize" })
            foreach (var row in new[] { (0d, 0d, true, 150000d), (10d, 5d, false, 5d), (5d, 5d, false, 5d), (150001d, 0d, false, 150000d) })
            {
                var unit = attr == "RppmSize" ? DFSizingUnit.rPPM : DFSizingUnit.nPPM;
                var recipe = LpdTest.Recipe(LpdTest.Collection("A", unit,
                    LpdTest.Collector(ChannelMode.DW2_Normal, row.Item1, row.Item2, 999999)));
                Range(a, recipe, attr, ChannelMode.DW2_Normal, "A", "DW2", row.Item3, row.Item1, row.Item4);
            }
        }
        finally { a.Flush(); }
    }
}
