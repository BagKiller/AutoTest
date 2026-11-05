// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using CommunityToolkit.Mvvm.Messaging;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.UI.Common;

namespace Vega.UI.Inspection.Tests;

[Collection("Result summary binning")]
public sealed class WaferMapSpecialBinTests
{
    private const double OrdinarySize = 0.25;
    private const double OverRangeSize = 2.5;
    private const double SaturatedSize = 2.75;
    private readonly SummaryBinningDispatcher _dispatcher;

    public WaferMapSpecialBinTests(SummaryBinningDispatcher dispatcher) => _dispatcher = dispatcher;

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void BinnedResult_SpecialSegmentsFollowDataBeforeAndAfterSummary(bool hasSaturated, bool hasOverRange)
    {
        List<DefectInfo> defects = [CreateDefect(OrdinarySize)];
        if (hasSaturated)
        {
            defects.Add(CreateDefect(SaturatedSize, SizeType.LPD_SATURATED));
        }
        if (hasOverRange)
        {
            defects.Add(CreateDefect(OverRangeSize));
        }

        WithWaferMap(true, defects, map =>
        {
            AssertSpecialSegments(map, hasSaturated, hasOverRange);
            Assert.Equal(defects.Count, VisibleSizes(map).Length);

            LightColorBarViewModel colorBar = map.LightColorBarViewModel!;
            WeakReferenceMessenger.Default.Send(new SummaryBinStatusMessage(colorBar.ColorBar.CategoryName, [], true, true, colorBar.Id));
            AssertSpecialSegments(map, hasSaturated, hasOverRange);
            Assert.Equal(defects.Count, VisibleSizes(map).Length);

            uint normalBinCount = (uint) colorBar.ColorBar.Bins.Count;
            WeakReferenceMessenger.Default.Send(new SummaryBinStatusMessage(colorBar.ColorBar.CategoryName,
                [normalBinCount + 1, normalBinCount + 2], true, true, colorBar.Id));
            AssertSpecialSegments(map, hasSaturated, hasOverRange);
            Assert.Equal([OrdinarySize], VisibleSizes(map));
        });
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    public void UnbinnedResult_KeepsSatWithoutUsingPlaceholderAsOverRange(bool hasSaturated, bool hasOrdinary, bool selectCategory)
    {
        List<DefectInfo> defects = [];
        if (hasOrdinary)
        {
            defects.Add(CreateDefect(OverRangeSize));
        }
        if (hasSaturated)
        {
            defects.Add(CreateDefect(SaturatedSize, SizeType.LPD_SATURATED));
        }

        WithWaferMap(false, defects, map =>
        {
            if (selectCategory)
            {
                map.SelectedCategoryItem = map.CategoryItems[0];
            }

            AssertSpecialSegments(map, hasSaturated, false);
            Assert.Equal(defects.Select(defect => defect.Attributes[nameof(AttributeName.Size)]), VisibleSizes(map));
            Assert.Equal(!hasOrdinary, Assert.Single(map.LightColorBarViewModel!.ColorBar.Bins).IsEmpty);
        });
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    public void ResultMode_SpecialSegmentClickFiltersAndRestores(bool hasBinning, bool saturated, bool horizontal)
    {
        double specialSize = saturated ? SaturatedSize : OverRangeSize;
        SizeType specialType = saturated ? SizeType.LPD_SATURATED : SizeType.LPD_SIZED;
        Color specialColor = saturated ? Colors.Red : Colors.Pink;
        WithWaferMap(hasBinning, [CreateDefect(OrdinarySize), CreateDefect(specialSize, specialType)], map =>
        {
            LightColorBarViewModel colorBar = map.LightColorBarViewModel!;
            if (horizontal)
            {
                colorBar.ColorBarWidth = 150;
                colorBar.ColorBarHeight = 50;
                colorBar.DrawColorBar();
            }

            ClickSegment(colorBar, specialColor);
            Assert.Equal([specialSize], VisibleSizes(map));

            ClickSegment(colorBar, Colors.DeepSkyBlue);
            Assert.Equal([OrdinarySize, specialSize], VisibleSizes(map));

            ClickSegment(colorBar, Colors.DeepSkyBlue);
            Assert.Equal([OrdinarySize], VisibleSizes(map));
            Assert.Single(FindSegments(colorBar.ColorBarDrawingGroup, specialColor));

            ClickSegment(colorBar, specialColor, ctrl: true);
            Assert.Equal([OrdinarySize, specialSize], VisibleSizes(map));
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void AllMode_UsesFullDefectDataAndConfiguredOverRangeBounds(bool hasSaturated, bool hasOverRange)
    {
        List<DefectInfo> defects = [CreateDefect(4)];
        if (hasSaturated)
        {
            defects.Add(CreateDefect(SaturatedSize, SizeType.LPD_SATURATED));
        }
        if (hasOverRange)
        {
            defects.Add(CreateDefect(OverRangeSize));
        }

        WithWaferMap(true, defects, map =>
        {
            AssertSpecialSegments(map, false, hasOverRange);

            map.SelectedCategoryItem = null;
            map.IsAllDefectsMode = true;

            AssertSpecialSegments(map, hasSaturated, hasOverRange);
            Assert.Equal(defects.Select(defect => defect.Attributes[nameof(AttributeName.Size)]).Order(), VisibleSizes(map));
        }, excludeSaturated: true);
    }

    [Fact]
    public void UnbinnedResult_AggregatesOrdinaryAndSaturatedPointsAcrossCategories()
    {
        List<DefectInfo> defects =
        [
            CreateDefect(SaturatedSize, SizeType.LPD_SATURATED),
            CreateDefect(OverRangeSize, SizeType.PPD_SIZED, "PPD"),
            CreateDefect(4, SizeType.PPD_SATURATED, "PPD")
        ];
        WithWaferMap(false, defects, map =>
        {
            AssertSpecialSegments(map, true, false);
            Assert.False(Assert.Single(map.LightColorBarViewModel!.ColorBar.Bins).IsEmpty);
            Assert.Equal([OverRangeSize, SaturatedSize, 4], VisibleSizes(map));
            Assert.Equal(System.Drawing.Color.Red, Assert.Single(map.WaferMapImageViewModel.DefectGroups,
                group => group.Defects.Any(defect => defect.SizeType == SizeType.PPD_SATURATED)).DefectColor);
        }, includePpdCategory: true);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void MixedResult_SatLegendFiltersBinnedAndUnbinnedSaturation(bool hasBinnedSat, bool ppdFirst)
    {
        const double ppdOrdinarySize = 1.5;
        const double ppdSatSize = 4;
        List<DefectInfo> defects =
        [
            CreateDefect(OrdinarySize),
            CreateDefect(ppdOrdinarySize, SizeType.PPD_SIZED, "PPD"),
            CreateDefect(ppdSatSize, SizeType.PPD_SATURATED, "PPD")
        ];
        if (hasBinnedSat)
        {
            defects.Add(CreateDefect(SaturatedSize, SizeType.LPD_SATURATED));
        }

        WithWaferMap(true, defects, map =>
        {
            LightColorBarViewModel colorBar = map.LightColorBarViewModel!;
            AssertSpecialSegments(map, true, false);
            Assert.Equal(defects.Select(defect => defect.Attributes[nameof(AttributeName.Size)]).Order(), VisibleSizes(map));

            ClickSegment(colorBar, Colors.Red);
            Assert.Equal(hasBinnedSat ? [ppdOrdinarySize, SaturatedSize, ppdSatSize] : [ppdOrdinarySize, ppdSatSize], VisibleSizes(map));

            ClickSegment(colorBar, Colors.DeepSkyBlue);
            ClickSegment(colorBar, Colors.DeepSkyBlue);
            Assert.Equal([OrdinarySize, ppdOrdinarySize], VisibleSizes(map));
            AssertSpecialSegments(map, true, false);
            ClickSegment(colorBar, Colors.Red, ctrl: true);
            Assert.Equal(defects.Count, VisibleSizes(map).Length);

            uint satBinIndex = (uint) colorBar.ColorBar.Bins.Count + 2;
            WeakReferenceMessenger.Default.Send(new SummaryBinStatusMessage(colorBar.ColorBar.CategoryName,
                [satBinIndex], true, false, colorBar.Id));
            Assert.Equal([OrdinarySize, ppdOrdinarySize, ppdSatSize], VisibleSizes(map));
            ClickSegment(colorBar, Colors.DeepSkyBlue);
            Assert.Equal([OrdinarySize, ppdOrdinarySize], VisibleSizes(map));
            ClickSegment(colorBar, Colors.Red, ctrl: true);
            Assert.Equal([OrdinarySize, ppdOrdinarySize, ppdSatSize], VisibleSizes(map));
        }, includePpdCategory: true, ppdFirst: ppdFirst);
    }

    private void WithWaferMap(bool hasBinning, List<DefectInfo> defects, Action<WaferMapViewModel> verify,
        bool excludeSaturated = false, bool includePpdCategory = false, bool ppdFirst = false)
    {
        _dispatcher.Run(() =>
        {
            (WaferRunInfo run, DefectCategoryItemContent category, BinningContent binning) =
                SummaryBinningEnabledTests.CreateResult(hasBinning, savedSummary: false);
            BinningItemContent binningItem = Assert.Single(binning.BinningCollections);
            binningItem.Bins[0].ColorCode = 0xFF00BFFF;
            binningItem.Bins[^1].Max = 2;
            if (excludeSaturated)
            {
                binningItem.LpdSizeDefectIncludeMode = LpdSizeDefectIncludeMode.ExcludeSaturatedLpd;
            }

            List<DefectInfo> source = run.CollectionRunList[0].PhysicalAnalysisResults[0].Defects;
            source.Clear();
            source.AddRange(defects);
            (SizeType, double)[] original = source.Select(defect => (defect.SizeType, defect.Attributes[nameof(AttributeName.Size)])).ToArray();
            GraphicItemContent graphic = new(new Area(), GraphicTypes.WaferMap, category);
            if (includePpdCategory)
            {
                DefectCategoryItemContent ppdCategory = new("DW2", "Default", "PPD", nameof(AttributeName.Size), 0xFF00BFFF)
                {
                    ChannelMode = ChannelMode.DW2_Normal
                };
                graphic.CategoryItems.Insert(ppdFirst ? 0 : graphic.CategoryItems.Count, ppdCategory);
            }

            using var services = new SummaryBinningEnabledTests.TestServices(category);
            WaferMapViewModel map = new(DisplayMode.resultNormalMap);
            try
            {
                map.LoadData(run, graphic);
                map.OnNavigatingTo();
                verify(map);
                Assert.Equal(original, source.Select(defect => (defect.SizeType, defect.Attributes[nameof(AttributeName.Size)])));
                services.AssertNoErrors();
            }
            finally
            {
                map.OnNavigatingFrom();
            }
        });
    }

    private static DefectInfo CreateDefect(double size, SizeType sizeType = SizeType.LPD_SIZED, string defectName = "LPD") => new()
    {
        CollectionName = "Default",
        ChannelMode = ChannelMode.DW2_Normal,
        DefectName = defectName,
        SizeType = sizeType,
        Attributes = new Dictionary<string, double>
        {
            [nameof(AttributeName.Size)] = size,
            [nameof(AttributeName.X)] = 100 * size,
            [nameof(AttributeName.Y)] = 0
        }
    };

    private static double[] VisibleSizes(WaferMapViewModel map) => map.WaferMapImageViewModel.DefectGroups
        .SelectMany(group => group.Defects).Select(defect => defect.Attributes[nameof(AttributeName.Size)]).Order().ToArray();

    private static void AssertSpecialSegments(WaferMapViewModel map, bool hasSaturated, bool hasOverRange)
    {
        DrawingGroup drawing = map.LightColorBarViewModel!.ColorBarDrawingGroup;
        Assert.Equal(hasSaturated ? 1 : 0, FindSegments(drawing, Colors.Red).Count());
        Assert.Equal(hasOverRange ? 1 : 0, FindSegments(drawing, Colors.Pink).Count());
    }

    private static IEnumerable<GeometryDrawing> FindSegments(Drawing drawing, Color color) => drawing switch
    {
        DrawingGroup group => group.Children.SelectMany(child => FindSegments(child, color)),
        GeometryDrawing { Geometry: RectangleGeometry, Brush: SolidColorBrush brush } segment when brush.Color == color => [segment],
        _ => []
    };

    private static void ClickSegment(LightColorBarViewModel colorBar, Color color, bool ctrl = false)
    {
        // 以实际绘制的分段中心命中，避免测试依赖真实鼠标位置或内部布局字段。
        Rect bounds = Assert.Single(FindSegments(colorBar.ColorBarDrawingGroup, color)).Geometry.Bounds;
        Point center = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.True((bool) typeof(LightColorBarViewModel).GetProperty("IsInteractive", Flags)!.GetValue(colorBar)!);
        typeof(LightColorBarViewModel).GetMethod("UpdateColorBar", Flags)!.Invoke(colorBar, [center, ctrl]);
    }
}