// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.UI.Calibration;

namespace Vega.UI.Calibration.Tests;

public class RollingWindowTests
{
    [Fact]
    public void Push_EvictsAtOrBeforeCutoff_MeanTracksRunningSum()
    {
        var window = new RollingWindow();

        window.Push(10, now: 0, windowSeconds: 1);
        window.Push(20, now: 500, windowSeconds: 1);
        // cutoff=0：Tick ≤ 0（含恰好等于 cutoff 的边界样本）淘汰 → 剩 {500:20, 1000:30}
        window.Push(30, now: 1000, windowSeconds: 1);

        Assert.Equal(25, window.Mean!.Value, precision: 6);
    }

    [Fact]
    public void Push_WindowShrink_OnlyFreshSurvive()
    {
        var window = new RollingWindow();

        for (long t = 0; t <= 4000; t += 1000)
        {
            window.Push(5, t, 10);
        }

        // 窗口 10s→1s：cutoff=4000，旧样本全淘汰，只剩刚入队的
        window.Push(7, 5000, 1);

        Assert.Equal(7, window.Mean!.Value, precision: 6);
    }

    [Fact]
    public void Clear_MeanBecomesNull()
    {
        var window = new RollingWindow();
        window.Push(10, 0, 1);

        window.Clear();

        Assert.Null(window.Mean);
    }
}