// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using System.Text.Json;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 操作：登录 + 就绪编排。循环处理弹窗 → 记录设备状态变化 →
/// 等待「登录已提交 && RecipeMenuItem 可用 && 设备状态空闲」。
/// 超时记录前置证据并抛出明确的 BLOCKED 类型。
/// </summary>
public sealed class LoginFlow
{
    private readonly DialogHandler _dialogs;
    private readonly DeviceStateReader _states;
    private readonly EvidenceLog _log;
    private VegaApp? _vega;

    /// <summary>被测进程引用；设置后，就绪等待期间进程退出会立刻抛 VEGA_PROCESS_EXITED。</summary>
    public LoginFlow Watch(VegaApp? vega)
    {
        _vega = vega;
        return this;
    }

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
        var menuPresent = false;
        var menuEnabled = false;
        string? observationError = null;
        // 状态时间线：Busy 卡死时回答"到过什么状态、停了多久"，而不是只有最后快照。
        var timeline = new List<object>();
        var lastMongoCheck = DateTime.MinValue;
        string? mongoReachability = null;

        void SaveReadiness(string phase)
        {
            File.WriteAllText(Path.Combine(RunContext.Dir, "ui-readiness.json"), JsonSerializer.Serialize(new
            {
                phase, elapsedSeconds = sw.Elapsed.TotalSeconds,
                loginSubmitted = _dialogs.LoggedInSeen, menuPresent, menuEnabled,
                state = lastState, stateChannel = _states.Channel,
                observationError, dialogError = _dialogs.LastHandlingError,
                timeline, mongoReachability
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>节流探测 MongoDB（README 记载的设备卡 Busy 首因），每 30s 最多一次。</summary>
        string? MongoProbe()
        {
            if ((DateTime.UtcNow - lastMongoCheck).TotalSeconds < 30)
            {
                return mongoReachability;
            }

            lastMongoCheck = DateTime.UtcNow;
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connect = client.ConnectAsync("127.0.0.1", 27017);
                if (!connect.Wait(1500) || !client.Connected)
                {
                    return mongoReachability = "unreachable";
                }

                return mongoReachability = "reachable";
            }
            catch (Exception error)
            {
                return mongoReachability = "unreachable:" + error.GetType().Name;
            }
        }

        while (sw.Elapsed < timeout)
        {
            _dialogs.HandleAll(main);

            var state = _states.Read(main);
            if (state != lastState)
            {
                _log.Step($"  [{sw.Elapsed.TotalSeconds:F0}s] 设备状态 -> '{state}'");
                timeline.Add(new { atSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1), state });
                lastState = state;
            }

            try
            {
                var menu = main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeMenuItem"));
                menuPresent = menu is not null;
                menuEnabled = menu?.Properties.IsEnabled.ValueOrDefault == true;
                observationError = null;
            }
            catch (Exception error)
            {
                menuPresent = menuEnabled = false;
                observationError = error.GetType().Name;
            }
            MongoProbe();
            SaveReadiness("WAITING");
            if (_dialogs.LoggedInSeen && menuEnabled && state == "空闲")
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 就绪：状态空闲 + 菜单可用 ✔");
                SaveReadiness("READY");
                return;
            }

            // Vega 中途退出（崩溃/授权弹窗后自灭）：等满超时只会得到失真的"忙碌"。
            // （LoginFlow 无法持有 VegaApp；由测试把实例挂到 DialogHandler 所在上下文。）
            if (_vega is { } app)
            {
                if (app.HasExited)
                {
                    SaveReadiness("BLOCKED");
                    throw new UiPrerequisiteException("VEGA_PROCESS_EXITED",
                        $"Vega 进程在就绪等待期间退出（exitCode 未知，见取证日志），最后状态='{lastState}'；见 ui-readiness.json。");
                }
            }

            Thread.Sleep(3000);
        }

        MongoProbe();
        _log.Step($"!! {timeout.TotalMinutes:F0} 分钟内未就绪（最后状态 '{lastState}'，登录={_dialogs.LoggedInSeen}，MongoDB={mongoReachability}）");
        SaveReadiness("BLOCKED");
        var code = !_dialogs.LoggedInSeen ? "LOGIN_NOT_READY"
            : lastState != "空闲" ? "DEVICE_NEVER_IDLE" : "UI_NOT_READY";
        throw new UiPrerequisiteException(code,
            $"登录/就绪超时：状态='{lastState}', 登录已提交={_dialogs.LoggedInSeen}, 菜单存在={menuPresent}, 菜单可用={menuEnabled}, MongoDB={mongoReachability}；状态时间线见 ui-readiness.json。");
    }
}
