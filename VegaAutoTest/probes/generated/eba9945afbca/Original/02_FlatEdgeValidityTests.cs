// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// Flat 边缘有效性测试：括号化边缘交叉、NaN 缺失点、DUV 外侧下降沿选点与高斯窗口校验。
/// 不依赖 trace DTO；Notch 行为作为对照保留。
/// </summary>
public sealed class FlatEdgeValidityTests
{
    [Fact]
    public void DarkOuterTracks_Skipped_AcceptedInnerCrossing()
    {
        ushort[,] data = { { 0 }, { 100 }, { 0 }, { 0 } };
        (PointInPolar point, bool valid) = WaferAlignmentService.FindBracketedNotchEdgeInColumn(
            data, 0, 50, 100, 1);

        Assert.True(valid);
        Assert.Equal(100.5, point.Radius);
    }

    [Fact]
    public void NoCrossing_IsMissingRatherThanExtrapolatedPoint()
    {
        ushort[,] data = { { 10 }, { 10 }, { 10 } };
        (PointInPolar _, bool valid) = WaferAlignmentService.FindBracketedNotchEdgeInColumn(
            data, 0, 50, 100, 1);

        Assert.False(valid);
    }

    [Fact]
    public void FindFlatEdgePoint_InvalidCrossing_ReturnsNaN()
    {
        ushort[,] data = { { 10 }, { 10 }, { 10 } };
        PointInPolar point = WaferAlignmentService.FindFlatEdgePoint(data, 0, 50, 100, 1);

        Assert.True(double.IsNaN(point.Radius));
        Assert.Equal(0.0, point.Theta);
    }

    [Fact]
    public void FindFlatEdgePoint_ValidCrossing_PreservesInterpolatedRadius()
    {
        ushort[,] data = { { 0 }, { 100 }, { 0 }, { 0 } };
        PointInPolar point = WaferAlignmentService.FindFlatEdgePoint(data, 0, 50, 100, 1);

        Assert.Equal(100.5, point.Radius);
    }

    [Fact]
    public void CircleFit_IgnoresInvalidAnglesWithoutTreatingDefaultRadiusAsEdge()
    {
        PointInPolar[] points = Enumerable.Range(0, 360)
            .Select(i => new PointInPolar(i % 20 == 0 ? double.NaN : 75000, i * Math.PI / 180)).ToArray();

        WaferAlignmentService.CircleFitCoreResult fit = WaferAlignmentService.FitCircleCore(points, 23);

        Assert.InRange(Math.Abs(fit.Cx), 0, 0.1);
        Assert.InRange(Math.Abs(fit.Cy), 0, 0.1);
        Assert.InRange(Math.Abs(fit.Radius - 75000), 0, 0.1);
    }

    [Fact]
    public void CircleFit_AllAnglesInvalid_FailsWithoutInventingCircle()
    {
        PointInPolar[] points = Enumerable.Range(0, 20)
            .Select(i => new PointInPolar(double.NaN, i * Math.PI / 10)).ToArray();

        WaferAlignmentService.CircleFitCoreResult fit = WaferAlignmentService.FitCircleCore(points, 23);

        Assert.True(double.IsNaN(fit.Cx));
        Assert.True(double.IsNaN(fit.Radius));
        Assert.Equal("InsufficientValidPoints", fit.ExitCondition);
    }

    [Fact]
    public void DuvGaussianCenter_InsideFitWindowIncludingBoundaries_IsAccepted()
    {
        double lowerBound = 7 * 23.0;
        double upperBound = 13 * 23.0;

        Assert.True(WaferAlignmentService.IsDuvGaussianCenterValid(lowerBound, lowerBound, upperBound));
        Assert.True(WaferAlignmentService.IsDuvGaussianCenterValid(10 * 23.0, lowerBound, upperBound));
        Assert.True(WaferAlignmentService.IsDuvGaussianCenterValid(upperBound, lowerBound, upperBound));
    }

    [Fact]
    public void DuvGaussianCenter_OutsideFitWindowOrNonFinite_IsRejected()
    {
        double lowerBound = 7 * 23.0;
        double upperBound = 13 * 23.0;

        Assert.False(WaferAlignmentService.IsDuvGaussianCenterValid(lowerBound - 0.001, lowerBound, upperBound));
        Assert.False(WaferAlignmentService.IsDuvGaussianCenterValid(upperBound + 0.001, lowerBound, upperBound));
        Assert.False(WaferAlignmentService.IsDuvGaussianCenterValid(double.NaN, lowerBound, upperBound));
        Assert.False(WaferAlignmentService.IsDuvGaussianCenterValid(double.PositiveInfinity, lowerBound, upperBound));
        Assert.False(WaferAlignmentService.IsDuvGaussianCenterValid(double.NegativeInfinity, lowerBound, upperBound));
    }

    [Fact]
    public void FlatDuvPeakSelection_SaturatedPlateau_PrefersOutermostQualifiedDrop()
    {
        ushort[,] data = { { 10000 }, { 65535 }, { 65535 }, { 65535 }, { 20000 }, { 2400 } };

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 6, true, out ushort max, out double min);

        Assert.Equal(4, index);
        Assert.Equal(65535, max);
        Assert.Equal(2400, min);
    }

    [Fact]
    public void FlatDuvPeakSelection_UniqueMaximum_RemainsSelected()
    {
        ushort[,] data = { { 10000 }, { 20000 }, { 65535 }, { 30000 }, { 2400 } };

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 5, true, out _, out _);

        Assert.Equal(3, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_EqualOutwardDrops_PrefersOutermostCandidate()
    {
        ushort[,] data = { { 10000 }, { 65535 }, { 20000 }, { 10000 }, { 65535 }, { 20000 } };

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 6, true, out _, out _);

        Assert.Equal(4, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_NoPositiveOutwardDrop_ReturnsInvalidPoint()
    {
        ushort[,] data = { { 10000 }, { 65535 }, { 65535 }, { 65535 } };

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 4, true, out _, out _);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_SelectsOutermostEligibleDropAfterOutsideRecovery()
    {
        ushort[,] data = CreateDuvProfile();
        data[0, 0] = 65535;
        data[1, 0] = 65535;
        data[2, 0] = 4111;
        for (int i = 9; i <= 246; i++) { data[i, 0] = 60000; }
        data[247, 0] = 65535;
        data[248, 0] = 7403;

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 370, true, out _, out _);

        // index 1 的下降沿被外侧回亮门拒绝；从外向内首个合格下降沿是 248 → 249，内侧更强候选不覆盖它。
        Assert.Equal(248, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_WeakProfile_ReturnsInvalidPoint()
    {
        ushort[,] data = CreateDuvProfile();
        data[4, 0] = 2558;
        data[5, 0] = 2488;
        for (int i = 338; i < 370; i++)
        {
            data[i, 0] = (ushort) (i % 3 == 0 ? 2400 : 2420);
        }

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 370, true, out _, out _);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_OutermostQualifiedDropBeatsStrongerInnerDrop()
    {
        ushort[,] data = CreateDuvProfile();
        data[2, 0] = 60000;
        data[3, 0] = 30000;
        data[10, 0] = 40000;
        for (int i = 11; i < 370; i++) { data[i, 0] = 30000; }

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 370, true, out _, out _);

        Assert.Equal(10, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_EqualDropKeepsOutermostEligibleCandidate()
    {
        ushort[,] data = CreateDuvProfile();
        data[2, 0] = 60000;
        for (int i = 3; i < 10; i++) { data[i, 0] = 50000; }
        data[10, 0] = 40000;
        for (int i = 11; i < 370; i++) { data[i, 0] = 30000; }

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 370, true, out _, out _);

        Assert.Equal(10, index);
    }

    [Fact]
    public void FlatDuvPeakSelection_OnlyDarkToBrightTransition_ReturnsInvalidPoint()
    {
        ushort[,] data = CreateDuvProfile();
        for (int i = 20; i < 370; i++) { data[i, 0] = 50000; }

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 370, true, out _, out _);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void NotchDuvPeakSelection_PreservesFirstMaximumBehavior()
    {
        ushort[,] data = { { 10000 }, { 65535 }, { 65535 }, { 20000 } };

        int index = WaferAlignmentService.FindDuvPeakIndex(data, 0, 4, false, out _, out _);

        Assert.Equal(1, index);
    }

    [Fact]
    public void ComputeDuvEdgePoint_IncompleteGaussianWindow_RetainsDiscretePoint()
    {
        // 峰值索引 2，3 点半窗向内侧越界：不拟合，降采样点保持离散半径。
        ushort[,] data = { { 10000 }, { 65535 }, { 20000 }, { 2400 } };

        (PointInPolar point, PointInPolar downsampled) = WaferAlignmentService.ComputeDuvEdgePoint(
            data, 0, 4, 23.0, 100.0, 1, preferOuterDrop: true, useGaussianFitting: true);

        Assert.Equal(100.0 + 2 * 23.0, point.Radius);
        Assert.Equal(point.Radius, downsampled.Radius);
        Assert.Equal(0.0, point.Theta);
    }

    [Fact]
    public void ComputeDuvEdgePoint_GaussianFittingDisabled_RetainsDiscretePoint()
    {
        ushort[,] data = { { 10000 }, { 65535 }, { 60000 }, { 55000 }, { 50000 }, { 45000 }, { 2400 } };

        (PointInPolar point, PointInPolar downsampled) = WaferAlignmentService.ComputeDuvEdgePoint(
            data, 0, 7, 23.0, 100.0, 1, preferOuterDrop: true, useGaussianFitting: false);

        Assert.Equal(100.0 + 5 * 23.0, point.Radius);
        Assert.Equal(point.Radius, downsampled.Radius);
    }

    private static ushort[,] CreateDuvProfile()
    {
        ushort[,] data = new ushort[370, 1];
        for (int i = 0; i < 370; i++) { data[i, 0] = 2400; }
        return data;
    }
}