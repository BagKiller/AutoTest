// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace Vega.AutoTest.Harness;

/// <summary>
/// L2 UIA 回归用例基类：持有取证日志、VegaApp、弹窗处理、状态读取与主窗口。
/// LaunchAndPrepare 完成清场 + 拉起 + 主窗口就绪；Dispose 先落盘取证再杀进程
/// （对齐 spike finally 顺序：证据先写，进程后杀）。
/// 静态构造声明 PerMonitorV2 DPI 感知：Vega 宿主在高 DPI（如 150%）下 UIA 返回物理
/// 坐标，若测试进程仍被 DPI 虚拟化，SendInput 物理点击会被放大偏移到错误控件。
/// </summary>
public abstract class L2TestBase : IDisposable
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    static L2TestBase()
    {
        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4；失败（已设置/不支持）不致命
        SetProcessDpiAwarenessContext(new IntPtr(-4));
    }
    /// <summary>取证文件名（OutDir 下），如 "_wi3478_l2.txt"。</summary>
    private readonly string _evidenceFileName;

    protected EvidenceLog Log { get; }
    protected AssertionRecorder Assertions { get; } = new();
    protected VegaApp? Vega { get; private set; }
    protected DialogHandler Dialogs { get; }
    protected DeviceStateReader States { get; }
    protected Window? MainWindow { get; private set; }

    protected L2TestBase(string evidenceFileName)
    {
        _evidenceFileName = evidenceFileName;
        Log = new EvidenceLog();
        Dialogs = new DialogHandler(Log);
        States = new DeviceStateReader(Log);
    }

    /// <summary>清理残留 → 启动 Vega → 等主窗口（180s）。主窗口失败直接断言失败。</summary>
    protected void LaunchAndPrepare()
    {
        VegaApp.KillLeftover(Log);
        Vega = VegaApp.Launch(Log);
        MainWindow = Vega.WaitForMainWindow();
        Assert.NotNull(MainWindow);
    }

    public void Dispose()
    {
        try
        {
            // 先记住界面再杀进程。修复前这次快照就是未改基线。
            if (MainWindow is not null)
            {
                UiSnapshot.Save(MainWindow);
                Log.Step("已记住当前界面 ui-snapshot.json");
            }
        }
        catch
        {
            // 快照失败不掩盖原始测试异常
        }

        try
        {
            Log.Flush(_evidenceFileName);
        }
        catch
        {
            // 落盘失败不掩盖原始测试异常
        }

        try
        {
            Assertions.Flush();
        }
        catch
        {
            // 结构化断言落盘失败不掩盖原始测试异常
        }

        Vega?.Dispose();
    }
}
