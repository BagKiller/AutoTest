// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// DUV 外边缘半径剖面的中值预滤波：抑制列间白噪声/孤立尖刺、以邻域中值填充孤立 NaN 列、
/// 保留远宽于滤波窗的坡与缺口形状。滤波仅作用于扫描剖面，精修仍消费原始点（见 wafer-run state.md）。
/// </summary>
public class DuvNotchProfileMedianFilterTests
{
    private static PointInPolar[] MakePoints(params double[] radii)
        => [.. radii.Select((radius, i) => new PointInPolar(radius, (double) i / radii.Length * 2.0 * Math.PI))];

    [Fact]
    public void IsolatedSpike_Suppressed()
    {
        var points = MakePoints(100, 100, 100, 40, 100, 100, 100);

        PointInPolar[] filtered = WaferAlignmentService.MedianFilterProfile(points, halfWindow: 1);

        Assert.Equal(100.0, filtered[3].Radius);
        Assert.Equal(100.0, filtered[2].Radius);
        Assert.Equal(100.0, filtered[4].Radius);
    }

    [Fact]
    public void IsolatedNaN_FilledByNeighborhoodMedian()
    {
        var points = MakePoints(100, 100, double.NaN, 100, 100);

        PointInPolar[] filtered = WaferAlignmentService.MedianFilterProfile(points, halfWindow: 1);

        Assert.Equal(100.0, filtered[2].Radius);
    }

    [Fact]
    public void WideRamp_Preserved()
    {
        //窗宽 3（half 1）远小于坡长：中部取奇数窗真中值（130），两端窗被裁成偶数取均值；
        //单调坡整体保持，无平台化
        var points = MakePoints(100, 110, 120, 130, 140, 150, 160);

        PointInPolar[] filtered = WaferAlignmentService.MedianFilterProfile(points, halfWindow: 1);

        Assert.Equal(130.0, filtered[3].Radius);
        Assert.Equal(105.0, filtered[0].Radius);
        Assert.Equal(155.0, filtered[^1].Radius);
        Assert.Equal(6, filtered.Zip(filtered.Skip(1)).Count(pair => pair.Second.Radius > pair.First.Radius));
    }

    [Fact]
    public void AllNaNWindow_ColumnStaysNaN()
    {
        var points = MakePoints(double.NaN, double.NaN, double.NaN);

        PointInPolar[] filtered = WaferAlignmentService.MedianFilterProfile(points, halfWindow: 1);

        Assert.All(filtered, p => Assert.True(double.IsNaN(p.Radius)));
    }

    [Fact]
    public void InputArray_NotModified()
    {
        var points = MakePoints(100, 40, 100);

        _ = WaferAlignmentService.MedianFilterProfile(points, halfWindow: 1);

        Assert.Equal(40.0, points[1].Radius);
    }

    [Fact]
    public void DegenerateWindow_ReturnsOriginal()
    {
        var points = MakePoints(100, 200);

        Assert.Same(points, WaferAlignmentService.MedianFilterProfile(points, halfWindow: 0));
        PointInPolar[] empty = WaferAlignmentService.MedianFilterProfile([], halfWindow: 4);
        Assert.Empty(empty);
    }
}