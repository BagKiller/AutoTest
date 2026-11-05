using System;
using System.Linq;
using System.Windows.Media;
using Vega.Data.Defects;
using Vega.UI.Common;
using Xunit;
using static Vega.AutoTest.Authored.MapKit;

namespace Vega.AutoTest.Authored;

[Collection("Authored map regression")]
public sealed class WaferMapDrawingRegression
{
    readonly MapDispatcher dispatcher;
    public WaferMapDrawingRegression(MapDispatcher dispatcher) => this.dispatcher = dispatcher;
    [Fact]
    public void VectorZoomRejectsInvalidCoordinatesAndReturnsToBitmap()
    {
        dispatcher.Run(r =>
        {
            var vm = new WaferMapImageViewModel(); vm.SetWaferInfos(150000, 3000);
            var good = new[] { Def(.25), Def(.5), Def(.75) };
            good[1].Attributes["X"] = 1000; good[2].Attributes["Y"] = -1000;
            var group = new DefectGroup(System.Drawing.Color.Blue, good.ToList()); vm.DrawDefects(new() { group });
            Eq(r, "Initial bitmap has finite wafer-sized bounds", true, double.IsFinite(vm.WaferMapDrawingGroup.Bounds.Width) && vm.WaferMapDrawingGroup.Bounds.Width <= 4300);
            foreach (var xy in new[] { (5e8, -5e8), (-5e8, 5e8), (double.NaN, 0.0), (0.0, double.PositiveInfinity), (double.NegativeInfinity, 0.0) })
            { var bad = Def(.3); bad.Attributes["X"] = xy.Item1; bad.Attributes["Y"] = xy.Item2; group.Defects.Add(bad); }
            foreach (double scale in new[] { 10.0, 10.5 })
            {
                vm.ScaleTransform = scale; vm.DrawDefects(new() { group }, pointsInBitmap: false);
                int dots = vm.WaferMapDrawingGroup.Children.OfType<GeometryDrawing>().Where(g => g.Geometry is GeometryGroup).Sum(g => ((GeometryGroup)g.Geometry).Children.Count);
                Eq(r, "Only the three valid vector dots survive", 3, dots);
                var bounds = vm.WaferMapDrawingGroup.Bounds;
                Eq(r, "Invalid points cannot expand vector bounds", true, double.IsFinite(bounds.Width) && double.IsFinite(bounds.Height) && bounds.Width <= 4300 && bounds.Height <= 4300);
                Eq(r, "High-zoom drawing remains usable", false, vm.IsEmpty);
            }
            vm.ScaleTransform = 9.5; vm.DrawDefects(new() { new DefectGroup(System.Drawing.Color.Blue, good.ToList()) });
            Eq(r, "Returning below threshold preserves bounded bitmap rendering", true, double.IsFinite(vm.WaferMapDrawingGroup.Bounds.Width) && vm.WaferMapDrawingGroup.Bounds.Width <= 4300);
            Eq(r, "Returning below threshold remains nonempty", false, vm.IsEmpty);
        });
    }
    [Fact]
    public void HorizontalAndVerticalCrosses_ReadOnlyStyle_AndGrayTickIsolation()
    {
        dispatcher.Run(r =>
        {
            foreach (bool horizontal in new[] { false, true })
            {
                var bar = new LightColorBarViewModel(Bin(Cat()), horizontal ? 300 : 60, horizontal ? 60 : 300, Guid.Empty);
                try
                {
                    bar.ColorBar.ShowSaturatedBin = true; bar.ColorBar.ShowOverRangeBin = true; bar.DrawColorBar();
                    Eq(r, "Occupied enabled bins have no crosses", 0, CrossLines(bar.ColorBarDrawingGroup));
                    bar.ColorBar.Bins[0].IsEmpty = true; bar.DrawColorBar(); Eq(r, "Empty bin draws two cross strokes", 2, CrossLines(bar.ColorBarDrawingGroup));
                    bar.ColorBar.Bins[0].IsEmpty = false; bar.ColorBar.Bins[0].IsEnabled = false; bar.DrawColorBar(); Eq(r, "Disabled and empty bins share cross construction", 2, CrossLines(bar.ColorBarDrawingGroup));
                    foreach (var bin in bar.ColorBar.Bins) { bin.IsEnabled = true; bin.IsEmpty = true; }
                    bar.DrawColorBar(); Eq(r, "Zero ordinary occupancy crosses normal bins only, never special tails", 4, CrossLines(bar.ColorBarDrawingGroup));
                    foreach (var bin in bar.ColorBar.Bins) { bin.IsEmpty = false; bin.IsHidden = false; }
                    bar.DrawColorBar(); var full = SegmentSpans(bar, horizontal);
                    foreach (var bin in bar.ColorBar.Bins) bin.IsHidden = true;
                    bar.DrawColorBar(); Eq(r, "Read-only legend ignores hidden styling", full, SegmentSpans(bar, horizontal));
                    typeof(LightColorBarViewModel).GetProperty("IsInteractive", Flags)!.SetValue(bar, true); bar.DrawColorBar();
                    Eq(r, "Interactive hidden segments narrow to one third", full.Select(x => Math.Round(x / 3, 6)).ToArray(), SegmentSpans(bar, horizontal).Select(x => Math.Round(x, 6)).ToArray());
                    var gray = new LightColorBarViewModel(0, 255) { ColorBarWidth = horizontal ? 300 : 60, ColorBarHeight = horizontal ? 60 : 300 };
                    try { gray.DrawColorBar(); string before = DrawingShape(gray); gray.UniformTickIntervalCount = 1; gray.DrawColorBar(); Eq(r, "Explicit legend intervals do not alter grayscale ticks", before, DrawingShape(gray)); }
                    finally { gray.OnNavigateFrom(); }
                }
                finally { bar.OnNavigateFrom(); }
            }
        });
    }
    static double[] SegmentSpans(LightColorBarViewModel bar, bool horizontal) => Flat(bar.ColorBarDrawingGroup).OfType<GeometryDrawing>().Where(g => g.Geometry is RectangleGeometry && g.Brush is SolidColorBrush b && (b.Color == Colors.Blue || b.Color == Colors.Lime)).Select(g => horizontal ? g.Geometry.Bounds.Height : g.Geometry.Bounds.Width).ToArray();
    static string DrawingShape(LightColorBarViewModel bar) => string.Join(";", Flat(bar.ColorBarDrawingGroup).Select(d => d.GetType().Name + ":" + d.Bounds));
}
