// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Runtime.CompilerServices;
using Vega.Data.Analysis.Helper;
using Vega.Data.Commons;
using Vega.Data.Recipes;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// LPD 尺寸分类取数的单位口径回归护栏（decision 0024 / WI 3757）。
/// 属性值单位由属性名写死（Size→配方单位、NppmSize→nPPM、RppmSize→rPPM）：
/// 同单位组合取原始阈值，nPPM↔rPPM 交叉边界语义未定义返回 false。
/// 覆盖全部不触曲线基础设施的分支；umLSE 源换算分支（本地 DF/Film 曲线）无法在此宿主测试——
/// FilmCaliResultHelper 的静态构造需初始化 Galaxy 容器（TypeInitializationException），
/// 该分支走 Sim/真机验证清单。
/// 配方对象经 <see cref="Uninitialized{T}"/> 绕过含机台依赖的默认构造（与 SummaryBinningEnabledTests 同模式）。
/// </summary>
public class LpdBinningRangeResolverTests
{
    private const string DefectName = "LPD";
    private const string SizeAttr = "Size";
    private const string NppmAttr = "NppmSize";
    private const string RppmAttr = "RppmSize";
    private const double Boundary = 0.09;

    [Theory]
    [InlineData(DFSizingUnit.umLSE)]
    [InlineData(DFSizingUnit.nPPM)]
    [InlineData(DFSizingUnit.rPPM)]
    public void SizeAttribute_FollowsRecipeUnit_ReturnsRawThresholds(DFSizingUnit unit)
    {
        var recipe = CreateRecipe(unit, thresMin: 100, thresMax: 300);

        var resolved = LpdBinningRangeResolver.TryGetRange(
            recipe, DefectName, SizeAttr, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out var minValue, out var maxValue);

        Assert.True(resolved);
        Assert.Equal(100, minValue);
        Assert.Equal(300, maxValue);
    }

    [Theory]
    [InlineData(DFSizingUnit.nPPM, NppmAttr)]
    [InlineData(DFSizingUnit.rPPM, RppmAttr)]
    public void SameUnitProcessAttribute_ReturnsRawThresholds(DFSizingUnit unit, string attribute)
    {
        var recipe = CreateRecipe(unit, thresMin: 200, thresMax: 600);

        var resolved = LpdBinningRangeResolver.TryGetRange(
            recipe, DefectName, attribute, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out var minValue, out var maxValue);

        Assert.True(resolved);
        Assert.Equal(200, minValue);
        Assert.Equal(600, maxValue);
    }

    [Theory]
    [InlineData(DFSizingUnit.nPPM, RppmAttr)]
    [InlineData(DFSizingUnit.rPPM, NppmAttr)]
    public void CrossUnitProcessAttribute_UndefinedSemantics_ReturnsFalse(DFSizingUnit unit, string attribute)
    {
        var recipe = CreateRecipe(unit, thresMin: 200, thresMax: 600);

        var resolved = LpdBinningRangeResolver.TryGetRange(
            recipe, DefectName, attribute, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out _, out _);

        Assert.False(resolved);
    }

    [Fact]
    public void SizeAttribute_NonPositiveMax_FallsBackToDefaultLpdSizeMax()
    {
        var recipe = CreateRecipe(DFSizingUnit.umLSE, thresMin: 0, thresMax: 0);

        var resolved = LpdBinningRangeResolver.TryGetRange(
            recipe, DefectName, SizeAttr, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out _, out var maxValue);

        Assert.True(resolved);
        Assert.Equal(150000, maxValue);
    }

    [Fact]
    public void ProcessAttribute_ExcludesDisabledCollector_SizeAttributeIncludesIt()
    {
        // 参与范围既有行为：过程数据属性（NppmSize/RppmSize）不含禁用采集器，Size 属性包含
        var disabledNppm = CreateRecipe(DFSizingUnit.nPPM, thresMin: 200, thresMax: 600, collectorEnabled: false);
        var disabledSize = CreateRecipe(DFSizingUnit.nPPM, thresMin: 200, thresMax: 600, collectorEnabled: false);

        var nppmResolved = LpdBinningRangeResolver.TryGetRange(
            disabledNppm, DefectName, NppmAttr, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out _, out _);
        var sizeResolved = LpdBinningRangeResolver.TryGetRange(
            disabledSize, DefectName, SizeAttr, ChannelMode.DW2_Normal, "Default", "DW2",
            Boundary, applyActualSizes: false, applyTrueFilmCurve: false,
            out var minValue, out var maxValue);

        Assert.False(nppmResolved);
        Assert.True(sizeResolved);
        Assert.Equal(200, minValue);
        Assert.Equal(600, maxValue);
    }

    private static Recipe CreateRecipe(DFSizingUnit unit, double thresMin, double thresMax, bool collectorEnabled = true)
    {
        var collector = new DFCollectorSettingContent(ChannelMode.DW2_Normal)
        {
            SurfImageOptimizers = [],
            IsEnabled = collectorEnabled,
            ThresMin = thresMin,
            ThresMax = thresMax,
        };
        var collection = Uninitialized<CollectionContent>();
        collection.CollectionName = "Default";
        collection.DarkField = Uninitialized<DarkFieldSettingContent>();
        collection.DarkField.SizingContent = Uninitialized<DFSizingContent>();
        collection.DarkField.SizingContent.LPDUnit = unit;
        collection.DarkField.ChannelSettingContent = Uninitialized<DFChannelSettingContent>();
        typeof(DFChannelSettingContent).GetProperty(nameof(DFChannelSettingContent.Collectors))!.SetValue(
            collection.DarkField.ChannelSettingContent,
            new List<DFCollectorSettingContent> { collector });
        collection.WaferScan = Uninitialized<WaferScanSettingContent>();
        var collections = Uninitialized<CollectionsContent>();
        collections.ElementType = RecipeElementType.Collection;
        collections.Collections = [collection];
        var recipe = Uninitialized<Recipe>();
        recipe.RecipeElements = [new RecipeInfoContent(), collections];
        return recipe;
    }

    private static T Uninitialized<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}