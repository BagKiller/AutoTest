// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 操作：登录 + 就绪编排。循环处理弹窗 → 记录设备状态变化 →
/// 等待「登录已提交 && RecipeMenuItem 可用 && 设备状态空闲」。
/// 超时 Assert.Fail。
/// </summary>
public sealed class LoginFlow
{
    private readonly DialogHandler _dialogs;
    private readonly DeviceStateReader _states;
    private readonly EvidenceLog _log;

    public LoginFlow(DialogHandler dialogs, DeviceStateReader states, EvidenceLog log)
    {
        _dialogs = dialogs;
        _states = states;
        _log = log;
    }

    /// <summary>登录后等设备状态空闲且菜单可用（吸收启动后 Busy 初始化链）。</summary>
    public void WaitReady(Window main, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        var lastState = string.Empty;

        while (sw.Elapsed < timeout)
        {
            _dialogs.HandleAll(main);

            var state = _states.Read(main);
            if (state != lastState)
            {
                _log.Step($"  [{sw.Elapsed.TotalSeconds:F0}s] 设备状态 -> '{state}'");
                lastState = state;
            }

            var menu = main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeMenuItem"));
            if (_dialogs.LoggedInSeen && menu is not null && menu.Properties.IsEnabled.ValueOrDefault && state == "空闲")
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 就绪：状态空闲 + 菜单可用 ✔");
                return;
            }

            Thread.Sleep(3000);
        }

        _log.Step($"!! {timeout.TotalMinutes:F0} 分钟内未就绪（最后状态 '{lastState}'，登录={_dialogs.LoggedInSeen}）");
        Assert.Fail($"登录/就绪超时，最后状态 '{lastState}'");
    }
}
