// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;

namespace Vega.AutoTest.Runner;

/// <summary>被测对象指纹：git 提交/分支/脏区 + Vega.exe 时间戳（防止"验的不是你改的那份"）。</summary>
public sealed class TargetProbeResult
{
    public string RepoRoot { get; init; } = string.Empty;

    public string? GitCommit { get; init; }

    public string? GitBranch { get; init; }

    public bool GitDirty { get; init; }

    public string? VegaExe { get; init; }

    public string? VegaExeBuiltAt { get; init; }

    public bool VegaExeExists { get; init; }
}

public static class TargetProbe
{
    public static TargetProbeResult Probe(string repoRoot, string configuration)
    {
        return new TargetProbeResult
        {
            RepoRoot = repoRoot,
            GitCommit = Git(repoRoot, "rev-parse HEAD"),
            GitBranch = Git(repoRoot, "rev-parse --abbrev-ref HEAD"),
            GitDirty = !string.IsNullOrWhiteSpace(Git(repoRoot, "status --porcelain")),
            VegaExe = FindVegaExe(repoRoot, configuration, out var exists),
            VegaExeBuiltAt = FindVegaExeTime(repoRoot, configuration),
            VegaExeExists = exists,
        };
    }

    public static string DefaultVegaExePath(string repoRoot, string configuration)
        => Path.Combine(repoRoot, "Vega", "bin", configuration,
            "net8.0-windows10.0.19041.0", "win-x64", "Vega.exe");

    private static string? FindVegaExe(string repoRoot, string configuration, out bool exists)
    {
        var path = DefaultVegaExePath(repoRoot, configuration);
        exists = File.Exists(path);
        return path;
    }

    private static string? FindVegaExeTime(string repoRoot, string configuration)
    {
        var path = DefaultVegaExePath(repoRoot, configuration);
        return File.Exists(path) ? File.GetLastWriteTime(path).ToString("yyyy-MM-dd'T'HH:mm:sszzz") : null;
    }

    private static string? Git(string repoRoot, string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git")
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var token in args.Split(' '))
            {
                psi.ArgumentList.Add(token);
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(10_000);
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
