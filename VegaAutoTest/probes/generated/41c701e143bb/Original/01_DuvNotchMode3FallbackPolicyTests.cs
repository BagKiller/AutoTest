// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// 模式三回退选路策略（纯函数测试缝）：
/// 模式二未产出有限值必回退模式一；合成剖面（低置信）结果仅在 VacuumChuck 下回退——
/// EdgeChuck 的模式一冻结在 8c8583d50、不含 PR 4975 的粗定位假候选修复，
/// 不得覆盖锚定真实测量点的低置信模式二结果（取舍记录于 wafer-run state.md 的 DUV 对位模式条目）。
/// </summary>
public class DuvNotchMode3FallbackPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)] // 非双保险模式（诊断页单模式）永不回退
    [InlineData(false, false, true,  false, true)]  // 模式二无值 → 必回退（VacuumChuck）
    [InlineData(false, false, true,  true,  true)]  // 模式二无值 → EdgeChuck 同样回退（无值时旧链是唯一出路）
    [InlineData(false, true,  true,  true,  true)]  // 无值优先于合成标记与机型门
    [InlineData(true,  false, true,  false, false)] // 原始点结果（非合成）不回退
    [InlineData(true,  true,  true,  false, true)]  // 合成剖面 + VacuumChuck → 回退（AC3 的 falling back 分支）
    [InlineData(true,  true,  true,  true,  false)] // 合成剖面 + EdgeChuck → 保留低置信结果，不咨询旧链
    [InlineData(true,  true,  false, false, false)] // 纯模式二的合成结果不受回退门影响
    public void ShouldRunLegacyFallback_MatchesPolicyMatrix(
        bool mode2ProducedFinite, bool usedSyntheticProfile, bool autoFallbackToLegacy, bool isEdgeChuck, bool expected)
    {
        Assert.Equal(expected, WaferAlignmentService.ShouldRunLegacyFallback(
            mode2ProducedFinite, usedSyntheticProfile, autoFallbackToLegacy, isEdgeChuck));
    }

    [Fact]
    public void PreferLegacyTheta_FiniteLegacyWins()
    {
        Assert.Equal(1.25, WaferAlignmentService.PreferLegacyTheta(5.825, 1.25));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void PreferLegacyTheta_NonFiniteLegacyKeepsMode2(double legacyTheta)
    {
        const double mode2Theta = 5.825;
        Assert.Equal(mode2Theta, WaferAlignmentService.PreferLegacyTheta(mode2Theta, legacyTheta));
    }
}