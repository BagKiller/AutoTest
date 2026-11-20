using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;
using ProductResources = Vega.Utility.Common.Properties.Resources;

namespace Vega.AutoTest.Authored;

public sealed class SpaKlarfReproTests : L2TestBase
{
    private const string ExpectedResult = "新增的缺陷/属性只出现在选择数据列表中，默认不勾选；原有勾选状态保持不变。";

    public SpaKlarfReproTests() : base("_wi3774_spa_klarf.txt") { }

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

            // Establish the work item's manually selected, non-select-all precondition.
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
            var node = page.FindRecipeNode(TimeSpan.FromSeconds(45));
            if (node is null) throw new UiPrerequisiteException("RECIPE_REQUIRED", "No recipe in prepared Simulator workspace");
            node.DoubleClick();
            UiFind.Wait(MainWindow!, "Recipe document", TimeSpan.FromSeconds(45), automationId: "RecipeView_ListView");
        }
        UiFind.Wait(MainWindow!, "Recipe element list", automationId: "RecipeView_ListView");
        var output = UiFind.Require(MainWindow!, "Result Output",
            automationId: "RecipeView_ResultsOutputExpander");
        output.Patterns.ExpandCollapse.Pattern.Expand();
        var klarf = UiFind.Wait(output, "KLARF output checkbox",
            automationId: "OptionsView_IsKLARFCheckBox");
        if (!klarf.IsEnabled)
            ClickNamed(MainWindow!, "Edit recipe", "Edit", "编辑", "编辑配方");
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
        UiFind.Require(tabs, "KLARF settings tab", name: "KLARF", controlType: ControlType.TabItem)
            .AsTabItem().Select();
        return UiFind.Wait(output, "KLARF selection data tree",
            automationId: "CheckBoxTreeView_checkboxTreeTreeView");
    }

    private AutomationElement AddSpa(string channelName)
    {
        var anchor = UiFind.Require(MainWindow!, "Collections element insertion anchor",
            automationId: "RecipeView_CollectionsExpander");
        anchor.Patterns.ExpandCollapse.Pattern.Collapse();
        anchor.RightClick();
        var desktop = MainWindow!.Automation.GetDesktop();
        UiFind.Wait(desktop, "Insert recipe element menu", name: ProductResources.InsertElement,
            controlType: ControlType.MenuItem).Click();
        UiFind.Wait(desktop, "Insert Spatial Analysis", name: ProductResources.SpatialAnalysis,
            controlType: ControlType.MenuItem).Click();
        var spa = UiFind.Wait(MainWindow!, "New Spatial Analysis element",
            automationId: "RecipeView_SpatialAnalysisExpander");
        UiFind.Require(spa, "Enable SPA", automationId: "RecipeExpander_GenericCheckBox")
            .AsCheckBox().IsChecked = true;
        spa.Patterns.ExpandCollapse.Pattern.Expand();
        UiFind.Require(spa, "SPA channel selector", controlType: ControlType.ComboBox)
            .AsComboBox().Select(channelName);
        return spa;
    }

    private static void ClickNamed(AutomationElement root, string description, params string[] names)
    {
        foreach (string name in names)
        {
            var element = UiFind.Optional(root, description, name: name);
            if (element is null) continue;
            element.Click();
            return;
        }
        UiFind.Require(root, description, name: names[0]).Click();
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
