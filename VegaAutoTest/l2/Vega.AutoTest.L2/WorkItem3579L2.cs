// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3579：Vega100 BPSR 档位改为 OD1-OD6，诊断界面在无 BPSI 的机型上隐藏 BPSI 控件。
/// 修复已在本地历史 0964d4f2（PR 4844）。配置断言读源码与 Vega.exe 旁的输出资源；
/// 界面断言按当前 SelectedToolType 分支：Vega100 查隐藏与六档下拉，其他机型查 BPSI 仍显示。
/// 不点击开始校准，不驱动滤光片电机。
/// </summary>
public sealed class WorkItem3579L2 : L2TestBase
{
    private static readonly string[] Vega100Rods = ["OD1", "OD2", "OD3", "OD4", "OD5", "OD6"];

    private static readonly string[] Vega100PositionNames =
        ["PositionOD1", "PositionOD2", "PositionOD3", "PositionOD4", "PositionOD5", "PositionOD6"];

    private static readonly string[] OtherToolsWithBpsi =
        ["Vega200,Gen1", "Vega200,Gen2|Gen3", "Vega300", "Vega500"];

    private readonly List<string> _failures = new();

    public WorkItem3579L2() : base("_wi3579_l2.txt") { }

    [Fact]
    public void GearConfig_MatchesAcceptance()
    {
        var root = RequireEnv("VEGA_SW_ROOT");
        var gearPath = Path.Combine(root, "Vega", "Resources", "Configs", "OpticsGearConfig.json");
        using var gear = JsonDocument.Parse(File.ReadAllText(gearPath));
        var mappings = gear.RootElement.GetProperty("DeviceMappings");

        var vega100 = FindMapping(mappings, "Vega100");
        Check(NamesOf(vega100, "BPSRODs").SequenceEqual(Vega100Rods),
            "vega100.bpsrods", "Vega100 BPSRODs 为 OD1-OD6",
            string.Join("/", Vega100Rods), string.Join("/", NamesOf(vega100, "BPSRODs")));
        Check(NamesOf(vega100, "BPSIODs").Count == 0,
            "vega100.bpsiods", "Vega100 BPSIODs 为空（诊断页据此隐藏 BPSI）",
            "[]", string.Join("/", NamesOf(vega100, "BPSIODs")));

        foreach (var condition in OtherToolsWithBpsi)
        {
            var mapping = FindMapping(mappings, condition);
            var iods = NamesOf(mapping, "BPSIODs");
            Check(iods.Count > 0,
                "bpsi." + condition, $"{condition} 仍配置 BPSI 档位",
                "非空", iods.Count == 0 ? "[]" : string.Join("/", iods));
        }

        foreach (var relative in new[]
        {
            Path.Combine("Vega", "Resources", "MachineConfiguration", "Optics", "BPSRODPosition.json"),
            Path.Combine("Vega", "Resources", "MachineConfiguration", "Optics", "BPSRODPosition.zh-Hans.json"),
        })
        {
            var names = PositionNames(Path.Combine(root, relative));
            Check(names.SequenceEqual(Vega100PositionNames),
                "position." + Path.GetFileName(relative),
                "BPSR 位置配置为 PositionOD1-OD6",
                string.Join("/", Vega100PositionNames), string.Join("/", names));
        }

        var opticsIndex = File.ReadAllText(Path.Combine(root, "Vega", "Resources", "MachineConfiguration", "Optics.json"));
        var positionAt = opticsIndex.IndexOf("\"Name\": \"BPSRODPosition\"", StringComparison.Ordinal);
        var positionWindow = positionAt < 0 ? "" : opticsIndex.Substring(positionAt, Math.Min(240, opticsIndex.Length - positionAt));
        Check(positionWindow.Contains("\"ValidOn\": \"Vega100\"", StringComparison.Ordinal),
            "position.validOn", "BPSRODPosition 只在 Vega100 机台配置中出现",
            "ValidOn Vega100", positionAt < 0 ? "未找到条目" : "见 Optics.json BPSRODPosition");

        foreach (var xaml in new[]
        {
            "Vega.UI.Diagnostics/DeviceDiagnostics/OpticsDiagnostics/OpticsDiagnosticsView.xaml",
            "Vega.UI.Diagnostics/DeviceDiagnostics/AFSDiagnostics/AFSDiagnosticsView.xaml",
            "Vega.UI.Diagnostics/DeviceDiagnostics/BPSDiagnostics/BPSDiagnosticsView.xaml",
        })
        {
            var text = File.ReadAllText(Path.Combine(root, xaml.Replace('/', Path.DirectorySeparatorChar)));
            Check(text.Contains("IsBPSISupported", StringComparison.Ordinal),
                "xaml." + Path.GetFileNameWithoutExtension(xaml),
                "诊断页按 IsBPSISupported 控制 BPSI 可见性",
                "含 IsBPSISupported", text.Contains("IsBPSISupported", StringComparison.Ordinal) ? "有" : "无");
        }

        var setBps = File.ReadAllText(Path.Combine(root, "Vega.LD.Optics", "IlluminationOpticsLiteVega100.cs"));
        var mapsOd1To6 = Vega100Rods.All(od => setBps.Contains($"BPSROD.{od} =>", StringComparison.Ordinal));
        Check(mapsOd1To6 && !setBps.Contains("BPSROD.OD1_5 =>", StringComparison.Ordinal),
            "setbpsrod.map", "Vega100 SetBPSROD 映射 OD1-OD6",
            "OD1-OD6", mapsOd1To6 ? "OD1-OD6" : "未齐");

        var bpsData = File.ReadAllText(Path.Combine(root,
            "Vega.UI.Diagnostics", "DeviceDiagnostics", "BPSDiagnostics", "BPSDiagnosticsData.cs"));
        Check(bpsData.Contains("[\"BPSR\", \"BPSI\", \"Both\"]", StringComparison.Ordinal)
              && bpsData.Contains("[\"BPSR\"]", StringComparison.Ordinal),
            "testmode.list", "无 BPSI 时机型测试通道只剩 BPSR",
            "BPSR / BPSR+BPSI+Both", "见 BuildTestModeList");

        var exe = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE");
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            Check(false, "output.gear", "Vega.exe 输出目录中的档位配置与源码一致",
                string.Join("/", Vega100Rods), "未找到 Vega.exe");
        }
        else
        {
            var outputGear = Path.Combine(Path.GetDirectoryName(exe)!, "Resources", "Configs", "OpticsGearConfig.json");
            using var shipped = JsonDocument.Parse(File.ReadAllText(outputGear));
            var shipped100 = FindMapping(shipped.RootElement.GetProperty("DeviceMappings"), "Vega100");
            var shippedRods = NamesOf(shipped100, "BPSRODs");
            var shippedIods = NamesOf(shipped100, "BPSIODs");
            Check(shippedRods.SequenceEqual(Vega100Rods) && shippedIods.Count == 0,
                "output.gear", "Vega.exe 旁 OpticsGearConfig 的 Vega100 档位已是 OD1-OD6 且无 BPSI",
                string.Join("/", Vega100Rods) + " / []",
                string.Join("/", shippedRods) + " / [" + string.Join("/", shippedIods) + "]");
        }

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    [Fact]
    public void DiagnosticPages_FollowCurrentToolType()
    {
        var tool = ReadSelectedToolType();
        Log.Step($"SelectedToolType={tool}");
        var vega100 = string.Equals(tool, "Vega100", StringComparison.OrdinalIgnoreCase);

        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        OpenDiagnosticsTree();
        OpenLeaf("光学", "Optics", "BPSr OD");

        var bpsiShown = IsShown(FindText(MainWindow!, "BPSi OD"));
        var setShown = IsShown(FindNamed(MainWindow!, "设置BPSi的光密度", "Set BPSi OD"));
        var bpsrCombo = FindComboBoxByLabel(MainWindow!, "BPSr OD");
        var bpsrItems = bpsrCombo is null ? new List<string>() : ReadComboItems(bpsrCombo);
        Log.Step($"BPSi OD shown={bpsiShown}; 设置按钮 shown={setShown}; BPSr 下拉=[{string.Join("/", bpsrItems)}]");

        if (vega100)
        {
            Check(!bpsiShown, "ui.optics.bpsi", "Vega100 光学页不显示 BPSi OD", "不显示", bpsiShown ? "显示" : "不显示");
            Check(!setShown, "ui.optics.setbpsi", "Vega100 光学页不显示设置 BPSi 按钮", "不显示", setShown ? "显示" : "不显示");
            Check(bpsrItems.SequenceEqual(Vega100Rods), "ui.optics.bpsr",
                "Vega100 BPSr OD 下拉只有 OD1-OD6", string.Join("/", Vega100Rods), string.Join("/", bpsrItems));
        }
        else
        {
            Check(bpsiShown, "ui.optics.bpsi", $"{tool} 光学页仍显示 BPSi OD", "显示", bpsiShown ? "显示" : "不显示");
            Check(setShown, "ui.optics.setbpsi", $"{tool} 光学页仍显示设置 BPSi 按钮", "显示", setShown ? "显示" : "不显示");
            Check(bpsrItems.Count > 0 && !bpsrItems.SequenceEqual(Vega100Rods), "ui.optics.bpsr",
                $"{tool} BPSr 下拉不是 Vega100 的 OD1-OD6 六档", "其他机型档位", string.Join("/", bpsrItems));
        }

        OpenLeaf("自动对焦", "AFS", "BPS-R质心", "BPS-R Centroid");
        var afsCentroid = IsShown(FindText(MainWindow!, "BPS-I质心", "BPS-I Centroid"));
        var afsFilter = IsShown(FindText(MainWindow!, "BPS-I过滤器", "BPS-I Filter"));
        var afsShutter = IsShown(FindText(MainWindow!, "BPS-I快门", "BPS-I Shutter"));
        Log.Step($"AFS BPS-I 质心={afsCentroid} 过滤器={afsFilter} 快门={afsShutter}");
        Check(vega100 ? !afsCentroid && !afsFilter && !afsShutter : afsCentroid && afsFilter && afsShutter,
            "ui.afs.bpsi",
            vega100 ? "Vega100 AFS 页不显示 BPS-I 质心/过滤器/快门" : $"{tool} AFS 页仍显示 BPS-I 质心/过滤器/快门",
            vega100 ? "均不显示" : "均显示",
            $"质心={afsCentroid},过滤器={afsFilter},快门={afsShutter}");

        OpenLeaf("BPS", "BPS", "测试通道", "BPSR强度范围", "BPSR Range");
        var rangeShown = IsShown(FindText(MainWindow!, "BPSI强度范围", "BPSI Range"));
        var channel = FindComboBoxByLabel(MainWindow!, "测试通道")
            ?? FindComboBoxByLabel(MainWindow!, "Test Channel");
        var channelItems = channel is null ? new List<string>() : ReadComboItems(channel);
        var selected = channel is null ? "" : ComboBoxReader.ReadSelectedDisplay(channel);
        Log.Step($"BPSI强度范围 shown={rangeShown}; 测试通道=[{string.Join("/", channelItems)}] selected='{selected}'");

        if (vega100)
        {
            Check(!rangeShown, "ui.bps.range", "Vega100 BPS 页不显示 BPSI 强度范围", "不显示", rangeShown ? "显示" : "不显示");
            Check(channelItems.Count == 1 && channelItems[0] == "BPSR" && selected == "BPSR",
                "ui.bps.channel", "Vega100 测试通道只有 BPSR 且已选中",
                "BPSR", $"[{string.Join("/", channelItems)}] selected={selected}");
        }
        else
        {
            Check(rangeShown, "ui.bps.range", $"{tool} BPS 页仍显示 BPSI 强度范围", "显示", rangeShown ? "显示" : "不显示");
            Check(channelItems.Contains("BPSI") && channelItems.Contains("BPSR"),
                "ui.bps.channel", $"{tool} 测试通道仍包含 BPSR 与 BPSI",
                "BPSR+BPSI", string.Join("/", channelItems));
        }

        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
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

    private void OpenDiagnosticsTree()
    {
        var diagButton = Waits.ForEnabled(MainWindow!, "DiagnosticsButton", TimeSpan.FromSeconds(60), Log);
        Assert.True(diagButton is not null, "DiagnosticsButton 未就绪");
        InvokeSafely(diagButton!);
        Log.Step("已打开诊断导航树");

        var deviceNode = Waits.ForElement(MainWindow!, "设备诊断", TimeSpan.FromSeconds(30), Log)
            ?? Waits.ForElement(MainWindow!, "DeviceDiagnostics", TimeSpan.FromSeconds(5), Log);
        Assert.True(deviceNode is not null, "诊断树未找到设备诊断节点");
        try
        {
            deviceNode!.Patterns.ExpandCollapse.Pattern.Expand();
        }
        catch
        {
            Log.Step("设备诊断展开失败，可能已展开");
        }

        Thread.Sleep(800);
    }

    private void OpenLeaf(string chineseId, string englishId, params string[] readyTexts)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var node = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(chineseId))
                ?? MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(englishId));
            if (node is null)
            {
                Log.Step($"第 {attempt} 次未找到节点 {chineseId}/{englishId}");
                Thread.Sleep(1000);
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

            Log.Step($"双击 {chineseId} name='{UiProps.Name(node)}'");
            node.DoubleClick();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(20))
            {
                Dialogs.HandleAll(MainWindow!);
                if (readyTexts.Any(text => FindText(MainWindow!, text) is not null))
                {
                    SelectDocument(chineseId, englishId);
                    Log.Step($"已打开 {chineseId}");
                    return;
                }

                Thread.Sleep(1000);
            }
        }

        UiTreeDumper.Dump(MainWindow!, maxDepth: 3, Log);
        Assert.Fail($"未打开诊断页 {chineseId}");
    }

    private void SelectDocument(params string[] names)
    {
        try
        {
            var tabs = MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            foreach (var tab in tabs)
            {
                var name = UiProps.Name(tab);
                if (names.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase)))
                {
                    tab.Patterns.SelectionItem.Pattern.Select();
                    Thread.Sleep(500);
                    return;
                }
            }
        }
        catch
        {
            // 页签选择失败不掩盖后续控件查找
        }
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

    private static AutomationElement? FindNamed(AutomationElement root, params string[] names)
    {
        try
        {
            foreach (var el in root.FindAllDescendants())
            {
                var name = UiProps.Name(el);
                if (names.Any(n => string.Equals(name, n, StringComparison.Ordinal)))
                {
                    return el;
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

    private static AutomationElement? FindComboBoxByLabel(AutomationElement root, string labelText)
    {
        var label = FindText(root, labelText);
        if (label is null)
        {
            return null;
        }

        try
        {
            label.Patterns.ScrollItem.Pattern.ScrollIntoView();
            Thread.Sleep(300);
        }
        catch
        {
            // 不支持滚动则按当前坐标匹配
        }

        var labelTop = label.BoundingRectangle.Top;
        AutomationElement? best = null;
        var bestDelta = double.MaxValue;
        foreach (var combo in root.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)))
        {
            var top = combo.BoundingRectangle.Top;
            if (top <= 0)
            {
                continue;
            }

            var delta = Math.Abs(top - labelTop);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = combo;
            }
        }

        return bestDelta <= 80 ? best : null;
    }

    private static List<string> ReadComboItems(AutomationElement combo)
    {
        try
        {
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(600);
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

    private static string ReadSelectedToolType()
    {
        var exe = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_EXE");
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "";
        }

        var config = Path.Combine(Path.GetDirectoryName(exe)!, "Resources", "ComponentConfig.json");
        var text = File.ReadAllText(config);
        var match = Regex.Match(text, "\"SelectedToolType\"\\s*:\\s*\"([^\"]+)\"");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string RequireEnv(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.False(string.IsNullOrWhiteSpace(value), $"未设置 {name}");
        return value!;
    }

    private static JsonElement FindMapping(JsonElement mappings, string condition)
    {
        foreach (var mapping in mappings.EnumerateArray())
        {
            if (mapping.GetProperty("Condition").GetString() == condition)
            {
                return mapping;
            }
        }

        throw new InvalidOperationException($"OpticsGearConfig 没有 Condition={condition}");
    }

    private static List<string> NamesOf(JsonElement mapping, string property)
    {
        if (!mapping.TryGetProperty(property, out var array))
        {
            return new List<string>();
        }

        return array.EnumerateArray().Select(item => item.GetString() ?? "").ToList();
    }

    private static List<string> PositionNames(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("Configurations").EnumerateArray()
            .Select(item => item.GetProperty("Name").GetString() ?? "")
            .ToList();
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
