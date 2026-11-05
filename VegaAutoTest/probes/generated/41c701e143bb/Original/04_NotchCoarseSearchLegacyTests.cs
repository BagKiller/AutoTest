// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// 模式一（8c8583d50 旧链路）排序式粗定位回归：按到 CoW 距离升序取最近点，
/// ±NotchSearchingHalfRange/2 邻域全部低于 average − (average − r)/4 阈值线才通过；
/// 单点深谷（邻域验证不过）与无谷平线的行为对照见各用例。与模式二谷检测粗定位
/// （NotchCoarseSearchCoreTests）互为行为快照。
/// </summary>
public class NotchCoarseSearchLegacyTests
{
    private static readonly PointInPolar CowAtOrigin = new(0.0, 0.0);

    private static PointInPolar[] MakePoints(params double[] radii)
    {
        return [.. radii.Select((radius, i) => new PointInPolar(radius, (double) i / radii.Length * 2.0 * Math.PI))];
    }

    [Fact]
    public void WideDip_SelectsDeepestPoint()
    {
        //谷底在 idx3（r=8）：avg=12.875 → 阈值线 11.65625，验证邻域 j2..4（11/8/10）全部低于 → 选中 3
        var points = MakePoints(16, 14, 11, 8, 10, 12, 15, 17);

        var index = WaferAlignmentService.NotchCoarseSearchLegacyCore(CowAtOrigin, points, notchSearchingHalfRange: 2);

        Assert.Equal(3, index);
    }

    [Fact]
    public void SingleDeepSpike_NeighborhoodFails_AllCandidatesRejected()
    {
        //idx50 单点深谷（r=50，其余 100）：候选 idx50 邻域含 100 > 阈值线 87.125 被拒；
        //其余候选自身或邻域必含 100 > 99.625 同样被拒 → -1（旧算法不认窄于验证窗的谷）
        double[] radii = Enumerable.Repeat(100.0, 100).ToArray();
        radii[50] = 50.0;
        var points = MakePoints(radii);

        var index = WaferAlignmentService.NotchCoarseSearchLegacyCore(CowAtOrigin, points, notchSearchingHalfRange: 2);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void FlatProfile_FirstSortedCandidatePasses()
    {
        //全平等半径：阈值线 = avg，任何点都不 ">" 阈值线 → 首个排序候选（idx0）即通过。
        //钉死平输入的退化行为：不失败、返回首点（Array.Sort 平局取首个），与模式二谷检测行为不同
        var points = MakePoints(100, 100, 100, 100, 100, 100, 100, 100);

        var index = WaferAlignmentService.NotchCoarseSearchLegacyCore(CowAtOrigin, points, notchSearchingHalfRange: 2);

        Assert.Equal(0, index);
    }

    [Fact]
    public void EmptyPoints_ReturnsMinusOne()
    {
        var index = WaferAlignmentService.NotchCoarseSearchLegacyCore(CowAtOrigin, [], notchSearchingHalfRange: 2);

        Assert.Equal(-1, index);
    }

    [Fact]
    public void OffCenterCow_DistanceToCowDrivesSearch()
    {
        //CoW 在 (5, π/2)：θ=π/2 点距离 = |r−5|，θ=π 点距离 = √(25+r²)。唯一谷 idx5（r=1@π → 距离 5.099），
        //avg=7.761 → 阈值线 7.0955，邻域 idx4/6（5.463/5.590）通过 → 选中 5。
        //距 CoW 距离而非原始半径驱动排序：idx0（r=12）原始半径更大但距离仅 7，仍高于阈值线不干扰
        var cow = new PointInPolar(5.0, Math.PI / 2);
        PointInPolar[] points =
        [
            new(12.0, Math.PI / 2),
            new(3.2, Math.PI),
            new(20.0, Math.PI / 2),
            new(13.0, Math.PI / 2),
            new(2.2, Math.PI),
            new(1.0, Math.PI),
            new(2.5, Math.PI),
            new(15.0, Math.PI / 2),
        ];

        var index = WaferAlignmentService.NotchCoarseSearchLegacyCore(cow, points, notchSearchingHalfRange: 2);

        Assert.Equal(5, index);
    }
}