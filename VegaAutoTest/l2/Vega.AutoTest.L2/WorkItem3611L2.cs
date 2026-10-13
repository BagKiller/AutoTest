// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3611：Vega100 摘除 CLPA / L3L4 / 窄掩膜，激光功率收敛为 BEA OD 三挡。
/// 配置断言读源码与 Vega.exe 旁的输出资源。界面按当前 SelectedToolType 分支：
/// Vega100 查三电机与 CLPA/L3L4/PMTCLPA 隐藏、功率三挡；其他机型查这些入口仍在。
/// 不改配方、不点循环测试开始、不启动 Wafer Run。
/// </summary>
public sealed class WorkItem3611L2 : L2TestBase
{
    private const string ToolsWithMotors = "Vega200|Vega300|Vega500";

    private readonly List<string> _failures = new();

    public WorkItem3611L2() : base("_wi3611_l2.txt") { }

    [Fact]
    public void GearMapping_IsTheSingleSource()
    {
        Check(Vega100LaserPowerGears.PowerGears.SequenceEqual(new double[] { 100, 10, 1 }),
            "gears.three", "功率挡位只有 100/10/1",
            "100/10/1", string.Join("/", Vega100LaserPowerGears.PowerGears));
        Check(Vega100LaserPowerGears.PowerToOD(100) == IncidenceFilterType.IncidenceOD0
              && Vega100LaserPowerGears.PowerToOD(10) == IncidenceFilterType.IncidenceOD1
              && Vega100LaserPowerGears.PowerToOD(1) == IncidenceFilterType.IncidenceOD2,
            "gears.to-od", "100%→OD0、10%→OD1、1%→OD2",
            "OD0/OD1/OD2",
            $"{Vega100LaserPowerGears.PowerToOD(100)}/{Vega100LaserPowerGears.PowerToOD(10)}/{Vega100LaserPowerGears.PowerToOD(1)}");
        Check(!Vega100LaserPowerGears.IsPowerGear(50) && !Vega100LaserPowerGears.TryPowerToOD(50, out _),
            "gears.reject-50", "50% 不是功率挡", "拒绝",
            Vega100LaserPowerGears.IsPowerGear(50) ? "接受" : "拒绝");
        Check(!Vega100LaserPowerGears.IsRecipeBEAODGear(IncidenceFilterType.IncidenceOD4),
            "gears.od4", "OD4 保留给 PMT 校准，不是配方挡", "排除",
            Vega100LaserPowerGears.IsRecipeBEAODGear(IncidenceFilterType.IncidenceOD4) ? "可选" : "排除");
        Check(Math.Abs(Vega100LaserPowerGears.SnapToNearestGear(80) - 100) < 0.00001
              && Math.Abs(Vega100LaserPowerGears.SnapToNearestGear(20) - 10) < 0.00001
              && Math.Abs(Vega100LaserPowerGears.SnapToNearestGear(2) - 1) < 0.00001
              && Math.Abs(Vega100LaserPowerGears.SnapToNearestGear(0) - 1) < 0.00001,
            "gears.snap", "旧功率按对数吸附到最近挡",
            "80→100, 20→10, 2→1, 0→1",
            $"80→{Vega100LaserPowerGears.SnapToNearestGear(80)}, 20→{Vega100LaserPowerGears.SnapToNearestGear(20)}, 2→{Vega100LaserPowerGears.SnapToNearestGear(2)}, 0→{Vega100LaserPowerGears.SnapToNearestGear(0)}");
        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    [Fact]
    public void MotorAndCaliConfig_ExcludesVega100()
    {
        var root = RequireEnv("VEGA_SW_ROOT");
        CheckMotorPair(root, "source", Path.Combine("Vega", "Resources", "MachineConfiguration"));
        CheckPmtClpa(Path.Combine(root, "Vega.UI.Calibration", "CalibrationTreeNode.json"), "source.tree", "ValidOn");
        CheckPmtClpa(Path.Combine(root, "Vega", "Resources", "CalibrationComponents.json"), "source.components", "Condition");
        CheckNarrowMaskFlag(Path.Combine(root, "Vega", "Resources", "MachineConfiguration", "Optics.json"), "source.optics");

        var exe = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            Check(false, "output.motors", "Vega.exe 旁的电机配置已摘除 Vega100 三电机",
                "CLPA/L3L4/NarrowMask 不含 Vega100", "未找到 Vega.exe");
        }
        else
        {
            var outDir = Path.GetDirectoryName(exe)!;
            CheckMotorPair(outDir, "output", Path.Combine("Resources", "MachineConfiguration"));
            CheckPmtClpa(Path.Combine(outDir, "Resources", "CalibrationComponents.json"), "output.components", "Condition");
            CheckNarrowMaskFlag(Path.Combine(outDir, "Resources", "MachineConfiguration", "Optics.json"), "output.optics");
        }

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    [Fact]
    public void Surfaces_FollowCurrentToolType()
    {
        var tool = ReadSelectedToolType();
        Log.Step($"SelectedToolType={tool}");
        var vega100 = string.Equals(tool, "Vega100", StringComparison.OrdinalIgnoreCase);
        var vega200 = tool.StartsWith("Vega200", StringComparison.OrdinalIgnoreCase);

        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        CheckMotorTree(vega100, vega200);
        CheckOpticsDiagnostics(vega100);
        CheckCycleAperture(vega100);
        CheckPmtClpaNode(vega100);
        CheckRecipePower(vega100);

        if (!vega100)
        {
            Assertions.Skip("ui.vega100.only", "ui-value",
                "Vega100 配方三挡、窄掩膜锁 Full、旧配方吸附与运行前拦截",
                $"当前 SelectedToolType={tool}，这些界面只在 Vega100 上出现，本次未切换机型");
        }

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    private void CheckMotorPair(string root, string tag, string relativeDir)
    {
        foreach (var file in new[] { "HardWareMotors.json", "HardWareMotors.zh-Hans.json" })
        {
            var path = Path.Combine(root, relativeDir, file);
            CheckMotor(path, $"{tag}.{file}.CLPA", "CLPAMotor", ToolsWithMotors);
            CheckMotor(path, $"{tag}.{file}.L3L4", "L3L4Motor", "Vega200");
            CheckMotor(path, $"{tag}.{file}.Narrow", "NarrowMaskMotor", ToolsWithMotors);
        }
    }

    private void CheckMotor(string path, string id, string name, string expected)
    {
        if (!File.Exists(path))
        {
            Check(false, id, $"{name} 的 ValidOn", expected, "文件不存在");
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var entries = doc.RootElement.TryGetProperty("Sections", out var sections)
            ? sections
            : doc.RootElement.GetProperty("Configurations");
        string? validOn = null;
        foreach (var item in entries.EnumerateArray())
        {
            if (item.GetProperty("Name").GetString() == name)
            {
                validOn = item.TryGetProperty("ValidOn", out var value) ? value.GetString() : "(无 ValidOn)";
                break;
            }
        }

        var ok = validOn == expected && (validOn is null || !validOn.Contains("Vega100", StringComparison.Ordinal));
        Check(ok, id, $"{name} 不在 Vega100 配置中，V200/300/500 注册保持",
            expected, validOn ?? "未找到条目");
    }

    private void CheckPmtClpa(string path, string id, string property)
    {
        if (!File.Exists(path))
        {
            Check(false, id, "PMTCLPA 校准入口不对 Vega100 开放", ToolsWithMotors, "文件不存在");
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var found = FindProperty(doc.RootElement, "PMTCLPA", property)
                    ?? FindProperty(doc.RootElement, "PMTCLPA Calibration", property);
        Check(found == ToolsWithMotors, id, "PMTCLPA 只在 V200/300/500", ToolsWithMotors, found ?? "未找到");
    }

    private void CheckNarrowMaskFlag(string path, string id)
    {
        if (!File.Exists(path))
        {
            Check(false, id, "Vega100 IsNarrowMaskInstalled=false", "false", "文件不存在");
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        string? value = null;
        foreach (var item in doc.RootElement.GetProperty("Configurations").EnumerateArray())
        {
            if (item.GetProperty("Name").GetString() != "IsNarrowMaskInstalled")
            {
                continue;
            }

            var validOn = item.TryGetProperty("ValidOn", out var on) ? on.GetString() : "";
            if (validOn is not null && validOn.Contains("Vega100", StringComparison.Ordinal))
            {
                value = item.GetProperty("Value").GetString();
            }
        }

        Check(value == "false", id, "Vega100 未安装窄掩膜轮", "false", value ?? "未找到 Vega100 条目");
    }

    private static string? FindProperty(JsonElement element, string name, string property)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var header = element.TryGetProperty("Header", out var headerValue) ? headerValue.GetString() : null;
            var calib = element.TryGetProperty("CalibrationID", out var calibValue) ? calibValue.GetString() : null;
            if ((header == name || calib == name) && element.TryGetProperty(property, out var hit))
            {
                return hit.GetString();
            }

            foreach (var child in element.EnumerateObject())
            {
                var found = FindProperty(child.Value, name, property);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var found = FindProperty(child, name, property);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private void CheckMotorTree(bool vega100, bool vega200)
    {
        if (!OpenToolbar("ConfigurationButton", "文件配置", "FileConfigs"))
        {
            Check(false, "ui.config.tree", "打开电机配置树", "文件配置", "未打开");
            return;
        }

        ExpandNode("文件配置", "FileConfigs");
        ExpandNode("电机", "Motor");
        Thread.Sleep(800);

        var clpa = NodeExists("CLPA电机", "CLPA Motor", "CLPAMotor");
        var l3l4 = NodeExists("L3L4电机", "L3L4 Motor", "L3L4Motor");
        var narrow = NodeExists("Narrow掩模电机", "Narrow Mask Motor", "NarrowMaskMotor");
        Log.Step($"配置树 CLPA={clpa} L3L4={l3l4} Narrow={narrow}");

        if (vega100)
        {
            Check(!clpa && !l3l4 && !narrow, "ui.config.motors",
                "Vega100 电机配置不出现 CLPA、L3L4、窄掩膜", "均不出现",
                $"CLPA={clpa}, L3L4={l3l4}, Narrow={narrow}");
            return;
        }

        Check(clpa, "ui.config.clpa", "V200/300/500 仍注册 CLPA 电机", "出现", clpa ? "出现" : "不出现");
        Check(narrow, "ui.config.narrow", "V200/300/500 仍注册窄掩膜电机", "出现", narrow ? "出现" : "不出现");
        Check(vega200 ? l3l4 : !l3l4, "ui.config.l3l4",
            vega200 ? "Vega200 仍注册 L3L4 电机" : "V300/500 的 L3L4 本来就不注册",
            vega200 ? "出现" : "不出现", l3l4 ? "出现" : "不出现");
    }

    private void CheckOpticsDiagnostics(bool vega100)
    {
        if (!OpenToolbar("DiagnosticsButton", "设备诊断", "DeviceDiagnostics"))
        {
            Check(false, "ui.optics.clpa", "打开光学诊断", "光学页", "未打开诊断");
            return;
        }

        ExpandNode("设备诊断", "DeviceDiagnostics");
        if (!OpenLeaf("光学", "Optics", "激光功率（%）", "Laser Power (%)"))
        {
            Check(false, "ui.optics.clpa", "打开光学诊断页", "激光功率", "未打开");
            return;
        }

        var degree = IsPresent("激光功率（°）", "Laser Power (°)");
        var setPower = IsPresent("设置激光功率(W)", "Set Laser Power (W)");
        var l3l4 = IsPresent("L3L4位置（µm）", "L3L4 Position (µm)");
        var move = IsPresent("移动L3L4位置", "Move L3L4 Position");
        Log.Step($"光学页 功率角度={degree} 设置功率={setPower} L3L4={l3l4} 移动={move}");
        var shown = degree || setPower || l3l4 || move;
        Check(vega100 ? !shown : shown, "ui.optics.clpa-l3l4",
            vega100 ? "Vega100 光学诊断不显示 CLPA/L3L4 控件" : "当前机型光学诊断仍显示 CLPA/L3L4 控件",
            vega100 ? "均不显示" : "仍显示",
            $"角度={degree},设置={setPower},L3L4={l3l4},移动={move}");
    }

    private void CheckCycleAperture(bool vega100)
    {
        if (!OpenLeaf("光学循环测试", "OpticsCycleTest", "Narrow Channel", "窄通道"))
        {
            Check(false, "ui.cycle.aperture", "打开光学循环测试", "Narrow Channel", "未打开");
            return;
        }

        var aperture = GroupContains("Narrow Channel", "窄通道", "Aperture");
        Log.Step($"窄通道循环 Aperture={aperture}");
        Check(vega100 ? !aperture : aperture, "ui.cycle.aperture",
            vega100 ? "Vega100 不显示窄掩膜循环测试项" : "当前机型仍显示窄通道 Aperture 循环项",
            vega100 ? "不显示" : "显示", aperture ? "显示" : "不显示");
    }

    private void CheckPmtClpaNode(bool vega100)
    {
        if (!OpenToolbar("CalibrationButton", "PMT校准", "PMTCalibration"))
        {
            Check(false, "ui.cali.pmtclpa", "打开校准树", "PMT校准", "未打开");
            return;
        }

        ExpandNode("PMT校准", "PMTCalibration");
        Thread.Sleep(800);
        var shown = NodeExists("CLPA", "PMTCLPA");
        Log.Step($"PMTCLPA shown={shown}");
        Check(vega100 ? !shown : shown, "ui.cali.pmtclpa",
            vega100 ? "Vega100 隐藏 PMTCLPA 校准入口" : "当前机型仍显示 PMTCLPA 校准入口",
            vega100 ? "不显示" : "显示", shown ? "显示" : "不显示");
    }

    private void CheckRecipePower(bool vega100)
    {
        var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
        page.OpenInspection();
        var recipeNode = page.FindRecipeNode(TimeSpan.FromSeconds(60));
        if (!page.IsDocumentLoaded)
        {
            if (recipeNode is null)
            {
                Check(false, "ui.recipe.power", "打开配方后看到激光功率控件", "功率控件", "没有配方节点");
                return;
            }

            recipeNode.DoubleClick();
        }

        page.WaitForDocument(TimeSpan.FromSeconds(90));
        var collections = Waits.ForElement(MainWindow!, "RecipeView_CollectionsExpander", TimeSpan.FromSeconds(30), Log);
        if (collections is not null)
        {
            try
            {
                collections.Patterns.ExpandCollapse.Pattern.Expand();
            }
            catch
            {
                collections.Click();
            }

            Thread.Sleep(1000);
        }

        var gear = FindId("DFSizingView_LaserPowerGearComboBox");
        var numeric = FindId("DFSizingView_LaserPowerNumericUpDownView");
        var od = FindId("DarkFieldSettingView_IncidenceFilterComboBox");
        var gearShown = IsShown(gear);
        var numericShown = IsShown(numeric);
        var gearItems = gearShown ? ReadComboItems(gear!) : new List<string>();
        var odItems = IsShown(od) ? ReadComboItems(od!) : new List<string>();
        Log.Step($"功率三挡 shown={gearShown} items=[{string.Join("/", gearItems)}] 连续值 shown={numericShown} OD=[{string.Join("/", odItems)}]");

        if (vega100)
        {
            Check(gearShown && !numericShown && gearItems.Select(NormalizeGear).SequenceEqual(new[] { "100", "10", "1" }),
                "ui.recipe.power", "Vega100 激光功率是 100%/10%/1% 下拉",
                "100/10/1", gearShown ? string.Join("/", gearItems) : "下拉未显示");
            var odNorm = odItems.Select(NormalizeOd).Where(s => s.Length > 0).ToList();
            Check(odNorm.SequenceEqual(new[] { "OD0", "OD1", "OD2" }),
                "ui.recipe.od", "Vega100 BEA OD 候选只有 OD0/OD1/OD2",
                "OD0/OD1/OD2", odNorm.Count == 0 ? "未读到" : string.Join("/", odNorm));

            var nmask = FindId("DFChannelSettingView_NMaskComboBox");
            var nmaskItems = IsShown(nmask) ? ReadComboItems(nmask!) : new List<string>();
            Check(nmaskItems.Count == 1 && nmaskItems[0] == "Full",
                "ui.recipe.nmask", "Vega100 窄掩膜锁定为 Full",
                "Full", nmaskItems.Count == 0 ? "未读到" : string.Join("/", nmaskItems));
            return;
        }

        Check(!gearShown && numericShown, "ui.recipe.power",
            "非 Vega100 激光功率仍是连续数值，不是三挡下拉",
            "连续值", $"三挡={gearShown},连续值={numericShown}");
    }

    private bool OpenToolbar(string buttonId, params string[] readyIds)
    {
        Dialogs.HandleAll(MainWindow!);
        var button = Waits.ForEnabled(MainWindow!, buttonId, TimeSpan.FromSeconds(60), Log);
        if (button is null)
        {
            Log.Step($"未找到 {buttonId}");
            return false;
        }

        InvokeSafely(button);
        Log.Step($"已点击 {buttonId}");
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

        return FindId(readyIds[0]) is not null;
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

        Thread.Sleep(600);
    }

    private bool OpenLeaf(string chineseId, string englishId, params string[] readyTexts)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var node = FindId(chineseId, englishId);
            if (node is null)
            {
                Log.Step($"第 {attempt} 次未找到 {chineseId}/{englishId}");
                Thread.Sleep(800);
                continue;
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
                if (readyTexts.Any(text => FindText(MainWindow!, text) is not null || FindId(text) is not null))
                {
                    Log.Step($"已打开 {chineseId}");
                    return true;
                }

                Thread.Sleep(500);
            }
        }

        return false;
    }

    private bool NodeExists(params string[] ids) => FindId(ids) is not null;

    private bool IsPresent(params string[] names) => IsShown(FindText(MainWindow!, names));

    private bool GroupContains(string english, string chinese, string childText)
    {
        try
        {
            foreach (var group in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Group)))
            {
                var name = UiProps.Name(group);
                if (!name.Contains(english, StringComparison.OrdinalIgnoreCase)
                    && !name.Contains(chinese, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (group.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                    .Any(text => string.Equals(UiProps.Name(text), childText, StringComparison.Ordinal)))
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

    private static AutomationElement? FindText(AutomationElement root, params string[] names)
    {
        try
        {
            foreach (var text in root.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
            {
                var name = UiProps.Name(text);
                if (names.Any(n => string.Equals(name, n, StringComparison.Ordinal)))
                {
                    return text;
                }
            }
        }
        catch
        {
            // 树瞬时异常
        }

        return null;
    }

    private static bool IsShown(AutomationElement? element)
    {
        if (element is null)
        {
            return false;
        }

        try
        {
            element.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }
        catch
        {
            // 不支持滚动则看矩形
        }

        try
        {
            if (element.Properties.IsOffscreen.ValueOrDefault)
            {
                return false;
            }
        }
        catch
        {
            // 读不到离屏状态时看矩形
        }

        var rect = element.BoundingRectangle;
        return rect.Width > 1 && rect.Height > 1;
    }

    private static List<string> ReadComboItems(AutomationElement combo)
    {
        try
        {
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(400);
        }
        catch
        {
            // 展不开则按现有子树读
        }

        var names = new List<string>();
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            names.Add(ComboBoxReader.FindChildText(item) ?? UiProps.Name(item));
        }

        try
        {
            combo.Patterns.ExpandCollapse.Pattern.Collapse();
        }
        catch
        {
            // 收起失败不致命
        }

        return names;
    }

    private static string NormalizeGear(string text)
        => text.Replace("%", "", StringComparison.Ordinal).Trim();

    private static string NormalizeOd(string text)
    {
        var match = Regex.Match(text, "OD\\d+", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : text.Trim();
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

    private static string ReadSelectedToolType()
    {
        var exe = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE");
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "";
        }

        var config = Path.Combine(Path.GetDirectoryName(exe)!, "Resources", "ComponentConfig.json");
        if (!File.Exists(config))
        {
            return "";
        }

        var match = Regex.Match(File.ReadAllText(config), "\"SelectedToolType\"\\s*:\\s*\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "";
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
