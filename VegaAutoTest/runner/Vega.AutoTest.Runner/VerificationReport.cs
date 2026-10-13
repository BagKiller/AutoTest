// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text;

namespace Vega.AutoTest.Runner;

/// <summary>
/// 把 result.json 转写成同目录 report.md。只复述 Runner 已落盘的判定，不另作结论。
/// </summary>
public static class VerificationReport
{
    public static string Write(string runDir, RunResult result)
    {
        var path = Path.Combine(runDir, "report.md");
        Directory.CreateDirectory(runDir);
        File.WriteAllText(path, Render(result), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string Render(RunResult result)
    {
        var sb = new StringBuilder();
        var target = result.Target;
        sb.AppendLine($"# 工作项 #{result.WorkItemId} 验证报告");
        sb.AppendLine();
        sb.AppendLine($"- 结论：{VerdictLabel(result.Verdict)}（{result.Verdict} / {result.VerdictCode}）");
        if (!string.IsNullOrWhiteSpace(result.VerdictReason))
        {
            sb.AppendLine($"- 说明：{OneLine(result.VerdictReason)}");
        }

        sb.AppendLine($"- runId：`{result.RunId}`");
        if (!string.IsNullOrWhiteSpace(result.CaseId))
        {
            sb.AppendLine($"- 用例：{result.CaseId}");
        }

        var summary = result.Summary;
        sb.AppendLine($"- 断言：{summary.Passed} 过 {summary.Total}（失败 {summary.Failed}，跳过 {summary.Skipped}）");
        sb.AppendLine($"- 被测：commit `{ShortCommit(target.GitCommit)}`，分支 {target.GitBranch ?? "未知"}，{BinaryNote(target)}");
        if (!string.IsNullOrWhiteSpace(target.VegaExeBuiltAt))
        {
            sb.AppendLine($"- 二进制构建时间：{target.VegaExeBuiltAt}");
        }

        sb.AppendLine($"- 起止：{result.StartedAt} → {result.FinishedAt}（{FormatDuration(result.DurationMs)}）");

        var failedTests = result.Tests.Where(t => !string.Equals(t.Outcome, "Passed", StringComparison.OrdinalIgnoreCase)).ToList();
        if (failedTests.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## 失败用例");
            sb.AppendLine();
            foreach (var test in failedTests)
            {
                var reason = OneLine(test.Message);
                sb.AppendLine(string.IsNullOrEmpty(reason)
                    ? $"- `{test.Name}`（{test.Outcome}）"
                    : $"- `{test.Name}`（{test.Outcome}）：{reason}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 断言");
        sb.AppendLine();
        if (result.Assertions.Count == 0)
        {
            sb.AppendLine("无结构化断言。");
        }
        else
        {
            sb.AppendLine("| 结果 | 编号 | 说明 | 期望 | 实际 |");
            sb.AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var assertion in result.Assertions)
            {
                sb.AppendLine($"| {Cell(assertion.Result)} | {Cell(assertion.Id)} | {Cell(assertion.Description)} | {Cell(assertion.Expected)} | {Cell(assertion.Actual)} |");
            }
        }

        if (!string.IsNullOrWhiteSpace(result.Evidence.Dir))
        {
            sb.AppendLine();
            sb.AppendLine("## 取证");
            sb.AppendLine();
            sb.AppendLine($"`{result.Evidence.Dir}`");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    private static string VerdictLabel(string verdict) => verdict switch
    {
        "PASS" => "已验证通过",
        "FAIL" => "未通过",
        "BLOCKED" => "无法验证",
        _ => "验证工具异常",
    };

    private static string BinaryNote(TargetInfo target)
    {
        var dirty = target.GitDirty ? "，工作区有未提交改动" : "";
        return target.BuiltFromCurrentSource
            ? $"已用当前源码重编（builtFromCurrentSource=true）{dirty}"
            : $"未重编（builtFromCurrentSource=false，结论仅对旧二进制有效）{dirty}";
    }

    private static string ShortCommit(string? commit)
    {
        if (string.IsNullOrWhiteSpace(commit))
        {
            return "未知";
        }

        return commit.Length <= 8 ? commit : commit[..8];
    }

    private static string FormatDuration(long durationMs)
    {
        if (durationMs < 1000)
        {
            return $"{durationMs} 毫秒";
        }

        return $"{durationMs / 1000.0:0.0} 秒";
    }

    private static string OneLine(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var line = text.Replace("\r\n", "\n").Split('\n', 2)[0].Trim();
        return line;
    }

    private static string Cell(string? text)
        => OneLine(text).Replace("|", "\\|");
}
