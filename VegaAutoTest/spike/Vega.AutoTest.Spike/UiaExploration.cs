// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using System.Text;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace Vega.AutoTest.Spike;

/// <summary>
/// UIA 通道探索：确认 Vega 能否被自动启动、主窗口能否抓到、
/// #3478 涉及的控件能否通过 AutomationId 定位。
///
/// 这一层回答的是 ViewModel 测试回答不了的问题：XAML 绑定有没有真的接上、
/// 窗口有没有按预期出现、控件在不在该在的位置。
///
/// 注意：本类是 UIA 探索工具（独有全树 AutomationId 扫描），非回归用例。
/// </summary>
public class UiaExploration
{
    // 探索工具非回归用例：exe 路径不进 harness 常量（VegaEnv 面向回归用例），就地用环境变量覆盖
    private static readonly string VegaExe = System.Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE")
        ?? @"D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.exe";

    private static readonly string OutDir = System.Environment.GetEnvironmentVariable("VEGA_AUTOTEST_OUTDIR")
        ?? AppContext.BaseDirectory;

    [Fact]
    public void LaunchVegaAndDumpUiaTree()
    {
        Assert.True(System.IO.File.Exists(VegaExe), $"找不到 Vega.exe: {VegaExe}");

        var log = new StringBuilder();
        var sw = Stopwatch.StartNew();

        using var app = Application.Launch(new ProcessStartInfo
        {
            FileName = VegaExe,
            WorkingDirectory = System.IO.Path.GetDirectoryName(VegaExe)!,
        });

        log.AppendLine($"launched pid={app.ProcessId}");

        using var automation = new UIA3Automation();

        Window? main = null;
        var deadline = TimeSpan.FromSeconds(240);
        while (sw.Elapsed < deadline)
        {
            if (app.HasExited)
            {
                log.AppendLine($"!! process exited early at {sw.Elapsed.TotalSeconds:F0}s, code={app.ExitCode}");
                break;
            }

            try
            {
                main = app.GetMainWindow(automation, TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                log.AppendLine($"  [{sw.Elapsed.TotalSeconds:F0}s] GetMainWindow: {ex.GetType().Name}");
            }

            if (main is not null && !string.IsNullOrWhiteSpace(main.Title))
            {
                break;
            }

            Thread.Sleep(3000);
        }

        log.AppendLine($"main window after {sw.Elapsed.TotalSeconds:F0}s: " +
                       (main is null ? "NULL" : $"'{main.Title}' class={main.ClassName}"));

        // 所有顶层窗口，用来看有没有登录框之类的额外窗口
        foreach (var w in automation.GetDesktop().FindAllChildren())
        {
            try
            {
                if (w.Properties.ProcessId.ValueOrDefault == app.ProcessId)
                {
                    log.AppendLine($"  top-level window: '{w.Name}' type={w.ControlType} class={w.ClassName}");
                }
            }
            catch
            {
                // 桌面枚举期间窗口可能消失，忽略
            }
        }

        if (main is not null)
        {
            log.AppendLine();
            log.AppendLine("=== UIA tree (depth<=4) ===");
            Dump(main, log, 0, 4);

            log.AppendLine();
            log.AppendLine("=== elements whose AutomationId mentions InspRegion / EdgeExclusion ===");
            foreach (var e in main.FindAllDescendants())
            {
                var id = SafeId(e);
                if (id.Contains("InspRegion", StringComparison.OrdinalIgnoreCase) ||
                    id.Contains("EdgeExclusion", StringComparison.OrdinalIgnoreCase))
                {
                    log.AppendLine($"  {id}  type={e.ControlType} rect={e.BoundingRectangle}");
                }
            }
        }

        System.IO.File.WriteAllText(System.IO.Path.Combine(OutDir, "_uia.txt"), log.ToString());

        try
        {
            app.Kill();
        }
        catch
        {
            // 进程可能已退出
        }

        Assert.NotNull(main);
    }

    private static void Dump(AutomationElement e, StringBuilder sb, int depth, int maxDepth)
    {
        if (depth > maxDepth)
        {
            return;
        }

        var id = SafeId(e);
        sb.AppendLine($"{new string(' ', depth * 2)}{e.ControlType} name='{Trim(SafeName(e))}'" +
                      (id.Length > 0 ? $" id='{id}'" : string.Empty));

        AutomationElement[] children;
        try
        {
            children = e.FindAllChildren();
        }
        catch
        {
            return;
        }

        foreach (var c in children)
        {
            Dump(c, sb, depth + 1, maxDepth);
        }
    }

    private static string SafeId(AutomationElement e)
    {
        try
        {
            return e.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string SafeName(AutomationElement e)
    {
        try
        {
            return e.Properties.Name.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Trim(string s)
        => s.Length <= 40 ? s : s[..40] + "...";
}
