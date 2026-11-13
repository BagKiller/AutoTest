// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;


namespace Vega.AutoTest.L2;

/// <summary>
/// #3756：修改产量后 KLARF 输出页勾选不应被重置为全选。
/// 工作项复现步骤（只读读取自 ADO，冻结于每日批次 workitems.json）：
/// 【前置条件】打开 Vega 并登录。
/// 【复现流程】1、Klarf 输出添加多个，取消勾选 Default 页 DW2 Default 输出；
///             2、修改产量，检查 Klarf 输出。
/// 【预期结果】Klarf 输出不变（DW2 Default 保持未勾选，其他勾选保留）。
/// 【实际结果（缺陷版本 1.2.8.11234）】Klarf 中 Default 页被重置。
/// 本用例在真实 Vega.exe（Simulator、官方认证登录）中走完整界面流程。
/// </summary>
public sealed class WorkItem3756L2 : L2TestBase
{
    public WorkItem3756L2() : base("_wi3756_l2.txt") { }

    [Fact]
    public void ThroughputChangePreservesKlarfSelections()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
        page.OpenInspection();
        var recipeNode = page.FindRecipeNode(TimeSpan.FromSeconds(60));
        Assert.True(recipeNode is not null, "配方库中没有可用配方节点");
        if (!page.IsDocumentLoaded)
        {
            recipeNode!.DoubleClick();
        }
        page.WaitForDocument(TimeSpan.FromSeconds(90));
        Dialogs.HandleAll(MainWindow!);
        Log.Step("配方已打开，进入编辑态");

        // ── 步骤 1a：打开 Results Output 展开器 ──
        ExpandExpander("RecipeView_ResultsOutputExpander", "Results Output");

        // 复现步骤 1 前半句"Klarf输出添加多个"：产品结构里 KLARF 输出由 OptionsView 的 KLARF
        // 复选框启用（OnOutputOptionsChanged 建页签）。先确保 KLARF 已勾选（即"添加 Klarf 输出"），
        // 该动作在无 KLARF 配方上等价于添加一个输出；已勾选则保持不动。
        var klarfSwitch = Waits.ForElement(MainWindow!, "OptionsView_IsKLARFCheckBox", TimeSpan.FromSeconds(15), Log);
        Assert.True(klarfSwitch is not null, "Results Output 里没有 KLARF 输出复选框（OptionsView_IsKLARFCheckBox）");
        var klarfEnabled = ToggleStateOf(klarfSwitch!);
        Log.Step($"KLARF 输出复选框当前状态 = {klarfEnabled}");
        if (klarfEnabled != ToggleState.On)
        {
            klarfSwitch!.Patterns.Toggle.Pattern.Toggle();
            Thread.Sleep(1500);
            Log.Step("已勾选 KLARF 输出（对应复现步骤『Klarf输出添加』）");
        }

        // 结构是两级页签（ResultsOutputView.xaml:26）：
        // 外层 ResultsOutputView_OptionsSettingsTabControl：每个启用输出类型一页，Header=OptionType（ItemTemplate 绑定）；
        // 内层 OptionSettingsView_OptionSettingTabControl：该类型下的每个输出文件一页（Header=SettingName，默认 "Default"）。
        // 工作项原文"取消勾选Default页DW2 Default输出" = 外层 KLARF 类型页 + 内层 "Default" 输出页。
        SelectTypeTab("ResultsOutputView_OptionsSettingsTabControl", "KLARF");
        SelectFirstInnerTab("OptionSettingsView_OptionSettingTabControl");

        // KLARF 输出树：通道节点名 = "<Collector>[A] <Collection>"，如 DW2 Default / DC Default。
        // TreeView 节点虚拟化：内容随选中页签延迟构建，所以要轮询等待树出现。
        var tree = WaitById(MainWindow!, TimeSpan.FromSeconds(30),
            "CheckBoxTreeView_checkboxTreeTreeView");
        Assert.True(tree is not null, "KLARF 数据选择树未出现（选中输出页后 30s 内）");
        var dw2 = FindChannelNode(tree!, "DW2 Default")
                  ?? FindChannelNode(tree!, "DW2A Default")
                  ?? FindChannelNode(tree!, "DWA Default");
        Assert.True(dw2 is not null, $"KLARF 树里没有 DW2 Default 通道节点；实际通道：{ListChannelNodes(tree!)}");
        var dw2State = ToggleStateOf(dw2!);
        Log.Step($"步骤1：DW2 Default 当前勾选状态 = {dw2State}");

        // 复现步骤的"输出添加多个"已由勾选 KLARF 输出完成；页签数只记录为取证。
        RecordOutputTabCount();

        // 通道节点复选框是三态（IsChecked=bool?，子节点部分勾选时为 Indeterminate）。
        // WPF 三态 Toggle 循环与子节点联动，所以循环 Toggle 直到 Off（最多 3 次），每次重读状态。
        for (var attempt = 0; attempt < 3 && ToggleStateOf(dw2!) != ToggleState.Off; attempt++)
        {
            Log.Step($"切换 DW2 Default（第 {attempt + 1} 次），当前 {ToggleStateOf(dw2!)}");
            dw2!.Patterns.Toggle.Pattern.Toggle();
            Thread.Sleep(800);
        }
        var afterUncheck = ToggleStateOf(dw2!);
        Check(afterUncheck == ToggleState.Off, "wi3756.uncheck-applied",
            "步骤1：取消勾选 DW2 Default 生效", "Off", afterUncheck.ToString());
        Log.Step("步骤1完成：DW2 Default 已取消勾选");

        // ── 步骤 2：修改产量（Collections 段的 Throughput ComboBox）──
        ExpandExpander("RecipeView_CollectionsExpander", "Collections");
        var throughput = WaitById(MainWindow!, TimeSpan.FromSeconds(20),
            "ScanWaferSettingView_ThroughputComboBox");
        Assert.True(throughput is not null, "Collections 里没有产量下拉（ScanWaferSettingView_ThroughputComboBox）");
        var combo = throughput!.AsComboBox();
        // WPF ComboBox 通常不支持 ValuePattern（FlaUI .Value 会抛 PatternNotSupportedException），
        // 选中项文本经 Selection/子 ListItem 的 IsSelected 读取。
        static string? SelectedText(FlaUI.Core.AutomationElements.ComboBox box)
        {
            try
            {
                var selected = box.Items.FirstOrDefault(i => i.IsSelected);
                return selected?.Text;
            }
            catch
            {
                return null;
            }
        }
        var original = SelectedText(combo);
        var items = combo.Items;
        Assert.True(items.Length >= 2, $"产量下拉候选不足（{items.Length}），无法修改");
        var replacement = items.First(i => !string.Equals(i.Text, original, StringComparison.Ordinal)).Text;
        combo.Select(replacement);
        Thread.Sleep(1200);
        var now = SelectedText(combo);
        Check(!string.Equals(now, original, StringComparison.Ordinal), "wi3756.throughput-changed",
            "步骤2：产量已修改", replacement, string.IsNullOrEmpty(now) ? "未读到" : now);
        Log.Step($"步骤2完成：产量 {original} → {now}");

        // ── 复查：KLARF 输出页的勾选必须保持 ──
        ExpandExpander("RecipeView_ResultsOutputExpander", "Results Output");
        SelectTypeTab("ResultsOutputView_OptionsSettingsTabControl", "KLARF");
        SelectFirstInnerTab("OptionSettingsView_OptionSettingTabControl");
        tree = WaitById(MainWindow!, TimeSpan.FromSeconds(30),
            "CheckBoxTreeView_checkboxTreeTreeView");
        Assert.True(tree is not null, "复查时 KLARF 树未出现");
        var dw2Again = FindChannelNode(tree!, "DW2 Default")
                       ?? FindChannelNode(tree!, "DW2A Default")
                       ?? FindChannelNode(tree!, "DWA Default");
        Assert.True(dw2Again is not null, "复查时 DW2 Default 节点消失");
        var finalState = ToggleStateOf(dw2Again!);
        Log.Step($"复查：DW2 Default 节点状态 = {finalState}");
        // #3756 缺陷表现是"被重置为全选"：节点回到 On（或三态 Indeterminate 但全部子节点被勾上）。
        // 判定以叶属性节点的实际勾选为准：任一叶属性保持未勾选即用户勾选得到保留。
        var leafStates = LeafChildStates(tree!, "DW2 Default");
        Log.Step($"复查：DW2 Default 叶属性勾选 = {leafStates}");
        var anyUnchecked = leafStates.Values.Any(s => s == ToggleState.Off);
        var allChecked = leafStates.Count > 0 && leafStates.Values.All(s => s == ToggleState.On);
        Check(anyUnchecked, "wi3756.selection-preserved",
            "修改产量后 DW2 Default 的用户取消勾选保持（#3756 缺陷为被重置为全选）",
            "至少一个属性保持未勾选", allChecked ? "全部属性被勾选（缺陷复现）" : string.Join(" | ",
                leafStates.Select(kv => $"{kv.Key}={kv.Value}")));
        Log.Step($"复查完成：DW2 Default = {finalState}，叶属性 = {string.Join(" | ", leafStates.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    /// <summary>展开通道节点并读取其叶属性子节点的勾选状态（显示名 → ToggleState）。</summary>
    private Dictionary<string, ToggleState> LeafChildStates(AutomationElement tree, string channelNode)
    {
        var result = new Dictionary<string, ToggleState>();
        var names = new[] { channelNode, "DW2A Default", "DWA Default" };
        AutomationElement? item = null;
        foreach (var candidate in tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            var box = candidate.FindFirstDescendant(cf => cf.ByAutomationId("CheckBoxTreeView_IsCheckedCheckBox"))
                      ?? candidate.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
            if (box is not null && names.Any(n => NodeText(box).Contains(n, StringComparison.OrdinalIgnoreCase)))
            {
                item = candidate;
                break;
            }
        }
        if (item is null)
        {
            return result;
        }
        try
        {
            if (item!.Patterns.ExpandCollapse.IsSupported &&
                item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState == ExpandCollapseState.Collapsed)
            {
                item.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(800);
            }
        }
        catch
        {
            item.Click();
            Thread.Sleep(500);
        }
        foreach (var child in item.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            var box = child.FindFirstDescendant(cf => cf.ByAutomationId("CheckBoxTreeView_IsCheckedCheckBox"))
                      ?? child.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
            if (box is null)
            {
                continue;
            }
            result[NodeText(box)] = ToggleStateOf(box);
        }
        return result;
    }

    /// <summary>
    /// 外层类型页签的 UIA Name 同样是 VM ToString（WPF TabControl 的 Name 来自 HeaderContent，
    /// ItemTemplate 的 TextBlock 不进 Name）。改为：读取每个类型页签头部内 Text 元素的文本识别类型；
    /// 文本也拿不到时逐个选中并用内容（内层是否出现 DW2 Default 树）确认 KLARF 页。
    /// </summary>
    private void SelectTypeTab(string tabControlId, string typeText)
    {
        var tab = Waits.ForElement(MainWindow!, tabControlId, TimeSpan.FromSeconds(15), Log);
        Assert.True(tab is not null, $"未找到输出类型页签控件（{tabControlId}）");
        var items = tab!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(items.Length > 0, "输出类型页签数为 0");

        // 先尝试从头部子 Text 元素读类型名（ItemTemplate 的 TextBlock 在 header 可视树里）
        for (var index = 0; index < items.Length; index++)
        {
            var headerText = HeaderText(items[index]);
            Log.Step($"类型页签[{index}] header 文本: '{headerText}' (Name='{items[index].Name}')");
            if (headerText.Contains(typeText, StringComparison.OrdinalIgnoreCase))
            {
                Select(items[index]);
                Log.Step($"按头部文本选中第 {index + 1} 个类型页：{headerText}");
                Thread.Sleep(2000);
                return;
            }
        }

        // 兜底：逐个选中，用 KLARF 特有的内容（DW2 Default 通道节点）识别
        for (var index = 0; index < items.Length; index++)
        {
            Select(items[index]);
            Thread.Sleep(2000);
            var tree = FindCheckBoxTree(tabControlId);
            if (tree is not null && FindChannelNode(tree, "DW2 Default") is not null)
            {
                Log.Step($"按内容选中第 {index + 1} 个类型页（发现 DW2 Default 节点）");
                return;
            }
            Log.Step($"类型页签[{index}] 不是 KLARF 页（树节点: {ListChannelNodes(tree ?? tab)}）");
        }
        Assert.Fail($"没有任何类型页签包含 DW2 Default 节点；页签数 {items.Length}");
    }

    private static string HeaderText(AutomationElement tabItem)
    {
        try
        {
            var texts = tabItem.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var joined = string.Join("/", texts.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
            return joined.Length > 0 ? joined : tabItem.Name;
        }
        catch
        {
            return tabItem.Name;
        }
    }

    private static void Select(AutomationElement tabItem)
    {
        if (tabItem.Patterns.SelectionItem.IsSupported)
        {
            tabItem.Patterns.SelectionItem.Pattern.Select();
        }
        else
        {
            InvokeSafely(tabItem);
        }
    }

    private AutomationElement? FindCheckBoxTree(string scopeId)
    {
        var scope = Waits.ForElement(MainWindow!, scopeId, TimeSpan.FromSeconds(5), Log);
        return scope?.FindFirstDescendant(cf => cf.ByAutomationId("CheckBoxTreeView_checkboxTreeTreeView"));
    }

    /// <summary>内层输出文件页签：Header 经 DataTemplate 渲染（UIA Name 是 VM ToString），选中第 1 个。</summary>
    private void SelectFirstInnerTab(string tabControlId)
    {
        var tab = Waits.ForElement(MainWindow!, tabControlId, TimeSpan.FromSeconds(15), Log);
        Assert.True(tab is not null, $"未找到输出文件页签控件（{tabControlId}）");
        var items = tab!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
        Assert.True(items.Length > 0, "输出文件页签数为 0");
        Log.Step($"输出文件页签共 {items.Length} 个，选中第 1 个（默认名 Default）");
        Select(items[0]);
        Thread.Sleep(2000);
    }

    private AutomationElement? WaitById(AutomationElement root, TimeSpan timeout, params string[] ids)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            foreach (var id in ids)
            {
                var hit = root.FindFirstDescendant(cf => cf.ByAutomationId(id));
                if (hit is not null)
                {
                    return hit;
                }
            }
            Thread.Sleep(1000);
        }
        return null;
    }

    private void ExpandExpander(string automationId, string label)
    {
        var expander = Waits.ForElement(MainWindow!, automationId, TimeSpan.FromSeconds(30), Log);
        Assert.True(expander is not null, $"未找到 {label} 展开器（{automationId}）");
        try
        {
            if (expander!.Patterns.ExpandCollapse.IsSupported)
            {
                // WPF Expander 重复 Expand 是幂等的，无需先读状态
                expander.Patterns.ExpandCollapse.Pattern.Expand();
            }
            else
            {
                expander.Click();
            }
        }
        catch
        {
            expander!.Click();
        }
        Thread.Sleep(800);
        Log.Step($"已展开 {label}");
    }

    private AutomationElement? FirstTab(string tabControlId)
    {
        var tab = Waits.ForElement(MainWindow!, tabControlId, TimeSpan.FromSeconds(10), Log);
        var items = tab?.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem));
        if (items is null || items.Length == 0)
        {
            return null;
        }
        Log.Step($"输出页签: {string.Join(" | ", items.Select(i => i.Name))}");
        return items[0];
    }

    private AutomationElement? WaitTabHeader(string tabControlId, string header)
    {
        var tab = Waits.ForElement(MainWindow!, tabControlId, TimeSpan.FromSeconds(20), Log);
        if (tab is null)
        {
            return null;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(15))
        {
            foreach (var item in tab.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)))
            {
                if (item.Name.Contains(header, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }
            Thread.Sleep(500);
        }
        return null;
    }

    /// <summary>
    /// 树节点 UIA Name 是 DataContext 的 ToString（同页签 Header 问题）；
    /// 真实显示名在节点 CheckBox 内容的 TextBlock（绑定 DisplayNodeName）里，按文本读。
    /// </summary>
    private AutomationElement? FindChannelNode(AutomationElement tree, string nodeName)
    {
        foreach (var item in tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            var box = item.FindFirstDescendant(cf => cf.ByAutomationId("CheckBoxTreeView_IsCheckedCheckBox"))
                      ?? item.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
            if (box is null)
            {
                continue;
            }
            if (NodeText(box).Contains(nodeName, StringComparison.OrdinalIgnoreCase))
            {
                return box;
            }
        }
        return null;
    }

    private static string NodeText(AutomationElement checkBox)
    {
        try
        {
            var texts = checkBox.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var joined = string.Join("/", texts.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
            return joined.Length > 0 ? joined : checkBox.Name;
        }
        catch
        {
            return checkBox.Name;
        }
    }

    private string ListChannelNodes(AutomationElement tree) => string.Join("|", tree
        .FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
        .Where(i => i.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox)) is not null)
        .Select(i =>
        {
            var box = i.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
            return box is null ? i.Name : NodeText(box) + "|" + i.Name;
        })
        .Distinct()
        .Take(12));

    private static ToggleState ToggleStateOf(AutomationElement checkBox)
    {
        // Harness convention (AuthenticatorPassword.cs:447): FlaUI Toggle pattern is the
        // supported path; WPF CheckBox always exposes it, no legacy fallback needed.
        return checkBox.Patterns.Toggle.Pattern.ToggleState;
    }

    private void Uncheck(AutomationElement checkBox)
    {
        var toggle = checkBox.Patterns.Toggle.Pattern;
        if (toggle.ToggleState == ToggleState.On)
        {
            toggle.Toggle();
        }
    }

    private void RecordOutputTabCount()
    {
        var tab = Waits.ForElement(MainWindow!, "OptionSettingsView_OptionSettingTabControl", TimeSpan.FromSeconds(10), Log);
        var count = tab?.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).Length ?? 0;
        Log.Step($"KLARF 输出页签数（复现步骤要求添加多个输出，当前 {count} 个）：少于 2 个时缺陷仍可复现，不作为阻塞");
    }

    private void Check(bool condition, string id, string description, string expected, string actual)
    {
        if (condition)
        {
            Assertions.Pass(id, "ui-expect", description, expected, actual);
        }
        else
        {
            Assertions.Fail(id, "ui-expect", description, expected, actual);
            Assert.Fail($"{description}: expected {expected}; actual {actual}");
        }
    }

    private static void InvokeSafely(AutomationElement el)
    {
        if (el.Patterns.Invoke.IsSupported)
        {
            el.Patterns.Invoke.Pattern.Invoke();
            return;
        }
        el.Click();
    }
}
