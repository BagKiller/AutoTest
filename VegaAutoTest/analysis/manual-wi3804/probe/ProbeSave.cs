using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>探针：过滤后选中 BFScanRecipe，保存，关掉页面再进入，然后把原来的选中项存回去。</summary>
public sealed class ProbeSave : L2TestBase
{
    public ProbeSave() : base("_probe_save.txt") { }

    [Fact]
    public void SelectFilteredRecipeSaveAndReopen()
    {
        LaunchAndPrepare();
        OpenLot();
        var combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");
        string original = ReadText(combo);
        Log.Step("original='" + original + "'");
        SetText(combo, "BFScan");
        Thread.Sleep(800);
        ExpandCombo(combo);
        bool selected = SelectItem(combo, "BFScanRecipe");
        Thread.Sleep(600);
        string afterSelect = ReadText(combo);
        Log.Step("select-called=" + selected + " text-after-select='" + afterSelect + "'");
        StepEvidence.Capture(MainWindow!, "probe-4-selected");
        if (!afterSelect.Contains("BFScanRecipe", StringComparison.OrdinalIgnoreCase))
            return;

        UiFind.Require(MainWindow!, "Save", automationId: "SaveButton").Click();
        Thread.Sleep(800);
        Dialogs.HandleAll(MainWindow!);
        CloseLotTab();
        Thread.Sleep(800);
        OpenLot();
        combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");
        string reopened = ReadText(combo);
        Log.Step("reopened='" + reopened + "'");
        StepEvidence.Capture(MainWindow!, "probe-4-reopened");

        if (!string.Equals(reopened, original, StringComparison.OrdinalIgnoreCase))
        {
            SetText(combo, "");
            Thread.Sleep(500);
            ExpandCombo(combo);
            SelectItem(combo, original);
            Thread.Sleep(500);
            UiFind.Require(MainWindow!, "Save", automationId: "SaveButton").Click();
            Thread.Sleep(800);
            Dialogs.HandleAll(MainWindow!);
            Log.Step("restored-selection='" + ReadText(combo) + "'");
        }
    }

    private void OpenLot()
    {
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1200);
        Dialogs.HandleAll(MainWindow!);
        var tree = UiFind.Wait(MainWindow!, "Job explorer", TimeSpan.FromSeconds(60),
            automationId: "JobExplorerView");
        var job = FindNamed(tree, "Default Job")
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有 Default Job。");
        Expand(job);
        var lot = job.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)).FirstOrDefault()
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Default Job 下没有 Lot。");
        lot.DoubleClick();
        Thread.Sleep(1200);
        Dialogs.HandleAll(MainWindow!);
    }

    private void CloseLotTab()
    {
        foreach (var tab in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)))
        {
            var title = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(t => (t.Name ?? "").Contains("WI3689", StringComparison.OrdinalIgnoreCase));
            if (title is null)
                continue;
            var close = tab.FindFirstDescendant(cf => cf.ByAutomationId("DocumentCloseButton"));
            if (close is null)
                throw new UiPrerequisiteException("UI_NOT_READY", "Lot 页签没有关闭按钮。");
            close.Click();
            Dialogs.HandleAll(MainWindow);
            return;
        }
        throw new UiPrerequisiteException("UI_NOT_READY", "没有找到 Lot 页签。");
    }

    private static bool SelectItem(AutomationElement combo, string needle)
    {
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            string path = ItemPath(item);
            if (!path.Contains(needle, StringComparison.OrdinalIgnoreCase))
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

    private static void ExpandCombo(AutomationElement combo)
    {
        if (combo.Patterns.ExpandCollapse.IsSupported
            && combo.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(400);
        }
    }

    private static string ReadText(AutomationElement combo)
    {
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is not null && edit.Patterns.Value.IsSupported)
            return edit.Patterns.Value.Pattern.Value ?? "";
        if (combo.Patterns.Value.IsSupported)
            return combo.Patterns.Value.Pattern.Value ?? "";
        return combo.Name ?? "";
    }

    private static void SetText(AutomationElement combo, string text)
    {
        combo.Focus();
        Thread.Sleep(200);
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is null || !edit.Patterns.Value.IsSupported)
            throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉不支持写入。");
        edit.Focus();
        edit.Patterns.Value.Pattern.SetValue(text);
    }

    private static AutomationElement? FindNamed(AutomationElement root, string automationId)
    {
        foreach (var item in root.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            if (string.Equals(item.AutomationId, automationId, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    private static void Expand(AutomationElement item)
    {
        if (item.Patterns.ExpandCollapse.IsSupported
            && item.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
        {
            item.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(400);
        }
    }
}
