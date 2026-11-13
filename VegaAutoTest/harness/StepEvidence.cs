// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 复现步骤过程截图（2026-11-13 用户规则）：工作项带复现步骤/验收标准的 UI 验证，
/// 每个关键步骤保留 1 张可证明当时界面状态的截图，作为判定成败的过程证据，
/// 与正式 verdict（只来自 Runner result.json）分开。
/// 文件落在 RunContext.Dir（与 result.json 同目录），命名 step-&lt;序号&gt;-&lt;标签&gt;.png。
/// 调用方传入当前 FlaUI Window（L2TestBase.MainWindow）。
/// </summary>
public static class StepEvidence
{
    private static int _sequence;

    public static string Capture(AutomationElement? window, string label)
    {
        _sequence++;
        var path = Path.Combine(RunContext.Dir, $"step-{_sequence:D2}-{Sanitize(label)}.png");
        try
        {
            if (window is null)
            {
                return string.Empty;
            }
            using var image = FlaUI.Core.Capturing.Capture.Element(window);
            image.ToFile(path);
        }
        catch
        {
            // 截图失败不能阻塞步骤执行；缺图在报告里体现为该步骤无像素证据
            return string.Empty;
        }
        return path;
    }

    private static string Sanitize(string label)
    {
        var chars = label.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-').ToArray();
        var text = new string(chars);
        return text.Length > 40 ? text[..40].TrimEnd('-') : text.TrimEnd('-');
    }
}
