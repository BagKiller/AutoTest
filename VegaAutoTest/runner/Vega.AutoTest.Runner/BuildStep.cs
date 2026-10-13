// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;

namespace Vega.AutoTest.Runner;

/// <summary>--rebuild 步骤：复用仓库沉淀的 run-vega-with-plugin.ps1（踩坑结论都在脚本里），不另起炉灶。</summary>
public static class BuildStep
{
    public static readonly string ScriptRelativePath =
        Path.Combine("Vega.BL.AgentControlService", "scripts", "run-vega-with-plugin.ps1");

    public sealed class BuildOutcome
    {
        public bool Succeeded { get; init; }

        public bool ScriptFound { get; init; }
    }

    /// <summary>
    /// 调 run-vega-with-plugin.ps1 -NoLaunch，全量输出写 runs/&lt;runId&gt;/build.log。
    /// 调用方按“失败 + 有旧 exe → stale 继续；失败 + 无 exe → ERROR”降级。
    /// </summary>
    public static BuildOutcome Run(string repoRoot, string configuration, string logPath)
    {
        var script = Path.Combine(repoRoot, ScriptRelativePath);
        if (!File.Exists(script))
        {
            File.WriteAllText(logPath, $"build script not found: {script}");
            return new BuildOutcome { Succeeded = false, ScriptFound = false };
        }

        var psi = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(script);
        psi.ArgumentList.Add("-NoLaunch");
        psi.ArgumentList.Add("-Configuration");
        psi.ArgumentList.Add(configuration);

        using var process = Process.Start(psi);
        if (process is null)
        {
            File.WriteAllText(logPath, "failed to start powershell.exe");
            return new BuildOutcome { Succeeded = false, ScriptFound = true };
        }

        // 逐行异步读避免管道缓冲塞死
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(15 * 60_000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // 进程可能已退出
            }

            File.WriteAllText(logPath, "build timed out after 15 minutes");
            return new BuildOutcome { Succeeded = false, ScriptFound = true };
        }

        File.WriteAllText(logPath, stdout.Result + stderr.Result);
        return new BuildOutcome { Succeeded = process.ExitCode == 0, ScriptFound = true };
    }
}
