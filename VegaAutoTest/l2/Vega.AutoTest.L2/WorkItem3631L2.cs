// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3631 / PR 4926：F2 增加 Default 位置；启动时移到该位置；
/// 诊断页设置产量和入射光后把 L3L4 同步到对应位置。
/// 配置和调用点读源码与 Vega.exe 旁的资源。界面只确认入口在，
/// 不点击「设置产量和入射光」，也不另发 F2/L3L4 运动指令。
/// </summary>
public sealed class WorkItem3631L2 : L2TestBase
{
    private readonly List<string> _failures = new();

    public WorkItem3631L2() : base("_wi3631_l2.txt") { }

    [Fact]
    public void F2DefaultAndL3L4Sync_AreInSourceAndOutput()
    {
        var root = RequireEnv("VEGA_SW_ROOT");
        CheckDefault(Path.Combine(root, "Vega", "Resources", "MachineConfiguration", "Optics", "F2Position.json"), "source.f2");
        CheckDefault(Path.Combine(root, "Vega", "Resources", "MachineConfiguration", "Optics", "F2Position.zh-Hans.json"), "source.f2.zh");

        var opticsConfig = File.ReadAllText(Path.Combine(root, "Vega.Utility.Common", "Configs", "OpticsConfig.cs"));
        Check(opticsConfig.Contains("[MachineConfiguration(\"Default\")]", StringComparison.Ordinal)
              && opticsConfig.Contains("public double Default { get; set; }", StringComparison.Ordinal),
            "source.property", "F2PositionConfig 有 Default 属性", "有", "有");

        var start = File.ReadAllText(Path.Combine(root, "Vega.LD.Optics", "IlluminationOpticsBase.cs"));
        Check(start.Contains("_f2Motor.MovePosition(_configsManager.OpticsConfig.F2Position.Default)", StringComparison.Ordinal),
            "source.start", "Start 时把 F2 移到 Default", "有调用", start.Contains("F2Position.Default", StringComparison.Ordinal) ? "有调用" : "无调用");

        var diagnostics = File.ReadAllText(Path.Combine(root, "Vega.UI.Diagnostics", "DeviceDiagnostics", "OpticsDiagnostics", "OpticsDiagnosticsViewModel.cs"));
        var spot = diagnostics.IndexOf("await IlluminationOptics.SetSpotModeAsync(Incidence, Throughput);", StringComparison.Ordinal);
        var window = spot < 0 ? "" : diagnostics.Substring(spot, Math.Min(280, diagnostics.Length - spot));
        Check(window.Contains("GetBEAL3L4Position(Incidence, Throughput)", StringComparison.Ordinal)
              && window.Contains("await MoveL3L4PositionAsync()", StringComparison.Ordinal),
            "source.sync", "设置产量/入射后同步并移动 L3L4", "紧随 SetSpotMode", spot < 0 ? "未找到 SetSpotMode" : "见 SetThrouIncidenceAsync");

        var exe = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            Check(false, "output.f2", "Vega.exe 旁的 F2Position 含 Default", "Value=0", "未找到 Vega.exe");
        }
        else
        {
            var opticsDir = Path.Combine(Path.GetDirectoryName(exe)!, "Resources", "MachineConfiguration", "Optics");
            CheckDefault(Path.Combine(opticsDir, "F2Position.json"), "output.f2");
            CheckDefault(Path.Combine(opticsDir, "F2Position.zh-Hans.json"), "output.f2.zh");
        }

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    [Fact]
    public void DiagnosticsAndConfig_ShowF2DefaultWithoutMovingMotors()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        CheckF2ConfigPage();
        CheckDiagnosticsEntry();
        Assertions.Skip("ui.motor.move", "ui-value",
            "启动移动 F2、以及点击设置产量和入射光来同步 L3L4",
            "这两步会驱动 F2/L3L4 电机，本次只核对调用点和界面入口，不发运动指令");

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    private void CheckDefault(string path, string id)
    {
        if (!File.Exists(path))
        {
            Check(false, id, "F2Position 含 Default（0，-30000，50000）", "Value=0", "文件不存在");
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement? entry = null;
        foreach (var item in doc.RootElement.GetProperty("Configurations").EnumerateArray())
        {
            if (item.GetProperty("Name").GetString() == "Default")
            {
                entry = item;
                break;
            }
        }

        if (entry is null)
        {
            Check(false, id, "F2Position 含 Default（0，-30000，50000）", "Value=0 Min=-30000 Max=50000", "无 Default");
            return;
        }

        var value = entry.Value.GetProperty("Value").GetDouble();
        var min = entry.Value.GetProperty("Min").GetDouble();
        var max = entry.Value.GetProperty("Max").GetDouble();
        var ok = Math.Abs(value) < 0.0001 && Math.Abs(min - (-30000)) < 0.0001 && Math.Abs(max - 50000) < 0.0001;
        Check(ok, id, "F2Position 含 Default（0，-30000，50000）",
            "0 / -30000 / 50000", $"{value} / {min} / {max}");
    }

    private void CheckF2ConfigPage()
    {
        if (!OpenToolbar("ConfigurationButton", "文件配置", "FileConfigs"))
        {
            Check(false, "ui.config.f2", "配置界面能看到 F2 Default", "默认位置", "未打开配置");
            return;
        }

        ExpandNode("文件配置", "FileConfigs");
        ExpandNode("光学", "Optics");
        if (!OpenLeaf(["F2位置", "F2 Positions", "F2Position", "F2的各个位置"], ["默认位置", "Default", "完全打开位置", "Fully open"]))
        {
            Check(false, "ui.config.f2", "配置界面能看到 F2 Default", "默认位置", "未打开 F2 位置");
            return;
        }

        var shown = TextExists("默认位置", "Default");
        Log.Step($"F2 Default 标签 shown={shown}");
        Check(shown, "ui.config.f2", "配置界面显示 F2 Default 位置", "显示", shown ? "显示" : "不显示");
    }

    private void CheckDiagnosticsEntry()
    {
        if (!OpenToolbar("DiagnosticsButton", "设备诊断", "DeviceDiagnostics"))
        {
            Check(false, "ui.diag.entry", "诊断页有设置产量和入射光入口", "按钮在", "未打开诊断");
            return;
        }

        ExpandNode("设备诊断", "DeviceDiagnostics");
        if (!OpenLeaf(["光学", "Optics"], ["设置产量和入射光", "Set Throughput and Incidence", "L3L4位置（µm）", "L3L4 Position (µm)"]))
        {
            Check(false, "ui.diag.entry", "诊断页有设置产量和入射光入口", "按钮在", "未打开光学页");
            return;
        }

        var setShown = TextExists("设置产量和入射光", "Set Throughput and Incidence");
        var l3l4Shown = TextExists("L3L4位置（µm）", "L3L4 Position (µm)");
        Log.Step($"设置产量和入射光={setShown} L3L4={l3l4Shown}");
        Check(setShown && l3l4Shown, "ui.diag.entry",
            "诊断页能设置产量/入射，且有 L3L4 位置",
            "两者都在", $"设置={setShown}, L3L4={l3l4Shown}");
    }

    private bool OpenToolbar(string buttonId, params string[] readyIds)
    {
        Dialogs.HandleAll(MainWindow!);
        var button = Waits.ForEnabled(MainWindow!, buttonId, TimeSpan.FromSeconds(60), Log);
        if (button is null)
        {
            return false;
        }

        InvokeSafely(button);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            Dialogs.HandleAll(MainWindow!);
            if (readyIds.Any(id => FindId(id) is not null))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return false;
    }

    private void ExpandNode(params string[] ids)
    {
        var node = FindId(ids);
        if (node is null)
        {
            Log.Step($"未找到节点 {string.Join("/", ids)}");
            return;
        }

        try
        {
            node.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }
        catch
        {
            // 不支持滚动则直接展开
        }

        try
        {
            node.Patterns.ExpandCollapse.Pattern.Expand();
        }
        catch
        {
            Log.Step($"{ids[0]} 展开失败，可能已展开");
        }

        Thread.Sleep(500);
    }

    private bool OpenLeaf(string[] nodeIds, string[] readyTexts)
    {
        var node = FindId(nodeIds);
        if (node is null)
        {
            return readyTexts.Any(text => TextExists(text));
        }

        try
        {
            node.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }
        catch
        {
            // 不支持滚动则直接点
        }

        node.DoubleClick();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(20))
        {
            Dialogs.HandleAll(MainWindow!);
            if (readyTexts.Any(text => TextExists(text)))
            {
                return true;
            }

            Thread.Sleep(500);
        }

        return false;
    }

    private bool TextExists(params string[] names)
    {
        try
        {
            foreach (var text in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
            {
                var name = UiProps.Name(text);
                if (names.Any(n => string.Equals(name, n, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }
        catch
        {
            // 树瞬时异常
        }

        return false;
    }

    private AutomationElement? FindId(params string[] ids)
    {
        foreach (var id in ids)
        {
            try
            {
                var found = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(id));
                if (found is not null)
                {
                    return found;
                }
            }
            catch
            {
                // 树瞬时异常
            }
        }

        return null;
    }

    private void Check(bool ok, string id, string desc, string expected, string actual)
    {
        if (ok)
        {
            Assertions.Pass(id, "ui-value", desc, expected, actual);
            Log.Step($"PASS {id}: {actual}");
            return;
        }

        Assertions.Fail(id, "ui-value", desc, expected, actual);
        Log.Step($"FAIL {id}: expected {expected}, actual {actual}");
        _failures.Add($"{id}: 期望 {expected}，实际 {actual}");
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"未设置 {name}");
        return value!;
    }

    private static void InvokeSafely(AutomationElement el)
    {
        try
        {
            if (el.Patterns.Invoke.IsSupported)
            {
                el.AsButton().Invoke();
                return;
            }
        }
        catch
        {
            // 落到物理点击
        }

        el.Click();
    }
}
