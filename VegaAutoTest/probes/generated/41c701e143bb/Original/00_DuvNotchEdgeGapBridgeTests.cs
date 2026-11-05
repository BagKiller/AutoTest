// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// DUV 外边缘剖面短程无效段桥接：≤maxGapSamples 的环形无效段按采样序线性插值并置有效；
/// 长段/全无效/长度不符原样返回，输入不被修改。桥接仅服务粗定位候选扫描
/// （动机与实测依据见 wafer-run state.md 的 DUV 对位模式条目）。
/// </summary>
public class DuvNotchEdgeGapBridgeTests
{
    private static PointInPolar[] MakePoints(params double[] radii)
    {
        return [.. radii.Select((radius, i) => new PointInPolar(radius, (double) i / radii.Length * 2.0 * Math.PI))];
    }

    private static PointInPolar NaNPoint(int index, int count)
        => new(double.NaN, (double) index / count * 2.0 * Math.PI);

    [Fact]
    public void ShortGap_Bridged_InterpolatedAndMarkedValid()
    {
        var points = new[] { new PointInPolar(100, 0), NaNPoint(1, 3), new PointInPolar(300, 2.0) };
        var valid = new[] { true, false, true };

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 2);

        Assert.True(bridgedValid[1]);
        Assert.Equal(200.0, bridged[1].Radius, 6);
        Assert.Equal(points[1].Theta, bridged[1].Theta);
        //输入不被修改
        Assert.True(double.IsNaN(points[1].Radius));
        Assert.False(valid[1]);
    }

    [Fact]
    public void TwoPointGap_InterpolatesBothInteriorFractions()
    {
        var points = new[]
        {
            new PointInPolar(100, 0), NaNPoint(1, 4), NaNPoint(2, 4), new PointInPolar(300, 1.5)
        };
        var valid = new[] { true, false, false, true };

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 2);

        Assert.True(bridgedValid[1] && bridgedValid[2]);
        Assert.Equal(100.0 + 1.0 / 3.0 * 200.0, bridged[1].Radius, 6);
        Assert.Equal(100.0 + 2.0 / 3.0 * 200.0, bridged[2].Radius, 6);
    }

    [Fact]
    public void GapLongerThanLimit_LeftInvalid()
    {
        var points = new[]
        {
            new PointInPolar(100, 0), NaNPoint(1, 4), NaNPoint(2, 4), NaNPoint(3, 4), new PointInPolar(200, 1.0)
        };
        var valid = new[] { true, false, false, false, true };

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 2);

        Assert.False(bridgedValid[1] || bridgedValid[2] || bridgedValid[3]);
        Assert.True(double.IsNaN(bridged[2].Radius));
    }

    [Fact]
    public void WrapAroundGap_BridgedAcrossZeroIndex()
    {
        //环形缺口：idx3 + idx0 跨 0° 连成一段（长度 2），锚点 idx2(200) 与 idx1(100)
        var points = new[] { NaNPoint(0, 4), new PointInPolar(100, 0.5), new PointInPolar(200, 1.0), NaNPoint(3, 4) };
        var valid = new[] { false, true, true, false };

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 2);

        Assert.True(bridgedValid[0] && bridgedValid[3]);
        //从 idx2 向 idx1 前进：idx3 = 1/3 处、idx0 = 2/3 处
        Assert.Equal(200.0 - 1.0 / 3.0 * 100.0, bridged[3].Radius, 6);
        Assert.Equal(200.0 - 2.0 / 3.0 * 100.0, bridged[0].Radius, 6);
    }

    [Fact]
    public void AllInvalid_ReturnedUnchanged()
    {
        var points = MakePoints(double.NaN, double.NaN);
        var valid = new bool[2];

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 4);

        Assert.Same(points, bridged);
        Assert.Same(valid, bridgedValid);
    }

    [Fact]
    public void MismatchedLength_ReturnedUnchanged()
    {
        var points = MakePoints(100, 200);
        var valid = new bool[3];

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 4);

        Assert.Same(points, bridged);
        Assert.Same(valid, bridgedValid);
    }

    [Fact]
    public void NoInvalid_ReturnsEqualCopy()
    {
        var points = MakePoints(100, 200, 300);
        var valid = new[] { true, true, true };

        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps(points, valid, maxGapSamples: 2);

        Assert.NotSame(points, bridged);
        Assert.Equal(points.Select(p => p.Radius), bridged.Select(p => p.Radius));
        Assert.All(bridgedValid, Assert.True);
    }

    [Fact]
    public void EmptyArray_ReturnedUnchanged()
    {
        (PointInPolar[] bridged, bool[] bridgedValid) = WaferAlignmentService.BridgeShortInvalidGaps([], [], maxGapSamples: 4);

        Assert.Empty(bridged);
        Assert.Empty(bridgedValid);
    }
}