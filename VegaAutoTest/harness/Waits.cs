// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core.AutomationElements;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 操作原语：同步轮询等待（保持 Thread.Sleep 节奏，不引 async）。
/// </summary>
public static class Waits
{
    /// <summary>
    /// 轮询执行 probe，直到 condition 为真或超时。瞬时异常按未就绪处理。
    /// 超时返回最后一次 probe 结果（不抛异常，由调用方决定断言）。
    /// </summary>
    public static T Until<T>(Func<T> probe, TimeSpan timeout, string what, EvidenceLog log, int pollMs = 2000)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                var result = probe();
                if (result is bool ok ? ok : result is not null)
                {
                    return result;
                }
            }
            catch
            {
                // 瞬时异常按未就绪处理
            }

            Thread.Sleep(pollMs);
        }

        log.Step($"!! {what} not ready within {timeout.TotalSeconds:F0}s");
        return probe();
    }

    /// <summary>轮询等待控件出现且 IsEnabled=true；超时返回 null 并写日志。</summary>
    public static AutomationElement? ForEnabled(AutomationElement root, string automationId, TimeSpan timeout, EvidenceLog log)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var el = root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null && el.Properties.IsEnabled.ValueOrDefault)
            {
                log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] {automationId} enabled ✔");
                return el;
            }

            Thread.Sleep(2000);
        }

        log.Step($"!! {automationId} not enabled within {timeout.TotalSeconds:F0}s");
        return null;
    }

    /// <summary>轮询等待控件出现（不要求启用）；超时返回 null 并写日志。</summary>
    public static AutomationElement? ForElement(AutomationElement root, string automationId, TimeSpan timeout, EvidenceLog log)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var el = root.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (el is not null)
            {
                log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] {automationId} found");
                return el;
            }

            Thread.Sleep(2000);
        }

        log.Step($"!! {automationId} not found within {timeout.TotalSeconds:F0}s");
        return null;
    }
}
