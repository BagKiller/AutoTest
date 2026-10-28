// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;
using Vega.UI.Diagnostics.SystemDiagnostics.DataCollectionSubsystem;

namespace Vega.UI.Diagnostics.Tests;

/// <summary>
/// EdgeAnalysis 极坐标图内径：快照锚点半径满足 0 &lt; radius &lt; 90000 µm 时按真实锚点绘制
/// （6 寸 Flat 常见 67500），其余（12/8 寸、90000 边界、非有限/非正值）回退历史 28500。
/// </summary>
public sealed class EdgeAnalysisPolarInnerRadiusTests
{
    private const int LegacyInnerRadius = Mercator2Polar.FallbackInnerRadius;

    private static EdgeScanInfo CreateInfo(double anchorRadius)
        => new() { StartPoint = new PointInPolar(anchorRadius, 0) };

    [Theory]
    [InlineData(67500, 67500)]   // 6 寸 Flat 常见锚点（waferR − FlatEdgeScope = 67500）
    [InlineData(73500, 73500)]   // 6 寸 Notch 锚点（waferR − 1500）
    [InlineData(1, 1)]           // 下界内最小正锚点
    [InlineData(89999, 89999)]   // 边界内侧（90000 不含）
    public void ResolvePolarInnerRadius_SmallAnchor_UsesAcquiredRadius(double anchorRadius, int expected)
    {
        Assert.Equal(expected, EdgeAnalysisViewModel.ResolvePolarInnerRadius(CreateInfo(anchorRadius)));
    }

    [Theory]
    [InlineData(90000)]    // 边界值本身不回退真实锚点
    [InlineData(97500)]    // 8 寸锚点
    [InlineData(98500)]    // 8 寸 Notch 理想半径
    [InlineData(148500)]   // 12 寸锚点
    public void ResolvePolarInnerRadius_LargeAnchor_FallsBackToLegacy(double anchorRadius)
    {
        Assert.Equal(LegacyInnerRadius, EdgeAnalysisViewModel.ResolvePolarInnerRadius(CreateInfo(anchorRadius)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(-1000)]
    public void ResolvePolarInnerRadius_InvalidAnchor_FallsBackToLegacy(double anchorRadius)
    {
        Assert.Equal(LegacyInnerRadius, EdgeAnalysisViewModel.ResolvePolarInnerRadius(CreateInfo(anchorRadius)));
    }
}