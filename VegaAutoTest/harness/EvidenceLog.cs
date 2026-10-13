// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 取证日志：Step 追加一行并同步打控制台；Flush 把全部内容写到 RunContext.Dir 下的证据文件。
/// 用例失败也必须落盘，调用方（L2TestBase.Dispose）负责 finally 语义。
/// </summary>
public sealed class EvidenceLog
{
    private readonly StringBuilder _log = new();

    public void Step(string message)
    {
        _log.AppendLine(message);
        Console.WriteLine(message);
    }

    /// <summary>把全部取证写到 RunContext.Dir/fileName（未注入 RUNDIR 时即 OutDir，手跑行为不变）。</summary>
    public void Flush(string fileName)
        => File.WriteAllText(Path.Combine(RunContext.Dir, fileName), _log.ToString());
}
