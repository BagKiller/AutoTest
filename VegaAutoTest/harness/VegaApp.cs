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
    private readonly Process? _process;
    private readonly int _pid;

    /// <summary>被测 Vega 进程是否已退出（崩溃/自灭检测；Launch 失败时恒 true）。</summary>
    public bool HasExited => _app.HasExited;

    /// <summary>被测 Vega 进程 ID（诊断用；进程不存在时为 null）。</summary>
    public int? ProcessId => _app.HasExited ? null : _app.ProcessId;

    /// <summary>进程已退出时的退出码（未退出或无法读取时为 null）。</summary>
    public int? ExitCode
    {
        get
        {
            try { return _process is { HasExited: true } ? _process.ExitCode : null; }
            catch { return null; }
        }
    }

    private VegaApp(Application app, UIA3Automation automation, EvidenceLog log)
    {
        _app = app;
        _automation = automation;
        _log = log;
        _pid = app.ProcessId;
        try { _process = Process.GetProcessById(_pid); } catch { _process = null; }
    }

    /// <summary>清理残留 Vega 进程（单实例 Mutex 防护，启动前必须清场）。</summary>
    public static void KillLeftover(EvidenceLog log)
    {
        foreach (var p in System.Diagnostics.Process.GetProcessesByName("Vega"))
        {
            try
            {
                var image = p.MainModule?.FileName;
                if (!string.Equals(image, VegaEnv.VegaExe, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unrelated Vega PID {p.Id} is running; isolated validation cannot take its single-instance slot");
                }
                if (!Path.GetFullPath(image!).StartsWith(@"C:\AutoTest\workspaces\", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Automatic cleanup is restricted to validation workspaces");
                using var config = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
                    Path.Combine(Path.GetDirectoryName(image!)!, "Resources", "ComponentConfig.json")));
                var collection = config.RootElement.GetProperty("ToolConfigCollection");
                var selected = collection.GetProperty("SelectedToolType").GetString();
                var tool = collection.GetProperty("ToolConfigs").EnumerateArray().Single(t => t.GetProperty("ToolType").GetString() == selected);
                if (!(tool.GetProperty("RootCondition").GetString() ?? "").Split(',').Contains("Simulator"))
                    throw new InvalidOperationException("Refusing cleanup of a non-Simulator Vega");
                log.Step($"清理残留 Vega 进程 pid={p.Id}");
                p.Kill();
                p.WaitForExit(5000);
            }
            catch (ArgumentException)
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

    /// <summary>轮询等主窗口出现（180s）；超时或进程提前退出返回 null，并写 vega-launch.json 说明原因。</summary>
    public Window? WaitForMainWindow()
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(MainWindowTimeoutSeconds))
        {
            if (_app.HasExited)
            {
                // 进程自己退出（授权/启动密钥失败、崩溃）时等满 180s 没有意义。
                _log.Step($"Vega 进程在 {sw.Elapsed.TotalSeconds:F0}s 时已退出，exitCode={ExitCode?.ToString() ?? "未知"}");
                SaveLaunchDiagnostics("EXITED", sw.Elapsed);
                return null;
            }

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
        SaveLaunchDiagnostics("TIMEOUT", sw.Elapsed);
        return null;
    }

    /// <summary>
    /// 主窗口没出现时，记录进程状态和该进程拥有的全部顶层窗口（标题/类名），
    /// 区分"进程退出""停在授权/启动提示框""完全无窗口"三种情况。不记录窗口内容。
    /// </summary>
    private void SaveLaunchDiagnostics(string phase, TimeSpan elapsed)
    {
        var windows = new List<object>();
        string? enumerationError = null;
        try
        {
            foreach (var element in _automation.GetDesktop().FindAllChildren())
            {
                try
                {
                    if (element.Properties.ProcessId.ValueOrDefault != _pid) continue;
                    windows.Add(new
                    {
                        title = element.Properties.Name.ValueOrDefault,
                        className = element.Properties.ClassName.ValueOrDefault,
                        controlType = element.Properties.ControlType.ValueOrDefault.ToString(),
                        offscreen = element.Properties.IsOffscreen.ValueOrDefault
                    });
                }
                catch
                {
                    // 窗口在枚举过程中关闭
                }
            }
        }
        catch (Exception error)
        {
            enumerationError = error.GetType().Name;
        }

        var record = new
        {
            phase,
            pid = _pid,
            exe = VegaEnv.VegaExe,
            elapsedSeconds = Math.Round(elapsed.TotalSeconds, 1),
            hasExited = _app.HasExited,
            exitCode = ExitCode,
            topLevelWindows = windows,
            enumerationError,
            interactiveSession = Environment.UserInteractive,
            sessionName = Environment.GetEnvironmentVariable("SESSIONNAME")
        };
        try
        {
            File.WriteAllText(Path.Combine(RunContext.Dir, "vega-launch.json"),
                System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }));
        }
        catch
        {
            // 诊断落盘失败不掩盖原始前置失败
        }

        _log.Step($"启动诊断：phase={phase}, 进程已退出={record.hasExited}, 顶层窗口={windows.Count}，见 vega-launch.json");
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
