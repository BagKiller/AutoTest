using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Recipes;
using Vega.UI.Common;
using Vega.UI.Inspection;
using Vega.Utility.Common;
using Xunit;
using static Vega.AutoTest.Authored.MapKit;

namespace Vega.AutoTest.Authored;

[Collection("Authored map regression")]
public sealed class RecipeAndOverRangeRegression
{
    readonly MapDispatcher dispatcher;
    public RecipeAndOverRangeRegression(MapDispatcher dispatcher) => this.dispatcher = dispatcher;
    sealed class HistogramProbe : HistogramViewModel
    {
        public List<BinningItemContent>? Entry; public int AfterBase;
        public override void LoadData(GraphicItemContent content) { Entry = BinningItems; base.LoadData(content); AfterBase = HistogramImageViewModel.HistogramDrawingGroup.Children.Count; }
    }
    sealed class MapProbe : WaferMapViewModel
    {
        public List<BinningItemContent>? Entry; public int Initializations;
        public MapProbe() { PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LightColorBarViewModel) && LightColorBarViewModel != null) Initializations++; }; }
        public override void LoadData(GraphicItemContent content) { Entry = BinningItems; base.LoadData(content); }
    }
    [Fact]
    public void RecipeLoaderInjectsBeforeDrawingAndUsesConfiguredPalette()
    {
        dispatcher.Run(r =>
        {
            foreach (string state in new[] { "enabled", "disabled", "empty", "missing" })
            {
                var recipe = New<Recipe>();
                foreach (var old in recipe.RecipeElements.OfType<BinningContent>().ToArray()) recipe.RecipeElements.Remove(old);
                if (!recipe.RecipeElements.OfType<CollectionsContent>().Any()) recipe.RecipeElements.Add(New<CollectionsContent>());
                var c = Cat(); var b = Bin(c); var element = new BinningContent("Regression") { IsElementEnabled = state != "disabled" };
                if (state != "empty") element.BinningCollections.Add(b);
                if (state != "missing") recipe.RecipeElements.Add(element);
                bool injected = state == "enabled";
                var layout = new GraphicLayoutViewModel(false); Set(layout, "_currentRecipe", recipe); Set(layout, "_unitByCollection", new Dictionary<string, DFSizingUnit?>());
                var histogram = new HistogramProbe(); var map = new MapProbe();
                try
                {
                    Call(layout, "LoadRecipeGraphicData", histogram, new GraphicItemContent(new Area(), GraphicTypes.Histogram, c));
                    Eq(r, "Histogram receives enabled nonempty bins before LoadData", injected, ReferenceEquals(element.BinningCollections, histogram.Entry));
                    Eq(r, "Preset bins suppress fallback drawing; other states retain it", !injected, histogram.AfterBase > 0);
                    Eq(r, "Recipe histogram ultimately draws", true, histogram.HistogramImageViewModel.HistogramDrawingGroup.Children.Count > 0);
                    Call(layout, "LoadRecipeGraphicData", map, new GraphicItemContent(new Area(), GraphicTypes.WaferMap, c));
                    Eq(r, "WaferMap receives enabled nonempty bins before LoadData", injected, ReferenceEquals(element.BinningCollections, map.Entry));
                    Eq(r, "First preview uses configured bins or the default category color", injected ? new uint[] { 0xFF0000FF, 0xFF00FF00 } : new uint[] { 0xFF00BFFF }, map.LightColorBarViewModel.ColorBar.Bins.Select(x => x.ColorCode).ToArray());
                    Eq(r, "Recipe loader initializes the wafer legend once", 1, map.Initializations);
                    Eq(r, "Recipe preview never marks occupancy empty", true, map.LightColorBarViewModel.ColorBar.Bins.All(x => !x.IsEmpty));
                    if (injected)
                    {
                        var data = new List<HistogramMatchData> { new() { BinningItem = b, CategoryName = c.CategoryName, Values = new() { .25, .75 } } };
                        Call(histogram, "RenderHistogram", data, HistogramType.DefectHist);
                        var colors = Flat(histogram.HistogramImageViewModel.HistogramDrawingGroup).OfType<GeometryDrawing>().Where(g => g.Brush is SolidColorBrush).Select(g => ((SolidColorBrush)g.Brush).Color).ToArray();
                        Eq(r, "Deterministic recipe histogram contains first configured bin color", true, colors.Contains(Colors.Blue));
                        Eq(r, "Deterministic recipe histogram contains second configured bin color", true, colors.Contains(Colors.Lime));
                    }
                }
                finally { map.OnNavigatingFrom(); histogram.OnNavigatingFrom(); }
            }
        });
    }
    [Fact]
    public void OverRangePresenceUsesPrecisionMatchingAndInclusionPolicy()
    {
        dispatcher.Run(r =>
        {
            var b = Bin(Cat());
            var rows = new List<(DefectInfo Defect, bool Expected)> { (Def(1), false), (Def(1.0009), false), (Def(1.001), true), (Def(3), true), (Def(3.0009), true), (Def(3.001), false), (Def(1.5, "LPD", SizeType.LPD_SATURATED), false) };
            var missing = Def(1.5); missing.Attributes.Remove("Size"); rows.Add((missing, false));
            var excluded = Def(1.5); excluded.Is_Excluded = true; rows.Add((excluded, false));
            var collection = Def(1.5); collection.CollectionName = "Other"; rows.Add((collection, false));
            var channel = Def(1.5); channel.ChannelMode = ChannelMode.DC_Normal; rows.Add((channel, false));
            var advanced = Def(1.5); advanced.IsAdvanced = true; rows.Add((advanced, false)); rows.Add((Def(1.5, "Scratch"), false));
            string before = string.Join(";", rows.Select(x => string.Join(",", x.Defect.Attributes.Select(a => a.Key + "=" + a.Value))));
            foreach (var row in rows) Eq(r, "OverRange requires a matching ordinary value in (last bin max, configured max] after truncation", row.Expected, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { row.Defect }, b));
            foreach (var mode in Enum.GetValues<LpdSizeDefectIncludeMode>())
            {
                b.LpdSizeDefectIncludeMode = mode;
                Eq(r, "LPD saturation is never ordinary OverRange", false, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { Def(1.5, "LPD", SizeType.LPD_SATURATED) }, b));
                Eq(r, "Extended defects are ordinary OverRange only in IncludeAllSizeDefects", mode == LpdSizeDefectIncludeMode.IncludeAllSizeDefects, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { Def(1.5, "Extended") }, b));
            }
            var empty = Bin(Cat()); empty.Bins.Clear(); Eq(r, "No normal bins means no OverRange", false, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { Def(1.5) }, empty));
            var invalid = Bin(Cat()); invalid.Category = new CategoryItemContentBase(); Eq(r, "Non-defect category has no OverRange", false, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { Def(1.5) }, invalid));
            Eq(r, "Non-LPD binning also detects ordinary OverRange", true, BinningDefectSelectionHelper.HasOverRangeDefects(new[] { Def(1.5, "Scratch") }, Bin(Cat("Scratch"))));
            Eq(r, "Presence queries leave source attributes intact", before, string.Join(";", rows.Select(x => string.Join(",", x.Defect.Attributes.Select(a => a.Key + "=" + a.Value)))));
        });
    }
}
