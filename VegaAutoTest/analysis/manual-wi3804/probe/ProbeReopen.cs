using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>探针：选中过滤结果后用「保存且关闭」关掉页面，再进入，然后把原配方存回。</summary>
public sealed class ProbeReopen : L2TestBase
{
    public ProbeReopen() : base("_probe_reopen.txt") { }

    [Fact]
    public void SaveCloseAndReopen()
    {
        LaunchAndPrepare();
        OpenLot();
        var combo = RecipeCombo();
        string original = ReadText(combo);
        Log.Step("original='" + original + "'");
        SetText(combo, "BFScan");
        Thread.Sleep(700);
        ExpandCombo(combo);
        if (!SelectItem(combo, "BFScanRecipe"))
            throw new UiPrerequisiteException("UI_NOT_READY", "过滤结果里没有 BFScanRecipe。");
        Thread.Sleep(500);
        Log.Step("selected='" + ReadText(combo) + "'");
        ClickSaveAndCloseLot();
        OpenLot();
        string reopened = ReadText(RecipeCombo());
        Log.Step("reopened='" + reopened + "'");
        StepEvidence.Capture(MainWindow!, "probe-5-reopened");

        combo = RecipeCombo();
        SetText(combo, "");
        Thread.Sleep(400);
        ExpandCombo(combo);
        if (!SelectItem(combo, original))
            throw new UiPrerequisiteException("UI_NOT_READY", "无法选回原配方 " + original);
        Thread.Sleep(400);
        ClickSaveAndCloseLot();
        OpenLot();
        Log.Step("restored='" + ReadText(RecipeCombo()) + "'");
        StepEvidence.Capture(MainWindow!, "probe-5-restored");
    }

    private AutomationElement RecipeCombo()
        => UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");

    private void ClickSaveAndCloseLot()
    {
        string before = LotTabTitle();
        UiFind.Require(MainWindow!, "Save", automationId: "SaveButton").Click();
        Thread.Sleep(800);
        Dialogs.HandleAll(MainWindow!);
        Log.Step("tab-after-save='" + LotTabTitle() + "' before='" + before + "'");
        var tab = LotTab()
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有找到 Lot 页签。");
        var close = tab.FindFirstDescendant(cf => cf.ByAutomationId("DocumentCloseButton"))
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Lot 页签没有关闭按钮。");
        if (close.Patterns.Invoke.IsSupported)
            close.Patterns.Invoke.Pattern.Invoke();
        else
            close.Click();
        Thread.Sleep(700);
        var ok = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("MessageBoxOK"));
        if (ok is not null)
        {
            Log.Step("click-save-and-close");
            if (ok.Patterns.Invoke.IsSupported)
                ok.Patterns.Invoke.Pattern.Invoke();
            else
                ok.Click();
            Thread.Sleep(800);
        }
        else
        {
            Log.Step("close-without-save-prompt");
        }
        if (LotTab() is not null)
            throw new UiPrerequisiteException("UI_NOT_READY", "Lot 页签仍在，关闭未完成。标题=" + LotTabTitle());
        Log.Step("lot-tab-closed");
    }

    private AutomationElement? LotTab()
    {
        foreach (var tab in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)))
        {
            var title = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .FirstOrDefault(t => (t.Name ?? "").Contains("WI3689", StringComparison.OrdinalIgnoreCase));
            if (title is not null)
                return tab;
        }
        return null;
    }

    private string LotTabTitle()
    {
        var tab = LotTab();
        if (tab is null)
            return "";
        var title = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .FirstOrDefault(t => (t.Name ?? "").Contains("WI3689", StringComparison.OrdinalIgnoreCase));
        return title?.Name ?? "";
    }

    private void OpenLot()
    {
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
        var deadline = DateTime.UtcNow.AddSeconds(30);
        AutomationElement? lot = null;
        while (DateTime.UtcNow < deadline && lot is null)
        {
            lot = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("WI3689_UI_20261026"));
            if (lot is null)
                Thread.Sleep(500);
        }
        if (lot is null)
        {
            UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
            Thread.Sleep(1500);
            lot = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("WI3689_UI_20261026"));
        }
        if (lot is null)
            throw new UiPrerequisiteException("UI_NOT_READY", "任务树里没有 WI3689_UI_20261026。");
        Log.Step("open-lot id=" + lot.AutomationId);
        lot.DoubleClick();
        Thread.Sleep(1200);
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
        return combo.Name ?? "";
    }

    private static void SetText(AutomationElement combo, string text)
    {
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit))
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉不支持写入。");
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
