// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using System.Xml.Linq;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>逐层 dotnet test 子进程 + trx 解析。RUNDIR 注入 runDir\&lt;layer&gt;\（方案修正 #1）。</summary>
public static class TestRunner
{
    public sealed class LayerRun
    {
        public List<TestEntry> Tests { get; } = new();

        public bool TimedOut { get; set; }

        /// <summary>过滤器零匹配（用例不存在/被条件编译剔除）——不算 PASS。</summary>
        public bool NoMatch { get; set; }

        /// <summary>dotnet test 非 0 退出且没有 trx：编译失败或宿主没起来。不能当成 0 断言 PASS。</summary>
        public bool HostFailed { get; set; }

        public string? Error { get; set; }
    }

    /// <summary>How long to keep draining output after `dotnet test` itself exited.</summary>
    public static TimeSpan PipeDrainTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Bounded read of redirected output; never waits on a pipe a leftover descendant holds.</summary>
    public static string StreamText(Task<string> stdout, Task<string> stderr, TimeSpan timeout)
    {
        try
        {
            Task.WaitAll(new Task[] { stdout, stderr }, timeout);
        }
        catch (AggregateException)
        {
            // Read faults are reported per stream below.
        }

        static string Read(Task<string> stream, string name) => stream.IsCompletedSuccessfully
            ? stream.Result
            : $"[{name} incomplete: dotnet test exited but a descendant process still holds the pipe]";
        return Read(stdout, "stdout") + Environment.NewLine + Read(stderr, "stderr");
    }

    public static LayerRun RunLayer(string autoTestRoot, string layerName, LayerConfig layer,
        string layerDir, string trxDir, string? vegaExe = null)
    {
        Directory.CreateDirectory(layerDir);
        Directory.CreateDirectory(trxDir);

        var outcome = new LayerRun();
        var project = Path.GetFullPath(Path.Combine(autoTestRoot, layer.Project));
        var trxName = $"{layerName}.trx";

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("test");
        psi.ArgumentList.Add(project);
        psi.ArgumentList.Add("--filter");
        psi.ArgumentList.Add(layer.Filter);
        psi.ArgumentList.Add("--logger");
        psi.ArgumentList.Add($"trx;LogFileName={trxName}");
        psi.ArgumentList.Add("--results-directory");
        psi.ArgumentList.Add(trxDir);
        // Isolate both compilation and runtime copies, including project
        // references. --results-directory alone isolates only TRX/evidence.
        psi.ArgumentList.Add("--artifacts-path");
        // Nested campaign/SHA/run/layer evidence paths exceed MAX_PATH in
        // MSBuild copy tasks even when the compiler successfully writes files.
        var artifactId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(layerDir))))[..24];
        var artifactRoot = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_ARTIFACTS_ROOT")
            ?? Path.Combine(Path.GetTempPath(), "vega-test-artifacts");
        var artifacts = Path.Combine(artifactRoot, artifactId);
        psi.ArgumentList.Add(artifacts);
        File.WriteAllText(Path.Combine(layerDir, "test-artifacts.json"),
            System.Text.Json.JsonSerializer.Serialize(new { artifacts, project, layerName }));
        psi.ArgumentList.Add("-p:UseSharedCompilation=false");
        // 关键：VEGA_AUTOTEST_RUNDIR 指向 layer 子目录，assert-*.json 与证据互不覆盖；
        // VEGA_AUTOTEST_EXE/OUTDIR：外部仓库不再嵌在 Vega 主仓库里，向上找 Vega.sln
        // 必然失败，由 Runner 把已定位的 exe/证据目录显式下传（RunContext/VegaEnv 优先读环境变量）
        psi.Environment["VEGA_AUTOTEST_RUNDIR"] = layerDir;
        if (!string.IsNullOrWhiteSpace(vegaExe))
        {
            psi.Environment["VEGA_AUTOTEST_EXE"] = vegaExe;
            psi.Environment["VEGA_AUTOTEST_OUTDIR"] = layerDir;
        }
        psi.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
        // MSBuild worker nodes outlive `dotnet test` and inherit its redirected
        // stdout. A reused node then keeps the pipe open after the test exited.
        psi.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using var process = Process.Start(psi);
        if (process is null)
        {
            outcome.Error = "failed to start dotnet test";
            return outcome;
        }

        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(layer.TimeoutSeconds * 1000))
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // 进程可能已退出
            }

            outcome.TimedOut = true;
            outcome.Error = $"layer '{layerName}' timed out after {layer.TimeoutSeconds}s";
            // Timeouts are the cases where the partial host log matters most.
            // Drain killed child pipes with a bounded grace period before disposing.
            try
            {
                process.WaitForExit(10000);
                Task.WaitAll(new Task[] { stdout, stderr }, 10000);
                File.WriteAllText(Path.Combine(trxDir, $"{layerName}.testhost.log"),
                    (stdout.IsCompletedSuccessfully ? stdout.Result : "[stdout incomplete]")
                    + Environment.NewLine + (stderr.IsCompletedSuccessfully ? stderr.Result : "[stderr incomplete]")
                    + Environment.NewLine + outcome.Error);
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(trxDir, $"{layerName}.testhost.log"), outcome.Error + Environment.NewLine + error.GetType().Name);
            }
            return outcome;
        }

        // `dotnet test` has exited, but a descendant it started (MSBuild node,
        // a tool launched by the test such as the Authenticator) may still hold
        // the redirected pipe. Reading to EOF would then block forever and the
        // Runner would never write result.json (2026-11-03/05 Vega100-PIC hang).
        var logText = StreamText(stdout, stderr, PipeDrainTimeout);
        File.WriteAllText(Path.Combine(trxDir, $"{layerName}.testhost.log"), logText);

        var trxPath = Path.Combine(trxDir, trxName);
        if (File.Exists(trxPath))
        {
            outcome.Tests.AddRange(ParseTrx(trxPath));
            // 零匹配防线：dotnet test 对"过滤器没命中任何用例"退出 0，必须显式识破
            // （spike 未设 VEGA_SW_ROOT 时探针被条件编译剔除即此情形）
            if (outcome.Tests.Count == 0)
            {
                outcome.NoMatch = true;
                outcome.Error = $"filter matched no test: '{layer.Filter}'";
            }
        }
        else if (process.ExitCode != 0)
        {
            // 编译失败 / 宿主没起来时没有 trx。logic 层 minAssertions=0，
            // 若这里只记 Error、判定器不看它，空跑会被判成 PASS。
            outcome.HostFailed = true;
            outcome.Error = Truncate(logText, 4000);
        }
        else
        {
            // trx 都没产出（runner 提前崩等），也按零匹配处理，杜绝空跑算 PASS
            outcome.NoMatch = true;
            outcome.Error = $"no trx produced; filter: '{layer.Filter}'";
        }

        return outcome;
    }

    /// <summary>XDocument 解析 trx：用例名 / outcome / duration / failure message。</summary>
    private static List<TestEntry> ParseTrx(string trxPath)
    {
        var entries = new List<TestEntry>();
        XDocument doc;
        try
        {
            doc = XDocument.Load(trxPath);
        }
        catch
        {
            return entries;
        }

        var ns = doc.Root?.Name.Namespace ?? throw new InvalidOperationException("trx root missing");
        var units = doc.Descendants(ns + "UnitTestResult");

        foreach (var unit in units)
        {
            var entry = new TestEntry
            {
                Name = (string?)unit.Attribute("testName") ?? string.Empty,
                Outcome = (string?)unit.Attribute("outcome") ?? string.Empty,
            };

            if (TimeSpan.TryParse((string?)unit.Attribute("duration"), out var duration))
            {
                entry.DurationMs = (long)duration.TotalMilliseconds;
            }

            var message = unit.Descendants(ns + "Message").FirstOrDefault()?.Value;
            var stackTrace = unit.Descendants(ns + "StackTrace").FirstOrDefault()?.Value;
            if (!string.IsNullOrWhiteSpace(message))
            {
                entry.Message = Truncate(message!.Trim(), 2000)
                                + (string.IsNullOrWhiteSpace(stackTrace)
                                    ? string.Empty
                                    : Environment.NewLine + Truncate(stackTrace.Trim(), 1000));
            }

            entries.Add(entry);
        }

        return entries;
    }

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + $"...(truncated, {text.Length} chars)";
}
