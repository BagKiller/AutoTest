// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using Vega.Data.Recipes;
using Vega.UI.Common;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// 配方布局装载链路的直方图单次绘制不变量：
/// LoadRecipeGraphicData 在 LoadData 前预注入配方 BinningItems，LoadData 的兜底重绘守卫
/// 真正生效，绘制仅由事后的显式 UpdateHistogram 完成一次。赋值若被挪回 LoadData 之后，
/// 代码仍编译、最终呈现不变，只是静默退回双绘——本测试以 LoadData 入口时序探针锁定。
/// LoadData 的跳过条件本身另用绘制痕迹直接断言。复用串行 WPF 夹具。
/// </summary>
[Collection("Result summary binning")]
public sealed class GraphicLayoutRecipeLoadTests
{
    private readonly SummaryBinningDispatcher _dispatcher;

    public GraphicLayoutRecipeLoadTests(SummaryBinningDispatcher dispatcher) => _dispatcher = dispatcher;

    private sealed class LoadEntryProbeHistogram : HistogramViewModel
    {
        public List<BinningItemContent>? BinningItemsAtLoadEntry { get; private set; }

        public override void LoadData(GraphicItemContent graphicItemContent)
        {
            BinningItemsAtLoadEntry = BinningItems;
            base.LoadData(graphicItemContent);
        }
    }

    private static void InvokeLoadRecipeGraphicData(GraphicLayoutViewModel layout, GraphicViewModelBase viewModel, GraphicItemContent content)
    {
        typeof(GraphicLayoutViewModel)
            .GetMethod("LoadRecipeGraphicData", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(layout, [viewModel, content]);
    }

    private static void SetPrivateField(object instance, string name, object value) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, value);

    [Fact]
    public void RecipeLoad_HistogramPreInjectsBinningBeforeLoadData()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var (wafer, category, binning) = SummaryBinningEnabledTests.CreateResult(enabled: true, savedSummary: false);
            var layout = new GraphicLayoutViewModel(false);
            SetPrivateField(layout, "_currentRecipe", wafer.RecipeSnapshot);
            SetPrivateField(layout, "_unitByCollection", new Dictionary<string, DFSizingUnit?>());
            var histogram = new LoadEntryProbeHistogram();
            var content = new GraphicItemContent(new Area(), GraphicTypes.Histogram, category);

            InvokeLoadRecipeGraphicData(layout, histogram, content);

            Assert.Same(binning.BinningCollections, histogram.BinningItemsAtLoadEntry);
            Assert.NotEmpty(histogram.HistogramImageViewModel.HistogramDrawingGroup.Children);
            services.AssertNoErrors();
        });
    }

    [Fact]
    public void HistogramLoadData_SkipsFallbackDrawWhenBinningItemsPreset()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var (_, category, binning) = SummaryBinningEnabledTests.CreateResult(enabled: true, savedSummary: false);
            var content = new GraphicItemContent(new Area(), GraphicTypes.Histogram, category);

            var preset = new HistogramViewModel { BinningItems = [binning.BinningCollections[0]] };
            preset.LoadData(content);
            Assert.Empty(preset.HistogramImageViewModel.HistogramDrawingGroup.Children);

            var fallback = new HistogramViewModel();
            fallback.LoadData(content);
            Assert.NotEmpty(fallback.HistogramImageViewModel.HistogramDrawingGroup.Children);
            services.AssertNoErrors();
        });
    }
}