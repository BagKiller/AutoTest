// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Newtonsoft.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>
/// Runner CLI：verify / doctor / list-cases。手写参数解析，不加包。
/// 退出码：PASS=0 FAIL=1 BLOCKED=2 ERROR=3。stdout 末行打 result.json 绝对路径。
/// </summary>
public static class Program
{
    private const int ExitPass = 0;
    private const int ExitFail = 1;
    private const int ExitBlocked = 2;
    private const int ExitError = 3;

    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return ExitError;
            }

            return args[0] switch
            {
                "verify" => RunVerify(args[1..]),
                "doctor" => RunDoctor(args[1..]),
                "list-cases" => RunListCases(args[1..]),
                "prune" => RunPrune(args[1..]),
                "--help" or "-h" => PrintUsage(),
                _ => UnknownCommand(args[0]),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"runner fatal: {ex}");
            return ExitError;
        }
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"unknown command: {command}");
        PrintUsage();
        return ExitError;
    }

    private static int PrintUsage()
    {
        Console.WriteLine("""
            Vega.AutoTest.Runner — 确定性验证 CLI（无 LLM 判定）

            用法:
              verify --work-item <id> [--layers logic,uia] [--rebuild] [--configuration Debug]
                     [--repo-root <path>] [--runs-dir <path>] [--case <id>]
              doctor
              list-cases [--json]
              prune [--runs-dir <path>] [--keep 50] [--dry-run]

            退出码: PASS=0 FAIL=1 BLOCKED=2 ERROR=3
            """);
        return ExitError;
    }

    private static Dictionary<string, string?> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string?>();
        for (var i = 0; i < args.Length; i++)
        {
            var key = args[i];
            if (!key.StartsWith("--"))
            {
                Console.Error.WriteLine($"unexpected token: {key}");
                Environment.Exit(ExitError);
            }

            if (key is "--rebuild" or "--json" or "--dry-run")
            {
                options[key] = "true";
                continue;
            }

            if (i + 1 >= args.Length)
            {
                Console.Error.WriteLine($"missing value for {key}");
                Environment.Exit(ExitError);
            }

            options[key] = args[++i];
        }

        return options;
    }

    // ── 定位 VegaAutoTest 根（本 exe 位于 VegaAutoTest/runner/Vega.AutoTest.Runner[/.../bin]）──
    private static string FindAutoTestRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Vega.AutoTest.sln");
            if (File.Exists(candidate))
            {
                return dir.FullName;
            }

            dir = dir.Parent!;
        }

        throw new InvalidOperationException(
            $"从 {AppContext.BaseDirectory} 向上未找到 Vega.AutoTest.sln，无法定位 VegaAutoTest 根");
    }

    // ── locate cases 目录等 ──
    private static string DefaultRunsDir(string autoTestRoot) => Path.Combine(autoTestRoot, "runs");

    // ────────────────────────────── verify ──────────────────────────────

    private static int RunVerify(string[] args)
    {
        var options = ParseOptions(args);
        var startedAt = DateTime.Now;

        var caseArg = options.GetValueOrDefault("--case");
        var hasWorkItem = int.TryParse(options.GetValueOrDefault("--work-item"), out var workItemId)
                          && workItemId > 0;
        if (!hasWorkItem && caseArg is null)
        {
            Console.Error.WriteLine("verify 需要 --work-item <id> 或 --case <id>");
            return ExitError;
        }

        var autoTestRoot = FindAutoTestRoot();
        // 主仓库根：显式参数 > VEGA_SW_ROOT（与 spike 工程同一约定）> autoTestRoot 的父目录（历史同仓布局）
        var repoRoot = options.GetValueOrDefault("--repo-root")
                       ?? Environment.GetEnvironmentVariable("VEGA_SW_ROOT")
                       ?? new DirectoryInfo(autoTestRoot).Parent!.FullName;
        var configuration = options.GetValueOrDefault("--configuration") ?? "Debug";
        var runsDir = options.GetValueOrDefault("--runs-dir") ?? DefaultRunsDir(autoTestRoot);
        var rebuild = options.ContainsKey("--rebuild");

        var probe = TargetProbe.Probe(repoRoot, configuration);
        var registry = CaseRegistry.Load(autoTestRoot);
        var testCase = caseArg is not null
            ? registry.Cases.FirstOrDefault(c => c.Id == caseArg)
            : CaseRegistry.FindByWorkItem(registry, workItemId, probe.GitCommit);
        if (testCase is null)
        {
            if (caseArg is not null)
            {
                Console.Error.WriteLine($"--case '{caseArg}' 未在 registry.json 中登记");
                return ExitError;
            }

            // 无登记用例：诚实地说"没有用例"，直接 BLOCKED 退出
            var runId = NewRunId(workItemId);
            var runDir = Path.Combine(runsDir, runId);
            Directory.CreateDirectory(runDir);
            var result = new RunResult
            {
                RunId = runId,
                WorkItemId = workItemId,
                Verdict = "BLOCKED",
                VerdictCode = "NO_CASE_REGISTERED",
                VerdictReason = $"工作项 #{workItemId} 未登记适用于当前提交的自动化用例（cases/registry.json）；先准备并检查真实测试",
                StartedAt = Stamp(startedAt),
                FinishedAt = Stamp(DateTime.Now),
                Target = ToTarget(TargetProbe.Probe(repoRoot, configuration), rebuilt: false,
                    builtFromCurrentSource: false),
                ExitCode = ExitBlocked,
            };
            result.Evidence.Dir = runDir;
            MarkChannelsNotRun(result.Channels, "未执行测试层（该工作项无登记用例）");
            return Finish(result, runDir, startedAt);
        }

        if (caseArg is not null && hasWorkItem && !testCase.WorkItems.Contains(workItemId))
        {
            Console.Error.WriteLine($"用例 {testCase.Id} 未关联工作项 #{workItemId}（registry.json workItems: " +
                                    $"{string.Join(',', testCase.WorkItems)}）");
            return ExitError;
        }

        // 未指定 --layers 时跑该用例登记的全部层（勿写死 logic,uia，否则纯 l1 用例会被滤空）
        var layersArg = options.GetValueOrDefault("--layers") ?? string.Join(',', testCase.Layers.Keys);
        var requestedLayers = layersArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => testCase.Layers.ContainsKey(l))
            .ToList();
        if (requestedLayers.Count == 0)
        {
            Console.Error.WriteLine($"--layers '{layersArg}' 与用例 {testCase.Id} 的层不匹配: " +
                                    string.Join(',', testCase.Layers.Keys));
            return ExitError;
        }

        var runId2 = NewRunId(workItemId);
        var runDir2 = Path.Combine(runsDir, runId2);
        Directory.CreateDirectory(runDir2);

        // ── TargetProbe + 可选重建 ──
        var builtFromCurrentSource = false;
        string? buildNote = null;
        if (!CaseRegistry.MatchesTarget(testCase, probe.GitCommit))
        {
            var mismatch = new RunResult
            {
                RunId = runId2, WorkItemId = workItemId, CaseId = testCase.Id,
                Verdict = "BLOCKED", VerdictCode = "CASE_TARGET_MISMATCH",
                VerdictReason = $"用例仅适用于提交 {testCase.TargetSha}；当前提交 {probe.GitCommit ?? "unknown"}。请准备当前提交的真实用例。",
                StartedAt = Stamp(startedAt), FinishedAt = Stamp(DateTime.Now),
                Target = ToTarget(probe, builtFromCurrentSource: false), ExitCode = ExitBlocked,
            };
            mismatch.Evidence.Dir = runDir2;
            MarkChannelsNotRun(mismatch.Channels, "未执行测试层（用例目标提交不匹配）");
            return Finish(mismatch, runDir2, startedAt);
        }
        if (rebuild)
        {
            var buildLog = Path.Combine(runDir2, "build.log");
            Console.WriteLine($"[rebuild] 调用 {BuildStep.ScriptRelativePath} -NoLaunch ...");
            var outcome = BuildStep.Run(repoRoot, configuration, buildLog);
            probe = TargetProbe.Probe(repoRoot, configuration); // 时间戳已变，重探
            if (outcome.Succeeded)
            {
                builtFromCurrentSource = true;
            }
            else if (probe.VegaExeExists)
            {
                buildNote = "build failed; falling back to stale exe";
            }
            else
            {
                var failed = new RunResult
                {
                    RunId = runId2,
                    WorkItemId = workItemId,
                    CaseId = testCase.Id,
                    Verdict = "ERROR",
                    VerdictCode = "BUILD_FAILED_NO_FALLBACK",
                    VerdictReason = "重编失败且无可用旧二进制",
                    StartedAt = Stamp(startedAt),
                    FinishedAt = Stamp(DateTime.Now),
                    Target = ToTarget(probe, rebuilt: true, builtFromCurrentSource: false),
                    ExitCode = ExitError,
                };
                failed.Evidence.Dir = runDir2;
                failed.Evidence.Files.Add("build.log");
                MarkChannelsNotRun(failed.Channels, "未执行测试层（重编失败且无可用旧二进制）");
                return Finish(failed, runDir2, startedAt);
            }
        }
        else
        {
            // 契约 §2.4：未重编 = 旧二进制，builtFromCurrentSource 必须 false
            // （exe 存在只说明"有东西可测"，与"它等于当前源码"无关）
            builtFromCurrentSource = false;
        }

        var staleUsed = rebuild && !builtFromCurrentSource;

        // ── uia 层前置：Vega.exe 存在性（快速 BLOCKED，不浪费 15 分钟）──
        var needsExe = requestedLayers.Any(l => testCase.Layers[l].RequiresVegaExe);
        if (needsExe && !probe.VegaExeExists)
        {
            var blocked = new RunResult
            {
                RunId = runId2,
                WorkItemId = workItemId,
                CaseId = testCase.Id,
                Verdict = "BLOCKED",
                VerdictCode = "VEGA_EXE_NOT_FOUND",
                VerdictReason = $"被测二进制不存在: {probe.VegaExe}",
                StartedAt = Stamp(startedAt),
                FinishedAt = Stamp(DateTime.Now),
                Target = ToTarget(probe, builtFromCurrentSource: false),
                ExitCode = ExitBlocked,
            };
            blocked.Evidence.Dir = runDir2;
            MarkChannelsNotRun(blocked.Channels, $"未执行测试层（被测二进制不存在: {probe.VegaExe}）");
            return Finish(blocked, runDir2, startedAt);
        }

        // ── 逐层执行 ──
        var allAssertions = new List<AssertionRecord>();
        var allTests = new List<TestEntry>();
        var layerVerdicts = new List<LayerVerdict>();
        var evidenceFiles = new List<string>();
        string? assertionChannelError = null;

        foreach (var layerName in requestedLayers)
        {
            var layer = testCase.Layers[layerName];
            var layerDir = Path.Combine(runDir2, layerName);
            var trxDir = Path.Combine(runDir2, layerName, "trx");

            Console.WriteLine($"[layer:{layerName}] dotnet test --filter \"{layer.Filter}\"");
            var run = TestRunner.RunLayer(autoTestRoot, layerName, layer, layerDir, trxDir,
                probe.VegaExeExists ? probe.VegaExe : null);
            allTests.AddRange(run.Tests);

            // 聚合该层 assert-*.json（修正 #2：多用例多文件）
            var layerAssertions = LoadAssertions(layerDir);
            allAssertions.AddRange(layerAssertions);

            // assert 缺失但用例通过 → 结构化断言通道故障（记入 channels 供 L0 观察）
            if (layerAssertions.Count == 0 && run.Tests.Any(t => t.Outcome == "Passed"))
            {
                assertionChannelError ??= $"layer '{layerName}' 用例通过但未产出 assert-*.json";
            }

            var verdict = VerdictResolver.ResolveLayer(layer, run, layerAssertions, probe.VegaExeExists, layerName);
            layerVerdicts.Add(verdict);
            Console.WriteLine($"[layer:{layerName}] {verdict.Verdict} ({verdict.Code}) {verdict.Reason}");

            evidenceFiles.Add($"{layerName}/trx/{layerName}.trx");
            if (File.Exists(Path.Combine(runDir2, layerName, "trx", $"{layerName}.testhost.log")))
            {
                evidenceFiles.Add($"{layerName}/trx/{layerName}.testhost.log");
            }

            foreach (var evidenceFile in testCase.EvidenceFiles)
            {
                var candidate = Path.Combine(layerDir, evidenceFile);
                if (File.Exists(candidate))
                {
                    evidenceFiles.Add($"{layerName}/{evidenceFile}");
                }
            }

            foreach (var snap in new[] { UiSnapshot.JsonName, UiSnapshot.PngName })
            {
                if (File.Exists(Path.Combine(layerDir, snap)))
                {
                    evidenceFiles.Add($"{layerName}/{snap}");
                }
            }
        }

        // ── 汇总判定：任一层 BLOCKED → BLOCKED；任一 FAIL → FAIL；否则 PASS ──
        var (verdictText, verdictCode) = Aggregate(layerVerdicts);
        var reason = string.Join(" | ", layerVerdicts.Select(v => $"{v.Code}: {v.Reason}"));
        if (staleUsed)
        {
            reason += " | 构建失败，沿用旧二进制（结论降级）";
        }

        if (buildNote is not null)
        {
            reason += $" | {buildNote}";
        }

        var final = new RunResult
        {
            RunId = runId2,
            WorkItemId = workItemId,
            CaseId = testCase.Id,
            Verdict = verdictText,
            VerdictCode = staleUsed && verdictText == "PASS" ? "OK_STALE_BUILD" : verdictCode,
            VerdictReason = reason.Trim(),
            StartedAt = Stamp(startedAt),
            FinishedAt = Stamp(DateTime.Now),
            Target = ToTarget(probe, rebuilt: rebuild, builtFromCurrentSource: builtFromCurrentSource),
            Assertions = allAssertions,
            Tests = allTests,
            ExitCode = MapExit(verdictText),
        };
        // channels 契约 §2.5：三字段各答一问，不得复用——
        //   l1Api.available = 18901 oracle 通道是否被断言实际消费（有 oracle-crosscheck 且非全 SKIP）
        //   l2Uia.available = 本次是否执行了 uia 层
        //   assertions.available = 结构化断言通道是否产出（用例 Passed 但 assert 缺失 → false）
        var oracleConsumed = allAssertions.Any(a =>
            a.Kind is "oracle-crosscheck" or "l1-query" && a.Result != "SKIP");
        var oraclePresent = allAssertions.Any(a => a.Kind == "oracle-crosscheck");
        final.Channels.L1Api.BaseUrl = VegaEnv.AgentBaseUrl;
        final.Channels.L1Api.Available = oracleConsumed;
        final.Channels.L1Api.Reason = oracleConsumed ? null
            : oraclePresent ? "存在 oracle-crosscheck 断言但全部 SKIP（用例内 L1 未就绪）"
            : $"用例未产出 oracle-crosscheck 断言（本次层: {string.Join(',', requestedLayers)}）";
        final.Channels.L2Uia.Available = requestedLayers.Contains("uia");
        final.Channels.L2Uia.Reason = requestedLayers.Contains("uia") ? null : "本次未请求 uia 层";
        final.Channels.Assertions.Available = assertionChannelError is null;
        final.Channels.Assertions.Reason = assertionChannelError;
        final.Summary = new SummaryInfo
        {
            Total = allAssertions.Count,
            Passed = allAssertions.Count(a => a.Result == "PASS"),
            Failed = allAssertions.Count(a => a.Result == "FAIL"),
            Skipped = allAssertions.Count(a => a.Result == "SKIP"),
        };
        final.Evidence.Dir = runDir2;
        final.Evidence.Files = evidenceFiles;
        if (File.Exists(Path.Combine(runDir2, "build.log")))
        {
            final.Evidence.Files.Insert(0, "build.log");
        }

        return Finish(final, runDir2, startedAt);
    }

    /// <summary>
    /// 早退路径（无登记用例 / 无被测 exe / 重编失败）一层未跑，三条通道一律 false + 带原因。
    /// 契约 §2.5 要求 available=false 时必须可诊断——留空 reason 会被自检判违例。
    /// </summary>
    private static void MarkChannelsNotRun(ChannelsInfo channels, string reason)
    {
        channels.L1Api.BaseUrl = VegaEnv.AgentBaseUrl;
        channels.L1Api.Available = false;
        channels.L1Api.Reason = reason;
        channels.L2Uia.Available = false;
        channels.L2Uia.Reason = reason;
        channels.Assertions.Available = false;
        channels.Assertions.Reason = reason;
    }

    private static (string Verdict, string Code) Aggregate(List<LayerVerdict> verdicts)
    {
        if (verdicts.Any(v => v.Verdict == "ERROR"))
        {
            var error = verdicts.First(v => v.Verdict == "ERROR");
            return ("ERROR", error.Code);
        }

        if (verdicts.Any(v => v.Verdict == "BLOCKED"))
        {
            var blocked = verdicts.First(v => v.Verdict == "BLOCKED");
            return ("BLOCKED", blocked.Code);
        }

        if (verdicts.Any(v => v.Verdict == "FAIL"))
        {
            var failed = verdicts.First(v => v.Verdict == "FAIL");
            return ("FAIL", failed.Code);
        }

        return ("PASS", "OK");
    }

    private static List<AssertionRecord> LoadAssertions(string layerDir)
    {
        var records = new List<AssertionRecord>();
        if (!Directory.Exists(layerDir))
        {
            return records;
        }

        foreach (var file in Directory.EnumerateFiles(layerDir, "assert-*.json").OrderBy(f => f))
        {
            try
            {
                var parsed = JsonConvert.DeserializeObject<List<AssertionRecord>>(File.ReadAllText(file));
                if (parsed is not null)
                {
                    records.AddRange(parsed);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"warn: 解析 {file} 失败: {ex.Message}");
            }
        }

        return records;
    }

    private static TargetInfo ToTarget(TargetProbeResult probe, bool rebuilt = false, bool builtFromCurrentSource = false)
        => new()
        {
            RepoRoot = probe.RepoRoot,
            GitCommit = probe.GitCommit,
            GitBranch = probe.GitBranch,
            GitDirty = probe.GitDirty,
            VegaExe = probe.VegaExe,
            VegaExeBuiltAt = probe.VegaExeBuiltAt,
            Rebuilt = rebuilt,
            BuiltFromCurrentSource = builtFromCurrentSource,
        };

    private static int Finish(RunResult result, string runDir, DateTime startedAt)
    {
        // 契约 §2.1：run 级墙钟耗时，落盘前必须赋值（不得留 0），含 BLOCKED/ERROR 早退路径。
        // 必须用真实 DateTime 差值——StartedAt/FinishedAt 的 Stamp 精度只到秒，
        // 早退路径（NO_CASE_REGISTERED 等）在同一秒内完成，回解字符串会得到 0。
        result.DurationMs = Math.Max(1, (long)(DateTime.Now - startedAt).TotalMilliseconds);

        // 契约违例：fail-closed 成 ERROR，但**照常落盘**——L0 靠 result.json 取结构化原因，
        // 不落盘只会让"仪表坏了"退化成"工具没反应"。原判定保留在 reason 里供诊断。
        var violations = ResultContractCheck.Validate(result);
        if (violations.Count > 0)
        {
            result.VerdictReason = $"契约自检违例：{string.Join("; ", violations)}"
                                   + $" ｜ 原判定 {result.Verdict}/{result.VerdictCode}: {result.VerdictReason}";
            result.Verdict = "ERROR";
            result.VerdictCode = "CONTRACT_VIOLATION";
            result.ExitCode = ExitError;
        }

        // 可读报告与 result.json 同期落盘。写失败不改判定：stdout 末行仍是 result.json。
        try
        {
            if (!result.Evidence.Files.Contains("report.md"))
            {
                result.Evidence.Files.Add("report.md");
            }

            var reportPath = VerificationReport.Write(runDir, result);
            Console.WriteLine($"[report] {reportPath}");
        }
        catch (Exception ex)
        {
            result.Evidence.Files.Remove("report.md");
            Console.Error.WriteLine($"report.md 写入失败: {ex.Message}");
        }

        var resultPath = Path.Combine(runDir, "result.json");
        ResultWriter.Write(resultPath, result);
        Console.WriteLine(resultPath);
        return result.ExitCode;
    }

    private static string NewRunId(int workItemId)
    {
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
        var suffix = Guid.NewGuid().ToString("N")[..7];
        return $"{stamp}_wi{workItemId}_{suffix}";
    }

    private static string Stamp(DateTime time) => time.ToString("yyyy-MM-dd'T'HH:mm:sszzz");

    private static int MapExit(string verdict) => verdict switch
    {
        "PASS" => ExitPass,
        "FAIL" => ExitFail,
        "BLOCKED" => ExitBlocked,
        _ => ExitError,
    };

    // ────────────────────────────── doctor ──────────────────────────────

    private static int RunDoctor(string[] args)
    {
        var options = ParseOptions(args);
        var autoTestRoot = FindAutoTestRoot();
        var repoRoot = options.GetValueOrDefault("--repo-root")
                       ?? Environment.GetEnvironmentVariable("VEGA_SW_ROOT")
                       ?? new DirectoryInfo(autoTestRoot).Parent!.FullName;
        var configuration = options.GetValueOrDefault("--configuration") ?? "Debug";

        var checks = new List<(string Item, bool Ok, string Detail)>();

        var vegaExe = TargetProbe.DefaultVegaExePath(repoRoot, configuration);
        checks.Add(("vega.exe", File.Exists(vegaExe), vegaExe));

        var portFree = !System.Net.NetworkInformation.IPGlobalProperties
            .GetIPGlobalProperties().GetActiveTcpListeners()
            .Any(p => p.Port == 18901);
        checks.Add(("port 18901 idle (Vega not running)", portFree, portFree ? "idle" : "in use"));

        var buildScript = Path.Combine(repoRoot, BuildStep.ScriptRelativePath);
        checks.Add(("build script", File.Exists(buildScript), buildScript));

        var gitOk = TargetProbe.Probe(repoRoot, configuration).GitCommit is not null;
        checks.Add(("git repo", gitOk, repoRoot));

        var dotnetOk = RunExit0("dotnet", "--version");
        checks.Add(("dotnet CLI", dotnetOk, "dotnet --version"));

        foreach (var (item, ok, detail) in checks)
        {
            Console.WriteLine($"{(ok ? "[OK]  " : "[MISS] ")}{item}: {detail}");
        }

        return checks.All(c => c.Ok) ? ExitPass : ExitBlocked;
    }

    private static bool RunExit0(string fileName, string args)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName)
            {
                ArgumentList = { args },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                return false;
            }

            process.WaitForExit(30_000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // ──────────────────────────── list-cases ────────────────────────────

    private static int RunListCases(string[] args)
    {
        var options = ParseOptions(args);
        var autoTestRoot = FindAutoTestRoot();
        var registry = CaseRegistry.Load(autoTestRoot);

        if (options.ContainsKey("--json"))
        {
            Console.WriteLine(JsonConvert.SerializeObject(registry, Formatting.Indented));
            return ExitPass;
        }

        foreach (var testCase in registry.Cases)
        {
            Console.WriteLine($"{testCase.Id}: {testCase.Title}");
            Console.WriteLine($"  work items: {string.Join(',', testCase.WorkItems)}");
            foreach (var (layerName, layer) in testCase.Layers)
            {
                Console.WriteLine($"  layer {layerName}: {layer.Project} [{layer.Filter}] minAssertions={layer.MinAssertions}");
            }
        }

        return ExitPass;
    }

    // ────────────────────────────── prune ──────────────────────────────

    /// <summary>
    /// 方案 §10 D2：保留最近 N 次 + 全部 FAIL/ERROR，其余删除。
    /// 无 result.json 的空目录（自检误伤残留）一律清掉。跳过 <c>manual</c>。
    /// </summary>
    private static int RunPrune(string[] args)
    {
        var options = ParseOptions(args);
        var autoTestRoot = FindAutoTestRoot();
        var runsDir = options.GetValueOrDefault("--runs-dir") ?? DefaultRunsDir(autoTestRoot);
        var keep = 50;
        if (options.TryGetValue("--keep", out var keepArg) && int.TryParse(keepArg, out var parsed) && parsed > 0)
        {
            keep = parsed;
        }

        var dryRun = options.ContainsKey("--dry-run");
        if (!Directory.Exists(runsDir))
        {
            Console.Error.WriteLine($"runs 目录不存在: {runsDir}");
            return ExitError;
        }

        var entries = new List<(string Dir, DateTime Sort, string? Verdict, bool HasResult)>();
        foreach (var dir in Directory.GetDirectories(runsDir))
        {
            if (string.Equals(Path.GetFileName(dir), "manual", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resultPath = Path.Combine(dir, "result.json");
            if (!File.Exists(resultPath))
            {
                entries.Add((dir, Directory.GetCreationTimeUtc(dir), null, false));
                continue;
            }

            try
            {
                var json = JsonConvert.DeserializeObject<RunResult>(File.ReadAllText(resultPath));
                var stamp = DateTime.TryParse(json?.StartedAt, out var started)
                    ? started.ToUniversalTime()
                    : Directory.GetCreationTimeUtc(dir);
                entries.Add((dir, stamp, json?.Verdict, true));
            }
            catch
            {
                entries.Add((dir, Directory.GetCreationTimeUtc(dir), null, true));
            }
        }

        var keepSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries.Where(e => e.HasResult)
                     .OrderByDescending(e => e.Sort)
                     .Take(keep))
        {
            keepSet.Add(e.Dir);
        }

        foreach (var e in entries.Where(e =>
                     e.Verdict is "FAIL" or "ERROR"))
        {
            keepSet.Add(e.Dir);
        }

        var removed = 0;
        var kept = 0;
        foreach (var e in entries.OrderBy(e => e.Sort))
        {
            var name = Path.GetFileName(e.Dir);
            if (keepSet.Contains(e.Dir))
            {
                kept++;
                Console.WriteLine($"keep   {name}  verdict={e.Verdict ?? "(none)"}");
                continue;
            }

            Console.WriteLine($"{(dryRun ? "would " : "")}rm     {name}  verdict={e.Verdict ?? "(no result.json)"}");
            if (!dryRun)
            {
                Directory.Delete(e.Dir, recursive: true);
            }

            removed++;
        }

        Console.WriteLine(dryRun
            ? $"dry-run: 将删除 {removed}，保留 {kept}（最近 {keep} + FAIL/ERROR）"
            : $"已删除 {removed}，保留 {kept}（最近 {keep} + FAIL/ERROR）");
        return ExitPass;
    }
}
