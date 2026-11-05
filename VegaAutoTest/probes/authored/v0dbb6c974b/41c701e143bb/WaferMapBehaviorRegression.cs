using System;
using System.Collections.Generic;
using System.Linq;
using Vega.AutoTest.Harness;
using Vega.Data.Defects;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.UI.Common;
using Xunit;
using static Vega.AutoTest.Authored.MapKit;

namespace Vega.AutoTest.Authored;

[Collection("Authored map regression")]
public sealed class WaferMapBehaviorRegression
{
    readonly MapDispatcher dispatcher;
    public WaferMapBehaviorRegression(MapDispatcher dispatcher) => this.dispatcher = dispatcher;
    [Fact]
    public void EmptyBins_DefaultLegends_MixedCategories_AndAllMode()
    {
        dispatcher.Run(r =>
        {
            foreach (string name in new[] { "LPD", "Scratch", "Cluster" })
            {
                var c = Cat(name); var b = Bin(c); var m = Map(c, b);
                try
                {
                    var bar = m.LightColorBarViewModel; var bins = bar.ColorBar.Bins;
                    Eq(r, "Single binned category can auto-select, including non-LPD", true, typeof(WaferMapViewModel).GetProperty("CanToggleAllDefectsMode", Flags)!.GetValue(m));
                    var groups = Generate(m, b, Def(.4999, name), Def(.5009, name), Def(.9999, name));
                    Eq(r, "Three-decimal truncation assigns boundary samples", new[] { 1, 2 }, groups.Select(g => g.Defects.Count).ToArray());
                    Eq(r, "Occupied bins are not empty", new[] { false, false }, bins.Select(x => x.IsEmpty).ToArray());
                    Generate(m, b);
                    Eq(r, "Zero defects marks every ordinary bin", new[] { true, true }, bins.Select(x => x.IsEmpty).ToArray());
                    bins[1].IsEnabled = false; Generate(m, b, Def(.25, name));
                    Eq(r, "Recomputation clears stale marks including skipped disabled bins", new[] { false, false }, bins.Select(x => x.IsEmpty).ToArray());
                    Eq(r, "Empty-state computation preserves statistics disabling", false, bins[1].IsEnabled);
                    bins[1].IsEnabled = true; m.LayoutType = LayoutTypes.Recipe; Generate(m, b);
                    Eq(r, "Recipe grouping does not write empty marks", new[] { false, false }, bins.Select(x => x.IsEmpty).ToArray());
                    m.LayoutType = LayoutTypes.Result; bins[1].IsEmpty = true;
                    Generate(m, Bin(Cat(name + "Other")));
                    Eq(r, "Another category cannot overwrite this legend", new[] { false, true }, bins.Select(x => x.IsEmpty).ToArray());
                    var before = bar.ColorBarDrawingGroup.Children.ToArray(); Generate(m, b, Def(.25, name));
                    Eq(r, "Grouping updates state without rebuilding drawing children", true, before.SequenceEqual(bar.ColorBarDrawingGroup.Children));
                    var emptyGroups = (List<DefectGroup>)Call(m, "BuildDefaultDisplayGroups", new List<BinningItemContent> { b })!;
                    Eq(r, "Default zero-defect category still reaches empty marking", new[] { true, true }, bins.Select(x => x.IsEmpty).ToArray());
                    Eq(r, "Empty marking does not manufacture defect points", 0, emptyGroups.SelectMany(g => g.Defects).Count());
                }
                finally { m.OnNavigatingFrom(); }
            }
            foreach (var row in new[] { ("LPD", SizeType.LPD_SATURATED), ("PPD", SizeType.PPD_SATURATED) })
            foreach (bool ordinary in new[] { false, true })
            {
                var c = Cat(row.Item1); var m = Map(c, null);
                try
                {
                    var bar = m.LightColorBarViewModel; var bin = bar.ColorBar.Bins.Single();
                    Eq(r, "Unbinned legend uses category color, [0,1], six intervals", "0,1,4278239231,6", $"{bin.Min},{bin.Max},{bin.ColorCode},{bar.UniformTickIntervalCount}");
                    var defects = new List<DefectInfo> { Def(2.75, row.Item1, row.Item2) };
                    if (ordinary) defects.Add(Def(2.5, row.Item1));
                    var groups = new List<DefectGroup>(); Add(m, c, groups, defects.ToArray());
                    Eq(r, "Placeholder range never clips ordinary or saturated data", ordinary ? new[] { 2.5, 2.75 } : new[] { 2.75 }, Sizes(groups));
                    Eq(r, "Only ordinary occupancy controls the main-segment empty mark", !ordinary, bin.IsEmpty);
                    Eq(r, "Unbinned saturation creates SAT", true, bar.ColorBar.ShowSaturatedBin);
                    Eq(r, "Unbinned legend has no OverRange", false, bar.ColorBar.ShowOverRangeBin);
                    bar.DrawColorBar(); Eq(r, "Empty single-color legend draws a whole-segment cross", ordinary ? 0 : 2, CrossLines(bar.ColorBarDrawingGroup));
                    bin.IsHidden = true; groups.Clear(); Add(m, c, groups, defects.ToArray());
                    Eq(r, "Ordinary segment filtering retains saturation", new[] { 2.75 }, Sizes(groups));
                    bin.IsHidden = false; bar.ColorBar.IsSaturatedBinHidden = true; groups.Clear(); Add(m, c, groups, defects.ToArray());
                    Eq(r, "SAT filtering retains ordinary data", ordinary ? new[] { 2.5 } : Array.Empty<double>(), Sizes(groups));
                    bar.ColorBar.IsSaturatedBinHidden = false; groups.Clear(); Add(m, c, groups, defects.ToArray());
                    Eq(r, "Restoring both segments restores original data", ordinary ? new[] { 2.5, 2.75 } : new[] { 2.75 }, Sizes(groups));
                }
                finally { m.OnNavigatingFrom(); }
            }
            var owner = Cat(); var ownerBin = Bin(owner); var mixed = Map(owner, ownerBin);
            try
            {
                var ppd = Cat("PPD"); var bar = mixed.LightColorBarViewModel;
                bar.ColorBar.IsSaturatedBinEnabled = false;
                var groups = Generate(mixed, ownerBin, Def(.25), Def(2.75, "LPD", SizeType.LPD_SATURATED));
                Add(mixed, ppd, groups, Def(1.5, "PPD"), Def(4, "PPD", SizeType.PPD_SATURATED));
                Eq(r, "Statistics disabling applies only to the owning binned category", new[] { .25, 1.5, 4.0 }, Sizes(groups));
                Eq(r, "Unbinned SAT contributes to the shared segment", true, bar.ColorBar.ShowSaturatedBin);
                bar.ColorBar.IsSaturatedBinHidden = true;
                groups = Generate(mixed, ownerBin, Def(.25), Def(2.75, "LPD", SizeType.LPD_SATURATED));
                Add(mixed, ppd, groups, Def(1.5, "PPD"), Def(4, "PPD", SizeType.PPD_SATURATED));
                Eq(r, "Shared SAT hiding affects binned and unbinned saturation", new[] { .25, 1.5 }, Sizes(groups));
                bar.ColorBar.IsSaturatedBinHidden = false; groups.Clear(); Add(mixed, ppd, groups, Def(4, "PPD", SizeType.PPD_SATURATED));
                Eq(r, "Restoring shared SAT still allows unbinned saturation", new[] { 4.0 }, Sizes(groups));
                bar.ColorBar.ShowSaturatedBin = false; var marks = bar.ColorBar.Bins.Select(x => x.IsEmpty).ToArray();
                var other = Generate(mixed, Bin(ppd), Def(2.75, "PPD", SizeType.PPD_SATURATED));
                Eq(r, "Another binned category does not determine owner SAT presence", false, bar.ColorBar.ShowSaturatedBin);
                Eq(r, "Another binned category retains its own saturation", new[] { 2.75 }, Sizes(other));
                Eq(r, "Another binned category leaves owner empty marks intact", marks, bar.ColorBar.Bins.Select(x => x.IsEmpty).ToArray());
            }
            finally { mixed.OnNavigatingFrom(); }
            var all = Map(owner, ownerBin);
            try
            {
                var defects = new[] { Def(.25), Def(1.5), Def(2.75, "LPD", SizeType.LPD_SATURATED), Def(4) };
                var analysis = New<AnalysisResult>();
                typeof(AnalysisResult).GetProperty("ChannelMode", Flags)!.SetValue(analysis, owner.ChannelMode);
                analysis.Defects.AddRange(defects); Field<List<AnalysisResult>>(all, "_allAnalysisResults").Add(analysis);
                Field<List<DefectGroup>>(all, "_allDefectsGroupsForAll").Add(new DefectGroup(System.Drawing.Color.Blue, defects.ToList()));
                var bar = all.LightColorBarViewModel; bar.ColorBar.Bins[0].IsHidden = true; bar.ColorBar.IsSaturatedBinHidden = true;
                all.IsAllDefectsMode = true;
                Eq(r, "All shows complete data including out-of-range and hidden points", new[] { .25, 1.5, 2.75, 4 }, Sizes(all.WaferMapImageViewModel.DefectGroups));
                Eq(r, "All derives special segments from full data", new[] { true, true }, new[] { bar.ColorBar.ShowSaturatedBin, bar.ColorBar.ShowOverRangeBin });
                Eq(r, "All legend is read-only", false, typeof(LightColorBarViewModel).GetProperty("IsInteractive", Flags)!.GetValue(bar));
                Eq(r, "All preserves stored filter flags", new[] { true, true }, new[] { bar.ColorBar.Bins[0].IsHidden, bar.ColorBar.IsSaturatedBinHidden });
                all.IsAllDefectsMode = false;
                Eq(r, "Leaving All reapplies stored ordinary and SAT filters", new[] { 1.5 }, Sizes(all.WaferMapImageViewModel.DefectGroups));
                Eq(r, "Leaving All restores interactivity", true, typeof(LightColorBarViewModel).GetProperty("IsInteractive", Flags)!.GetValue(bar));
                Eq(r, "Source defect values survive display changes", new[] { .25, 1.5, 2.75, 4 }, defects.Select(d => d.Attributes["Size"]).ToArray());
            }
            finally { all.OnNavigatingFrom(); }
        });
    }
}
