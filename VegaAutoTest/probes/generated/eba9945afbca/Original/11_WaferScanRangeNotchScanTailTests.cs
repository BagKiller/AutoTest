// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Shared;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// WaferScanRange.GetNotchScanTailTracks 单测：Flat NotchScan 的 DUV 捕获窗必须钉在分析锚点对应的
/// 轨道区间 [锚点, 锚点 + 有效范围]。扫描终点未达目标时由 exOffset 补到目标；达到/超过目标时只加
/// 500 µm 运动余量，末尾多出的轨道由尾部跳过数剔除，捕获窗起点仍回到锚点。
/// </summary>
public class WaferScanRangeNotchScanTailTests
{
    private const double AnchorRadius = 148500;
    private const double FlatEdgeScope = 8500;
    private const double Pitch = 25;
    private const double TargetEndRadius = AnchorRadius + FlatEdgeScope; // 157000

    [Theory]
    [InlineData(156000, 157000, 0)]   // 终点小于目标：补到目标，无尾部跳过
    [InlineData(157000, 157500, 20)]  // 终点等于目标：+500 µm 运动余量，跳过 20 轨
    [InlineData(160000, 160500, 140)] // 终点大于目标（ExtendScan 10 mm）：最终终点 160500，跳过 140 轨
    [InlineData(160500, 161000, 160)] // 已超目标再 +500 µm：跳过 160 轨
    public void TailTracks_PinWindowToAnalysisAnchor(double rawScanEndRadius, double expectedFinalEndRadius, int expectedTailTracks)
    {
        // SetupForCollectAsync：终点未达目标时补到目标；达到/超过目标时只加 500 µm 运动余量。
        int exOffset = (int) (TargetEndRadius - rawScanEndRadius);
        var finalEndRadius = rawScanEndRadius + (exOffset > 0 ? exOffset : 500);

        var tailTracks = WaferScanRange.GetNotchScanTailTracks(finalEndRadius, TargetEndRadius, Pitch);

        Assert.Equal(expectedFinalEndRadius, finalEndRadius, 1e-9);
        Assert.Equal(expectedTailTracks, tailTracks);

        // 捕获窗末轨（不含）在目标终点；窗首轨 = 锚点（TrackCount = scope / Pitch + 1）。
        var trackCount = (int) (FlatEdgeScope / Pitch + 1);
        var windowFirstRadius = finalEndRadius - tailTracks * Pitch - (trackCount - 1) * Pitch;

        Assert.Equal(AnchorRadius, windowFirstRadius, 1e-9);
    }

    [Fact]
    public void TailTracks_InvalidInputs_ReturnZero()
    {
        Assert.Equal(0, WaferScanRange.GetNotchScanTailTracks(double.NaN, TargetEndRadius, Pitch));
        Assert.Equal(0, WaferScanRange.GetNotchScanTailTracks(double.PositiveInfinity, TargetEndRadius, Pitch));
        Assert.Equal(0, WaferScanRange.GetNotchScanTailTracks(160500, TargetEndRadius, 0));
        Assert.Equal(0, WaferScanRange.GetNotchScanTailTracks(160500, double.NaN, Pitch));
    }
}