// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Commons;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   机型配置子目录常量（MachineConfigSubPaths）与随版本发布的资源目录布局一致性回归：
///   消费方多为静默兜底形态（如 PMT 公式表 EnsureFormulasLoaded 的 catch 回落内置默认公式），
///   常量打错字不会在运行时报错，只会在真机上悄悄换表——用测试把常量钉死在源码树布局上。
///   边界同 Vega100BpsTableCoverageTests：只守护仓库默认表，不含运行时用户目录副本（decision 0007）。
///   文件名一律取单源常量（LaserBpsConfigPaths / PmtCaliConfigPaths），勿内联字面量。
/// </summary>
public class MachineConfigSubPathsTests
{
    [Fact]
    public void SubPathConstants_MatchShippedResourceLayout()
    {
        var resources = Path.Combine(TestPaths.RepoRoot, "Vega", "Resources");

        // 默认共享目录：BPS 主表 + PMT 条件/公式表（V200/300/500 共用）
        var defaultFiles = new[] { LaserBpsConfigPaths.FileName, PmtCaliConfigPaths.ConditionsFileName, PmtCaliConfigPaths.AttenuationFormulasFileName };

        // Vega100 子目录：BPS 子表 + PMT 条件/公式表（机型专属）；Gen1 子目录：仅 BPS 表
        var vega100Files = defaultFiles;
        var gen1Files = new[] { LaserBpsConfigPaths.FileName };

        foreach (var (subPath, files) in new[]
        {
            (MachineConfigSubPaths.Default, defaultFiles),
            (MachineConfigSubPaths.Vega100, vega100Files),
            (MachineConfigSubPaths.Gen1, gen1Files),
        })
        {
            foreach (var file in files)
            {
                var path = Path.Combine(resources, subPath, file);
                Assert.True(File.Exists(path), $"missing {path}");
            }
        }
    }
}