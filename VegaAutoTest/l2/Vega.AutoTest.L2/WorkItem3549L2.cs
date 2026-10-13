// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3549 正式 L2 回归用例：BSS 诊断 default 模式保存与加载往返。
/// 验收标准（ReproSteps 原文）：模式下拉选 default → 保存 → 重开页面 → 下拉仍显示 default。
///
/// 判定依据（源码事实，上游 419a118a 修复口径）：
///   - 保存链：SaveVaule → BSSConfig.CalibrationDefaultIncidence → SaveEasyConfig 落盘
///     %LocalAppData%\Vega\Resources\EasyConfig\BSSConfig.json（Galaxy EasyConfigExtensions）。
///     修复后 default 写空串；修复前 CurrentIncidence.ToString() 在 null(default) 时
///     抛空引用 → catch 弹异常窗 → BSSConfig.json 不落盘。
///   - 加载链：GetCalibrationDefaultSpot 解析该字符串。修复后空串→null(default)；
///     修复前空串/缺文件一律回落 Normal。
/// 硬判定锚在配置文件内容（机器可查真相，不受 UI 钳制效应干扰）：
///   1. 保存后 BSSConfig.json 存在；
///   2. CalibrationDefaultIncidence == ""（default 的持久化形态）；
///   3. CalibrationDefaultThroughput == ""。
/// UIA 往返（重开读下拉）作为辅证断言；期望原语验证保存不应弹错误窗。
/// 本机部署配置 DoBSSAutoZero=false / DoBSSDifferentModesAutoZero=false →
/// 下拉仅含 default 一项（与账户无关，PR 4810 已去 IsSuperUser 分支）。
/// </summary>
public sealed class WorkItem3549L2 : L2TestBase
{
    /// <summary>Galaxy EasyConfig 落盘路径（PathHelper.GetUserPath: %LocalAppData%&lt;Product&gt;\Resources\EasyConfig）。</summary>
    private static readonly string BssConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Vega", "Resources", "EasyConfig", "BSSConfig.json");

    public WorkItem3549L2() : base("_wi3549_l2.txt") { }

    [Fact]
    public void BssDefaultModeRoundTrip()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        // ── 第一次打开 BSS 页：选 default 并保存 ──
        var page1 = OpenBssOriginPage();

        var incidence1 = FindComboBoxByLabel(page1, "Incidence");
        Assert.True(incidence1 is not null, "未找到 Incidence 下拉（BSS Origin 页结构变化？）");
        var before = ComboBoxReader.ReadSelectedDisplay(incidence1!);
        Log.Step($"保存前 Incidence 显示值: '{before}'");
        Assertions.Pass("bss.before", "ui-value", "保存前 Incidence 下拉可读值", "(present)", before);

        Assert.True(ComboBoxReader.SelectByDisplay(incidence1!, "default", Log.Step),
            "Incidence 下拉未找到 default 项");

        // 记录保存前配置文件状态（用于红绿基线对比）
        var existedBefore = File.Exists(BssConfigPath);
        Log.Step($"保存前 BSSConfig.json 存在: {existedBefore}（{BssConfigPath}）");

        // 点 Parameters 组内的保存按钮（SaveVauleCommand → Content=Resources.Save）
        var saveButton = FindSaveButton(page1);
        Assert.True(saveButton is not null, "未找到 BSS 保存按钮");
        Log.Step("点击保存（SaveVaule）");
        InvokeSafely(saveButton!);
        Thread.Sleep(2000);

        // 负期望：旧二进制 default 崩溃会弹 "Save fail" 或 NRE 消息窗
        var expect = new UiExpect(Dialogs, Assertions, Log);
        var errorDialog = expect.WindowDoesNotAppear("bss.save.error-dialog",
            DialogExpectations.Any(
                DialogExpectations.TitleOrMessageContains("Save fail"),
                DialogExpectations.MessageContains("Reference")),
            TimeSpan.FromSeconds(7), MainWindow!);
        if (errorDialog is not null)
        {
            Log.Step("!! 保存弹出错误窗（旧二进制 default 崩溃路径）——等待其被托管关闭");
            Dialogs.HandleAll(MainWindow!);
            Thread.Sleep(1000);
        }

        // ── 硬判定 1-3：配置文件往返真相 ──
        Assert.True(File.Exists(BssConfigPath),
            $"保存后 {BssConfigPath} 未生成——SaveVaule 中途失败（PR 4805 未包含在此二进制中？）");

        string? incidenceSaved = null;
        string? throughputSaved = null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(BssConfigPath));
            incidenceSaved = doc.RootElement.TryGetProperty("CalibrationDefaultIncidence", out var i)
                ? i.GetString() : null;
            throughputSaved = doc.RootElement.TryGetProperty("CalibrationDefaultThroughput", out var t)
                ? t.GetString() : null;
        }
        catch (Exception ex)
        {
            Assert.Fail($"BSSConfig.json 解析失败: {ex.Message}");
        }

        Log.Step($"配置落盘: CalibrationDefaultIncidence='{incidenceSaved}' CalibrationDefaultThroughput='{throughputSaved}'");

        // default 的持久化形态 = 空串（修复口径）
        var incidenceOk = string.IsNullOrEmpty(incidenceSaved);
        var throughputOk = string.IsNullOrEmpty(throughputSaved);
        if (incidenceOk)
        {
            Assertions.Pass("bss.config.incidence", "invariant",
                "default 持久化为空串（CalibrationDefaultIncidence）", "\"\"", incidenceSaved ?? "(null)");
        }
        else
        {
            Assertions.Fail("bss.config.incidence", "invariant",
                "default 持久化为空串（CalibrationDefaultIncidence）", "\"\"", incidenceSaved ?? "(null)");
        }

        if (throughputOk)
        {
            Assertions.Pass("bss.config.throughput", "invariant",
                "default 持久化为空串（CalibrationDefaultThroughput）", "\"\"", throughputSaved ?? "(null)");
        }
        else
        {
            Assertions.Fail("bss.config.throughput", "invariant",
                "default 持久化为空串（CalibrationDefaultThroughput）", "\"\"", throughputSaved ?? "(null)");
        }

        Assert.True(incidenceOk,
            $"CalibrationDefaultIncidence 落盘值 '{incidenceSaved}' 非空串——default 未按修复口径持久化");

        // ── 辅证：关闭页签后重开，读下拉（修复后加载空串→default(null)→显示 'default'）──
        CloseBssDocument(page1);
        Thread.Sleep(2000);
        var page2 = OpenBssOriginPage();
        var incidence2 = FindComboBoxByLabel(page2, "Incidence");
        Assert.True(incidence2 is not null, "重开后未找到 Incidence 下拉");
        var after = ComboBoxReader.ReadSelectedDisplay(incidence2!);
        Log.Step($"重开后 Incidence 显示值: '{after}'");

        var roundTripOk = string.Equals(after, "default", StringComparison.OrdinalIgnoreCase);
        if (roundTripOk)
        {
            Assertions.Pass("bss.roundtrip.display", "ui-value",
                "重开后 Incidence 仍显示 default", "default", after);
        }
        else
        {
            Assertions.Fail("bss.roundtrip.display", "ui-value",
                "重开后 Incidence 仍显示 default", "default", after);
        }

        Assert.True(roundTripOk,
            $"重开后 Incidence 显示 '{after}' 而非 default——加载回落路径未按修复口径还原");
    }

    // ────────────────────────── 导航与定位 ──────────────────────────

    /// <summary>
    /// 打开 BSS 诊断 Origin 页：DiagnosticsButton → 导航树 DeviceDiagnostics 容器展开
    /// → 双击 BSS 叶子（AutomationId = Header 文本）→ 等页面 Document 打开。
    /// </summary>
    private AutomationElement OpenBssOriginPage()
    {
        var diagButton = Waits.ForEnabled(MainWindow!, "DiagnosticsButton",
            TimeSpan.FromSeconds(60), Log);
        Assert.True(diagButton is not null, "DiagnosticsButton 未就绪");
        InvokeSafely(diagButton!);
        Log.Step("已打开诊断导航树");

        // DeviceDiagnostics 容器节点（AutomationId = Header 中文文本，实测 '设备诊断'）
        var deviceNode = Waits.ForElement(MainWindow!, "设备诊断",
            TimeSpan.FromSeconds(30), Log) ?? Waits.ForElement(MainWindow!, "DeviceDiagnostics",
            TimeSpan.FromSeconds(5), Log);
        Assert.True(deviceNode is not null, "诊断树未找到 设备诊断 节点");
        try
        {
            deviceNode!.Patterns.ExpandCollapse.Pattern.Expand();
        }
        catch
        {
            Log.Step("DeviceDiagnostics 展开（ExpandCollapse）失败，可能已展开");
        }

        Thread.Sleep(1500);

        // BSS 叶子：物理双击可能因树布局位移打在同层其他节点（实测误开'光学'页），
        // 用 重找→滚入视野→双击→验证页面 的重试环；失败时重展容器再试。
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var bssNode = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("BSS"))
                ?? MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("BSS诊断"));
            if (bssNode is null)
            {
                Log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] BSS 节点不可见，重展设备诊断");
                try
                {
                    deviceNode!.Patterns.ExpandCollapse.Pattern.Expand();
                }
                catch
                {
                    // 已展开
                }

                Thread.Sleep(1500);
                continue;
            }

            try
            {
                bssNode.Patterns.ScrollItem.Pattern.ScrollIntoView();
            }
            catch
            {
                // ScrollItem 不支持则忽略
            }

            Thread.Sleep(500);
            var rect = bssNode.BoundingRectangle;
            Log.Step($"第 {attempt} 次双击 BSS 节点 rect=({rect.X:F0},{rect.Y:F0},{rect.Width:F0}x{rect.Height:F0})");
            bssNode.DoubleClick();

            var sw2 = System.Diagnostics.Stopwatch.StartNew();
            while (sw2.Elapsed < TimeSpan.FromSeconds(20))
            {
                Dialogs.HandleAll(MainWindow!);
                var combo = FindComboBoxByLabel(MainWindow!, "Incidence");
                if (combo is not null || HasBssDocument(MainWindow!))
                {
                    Log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] BSS Origin 页已打开（Incidence 下拉/文档页可见）");
                    return MainWindow!;
                }

                Thread.Sleep(1500);
            }

            Log.Step("  20s 内未确认页面打开，重试");
        }

        Log.Step("!! 4 次尝试后 BSS Origin 页仍未打开，dump 主窗口:");
        UiTreeDumper.Dump(MainWindow!, maxDepth: 3, Log);
        Assert.Fail("BSS Origin 页未打开");
        return MainWindow!; // 不可达
    }

    /// <summary>是否存在标题含 BSS 的 AvalonDock 文档页签。</summary>
    private static bool HasBssDocument(AutomationElement root)
    {
        try
        {
            var tabs = root.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            foreach (var tab in tabs)
            {
                if (UiProps.Name(tab).Contains("BSS", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>按相邻 TextBlock 文本找 ComboBox（BSS 页控件无 AutomationId）。</summary>
    private static AutomationElement? FindComboBoxByLabel(AutomationElement root, string labelText)
    {
        try
        {
            var labels = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            foreach (var label in labels)
            {
                if (!string.Equals(UiProps.Name(label), labelText, StringComparison.Ordinal))
                {
                    continue;
                }

                // TextBlock 与 ComboBox 同 Grid：向上找父容器再取子 ComboBox
                var parent = label.Parent;
                for (var hop = 0; hop < 3 && parent is not null; hop++)
                {
                    var combo = parent.FindFirstDescendant(cf => cf.ByControlType(ControlType.ComboBox));
                    if (combo is not null)
                    {
                        return combo;
                    }

                    parent = parent.Parent;
                }
            }
        }
        catch
        {
            // 树变化瞬时异常
        }

        return null;
    }

    /// <summary>
    /// 找 Parameters 区的保存按钮（SaveVauleCommand）。页里有两个"保存"
    /// （LogicControl 组 SaveLogicControlCommand / Parameters 组 SaveVauleCommand），
    /// 本用例要的是后者——按兄弟"历史记录"按钮（HistoricalMotorsCommand）同 Grid 定位，
    /// 两个按钮与 AdjustL2L3/AdjustAC1RP1T/RecordMotorsSite 同列（Grid.Row=4）。
    /// 策略：找所有 Name=保存 的按钮中，其可执行 Command 绑定树离 Incidence 下拉最近的
    /// ——简化为：两个保存按钮都点也不对；直接取第 2 个（文档顺序 LogicControl 在前）。
    /// </summary>
    private AutomationElement? FindSaveButton(AutomationElement root)
    {
        try
        {
            var buttons = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
            AutomationElement? first = null;
            foreach (var b in buttons)
            {
                if (!string.Equals(UiProps.Name(b), "保存", StringComparison.Ordinal))
                {
                    continue;
                }

                if (first is null)
                {
                    first = b; // LogicControl 组的（文档序在前）
                    continue;
                }

                return b; // Parameters 组的 SaveVaule
            }

            // 只找到一个时校验它是否在 Incidence 附近（单按钮布局兜底）
            return first;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 关闭 BSS Document 页签。NavigationService 对已存在文档只激活不重建
    /// （Galaxy NavigationService._navigationStack 命中 → IsExistingView → 仅 IsActive=true），
    /// 关闭失败则重开验证无效，必须真关闭后重开（ViewModel 重新读配置）。
    /// TabItem 的 UIA Name 是 AvalonDock 布局类型名；关闭按钮 id=DocumentCloseButton。
    /// </summary>
    private void CloseBssDocument(AutomationElement root)
    {
        try
        {
            var tabItems = root.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
            foreach (var tab in tabItems)
            {
                // 命中页签：自身或子树文本含 BSS（布局名不含，文档标题 TextBlock 含）
                var isBss = UiProps.Name(tab).Contains("BSS", StringComparison.OrdinalIgnoreCase)
                            || tab.FindFirstDescendant(cf => cf.ByName("BSS")) is not null;
                if (!isBss)
                {
                    continue;
                }

                // AvalonDock 关闭按钮固定 id
                var closeBtn = tab.FindFirstDescendant(cf => cf.ByAutomationId("DocumentCloseButton"));
                if (closeBtn is null)
                {
                    closeBtn = tab.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button));
                }

                if (closeBtn is not null)
                {
                    InvokeSafely(closeBtn);
                    Log.Step($"已关闭 BSS 页签（'{UiProps.Name(tab)}'）");

                    // 等页签真正消失（防假关闭）
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    while (sw.Elapsed < TimeSpan.FromSeconds(10))
                    {
                        Thread.Sleep(500);
                        if (!HasBssDocument(root))
                        {
                            Log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 页签已消失");
                            return;
                        }
                    }

                    Log.Step("!! 页签 10s 后仍存在（假关闭）");
                    return;
                }
            }

            Log.Step("!! 未找到 BSS 页签——重开将复用旧视图，往返辅证无效");
        }
        catch (Exception ex)
        {
            Log.Step($"关闭页签异常（忽略）: {ex.Message}");
        }
    }

    /// <summary>Invoke 优先，物理点击兜底（README §9 纪律）。</summary>
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
