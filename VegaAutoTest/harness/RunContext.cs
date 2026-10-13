// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

namespace Vega.AutoTest.Harness;

/// <summary>
/// 单次运行的落盘上下文。Runner 通过 VEGA_AUTOTEST_RUNDIR 注入（指向 runDir\&lt;layer&gt;\，
/// 避免多层同跑时 assert-*.json 互相覆盖，方案修正 #1）；
/// 直接在 IDE 里跑用例时回落到 OutDir，保持原有手跑体验不变。
/// </summary>
public static class RunContext
{
    private static readonly Lazy<string> LazyDir = new(() =>
    {
        var dir = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_RUNDIR");
        if (string.IsNullOrWhiteSpace(dir))
        {
            dir = VegaEnv.OutDir;
        }

        Directory.CreateDirectory(dir);
        return dir;
    });

    public static string Dir => LazyDir.Value;
}
