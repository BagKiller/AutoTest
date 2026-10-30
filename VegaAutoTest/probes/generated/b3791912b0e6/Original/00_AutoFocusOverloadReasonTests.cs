// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.IWaferRun;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// AutoFocus 过载原因组合（BPSR/BPSI 双通道 × 过曝/信号不足双类型）：按类型聚合、段间 "; "，
/// 同类型跨通道合并 BPSR+BPSI，全 false 空串。参数顺序 (bpsrOverExposure, bpsrSignalInsufficient, bpsiOverExposure, bpsiSignalInsufficient)。
/// </summary>
public class AutoFocusOverloadReasonTests
{
    [Theory]
    [InlineData(false, false, false, false, "")]
    [InlineData(true, false, false, false, "BPSR OverExposure")]
    [InlineData(false, true, false, false, "BPSR SignalInsufficient")]
    [InlineData(false, false, true, false, "BPSI OverExposure")]
    [InlineData(false, false, false, true, "BPSI SignalInsufficient")]
    [InlineData(true, false, true, false, "BPSR+BPSI OverExposure")]
    [InlineData(false, true, false, true, "BPSR+BPSI SignalInsufficient")]
    [InlineData(true, false, false, true, "BPSR OverExposure; BPSI SignalInsufficient")]
    [InlineData(false, true, true, false, "BPSI OverExposure; BPSR SignalInsufficient")]
    [InlineData(true, true, false, false, "BPSR OverExposure; BPSR SignalInsufficient")]
    [InlineData(false, false, true, true, "BPSI OverExposure; BPSI SignalInsufficient")]
    [InlineData(true, true, true, true, "BPSR+BPSI OverExposure; BPSR+BPSI SignalInsufficient")]
    public void Build_CombinesChannelAndTypeDimensions(bool bpsrOverExposure, bool bpsrSignalInsufficient, bool bpsiOverExposure, bool bpsiSignalInsufficient, string expected)
    {
        Assert.Equal(expected, AutoFocusOverloadReason.Build(bpsrOverExposure, bpsrSignalInsufficient, bpsiOverExposure, bpsiSignalInsufficient));
    }
}