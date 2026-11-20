using System.Drawing;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3774 单独复验。复现步骤与断言与每日批次生成用例相同。
/// 上次失败是按英文名 Inspection 点菜单；实测主窗口入口是 AutomationId=InspectionButton。
/// 本文件不改原冻结用例。
/// </summary>
public sealed class WorkItem3774L2 : L2TestBase
{
    private const string ExpectedResult = "新增的缺陷/属性只出现在选择数据列表中，默认不勾选；原有勾选状态保持不变。";

    public WorkItem3774L2() : base("_wi3774_spa_klarf.txt") { }

    [Fact]
    public void AddingSpaPreservesExistingKlarfSelection()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega)
                .WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            PrepareEditor();
            var tree = OpenKlarfSelection();
            ExpandTree(tree);
            var rows = ReadRows(tree);
            var defects = rows.First(r => r.Label == "Defects" || r.Label == "缺陷");
            var channel = rows.Where(r => Contains(r.Descendants, defects.Element))
                .OrderBy(r => r.Descendants.Count).First();
            var channelLeaves = rows.Where(r => r.Descendants.Count == 0
                && Contains(channel.Descendants, r.Element)).ToArray();

            foreach (var row in rows.Where(r => r.Descendants.Count == 0 && r.Box.IsEnabled))
                row.Box.IsChecked = false;
            var selected = channelLeaves.First(r => r.Box.IsEnabled
                && r.Label != "X" && r.Label != "Y");
            selected.Box.IsChecked = true;
            var baseline = Snapshot(tree, leavesOnly: true);
            var baselineAll = Snapshot(tree, leavesOnly: false);
            int checkedCount = baseline.Count(p => p.Value == true);
            Compare("precondition", "KLARF 已有手工勾选项且非全选", "0 < checked < total",
                $"{checkedCount} / {baseline.Count}", checkedCount > 0 && checkedCount < baseline.Count);

            var spa = AddSpa(channel.Label);
            StepEvidence.Capture(MainWindow, "step-1-add-spa");
            var enabled = UiFind.Require(spa, "SPA enabled checkbox",
                automationId: "RecipeExpander_GenericCheckBox").AsCheckBox();
            Compare("spa-enabled", "新增 SPA 已启用", "True", enabled.IsChecked.ToString() ?? "null",
                enabled.IsChecked == true);
            var selector = UiFind.Require(spa, "SPA channel selector",
                controlType: ControlType.ComboBox).AsComboBox();
            string selectedChannel = selector.SelectedItem is { } item ? UiProps.Name(item) : "(none)";
            Compare("spa-channel", "SPA 使用原 KLARF 缺陷通道", channel.Label, selectedChannel,
                selectedChannel == channel.Label);

            tree = OpenKlarfSelection();
            StepEvidence.Capture(MainWindow, "step-2-open-klarf");

            ExpandTree(tree);
            StepEvidence.Capture(MainWindow, "step-3-expand-defects");

            var observed = Snapshot(tree, leavesOnly: false);
            var observedLeaves = Snapshot(tree, leavesOnly: true);
            var added = observed.Where(p => !baselineAll.ContainsKey(p.Key)).ToArray();
            var addedLeaves = observedLeaves.Where(p => !baseline.ContainsKey(p.Key)).ToArray();
            bool unchanged = baseline.All(p => observedLeaves.TryGetValue(p.Key, out var state)
                && state == p.Value);
            bool uncheckedNewItems = added.All(p => p.Value == false);
            StepEvidence.Capture(MainWindow, "step-4-observe-selection");
            try
            {
                bool visibleSpaDefects = addedLeaves.Any(p => IsSpaPath(p.Key));
                Compare("new-items-visible", "新增 SPA 缺陷及属性显示在选择数据中",
                    "At least one new Cluster/Scratch/Slipline attribute",
                    string.Join("; ", addedLeaves.Select(p => p.Key)), visibleSpaDefects);
                string actual = "新增项: " + string.Join("; ", added.Select(p => p.Key + "=" + State(p.Value)))
                    + "; 原有属性: " + string.Join("; ", baseline.Select(p => p.Key + "="
                        + (observedLeaves.TryGetValue(p.Key, out var state) ? State(state) : "missing")));
                Compare("result", ExpectedResult, ExpectedResult, actual,
                    visibleSpaDefects && added.Length > 0 && uncheckedNewItems && unchanged);
            }
            finally
            {
                StepEvidence.Capture(MainWindow, "result");
            }
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private void PrepareEditor()
    {
        if (UiFind.Optional(MainWindow!, "Open recipe editor", automationId: "RecipeView_ListView") is null)
        {
            var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
            page.OpenInspection();
            var recipeNode = page.FindRecipeNode(TimeSpan.FromSeconds(90));
            if (recipeNode is null)
                throw new UiPrerequisiteException("RECIPE_NOT_FOUND", "配方库中没有可用配方节点，无法进入 Recipe 编辑页。");
            recipeNode.DoubleClick();
            UiFind.Wait(MainWindow!, "Recipe element list", TimeSpan.FromSeconds(90),
                automationId: "RecipeView_ListView");
            Dialogs.HandleAll(MainWindow!);
        }

        var output = UiFind.Require(MainWindow!, "Result Output",
            automationId: "RecipeView_ResultsOutputExpander");
        output.Patterns.ExpandCollapse.Pattern.Expand();
        var klarf = UiFind.Wait(output, "KLARF output checkbox",
            automationId: "OptionsView_IsKLARFCheckBox");
        if (!klarf.IsEnabled)
            throw new UiPrerequisiteException("KLARF_NOT_EDITABLE", "Result Output 的 KLARF 复选框不可编辑，当前配方未进入可编辑状态。");
    }

    private AutomationElement OpenKlarfSelection()
    {
        var output = UiFind.Require(MainWindow!, "Result Output",
            automationId: "RecipeView_ResultsOutputExpander");
        output.Patterns.ExpandCollapse.Pattern.Expand();
        UiFind.Wait(output, "KLARF output checkbox", automationId: "OptionsView_IsKLARFCheckBox")
            .AsCheckBox().IsChecked = true;
        var tabs = UiFind.Wait(output, "Output format tabs",
            automationId: "ResultsOutputView_OptionsSettingsTabControl");
        // TabItem.Name is the view-model type. The visible type name is the header Text.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        var seen = "";
        while (true)
        {
            var items = tabs.FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
            seen = string.Join("; ", items.Select(HeaderText));
            var klarf = items.FirstOrDefault(item =>
                HeaderText(item).Contains("KLARF", StringComparison.OrdinalIgnoreCase));
            if (klarf is not null)
            {
                if (klarf.Patterns.SelectionItem.IsSupported)
                    klarf.Patterns.SelectionItem.Pattern.Select();
                else
                    klarf.Click();
                break;
            }
            if (DateTime.UtcNow >= deadline)
                throw new UiPrerequisiteException("KLARF_TAB_NOT_FOUND",
                    "勾选 KLARF 后未出现类型页签。已见页签: " + seen);
            Thread.Sleep(400);
        }
        return UiFind.Wait(output, "KLARF selection data tree", TimeSpan.FromSeconds(20),
            automationId: "CheckBoxTreeView_checkboxTreeTreeView");
    }

    private static string HeaderText(AutomationElement tabItem)
    {
        var texts = tabItem.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
        var joined = string.Join("/", texts.Select(t => t.Name).Where(n => !string.IsNullOrWhiteSpace(n)));
        return joined.Length > 0 ? joined : tabItem.Name;
    }

    private AutomationElement BringCollectionsTitleOnScreen(AutomationElement list)
    {
        var window = MainWindow!.BoundingRectangle;
        if (list.Patterns.Scroll.IsSupported)
        {
            try
            {
                list.Patterns.Scroll.Pattern.SetScrollPercent(-1, 0);
            }
            catch (Exception error)
            {
                Log.Step("配方列表未能按百分比回到顶部: " + error.Message);
            }
            Thread.Sleep(400);
        }
        for (var attempt = 0; attempt < 25; attempt++)
        {
            var rect = CollectionsTitle().BoundingRectangle;
            Log.Step($"采集设置文字[{attempt}] left={rect.Left:F0} top={rect.Top:F0} size={rect.Width:F0}x{rect.Height:F0}");
            if (TitleRowOnScreen(rect, window, list.BoundingRectangle))
                return CollectionsTitle();
            if (!list.Patterns.Scroll.IsSupported)
                break;
            var below = rect.Top >= window.Bottom;
            list.Patterns.Scroll.Pattern.Scroll(ScrollAmount.NoAmount,
                below ? ScrollAmount.SmallIncrement : ScrollAmount.SmallDecrement);
            Thread.Sleep(200);
        }
        var last = CollectionsTitle().BoundingRectangle;
        throw new UiElementNotFoundException(
            $"采集设置标题行不在可见区域内。text=({last.Left:F0},{last.Top:F0},{last.Width:F0}x{last.Height:F0}) windowTop={window.Top:F0} windowBottom={window.Bottom:F0}");
    }

    private AutomationElement CollectionsTitle()
    {
        var anchor = UiFind.Require(MainWindow!, "Collections expander",
            automationId: "RecipeView_CollectionsExpander");
        return UiFind.Optional(anchor, "采集设置文字", name: "采集设置", controlType: ControlType.Text)
            ?? UiFind.Require(anchor, "Collections header", automationId: "RecipeExpander_HeaderSiteToggleButton");
    }

    private static bool TitleRowOnScreen(Rectangle rect, Rectangle window, Rectangle list)
        => rect.Width >= 8 && rect.Height is >= 8 and <= 40
           && rect.Top >= window.Top + 30 && rect.Bottom <= window.Bottom - 8
           && rect.Left >= list.Left - 8 && rect.Left <= list.Right - 8;

    private AutomationElement AddSpa(string channelName)
    {
        var list = UiFind.Require(MainWindow!, "Recipe element list", automationId: "RecipeView_ListView");
        var title = BringCollectionsTitleOnScreen(list);
        var text = title.BoundingRectangle;
        var listRect = list.BoundingRectangle;
        // The context menu belongs only to the header row. The title text is left-aligned;
        // the blank stretch on that same row is the header button. A point below the row hits the body and does nothing.
        int y = (int)Math.Round((double)text.Top + (double)text.Height / 2.0);
        int x = (int)Math.Round(Math.Min((double)text.Right + 140.0, (double)listRect.Right - 30.0));
        if (x < text.Right + 12)
            x = (int)Math.Round((double)text.Right + 24.0);
        Log.Step($"右击采集设置标题行空白 left={text.Left:F0} top={text.Top:F0} size={text.Width:F0}x{text.Height:F0} click={x},{y}");
        Mouse.RightClick(new Point(x, y));
        var desktop = MainWindow!.Automation.GetDesktop();
        var insert = UiFind.Wait(desktop, "Insert recipe element menu", TimeSpan.FromSeconds(10),
            automationId: "RecipeExpander_GenericMenuItem");
        if (!insert.IsEnabled)
            throw new UiElementNotFoundException("插入元素菜单存在但不可用，配方未处于编辑状态。");
        insert.Click();
        UiFind.Wait(desktop, "Insert Spatial Analysis", TimeSpan.FromSeconds(10),
            automationId: "RecipeExpander_InsertElementCommandMenuItem_4").Click();
        var spa = UiFind.Wait(MainWindow!, "New Spatial Analysis element",
            automationId: "RecipeView_SpatialAnalysisExpander");
        UiFind.Require(spa, "Enable SPA", automationId: "RecipeExpander_GenericCheckBox")
            .AsCheckBox().IsChecked = true;
        spa.Patterns.ExpandCollapse.Pattern.Expand();
        UiFind.Require(spa, "SPA channel selector", controlType: ControlType.ComboBox)
            .AsComboBox().Select(channelName);
        return spa;
    }

    private static void ExpandTree(AutomationElement tree)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            bool expanded = false;
            foreach (var row in UiFind.All(tree, "Selection tree rows", controlType: ControlType.TreeItem))
            {
                var pattern = row.Patterns.ExpandCollapse;
                if (pattern.IsSupported && pattern.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed)
                {
                    pattern.Pattern.Expand();
                    expanded = true;
                }
            }
            if (!expanded) return;
            if (DateTime.UtcNow >= deadline)
                throw new UiPrerequisiteException("SELECTION_TREE_NOT_READY", "选择数据树未能在 30 秒内完成展开。");
            Thread.Sleep(100);
        }
    }

    private sealed record Row(AutomationElement Element, CheckBox Box, string Label,
        IReadOnlyList<AutomationElement> Descendants);

    private static Row[] ReadRows(AutomationElement tree)
    {
        return UiFind.All(tree, "Selection tree rows", controlType: ControlType.TreeItem)
            .Select(element =>
            {
                var box = UiFind.Require(element, "Selection row checkbox",
                    automationId: "CheckBoxTreeView_IsCheckedCheckBox").AsCheckBox();
                return new Row(element, box, UiProps.Name(box),
                    UiFind.All(element, "Nested selection rows", controlType: ControlType.TreeItem));
            }).ToArray();
    }

    private static Dictionary<string, bool?> Snapshot(AutomationElement tree, bool leavesOnly)
    {
        var rows = ReadRows(tree);
        var result = new Dictionary<string, bool?>(StringComparer.Ordinal);
        foreach (var row in rows.Where(r => !leavesOnly || r.Descendants.Count == 0))
        {
            string path = string.Join(" / ", rows.Where(parent => parent.Element.Equals(row.Element)
                    || Contains(parent.Descendants, row.Element))
                .OrderByDescending(parent => parent.Descendants.Count).Select(parent => parent.Label));
            result.Add(path, row.Box.IsChecked);
        }
        return result;
    }

    private static bool Contains(IReadOnlyList<AutomationElement> elements, AutomationElement target)
        => elements.Any(element => element.Equals(target));

    private static bool IsSpaPath(string path)
        => new[] { "Cluster", "Scratch", "Slipline" }
            .Any(name => path.Contains(name, StringComparison.OrdinalIgnoreCase));

    private static string State(bool? value) => value?.ToString() ?? "Indeterminate";

    private void Compare(string id, string description, string expected, string actual, bool matches)
    {
        if (matches) Assertions.Pass(id, "UI", description, expected, actual);
        else Assertions.Fail(id, "UI", description, expected, actual);
        Assert.True(matches, $"{description}: expected {expected}; actual {actual}");
    }
}
