// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Newtonsoft.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>runs/&lt;runId&gt;/result.json 契约载体（外挂化技术方案 §2.1）。</summary>
public sealed class RunResult
{
    [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = "1.0";

    [JsonProperty("runId")] public string RunId { get; set; } = string.Empty;

    [JsonProperty("workItemId")] public int WorkItemId { get; set; }

    [JsonProperty("caseId")] public string? CaseId { get; set; }

    [JsonProperty("verdict")] public string Verdict { get; set; } = "ERROR";

    [JsonProperty("verdictReason")] public string VerdictReason { get; set; } = string.Empty;

    [JsonProperty("verdictCode")] public string VerdictCode { get; set; } = "OK";

    [JsonProperty("startedAt")] public string StartedAt { get; set; } = string.Empty;

    [JsonProperty("finishedAt")] public string FinishedAt { get; set; } = string.Empty;

    [JsonProperty("durationMs")] public long DurationMs { get; set; }

    [JsonProperty("target")] public TargetInfo Target { get; set; } = new();

    [JsonProperty("channels")] public ChannelsInfo Channels { get; set; } = new();

    [JsonProperty("assertions")] public List<AssertionRecord> Assertions { get; set; } = new();

    [JsonProperty("summary")] public SummaryInfo Summary { get; set; } = new();

    [JsonProperty("tests")] public List<TestEntry> Tests { get; set; } = new();

    [JsonProperty("evidence")] public EvidenceInfo Evidence { get; set; } = new();

    [JsonProperty("exitCode")] public int ExitCode { get; set; }
}

public sealed class TargetInfo
{
    [JsonProperty("repoRoot")] public string RepoRoot { get; set; } = string.Empty;

    [JsonProperty("gitCommit")] public string? GitCommit { get; set; }

    [JsonProperty("gitBranch")] public string? GitBranch { get; set; }

    [JsonProperty("gitDirty")] public bool GitDirty { get; set; }

    [JsonProperty("vegaExe")] public string? VegaExe { get; set; }

    [JsonProperty("vegaExeBuiltAt")] public string? VegaExeBuiltAt { get; set; }

    [JsonProperty("rebuilt")] public bool Rebuilt { get; set; }

    [JsonProperty("builtFromCurrentSource")] public bool BuiltFromCurrentSource { get; set; }
}

public sealed class ChannelsInfo
{
    [JsonProperty("l1Api")] public ChannelInfo L1Api { get; set; } = new();

    [JsonProperty("l2Uia")] public ChannelInfo L2Uia { get; set; } = new();

    [JsonProperty("assertions")] public ChannelInfo Assertions { get; set; } = new();
}

public sealed class ChannelInfo
{
    [JsonProperty("available")] public bool Available { get; set; }

    [JsonProperty("baseUrl")] public string? BaseUrl { get; set; }

    [JsonProperty("reason")] public string? Reason { get; set; }
}

public sealed class SummaryInfo
{
    [JsonProperty("total")] public int Total { get; set; }

    [JsonProperty("passed")] public int Passed { get; set; }

    [JsonProperty("failed")] public int Failed { get; set; }

    [JsonProperty("skipped")] public int Skipped { get; set; }
}

public sealed class TestEntry
{
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;

    [JsonProperty("outcome")] public string Outcome { get; set; } = string.Empty;

    [JsonProperty("durationMs")] public long DurationMs { get; set; }

    [JsonProperty("message")] public string? Message { get; set; }
}

public sealed class EvidenceInfo
{
    [JsonProperty("dir")] public string Dir { get; set; } = string.Empty;

    [JsonProperty("files")] public List<string> Files { get; set; } = new();
}
