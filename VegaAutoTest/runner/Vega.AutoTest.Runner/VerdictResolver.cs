// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>归因后的单层判定输入。</summary>
public sealed class LayerVerdict
{
    public string Verdict { get; init; } = "PASS";

    public string Code { get; init; } = "OK";

    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// 判定矩阵唯一出处（外挂化技术方案 §3.4 + 修正 #3：minAssertions 按 layer 校验）。
/// 不得散落到任何提示词。
/// </summary>
public static class VerdictResolver
{
    public static LayerVerdict ResolveLayer(LayerConfig layer, TestRunner.LayerRun run,
        IReadOnlyList<AssertionRecord> assertions, bool vegaExeExists, string layerName)
    {
        if (run.TimedOut)
        {
            return new LayerVerdict { Verdict = "BLOCKED", Code = "TIMEOUT",
                Reason = $"layer '{layerName}' 超过 {layer.TimeoutSeconds}s" };
        }

        if (run.HostFailed)
        {
            return new LayerVerdict { Verdict = "ERROR", Code = "TEST_HOST_FAILED",
                Reason = $"测试工程未能启动（编译失败或宿主退出且没有 trx）。{FirstErrorLine(run.Error)}" };
        }

        if (run.NoMatch)
        {
            return new LayerVerdict { Verdict = "BLOCKED", Code = "NO_TEST_MATCHED",
                Reason = $"过滤器零匹配（用例未登记/被条件编译剔除）: {layer.Filter}" };
        }

        if (layer.RequiresVegaExe && !vegaExeExists)
        {
            return new LayerVerdict { Verdict = "BLOCKED", Code = "VEGA_EXE_NOT_FOUND",
                Reason = "被测二进制不存在" };
        }

        if (layer.RequiresL1Api)
        {
            var l1Consumed = assertions.Any(a =>
                (a.Kind is "l1-query" or "oracle-crosscheck") && a.Result != "SKIP");
            if (!l1Consumed)
            {
                return new LayerVerdict { Verdict = "BLOCKED", Code = "L1_UNAVAILABLE_REQUIRED",
                    Reason = "L1 API 未实际消费（无非 SKIP 的 l1-query/oracle-crosscheck）且该层必需" };
            }
        }

        // trx failure message 关键字归因：主窗口超时 / 设备永不空闲属环境问题，不算 FAIL
        var (mainWindowTimeout, deviceNeverIdle, harnessFailures) = ClassifyFailures(run.Tests, assertions);
        if (harnessFailures > 0)
        {
            return new LayerVerdict { Verdict = "ERROR", Code = "HARNESS_EXCEPTION",
                Reason = $"{harnessFailures} 个用例异常（非断言失败）" };
        }

        if (mainWindowTimeout)
        {
            return new LayerVerdict { Verdict = "BLOCKED", Code = "MAIN_WINDOW_TIMEOUT",
                Reason = "主窗口未就绪" };
        }

        if (deviceNeverIdle)
        {
            return new LayerVerdict { Verdict = "BLOCKED", Code = "DEVICE_NEVER_IDLE",
                Reason = "设备状态始终非空闲" };
        }

        var prerequisiteTests = run.Tests.Where(t => t.Outcome == "Failed"
            && PrerequisiteCode(t.Message) is not null).ToArray();
        var failed = assertions.Count(a => a.Result == "FAIL"
            && !(prerequisiteTests.Length > 0 && PrerequisiteCode(a.Actual) is not null));
        if (failed > 0)
        {
            return new LayerVerdict { Verdict = "FAIL", Code = "ASSERTION_FAILED",
                Reason = $"{failed} 条断言失败" };
        }

        var failedTests = run.Tests.Count(t => t.Outcome == "Failed") - prerequisiteTests.Length;
        if (failedTests > 0)
        {
            return new LayerVerdict { Verdict = "FAIL", Code = "ASSERTION_FAILED",
                Reason = $"{failedTests} 个用例失败" };
        }

        if (prerequisiteTests.Length > 0)
            return new LayerVerdict { Verdict = "BLOCKED",
                Code = PrerequisiteCode(prerequisiteTests[0].Message)!,
                Reason = prerequisiteTests[0].Message ?? "UI prerequisite unavailable" };

        // 按本层 minAssertions 校验（修正 #3）：跑完但没命中足够断言 = 没真正到目标界面
        var matched = assertions.Count(a => a.Result == "PASS");
        if (matched < layer.MinAssertions)
        {
            return new LayerVerdict { Verdict = "BLOCKED",
                Code = matched == 0 ? "ZERO_ASSERTIONS_MATCHED" : "ASSERTION_COUNT_BELOW_MINIMUM",
                Reason = $"仅命中 {matched}/{layer.MinAssertions} 条通过断言；需核对用例覆盖和登记门槛，不能据此推断业务通过或界面是否到达" };
        }

        return new LayerVerdict { Verdict = "PASS", Code = "OK",
            Reason = $"{matched}/{assertions.Count} 断言通过" };
    }

    private static string FirstErrorLine(string? log)
    {
        if (string.IsNullOrWhiteSpace(log))
        {
            return string.Empty;
        }

        foreach (var line in log.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Contains("error ", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains(": error", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Length <= 300 ? trimmed : trimmed[..300];
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// 当前前置异常，以及归档里 WaitReady 用 Assert.Fail 打出的原文。
    /// 只认失败消息的第一行，避免业务断言引用这句时被改判。
    /// </summary>
    private static string? PrerequisiteCode(string? message)
        => UiPrerequisiteException.ReadCode(message) ?? LegacyReadinessCode(message);

    private static string? LegacyReadinessCode(string? message)
    {
        var first = (message ?? "").Split('\n', 2)[0].Trim();
        const string marker = "登录/就绪超时，最后状态 '";
        var index = first.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return null;
        var prefix = first[..index];
        if (prefix.Length != 0 && prefix != "Xunit.Sdk.FailException: ")
            return null;
        var start = index + marker.Length;
        var end = first.IndexOf('\'', start);
        if (end < 0 || end != first.Length - 1)
            return null;
        return first[start..end] switch
        {
            "忙碌" or "初始化" or "启动中" or "错误" or "锁定" => "DEVICE_NEVER_IDLE",
            "空闲" => "UI_NOT_READY",
            _ => "LOGIN_NOT_READY",
        };
    }

    /// <summary>
    /// 从 trx failure message/stacktrace 归因。主窗口超时 / 设备永不空闲属环境问题，不算 FAIL。
    /// 用例自身硬断言（Assert.True/Fail）的堆栈首帧指向 L2 用例文件——有帧即断言失败，
    /// 不是 harness 异常；真正的 harness 异常（VegaApp 启动崩溃等）堆栈指向 harness 程序集。
    /// </summary>
    private static (bool MainWindowTimeout, bool DeviceNeverIdle, int HarnessFailures) ClassifyFailures(
        IReadOnlyList<TestEntry> tests, IReadOnlyList<AssertionRecord> assertions)
    {
        var mainWindowTimeout = false;
        var deviceNeverIdle = false;
        var harnessFailures = 0;

        foreach (var test in tests.Where(t => t.Outcome == "Failed"))
        {
            var message = test.Message ?? string.Empty;
            // TRX retains the top-level assertion text but does not expose the
            // exception type separately. Do not mistake a logic assertion for a
            // harness exception, or an assertion mentioning 'idle' for a timeout.
            var firstLine = message.Split('\n', 2)[0].Trim();
            if (PrerequisiteCode(firstLine) is not null)
                continue;
            if (firstLine.StartsWith("Assert.", StringComparison.Ordinal)
                || firstLine.StartsWith("Xunit.Sdk.", StringComparison.Ordinal))
            {
                continue;
            }
            // Assert.True(customMessage) loses its type in TRX. Accept typed
            // recorder evidence only when both message AND test stack match.
            // An arbitrary FAIL record or a wrapped initialization error is not
            // sufficient to reclassify a harness exception.
            if (!string.IsNullOrEmpty(test.Name) && assertions.Any(a =>
                a.Result == "FAIL" && a.Actual is { } actual
                && actual.Split('\n', 2)[0].Trim() is { } recorded
                && recorded.StartsWith("Xunit.Sdk.", StringComparison.Ordinal)
                && recorded.EndsWith("Exception: " + firstLine, StringComparison.Ordinal)
                && actual.Contains("at " + test.Name + "(", StringComparison.Ordinal)))
            {
                continue;
            }
            if (message.Contains("主窗口") || message.Contains("MainWindow", StringComparison.OrdinalIgnoreCase))
            {
                mainWindowTimeout = true;
            }
            else if (message.Contains("空闲") || message.Contains("idle", StringComparison.OrdinalIgnoreCase))
            {
                deviceNeverIdle = true;
            }
            else if (message.Contains(".L2.", StringComparison.OrdinalIgnoreCase))
            {
                // 堆栈/消息含 L2 用例命名空间帧 = 用例自身断言，普通 FAIL
            }
            else
            {
                harnessFailures++;
            }
        }

        return (mainWindowTimeout, deviceNeverIdle, harnessFailures);
    }
}
