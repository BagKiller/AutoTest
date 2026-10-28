// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Utility.Helper;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   Vega100 BPS 查表子表覆盖完整性回归（历史缺陷：子表缺 HT+Oblique+S 规则致跑片查表抛异常，WI 3695 / PR 5005）：
///   断言 V100 全部合法组合（HS/HT × S/P 斜入射 × 功率挡 100/10/1）均命中且无重叠歧义，规则值在 V100 支持档内。
///   BPSIOD 字段在 V100 无 BPSI 电机（SetBPSIOD 仅日志），属表结构兼容保留的死数据，不做断言。
/// </summary>
public class Vega100BpsTableCoverageTests
{
    private static Laser_BpsConfig LoadV100Table()
    {
        var tablePath = Path.Combine(TestPaths.RepoRoot, "Vega", "Resources", MachineConfigSubPaths.Vega100, LaserBpsConfigPaths.FileName);
        Assert.True(File.Exists(tablePath), $"Vega100 BPS table not found: {tablePath}");
        // 覆盖边界：直读仓库源码树默认表，而非运行时 LoadConfigManager 解析的副本（用户目录优先于安装目录）。
        // 本测试守护随版本发布的默认覆盖；现场改用户副本后的运行时行为不在范围内。
        return JsonHelper.Deserialize<Laser_BpsConfig>(tablePath)
            ?? throw new InvalidOperationException($"Failed to deserialize {tablePath}");
    }

    [Fact]
    public void AllV100Combos_HitExactlyOnePlainRule()
    {
        var config = LoadV100Table();
        foreach (var throughput in new[] { ThroughputType.HighSensitivity, ThroughputType.HighThroughput })
        {
            foreach (var polarizer in new[] { PolarizerType.S, PolarizerType.P })
            {
                foreach (var power in Vega100LaserPowerGears.PowerGears)
                {
                    var plain = config.PowerRules.Where(r =>
                        r.Incidence == IncidenceType.Oblique &&
                        r.Polarizer == polarizer &&
                        r.Throughput == throughput &&
                        !r.BEAFilter.HasValue &&
                        power > r.MinLaserPowerPercent &&
                        power <= r.MaxLaserPowerPercent).ToList();

                    var message = $"V100 combo {throughput}/{polarizer}/{power}%: expect exactly 1 plain rule, got {plain.Count}";
                    Assert.True(plain.Count == 1, message);

                    // 查表入口同口径复核：FindODAndExpTime 必须命中且返回该规则的推荐值（miss 会 throw，即线上报错形态）
                    var (_, bpsrOd, exposureTime) = config.FindODAndExpTime(IncidenceType.Oblique, polarizer, throughput, power, null);
                    Assert.Equal(plain[0].BPSROD, bpsrOd);
                    Assert.Equal(plain[0].ExposureTime, exposureTime);
                }
            }
        }
    }

    [Fact]
    public void AllRules_ObliqueAndWithinV100SupportedGears()
    {
        var config = LoadV100Table();
        Assert.All(config.PowerRules, r => Assert.Equal(IncidenceType.Oblique, r.Incidence));
        Assert.All(config.PowerRules, r => Assert.True(
            r.BPSROD is BPSROD.OD1 or BPSROD.OD2 or BPSROD.OD3 or BPSROD.OD4 or BPSROD.OD5 or BPSROD.OD6,
            $"BPSROD {r.BPSROD} is not a Vega100 gear (OD1-OD6)"));
        // OD1-OD6 的权威来源是 OpticsGearEnumService 的 Vega100 档位集（CreateDefaultConfig 映射，
        // 运行时经 OpticsGearConfig.json 用户副本），此处硬编码与其保持一致，档位集变更需两处同步。
        Assert.All(config.PowerRules, r => Assert.InRange(r.ExposureTime, 1, 9));
    }
}