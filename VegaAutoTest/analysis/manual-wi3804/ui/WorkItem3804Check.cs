using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>#3804 正式用例。过滤保留当前选中项；清空恢复；保存后重进；未匹配文字离开后回显。</summary>
public sealed class WorkItem3804Check : L2TestBase
{
    public WorkItem3804Check() : base("_wi3804.txt") { }

    [Fact]
    public void RecipeFilterSaveAndUnmatchedLeave()
    {
        try
        {
            LaunchAndPrepare();
            OpenLot();
            var combo = RecipeCombo();
            string original = ReadText(combo);
            ExpandCombo(combo);
            var before = ReadPaths(combo);
            Log.Step("original='" + original + "' count=" + before.Count);

            SetText(combo, "BFScan");
            Thread.Sleep(800);
            ExpandCombo(combo);
            var filtered = ReadPaths(combo);
            Log.Step("filtered=" + string.Join(" | ", filtered));
            StepEvidence.Capture(MainWindow!, "step-1-filter");
            bool shrunk = filtered.Count > 0 && filtered.Count < before.Count;
            bool selectedKept = filtered.Any(p => string.Equals(p, original, StringComparison.OrdinalIgnoreCase));
            bool hasMatch = filtered.Any(p => p.Contains("BFScan", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(p, original, StringComparison.OrdinalIgnoreCase));
            bool onlyMatchOrSelected = filtered.All(p =>
                p.Contains("BFScan", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, original, StringComparison.OrdinalIgnoreCase));
            Record(shrunk && selectedKept && hasMatch && onlyMatchOrSelected, "filter-match",
                "输入 BFScan 后列表收缩；不含关键词的未选中项消失；当前选中项仍在",
                "count<" + before.Count + "; keep '" + original + "'; other items contain BFScan",
                "count=" + filtered.Count + "; items=" + string.Join(" | ", filtered));

            SetText(combo, "");
            Thread.Sleep(800);
            ExpandCombo(combo);
            var restored = ReadPaths(combo);
            Log.Step("cleared count=" + restored.Count);
            StepEvidence.Capture(MainWindow!, "step-2-clear");
            Record(restored.Count >= before.Count, "filter-clear",
                "清空输入后恢复全量列表",
                "count>=" + before.Count, "count=" + restored.Count);

            if (!SelectItem(combo, "BFScanRecipe"))
                throw new UiPrerequisiteException("UI_NOT_READY", "过滤结果里没有 BFScanRecipe。");
            Thread.Sleep(400);
            SaveAndClose();
            OpenLot();
            string reopened = ReadText(RecipeCombo());
            Log.Step("reopened='" + reopened + "'");
            StepEvidence.Capture(MainWindow!, "step-3-reopened");
            Record(reopened.Contains("BFScanRecipe", StringComparison.OrdinalIgnoreCase), "save-reopen",
                "过滤结果中选中 BFScanRecipe，保存并关闭后再进入，显示该项",
                "contains BFScanRecipe", reopened);

            combo = RecipeCombo();
            SetText(combo, "ZZZNOMATCH");
            Thread.Sleep(400);
            UiFind.Require(MainWindow!, "Disabled", automationId: "LotView_IsLotDisabledCheckBox").Click();
            Thread.Sleep(500);
            string echoed = ReadText(combo);
            Log.Step("after-unmatched-leave='" + echoed + "'");
            StepEvidence.Capture(MainWindow!, "step-4-unmatched");
            CloseDiscarding();
            OpenLot();
            string still = ReadText(RecipeCombo());
            Log.Step("saved-after-unmatched='" + still + "'");
            Record(echoed == reopened && still == reopened, "unmatched-leave",
                "输入未匹配文字后离开，回显实际选中项，且不改变已保存内容",
                "echo='" + reopened + "' saved='" + reopened + "'",
                "echo='" + echoed + "' saved='" + still + "'");

            combo = RecipeCombo();
            SetText(combo, "");
            Thread.Sleep(300);
            ExpandCombo(combo);
            if (!SelectItem(combo, original))
                throw new UiPrerequisiteException("UI_NOT_READY", "无法选回原配方。");
            Thread.Sleep(300);
            SaveAndClose();
            OpenLot();
            Log.Step("restored='" + ReadText(RecipeCombo()) + "'");
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private void Record(bool ok, string id, string description, string expected, string actual)
    {
        if (ok)
            Assertions.Pass(id, "UI", description, expected, actual);
        else
            Assertions.Fail(id, "UI", description, expected, actual);
    }

    private void SaveAndClose()
    {
        UiFind.Require(MainWindow!, "Save", automationId: "SaveButton").Click();
        Thread.Sleep(600);
        var tab = LotTab() ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有 Lot 页签。");
        var close = tab.FindFirstDescendant(cf => cf.ByAutomationId("DocumentCloseButton"))
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Lot 页签没有关闭按钮。");
        Invoke(close);
        Thread.Sleep(600);
        var ok = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MessageBoxOK"));
        if (ok is not null)
        {
            Log.Step("save-and-close");
            Invoke(ok);
            Thread.Sleep(700);
        }
        if (LotTab() is not null)
            throw new UiPrerequisiteException("UI_NOT_READY", "Lot 页签仍在。");
    }

    private void CloseDiscarding()
    {
        var tab = LotTab();
        if (tab is null)
            return;
        var close = tab.FindFirstDescendant(cf => cf.ByAutomationId("DocumentCloseButton"));
        if (close is null)
            return;
        Invoke(close);
        Thread.Sleep(500);
        var no = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MessageBoxNo"));
        if (no is not null)
        {
            Log.Step("discard-close");
            Invoke(no);
            Thread.Sleep(600);
        }
    }

    private static void Invoke(AutomationElement element)
    {
        if (element.Patterns.Invoke.IsSupported)
            element.Patterns.Invoke.Pattern.Invoke();
        else
            element.Click();
    }

    private AutomationElement RecipeCombo()
        => UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");

    private AutomationElement? LotTab()
    {
        foreach (var tab in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)))
        {
            if (tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Any(t => (t.Name ?? "").Contains("WI3689", StringComparison.OrdinalIgnoreCase)))
                return tab;
        }
        return null;
    }

    private void OpenLot()
    {
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1200);
        Dialogs.HandleAll(MainWindow!);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        AutomationElement? lot = null;
        while (DateTime.UtcNow < deadline && lot is null)
        {
            lot = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("WI3689_UI_20261026"));
            if (lot is null)
                Thread.Sleep(400);
        }
        if (lot is null)
            throw new UiPrerequisiteException("UI_NOT_READY", "任务树里没有 WI3689_UI_20261026。");
        lot.DoubleClick();
        Thread.Sleep(1000);
        Dialogs.HandleAll(MainWindow!);
    }

    private static bool SelectItem(AutomationElement combo, string needle)
    {
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            if (!ItemPath(item).Contains(needle, StringComparison.OrdinalIgnoreCase))
                continue;
            if (item.Patterns.SelectionItem.IsSupported)
                item.Patterns.SelectionItem.Pattern.Select();
            else
                item.Click();
            return true;
        }
        return false;
    }

    private static string ItemPath(AutomationElement item)
    {
        foreach (var text in item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
        {
            if (!string.IsNullOrWhiteSpace(text.Name) && text.Name != "Vega.UI.Jobs.SimpleRecipe")
                return text.Name;
        }
        return item.Name ?? "";
    }

    private static List<string> ReadPaths(AutomationElement combo)
    {
        var paths = new List<string>();
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            string path = ItemPath(item);
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void ExpandCombo(AutomationElement combo)
    {
        if (combo.Patterns.ExpandCollapse.IsSupported
            && combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(300);
        }
    }

    private static string ReadText(AutomationElement combo)
    {
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is not null && edit.Patterns.Value.IsSupported)
            return edit.Patterns.Value.Pattern.Value ?? "";
        return combo.Name ?? "";
    }

    private static void SetText(AutomationElement combo, string text)
    {
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉不支持写入。");
        edit.Focus();
        edit.Patterns.Value.Pattern.SetValue(text);
    }
}
