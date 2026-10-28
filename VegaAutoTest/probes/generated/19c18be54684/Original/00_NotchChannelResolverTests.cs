// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.IWaferRun;
using Vega.Data.Commons;
using Vega.Data.Recipes;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// Vega100 notch 数据源家族可配置（Edge Detection/NotchDataChannel，默认 DW）：家族 + collection
/// 入射选型（与配方 GetPhysicalChannels 同族选型同规则；DW 家族 = DW2，决策 0020）。
/// </summary>
public class NotchChannelResolverTests
{
    [Theory]
    [InlineData(IncidenceType.Normal, NotchDataChannelFamily.DN, ChannelMode.DN_Normal)]
    [InlineData(IncidenceType.Oblique, NotchDataChannelFamily.DN, ChannelMode.DN_Oblique)]
    [InlineData(IncidenceType.Normal, NotchDataChannelFamily.DW, ChannelMode.DW2_Normal)]
    [InlineData(IncidenceType.Oblique, NotchDataChannelFamily.DW, ChannelMode.DW2_Oblique)]
    public void Resolve_MapsFamilyAndIncidenceToChannel(IncidenceType incidence, NotchDataChannelFamily family, ChannelMode expected)
    {
        Assert.Equal(expected, NotchChannelResolver.Resolve(incidence, family));
    }

    [Theory]
    [InlineData("DN", NotchDataChannelFamily.DN)]
    [InlineData("dw", NotchDataChannelFamily.DW)]
    [InlineData(null, NotchDataChannelFamily.DW)]
    [InlineData("XX", NotchDataChannelFamily.DW)]
    public void ParseFamily_FallsBackToDw(string? configured, NotchDataChannelFamily expected)
    {
        Assert.Equal(expected, NotchChannelResolver.ParseFamily(configured));
    }
}