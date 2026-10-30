// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 结构化断言记录器：与 EvidenceLog 并行——EvidenceLog 给人看，本类给 Runner 解析。
/// 落盘到 RunContext.Dir 下的 assert-{n:3d}.json（静态序号）：
/// 同进程多个 xUnit 用例各自 Flush 时文件名不冲突（方案修正 #2），
/// Runner 侧按 assert-*.json glob 聚合。
/// </summary>
public sealed class AssertionRecorder
{
    private static int s_sequence;

    private readonly List<AssertionRecord> _records = new();
    private readonly object _gate = new();
    private readonly string? _directory;
    private string? _path;

    public AssertionRecorder() : this(null) { }
    public AssertionRecorder(string? directory) => _directory = directory;

    public void Record(string id, string kind, string description,
                       string expected, string actual,
                       double? delta, double? tolerance, string result)
    {
        lock (_gate)
            _records.Add(new AssertionRecord(id, kind, description,
                expected, actual, delta, tolerance, result));
    }

    public void Pass(string id, string kind, string desc, string expected, string actual,
                     double? delta = null, double? tol = null)
        => Record(id, kind, desc, expected, actual, delta, tol, "PASS");

    public void Fail(string id, string kind, string desc, string expected, string actual,
                     double? delta = null, double? tol = null)
        => Record(id, kind, desc, expected, actual, delta, tol, "FAIL");

    public void Skip(string id, string kind, string desc, string reason)
        => Record(id, kind, desc, reason, "(skipped)", null, null, "SKIP");

    public int Count { get { lock (_gate) return _records.Count; } }

    /// <summary>同一记录器始终原子更新同一文件；finally/Dispose 重复调用不会重复计数。</summary>
    public string Flush()
    {
        lock (_gate)
        {
            var directory = _directory ?? RunContext.Dir;
            Directory.CreateDirectory(directory);
            _path ??= Path.Combine(directory, $"assert-{Interlocked.Increment(ref s_sequence):000}.json");
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(_records,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
            return _path;
        }
    }
}

public sealed record AssertionRecord(
    string Id, string Kind, string Description,
    string Expected, string Actual,
    double? Delta, double? Tolerance, string Result);
