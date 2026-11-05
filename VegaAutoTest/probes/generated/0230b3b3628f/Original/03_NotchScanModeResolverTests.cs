// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.IWaferRun;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// Vega100 notch 对位扫描模式可配置（Edge Detection/NotchScanMode，默认 Inline）：
/// 缺省/非法值回退 Inline（=现状，新模式红线：默认关闭时行为与现状一致）。
/// </summary>
public class NotchScanModeResolverTests
{
    [Theory]
    [InlineData("Dedicated", NotchScanMode.Dedicated)]
    [InlineData("dedicated", NotchScanMode.Dedicated)]
    [InlineData("Inline", NotchScanMode.Inline)]
    [InlineData(null, NotchScanMode.Inline)]
    [InlineData("", NotchScanMode.Inline)]
    [InlineData("XX", NotchScanMode.Inline)]
    public void ParseMode_FallsBackToInline(string? configured, NotchScanMode expected)
    {
        Assert.Equal(expected, NotchScanModeResolver.ParseMode(configured));
    }
}