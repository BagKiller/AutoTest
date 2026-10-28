// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.
using Vega.Shared;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// 平边(Flat)弦切几何推导(P2 边扫对准 flat 支持的公式锚定)。
/// 理想平边半径 = √(R²−(L/2)²)、半弧角 = asin(L/2R)、半弧长 = R·半弧角,全部由片径+配方主平边长度纯几何推导。
/// </summary>
public class WaferScanRangeFlatGeometryTests
{
    // 6寸 SEMI 标称:R=75000,主平边 L=57500
    private const int Wafer150DiameterUm = 150000;
    private const double Flat150NominalLengthUm = 57500;

    [Fact]
    public void TryGetFlatGeometry_6InchNominal_MatchesChordGeometry()
    {
        Assert.True(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, Flat150NominalLengthUm,
            out var idealFlatRadius, out var halfArcAngleRad, out var halfArcLength));

        // 半弦 28750:弦切半径 √(75000²−28750²) ≈ 69270.8,切深 ≈ 5.7mm(6寸平边量级)
        Assert.Equal(69270.8, idealFlatRadius, 1);
        // 半弧角 asin(28750/75000) ≈ 0.3934 rad ≈ 22.54°,整弧 ≈ 45°
        Assert.Equal(0.3934, halfArcAngleRad, 3);
        // 半弧长 = R·半弧角 ≈ 29505.2µm
        Assert.Equal(29505.2, halfArcLength, 1);
    }

    [Fact]
    public void TryGetFlatGeometry_InvalidLength_ReturnsFalse()
    {
        Assert.False(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, 0, out _, out _, out _));
        Assert.False(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, -1000, out _, out _, out _));
        // 半弦 ≥ 片半径在物理上不构成弦切
        Assert.False(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, Wafer150DiameterUm, out _, out _, out _));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void TryGetFlatGeometry_NonFiniteLength_ReturnsFalseWithoutNaNOutputs(double flatLengthUm)
    {
        // NaN 长度曾是缺口：两个大小比较均为 false，函数会返回 true + NaN 半径
        Assert.False(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, flatLengthUm,
            out var idealFlatRadius, out var halfArcAngleRad, out var halfArcLength));
        Assert.Equal(0, idealFlatRadius);
        Assert.Equal(0, halfArcAngleRad);
        Assert.Equal(0, halfArcLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-150000)]
    public void TryGetFlatGeometry_NonPositiveDiameter_ReturnsFalse(int waferDiameterUm)
    {
        Assert.False(WaferScanRange.TryGetFlatGeometry(waferDiameterUm, Flat150NominalLengthUm, out _, out _, out _));
    }

    [Fact]
    public void GetFlatDepth_6InchNominal_DerivesChordDepth()
    {
        // 75000 − √(75000² − 28750²) = 5729.2449875 µm，几何合法时不使用配置回退值
        var depth = WaferScanRange.GetFlatDepth(Wafer150DiameterUm, Flat150NominalLengthUm, fallbackDepthUm: 1500);

        Assert.Equal(5729.2449875, depth, 1e-3);
    }

    [Theory]
    [InlineData(Wafer150DiameterUm, 0)]
    [InlineData(Wafer150DiameterUm, -1000)]
    [InlineData(Wafer150DiameterUm, Wafer150DiameterUm)] // 半弦 ≥ 片半径
    [InlineData(0, Flat150NominalLengthUm)]
    [InlineData(-150000, Flat150NominalLengthUm)]
    public void GetFlatDepth_InvalidGeometry_FallsBack(int waferDiameterUm, double flatLengthUm)
    {
        Assert.Equal(1500, WaferScanRange.GetFlatDepth(waferDiameterUm, flatLengthUm, fallbackDepthUm: 1500));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void GetFlatDepth_NonFiniteLength_FallsBack(double flatLengthUm)
    {
        Assert.Equal(1500, WaferScanRange.GetFlatDepth(Wafer150DiameterUm, flatLengthUm, fallbackDepthUm: 1500));
    }

    [Fact]
    public void TryGetFlatGeometry_FlatRadiusIsWellBelowWaferRadius_AndAboveHalfRadius()
    {
        // 切深量级自检:平边弦半径显著低于片半径(平边比 notch 深得多),且高于半弦几何下限
        Assert.True(WaferScanRange.TryGetFlatGeometry(Wafer150DiameterUm, Flat150NominalLengthUm, out var idealFlatRadius, out _, out _));
        Assert.True(idealFlatRadius < 75000 - 5000, "flat depth should be in the millimeter-plus range, far deeper than a notch");
        Assert.True(idealFlatRadius > 0);
    }

    [Fact]
    public void GetIdealNotchRadius_NotchPathUnchanged()
    {
        // notch 公式保持不变:waferR − 1500(锚定 flat 分支不改变既有 notch 行为的输入)
        Assert.Equal(148500, WaferScanRange.GetIdealNotchRadius(300000));
        Assert.Equal(98500, WaferScanRange.GetIdealNotchRadius(200000));
        Assert.Equal(73500, WaferScanRange.GetIdealNotchRadius(150000));
    }
}