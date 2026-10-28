// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Shared;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// WaferScanRange.TryGetNotchScanCaptureWindow 单测：捕获窗 = [TotalTrack − 尾部跳过数 − edgeTracks,
/// TotalTrack − 尾部跳过数)。轨数不足时必须显式失败，不得静默截短窗口。
/// </summary>
public class WaferScanRangeCaptureWindowTests
{
    [Theory]
    [InlineData(6301, 0, 341, 5960, 6301)]    // Notch/EdgeChuck：无尾部跳过，窗口 = 末尾 edgeTracks 轨
    [InlineData(6301, 20, 341, 5940, 6281)]   // Flat +500 µm 兜底：跳过 20 轨后窗口起点回到锚点（5940 × 25 = 148500）
    [InlineData(6421, 140, 341, 5940, 6281)]  // ExtendScan 超目标：跳过 140 轨后窗口仍为锚点区间
    [InlineData(10, 5, 5, 0, 5)]              // 恰好吃满：末轨等于窗口宽度
    public void CaptureWindow_ValidInputs_PinsWindowBeforeTail(int totalTracks, int tailTracks, int edgeTracks,
        int expectedStart, int expectedEnd)
    {
        Assert.True(WaferScanRange.TryGetNotchScanCaptureWindow(totalTracks, tailTracks, edgeTracks,
            out var startTrack, out var endTrack));

        Assert.Equal(expectedStart, startTrack);
        Assert.Equal(expectedEnd, endTrack);
    }

    [Theory]
    [InlineData(0, 0, 340)]     // 无轨道
    [InlineData(-1, 0, 340)]    // 负总轨数
    [InlineData(340, -1, 340)]  // 负尾部跳过
    [InlineData(340, 340, 340)] // 尾部跳过吃掉全部轨道
    [InlineData(340, 351, 340)] // 尾部跳过超过总轨数
    [InlineData(340, 0, 0)]     // 窗口宽度非正
    [InlineData(340, 0, -5)]
    [InlineData(10, 5, 6)]      // 末轨（5）早于窗口宽度（6）：不可截短
    public void CaptureWindow_InvalidInputs_ReturnFalse(int totalTracks, int tailTracks, int edgeTracks)
    {
        Assert.False(WaferScanRange.TryGetNotchScanCaptureWindow(totalTracks, tailTracks, edgeTracks,
            out var startTrack, out var endTrack));

        Assert.Equal(0, startTrack);
        Assert.Equal(0, endTrack);
    }
}