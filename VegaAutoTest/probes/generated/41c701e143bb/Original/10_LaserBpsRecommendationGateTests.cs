// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Data.Recipes.Helper;
using Vega.Utility.Helper;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   BPSOD 推荐值机型门控回归（历史缺陷：Vega100 放开查表后门控布尔写反，非 V100 机型斜入射+P/S
///   也被退到默认值；且 V100 子表缺 Normal/C 规则时推荐值静默回退 LookupFailed，即"改错类 + 空转"，PR 5067 / WI 3755）。
///   走 LaserBpsCalculator.ComputeFromTable 纯函数核心，不依赖机台特征（ToolFeaturesHelper）。
/// </summary>
public class LaserBpsRecommendationGateTests
{
    private static readonly BPSROD[] V100SupportedOds =
    [
        BPSROD.OD1, BPSROD.OD2, BPSROD.OD3, BPSROD.OD4, BPSROD.OD5, BPSROD.OD6
    ];

    private static Laser_BpsConfig LoadV100Table()
    {
        var tablePath = Path.Combine(TestPaths.RepoRoot, "Vega", "Resources", MachineConfigSubPaths.Vega100, LaserBpsConfigPaths.FileName);
        Assert.True(File.Exists(tablePath), $"Vega100 BPS table not found: {tablePath}");
        return JsonHelper.Deserialize<Laser_BpsConfig>(tablePath)
            ?? throw new InvalidOperationException($"Failed to deserialize {tablePath}");
    }

    private static Laser_BpsConfig BuildTable(params Laser_BpsRule[] rules) => new() { PowerRules = [.. rules] };

    private static Laser_BpsRule Rule(IncidenceType incidence, PolarizerType polarizer,
        double min, double max, BPSROD od, ushort exposure) => new()
    {
        Incidence = incidence,
        Polarizer = polarizer,
        Throughput = ThroughputType.HighSensitivity,
        MinLaserPowerPercent = min,
        MaxLaserPowerPercent = max,
        BPSIOD = BPSIOD.OD0,
        BPSROD = od,
        ExposureTime = exposure,
    };

    [Fact]
    public void V100_NormalIncidence_HitsTableAndReturnsRuleValues()
    {
        var config = LoadV100Table();

        // 核心回归场景：Vega100 + 正入射 + 偏振 C（旧门控与旧子表双重缺失的组合），必须查表命中并返回表内 OD/曝光
        var recommendation = LaserBpsCalculator.ComputeFromTable(config, isVega100: true,
            IncidenceType.Normal, PolarizerType.C, ThroughputType.HighSensitivity,
            laserPower: 100, bEAFilter: null, BPSROD.OD1, V100SupportedOds);

        Assert.Equal(BpsRecommendationStatus.Matched, recommendation.Status);
        var expectedRule = config.PowerRules.Single(r =>
            r.Incidence == IncidenceType.Normal &&
            r.Polarizer == PolarizerType.C &&
            r.Throughput == ThroughputType.HighSensitivity &&
            !r.BEAFilter.HasValue &&
            100 > r.MinLaserPowerPercent &&
            100 <= r.MaxLaserPowerPercent);
        Assert.Equal(expectedRule.BPSROD, recommendation.BpsROD);
        Assert.Equal(expectedRule.ExposureTime, recommendation.ExposureTime);
    }

    [Fact]
    public void V100_AllNormalCombos_ResolveWithoutFallback()
    {
        var config = LoadV100Table();
        foreach (var throughput in new[] { ThroughputType.HighSensitivity, ThroughputType.HighThroughput })
        {
            foreach (var polarizer in new[] { PolarizerType.S, PolarizerType.P, PolarizerType.C })
            {
                foreach (var power in Vega100LaserPowerGears.PowerGears)
                {
                    var recommendation = LaserBpsCalculator.ComputeFromTable(config, isVega100: true,
                        IncidenceType.Normal, polarizer, throughput, power, null, BPSROD.OD1, V100SupportedOds);

                    Assert.True(recommendation.Status == BpsRecommendationStatus.Matched,
                        $"V100 Normal/{throughput}/{polarizer}/{power}% fell back: {recommendation.Status}");
                }
            }
        }
    }

    [Fact]
    public void NonV100_ObliquePolarized_StillLooksUpTable()
    {
        // 布尔回归守卫：非 V100 机型的斜入射+P/S 必须照常查表（不可被"非 V100"短路吞掉）
        var config = BuildTable(Rule(IncidenceType.Oblique, PolarizerType.S, 50, 100, BPSROD.OD3, 2));

        var recommendation = LaserBpsCalculator.ComputeFromTable(config, isVega100: false,
            IncidenceType.Oblique, PolarizerType.S, ThroughputType.HighSensitivity,
            laserPower: 100, bEAFilter: null, BPSROD.OD1, [BPSROD.OD1, BPSROD.OD2, BPSROD.OD3]);

        Assert.Equal(BpsRecommendationStatus.Matched, recommendation.Status);
        Assert.Equal(BPSROD.OD3, recommendation.BpsROD);
        Assert.Equal(2, recommendation.ExposureTime);
    }

    [Fact]
    public void NonV100_NormalOrCPolarizer_KeepsNotApplicableDefault()
    {
        // 口径保持：非 V100 机型的正入射/偏振 C 仍回机型默认值（主表无规则，不查表），不受 V100 放开影响
        var config = BuildTable(Rule(IncidenceType.Oblique, PolarizerType.S, 50, 100, BPSROD.OD3, 2));

        var normal = LaserBpsCalculator.ComputeFromTable(config, isVega100: false,
            IncidenceType.Normal, PolarizerType.S, ThroughputType.HighSensitivity,
            laserPower: 100, bEAFilter: null, BPSROD.OD1, [BPSROD.OD1, BPSROD.OD2, BPSROD.OD3]);
        var cPolarized = LaserBpsCalculator.ComputeFromTable(config, isVega100: false,
            IncidenceType.Oblique, PolarizerType.C, ThroughputType.HighSensitivity,
            laserPower: 100, bEAFilter: null, BPSROD.OD1, [BPSROD.OD1, BPSROD.OD2, BPSROD.OD3]);

        foreach (var recommendation in new[] { normal, cPolarized })
        {
            Assert.Equal(BpsRecommendationStatus.NotApplicable, recommendation.Status);
            Assert.Equal(BPSROD.OD1, recommendation.BpsROD);
            Assert.Equal(1, recommendation.ExposureTime);
        }
    }
}