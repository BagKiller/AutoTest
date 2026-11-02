// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Shared;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// AFS CCD 质心/峰值共用算法：中心取最大值首次出现的下标（与旧 IndexOf(Max()) 口径一致），
/// AFS 诊断页与扫描截图消费同一实现；空数据不抛异常，HasData = false。
/// </summary>
public class AfsCcdChannelStatsTests
{
    [Fact]
    public void Describe_EmptyData_ReturnsNoData()
    {
        var stats = AfsCcdChannelStats.Describe([]);

        Assert.False(stats.HasData);
        Assert.Equal(-1, stats.CenterIndex);
        Assert.Equal(0u, stats.PeakValue);
    }

    [Fact]
    public void Describe_SingleSample_IsBothCenterAndPeak()
    {
        var stats = AfsCcdChannelStats.Describe([4095u]);

        Assert.True(stats.HasData);
        Assert.Equal(0, stats.CenterIndex);
        Assert.Equal(4095u, stats.PeakValue);
    }

    [Fact]
    public void Describe_DuplicatePeak_ReturnsFirstOccurrence()
    {
        var stats = AfsCcdChannelStats.Describe([10u, 3000u, 20u, 3000u, 5u]);

        Assert.Equal(1, stats.CenterIndex);
        Assert.Equal(3000u, stats.PeakValue);
    }

    [Fact]
    public void Describe_PeakAtLastSample()
    {
        var stats = AfsCcdChannelStats.Describe([1u, 2u, 3u, 4u]);

        Assert.Equal(3, stats.CenterIndex);
        Assert.Equal(4u, stats.PeakValue);
    }

    [Fact]
    public void Describe_AllZero_ReturnsFirstIndexWithZeroPeak()
    {
        var stats = AfsCcdChannelStats.Describe([0u, 0u, 0u]);

        Assert.True(stats.HasData);
        Assert.Equal(0, stats.CenterIndex);
        Assert.Equal(0u, stats.PeakValue);
    }
}