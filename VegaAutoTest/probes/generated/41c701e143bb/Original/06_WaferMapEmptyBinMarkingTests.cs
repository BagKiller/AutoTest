// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using System.Windows.Media;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Recipes;
using Vega.UI.Common;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// 结果模式分箱色柱空 Bin × 标记：逐 bin 计数置位、先清后写防残留、预览模式与跨类别不标记。
/// GenerateDefectGroups 为私有入口，经反射调用，与轴模式测试同范式；复用串行 WPF 夹具。
/// </summary>
[Collection("Result summary binning")]
public sealed class WaferMapEmptyBinMarkingTests
{
    private readonly SummaryBinningDispatcher _dispatcher;

    public WaferMapEmptyBinMarkingTests(SummaryBinningDispatcher dispatcher) => _dispatcher = dispatcher;

    private static BinningItemContent CreateBinning(string defectName = "LPD")
    {
        return new BinningItemContent
        {
            Category = new DefectCategoryItemContent("DW2", "Default", defectName, nameof(AttributeName.Size), 0xFF00BFFF),
            MinValue = 0,
            MaxValue = 1,
            Bins =
            [
                new BinContent(1, 0, 0.5, true, 0xFF0000FF),
                new BinContent(2, 0.5, 1, true, 0xFF00FF00)
            ]
        };
    }

    private static DefectInfo CreateDefect(double size)
    {
        return new DefectInfo
        {
            CollectionName = "Default",
            ChannelMode = ChannelMode.DW2_Normal,
            DefectName = "LPD",
            SizeType = SizeType.LPD_SIZED,
            Attributes = new Dictionary<string, double> { [nameof(AttributeName.Size)] = size }
        };
    }

    private static List<DefectGroup>? InvokeGenerateDefectGroups(WaferMapViewModel waferMap, List<DefectInfo> defects, BinningItemContent binning)
    {
        return (List<DefectGroup>?)typeof(WaferMapViewModel)
            .GetMethod("GenerateDefectGroups", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(waferMap, [defects, binning]);
    }

    [Fact]
    public void ResultMode_EmptyRangeBinsMarked()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var binning = CreateBinning();
            var waferMap = new WaferMapViewModel(DisplayMode.resultNormalMap) { LayoutType = LayoutTypes.Result };
            waferMap.LightColorBarViewModel = new LightColorBarViewModel(binning, 50, 150, Guid.Empty, ColorBarMode.Recipe);

            var groups = InvokeGenerateDefectGroups(waferMap, [CreateDefect(0.2), CreateDefect(0.4)], binning);

            Assert.NotNull(groups);
            Assert.Single(groups);
            var bins = waferMap.LightColorBarViewModel.ColorBar.Bins;
            Assert.False(bins[0].IsEmpty);
            Assert.True(bins[1].IsEmpty);
            services.AssertNoErrors();
        });
    }

    [Fact]
    public void ResultMode_StaleMarksClearedIncludingSkippedBins()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var binning = CreateBinning();
            var waferMap = new WaferMapViewModel(DisplayMode.resultNormalMap) { LayoutType = LayoutTypes.Result };
            waferMap.LightColorBarViewModel = new LightColorBarViewModel(binning, 50, 150, Guid.Empty, ColorBarMode.Recipe);
            var bins = waferMap.LightColorBarViewModel.ColorBar.Bins;
            bins[0].IsEmpty = true;
            bins[1].IsEmpty = true;
            bins[1].IsEnabled = false;

            InvokeGenerateDefectGroups(waferMap, [CreateDefect(0.25)], binning);

            Assert.False(bins[0].IsEmpty);
            Assert.False(bins[1].IsEmpty);
            services.AssertNoErrors();
        });
    }

    [Fact]
    public void RecipeMode_EmptyRangeBinsNotMarked()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var binning = CreateBinning();
            var waferMap = new WaferMapViewModel(DisplayMode.layoutMap) { LayoutType = LayoutTypes.Recipe };
            waferMap.LightColorBarViewModel = new LightColorBarViewModel(binning, 50, 150, Guid.Empty, ColorBarMode.Recipe);

            InvokeGenerateDefectGroups(waferMap, [], binning);

            Assert.All(waferMap.LightColorBarViewModel.ColorBar.Bins, bin => Assert.False(bin.IsEmpty));
            services.AssertNoErrors();
        });
    }

    [Fact]
    public void ResultMode_CategoryMismatchLeavesMarksUntouched()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var colorBarBinning = CreateBinning();
            var otherCategoryBinning = CreateBinning("Scratch");
            var waferMap = new WaferMapViewModel(DisplayMode.resultNormalMap) { LayoutType = LayoutTypes.Result };
            waferMap.LightColorBarViewModel = new LightColorBarViewModel(colorBarBinning, 50, 150, Guid.Empty, ColorBarMode.Recipe);
            var bins = waferMap.LightColorBarViewModel.ColorBar.Bins;
            bins[1].IsEmpty = true;

            InvokeGenerateDefectGroups(waferMap, [], otherCategoryBinning);

            Assert.False(bins[0].IsEmpty);
            Assert.True(bins[1].IsEmpty);
            services.AssertNoErrors();
        });
    }

    /// <summary>
    /// 生产者不自绘不变量：GenerateDefectGroups 只计算分组与空态标记，不触发色柱绘制。
    /// DrawColorBar 每次执行会 Clear 后添加新 DrawingGroup 实例，故以子元素身份与数量不变作为绘制探针。
    /// </summary>
    [Fact]
    public void ResultMode_GenerateDefectGroups_DoesNotRedrawColorBar()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var binning = CreateBinning();
            var waferMap = new WaferMapViewModel(DisplayMode.resultNormalMap) { LayoutType = LayoutTypes.Result };
            waferMap.LightColorBarViewModel = new LightColorBarViewModel(binning, 50, 150, Guid.Empty, ColorBarMode.Recipe);
            var childrenBefore = waferMap.LightColorBarViewModel.ColorBarDrawingGroup.Children.Cast<Drawing>().ToArray();

            InvokeGenerateDefectGroups(waferMap, [CreateDefect(0.2)], binning);

            var childrenAfter = waferMap.LightColorBarViewModel.ColorBarDrawingGroup.Children.Cast<Drawing>().ToArray();
            Assert.Equal(childrenBefore.Length, childrenAfter.Length);
            for (var i = 0; i < childrenBefore.Length; i++)
            {
                Assert.Same(childrenBefore[i], childrenAfter[i]);
            }
            services.AssertNoErrors();
        });
    }
}