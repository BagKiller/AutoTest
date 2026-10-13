// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 操作：Vega 进程生命周期（清理残留 → 启动 → 等主窗口 → 退出）。
/// 实现 IDisposable：Dispose 杀进程并释放 UIA3Automation。
/// </summary>
public sealed class VegaApp : IDisposable
{
    private const int MainWindowTimeoutSeconds = 180;

    private readonly Application _app;
    private readonly UIA3Automation _automation;
    private readonly EvidenceLog _log;

    private VegaApp(Application app, UIA3Automation automation, EvidenceLog log)
    {
        _app = app;
        _automation = automation;
        _log = log;
    }

    /// <summary>清理残留 Vega 进程（单实例 Mutex 防护，启动前必须清场）。</summary>
    public static void KillLeftover(EvidenceLog log)
    {
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Vega"))
        {
            try
            {
                log.Step($"清理残留 Vega 进程 pid={p.Id}");
                p.Kill();
                p.WaitForExit(5000);
            }
            catch
            {
                // 已退出
            }
        }

        Thread.Sleep(1000);
    }

    /// <summary>启动 Vega.exe 并持有进程与 UIA3Automation。</summary>
    public static VegaApp Launch(EvidenceLog log)
    {
        var app = Application.Launch(new System.Diagnostics.ProcessStartInfo
        {
            FileName = VegaEnv.VegaExe,
            WorkingDirectory = Path.GetDirectoryName(VegaEnv.VegaExe)!,
        });

        return new VegaApp(app, new UIA3Automation(), log);
    }

    /// <summary>轮询等主窗口出现（180s）；超时返回 null（调用方断言）。</summary>
    public Window? WaitForMainWindow()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(MainWindowTimeoutSeconds))
        {
            try
            {
                var w = _app.GetMainWindow(_automation, TimeSpan.FromSeconds(5));
                if (w is not null && !string.IsNullOrWhiteSpace(w.Title))
                {
                    _log.Step($"主窗口就绪 ({sw.Elapsed.TotalSeconds:F0}s): {w.Title}");
                    return w;
                }
            }
            catch
            {
                // 还没起来
            }

            Thread.Sleep(2000);
        }

        _log.Step($"主窗口在 {MainWindowTimeoutSeconds}s 内未就绪");
        return null;
    }

    public void Dispose()
    {
        try
        {
            _app.Kill();
        }
        catch
        {
            // 已退出
        }

        _automation.Dispose();
    }
}
