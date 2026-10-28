// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.
using Vega.Utility.Common;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// EdgeDetectionConfig 的 Flat/Notch 边缘扫描范围解析：
/// 仅 Flat 且非 EdgeChuck 使用 FlatEdgeScope；Notch、EdgeChuck 与未配置回退均保持 EdgeScope。
/// </summary>
public class EdgeDetectionConfigScopeTests
{
    [Fact]
    public void GetEffectiveEdgeScope_FlatNonEdgeChuck_UsesFlatScope()
    {
        var config = new EdgeDetectionConfig { EdgeScope = 2500, FlatEdgeScope = 8500 };

        Assert.Equal(8500, config.GetEffectiveEdgeScope(isFlat: true, isEdgeChuck: false));
    }

    [Fact]
    public void GetEffectiveEdgeScope_Notch_UsesEdgeScope()
    {
        var config = new EdgeDetectionConfig { EdgeScope = 2500, FlatEdgeScope = 8500 };

        Assert.Equal(2500, config.GetEffectiveEdgeScope(isFlat: false, isEdgeChuck: false));
        Assert.Equal(2500, config.GetEffectiveEdgeScope(isFlat: false, isEdgeChuck: true));
    }

    [Fact]
    public void GetEffectiveEdgeScope_FlatEdgeChuck_UsesEdgeScope()
    {
        var config = new EdgeDetectionConfig { EdgeScope = 2500, FlatEdgeScope = 8500 };

        Assert.Equal(2500, config.GetEffectiveEdgeScope(isFlat: true, isEdgeChuck: true));
    }

    [Fact]
    public void GetEffectiveEdgeScope_FlatScopeNotConfigured_FallsBackToEdgeScope()
    {
        var config = new EdgeDetectionConfig { EdgeScope = 2500, FlatEdgeScope = 0 };

        Assert.Equal(2500, config.GetEffectiveEdgeScope(isFlat: true, isEdgeChuck: false));
    }

    [Fact]
    public void FlatEdgeScope_Default_CoversFlatDepth()
    {
        // 6 寸主平边 57500µm 的弦深约 5729µm；默认 FlatEdgeScope 必须大于该值。
        var config = new EdgeDetectionConfig();

        Assert.True(config.FlatEdgeScope > 5729, $"FlatEdgeScope default {config.FlatEdgeScope} does not cover the 6-inch flat depth.");
    }
}