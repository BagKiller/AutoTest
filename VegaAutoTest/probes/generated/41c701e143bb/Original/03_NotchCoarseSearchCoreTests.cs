// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// 粗定位核心回归（PR 5028 谷检测语义）：中值平滑后的谷候选须满足连续宽度、
/// 有效邻域与深度拟合验证；小数组窄谷与无效邻域均被拒，全无效/掩码契约破坏直接 -1。
/// 同输入下模式一旧链路（NotchCoarseSearchLegacyTests）可能选中窄谷谷底，两套行为在此分叉。
/// </summary>
public class NotchCoarseSearchCoreTests
{
    private static readonly PointInPolar CowAtOrigin = new(0.0, 0.0);

    private static PointInPolar[] MakePoints(params double[] radii)
    {
        return [.. radii.Select((radius, i) => new PointInPolar(radius, (double) i / radii.Length * 2.0 * Math.PI))];
    }

    [Fact]
    public void AllInvalid_ReturnsMinusOneWithWarning()
    {
        var points = MakePoints(16, 14, 11, 8, 10, 12, 15, 17);
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, points, notchSearchingHalfRange: 2, validMask: new bool[8], warn: warnings.Add);

        Assert.Equal(-1, index);
        var warning = Assert.Single(warnings);
        Assert.Contains("no valid edge points", warning);
    }

    [Fact]
    public void MismatchedMaskLength_ReturnsMinusOneWithWarning()
    {
        //掩码长度与点数不符是契约破坏：显式告警 + -1，不随机越界
        var points = MakePoints(16, 14, 11, 8, 10, 12, 15, 17);
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, points, notchSearchingHalfRange: 2, validMask: new bool[3], warn: warnings.Add);

        Assert.Equal(-1, index);
        var warning = Assert.Single(warnings);
        Assert.Contains("does not match", warning);
    }

    [Fact]
    public void EmptyPoints_Unmasked_ReturnsMinusOneSilently()
    {
        //空点数组 + 无掩码：零候选走 -1，"正确地失败"且不告警（无掩码路径保持零行为差异），本用例钉死该行为
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, [], notchSearchingHalfRange: 2, validMask: null, warn: warnings.Add);

        Assert.Equal(-1, index);
        Assert.Empty(warnings);
    }

    [Fact]
    public void NarrowDipOnSmallCircle_SmoothingErasesValley_ReturnsMinusOne()
    {
        //8 点圆上的 3 点窄谷（idx2..4，谷底 r=8）经 ±3 中值平滑后谷底抬到均值线
        //（smooth[3]=12=avg），无任何谷候选 → -1 + 单条汇总告警。
        //谷检测要求谷宽覆盖搜索窗（2·halfRange+1=3 的平滑后连续区段）。
        //同一输入下模式一旧链路返回 3（NotchCoarseSearchLegacyTests.WideDip_SelectsDeepestPoint），两套行为在此分叉
        var points = MakePoints(16, 14, 11, 8, 10, 12, 15, 17);
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, points, notchSearchingHalfRange: 2, validMask: null, warn: warnings.Add);

        Assert.Equal(-1, index);
        var warning = Assert.Single(warnings);
        Assert.Contains("no valley passed", warning);
    }

    [Fact]
    public void PartialValid_InvalidNeighborKillsOnlyValley_ReturnsMinusOne()
    {
        //idx0 无效：唯一谷候选 idx1（smooth=8.5）的验证邻域含无效点被拒（邻域有效性），
        //其余局部极小被平滑串联跳过 → -1 + 单条汇总告警。
        //有效均值分母语义（均值只计有效点）仍在阈值推导中生效，但在该输入下不再改变选中结果
        var points = MakePoints(0, 10, 9, 7, 8, 12, 14, 16);
        var validMask = new[] { false, true, true, true, true, true, true, true };
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, points, notchSearchingHalfRange: 2, validMask: validMask, warn: warnings.Add);

        Assert.Equal(-1, index);
        var warning = Assert.Single(warnings);
        Assert.Contains("no valley passed", warning);
    }

    [Fact]
    public void MinimumCandidateInvalid_NoValleyAfterSmoothing_ReturnsMinusOne()
    {
        //谷底 idx3 无效：±3 中值平滑以有效邻居（11/10）填充后剖面几乎水平（smooth≈13=avg），
        //无谷候选 → -1 + 单条汇总告警（不再有"7 valid candidate(s)"的逐候选评估文案）
        var points = MakePoints(15, 13, 11, 8, 10, 12, 14, 16);
        var validMask = new[] { true, true, true, false, true, true, true, true };
        var warnings = new List<string>();

        var index = WaferAlignmentService.NotchCoarseSearchCore(CowAtOrigin, points, notchSearchingHalfRange: 2, validMask: validMask, warn: warnings.Add);

        Assert.Equal(-1, index);
        var warning = Assert.Single(warnings);
        Assert.Contains("no valley passed", warning);
    }
}