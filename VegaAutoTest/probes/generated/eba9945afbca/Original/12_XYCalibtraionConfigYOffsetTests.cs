// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Calibration;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// XYCalibtraionConfig.IsYOffsetOutOfSpec 单测：Flat 的角度偏移经 PolarPoint.AngleDifference
/// 归一为有符号最短角差，负向偏移同样可能与正向一样超限，因此 Flat 按绝对值判定；
/// Notch 保持既有单侧判定（负向偏移不告警），保存的 NotchThetaOffset 符号语义不变。
/// </summary>
public class XYCalibtraionConfigYOffsetTests
{
    private const double TwoPi = 2 * Math.PI;

    private static XYCalibtraionConfig CreateConfig() => new() { YOffsetThreshold = 2000, FlatDepth = 1500 };

    [Fact]
    public void IsYOffsetOutOfSpec_FlatNegativeOffset_ReportsOutOfSpec()
    {
        // 评审示例：2π−0.015 与 0.015 的最短角差为 −0.03 rad；有效半径 98500 µm → yOffset ≈ −2955 µm。
        var config = CreateConfig();
        var thetaOffset = PolarPoint.AngleDifference(TwoPi - 0.015, 0.015);
        var yOffset = thetaOffset * (100000 - config.FlatDepth);

        Assert.Equal(-0.03, thetaOffset, 1e-12);
        Assert.Equal(-2955, yOffset, 1e-9);
        Assert.True(config.IsYOffsetOutOfSpec(yOffset, isFlat: true));
    }

    [Fact]
    public void IsYOffsetOutOfSpec_FlatPositiveOffset_ReportsOutOfSpec()
    {
        // 跨零角度的对称方向：0.015 与 2π−0.015 → +0.03 rad → +2955 µm。
        var config = CreateConfig();
        var thetaOffset = PolarPoint.AngleDifference(0.015, TwoPi - 0.015);
        var yOffset = thetaOffset * (100000 - config.FlatDepth);

        Assert.Equal(0.03, thetaOffset, 1e-12);
        Assert.Equal(2955, yOffset, 1e-9);
        Assert.True(config.IsYOffsetOutOfSpec(yOffset, isFlat: true));
    }

    [Fact]
    public void IsYOffsetOutOfSpec_FlatWithinThreshold_BothSigns_NoAlarm()
    {
        var config = CreateConfig();

        Assert.False(config.IsYOffsetOutOfSpec(1500, isFlat: true));
        Assert.False(config.IsYOffsetOutOfSpec(-1500, isFlat: true));
        Assert.False(config.IsYOffsetOutOfSpec(0, isFlat: true));
    }

    [Fact]
    public void IsYOffsetOutOfSpec_NotchNegativeOffset_KeepsLegacySingleSidedCheck()
    {
        // Notch 行为不变：仅正向超限告警，负向（如 −2955 µm）维持既有不告警语义。
        var config = CreateConfig();

        Assert.False(config.IsYOffsetOutOfSpec(-2955, isFlat: false));
        Assert.True(config.IsYOffsetOutOfSpec(2955, isFlat: false));
    }
}