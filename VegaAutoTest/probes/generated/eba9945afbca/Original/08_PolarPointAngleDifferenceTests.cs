// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Calibration;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// PolarPoint.AngleDifference 单测（既有共享实现，UI 校准可视化与 BL XY 校准共用）：
/// Flat 模式 XY 校准的角度域为 [0, 2π)，两路方向角跨 0/2π 边界时直接相减会得到近整圈差值，
/// CheckYOffset 的 Flat 分支经 AngleDifference 归一为有符号最短角差
/// （Notch 分支不经本函数，维持直接相减语义；CheckYOffset 本体依赖容器服务，按模块约定走集成验证）。
/// </summary>
public class PolarPointAngleDifferenceTests
{
    private const double TwoPi = 2 * Math.PI;

    [Fact]
    public void AngleDifference_CrossBoundaryDualChannelAngles_ProducesShortestSignedDiff()
    {
        // 双通道方向角 0.01 与 2π−0.01：真实差 ≈0.02 rad，直接相减 ≈−2π+0.02（近整圈）
        var edgeTheta = 0.01;
        var duvTheta = TwoPi - 0.01;

        Assert.Equal(0.02, PolarPoint.AngleDifference(edgeTheta, duvTheta), 1e-12);
        Assert.Equal(-0.02, PolarPoint.AngleDifference(duvTheta, edgeTheta), 1e-12);
    }

    [Fact]
    public void AngleDifference_WithinPi_IsIdentity()
    {
        foreach (var diff in new[] { 0.0, 1e-9, 0.02, Math.PI / 2, 2.7, -0.02, -Math.PI / 2, -2.7 })
        {
            Assert.Equal(diff, PolarPoint.AngleDifference(diff, 0.0), 1e-12);
        }
    }

    [Fact]
    public void AngleDifference_NearFullCircle_FoldsToSmallSignedDiff()
    {
        // 接近 ±2π 的角差折叠为接近 0 的有符号值
        Assert.Equal(0.0, PolarPoint.AngleDifference(TwoPi, 0.0), 1e-12);
        Assert.Equal(0.0, PolarPoint.AngleDifference(-TwoPi, 0.0), 1e-12);
        Assert.Equal(0.05, PolarPoint.AngleDifference(TwoPi + 0.05, 0.0), 1e-12);
        Assert.Equal(0.05, PolarPoint.AngleDifference(-TwoPi + 0.05, 0.0), 1e-12);
        Assert.Equal(-0.05, PolarPoint.AngleDifference(TwoPi - 0.05, 0.0), 1e-12);
    }

    [Fact]
    public void AngleDifference_BeyondPi_FoldsIntoNegativeRange()
    {
        // (π, 2π) 的角差折叠为 (−π, 0)
        Assert.Equal(-Math.PI / 2, PolarPoint.AngleDifference(3 * Math.PI / 2, 0.0), 1e-12);
        // 精确 π 边界：结果模长为 π（符号由浮点细节决定，只验证模长）
        Assert.Equal(Math.PI, Math.Abs(PolarPoint.AngleDifference(Math.PI, 0.0)), 1e-12);
        Assert.Equal(Math.PI, Math.Abs(PolarPoint.AngleDifference(-Math.PI, 0.0)), 1e-12);
        Assert.Equal(Math.PI, Math.Abs(PolarPoint.AngleDifference(3 * Math.PI, 0.0)), 1e-12);
    }

    [Fact]
    public void AngleDifference_RoundTripWithFlatThetaDomain()
    {
        // Flat 输出域 [0,2π) 内任意两角度的最短角差：|AngleDifference(a,b)| ≤ π 且与真值一致
        var thetas = new[] { 0.0, 0.003, 0.5, Math.PI, TwoPi - 0.5, TwoPi - 0.003, 6.28 };
        foreach (var a in thetas)
        {
            foreach (var b in thetas)
            {
                var wrapped = PolarPoint.AngleDifference(a, b);
                Assert.True(Math.Abs(wrapped) <= Math.PI + 1e-12, $"a={a}, b={b}, wrapped={wrapped}");
                // 最短角差唯一：wrapped ≡ a−b (mod 2π)
                Assert.Equal(0.0, PolarPoint.AngleDifference(wrapped, a - b), 1e-9);
            }
        }
    }
}