using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>探针：清空 Recipe 过滤词后只读列表项数。</summary>
public sealed class ProbeClear : L2TestBase
{
    public ProbeClear() : base("_probe_clear.txt") { }

    [Fact]
    public void ClearFilterAndReadCount()
    {
        LaunchAndPrepare();
        OpenLot();
        var combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");
        SetText(combo, "BFScan");
        Thread.Sleep(700);
        SetText(combo, "");
        Thread.Sleep(900);
        ExpandCombo(combo);
        var restored = ReadPaths(combo);
        Log.Step("cleared count=" + restored.Count + " items=" + string.Join(" | ", restored)
            + " text='" + ReadText(combo) + "'");
        StepEvidence.Capture(MainWindow!, "probe-3-clear");
    }

    private void OpenLot()
    {
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
        var tree = UiFind.Wait(MainWindow!, "Job explorer", TimeSpan.FromSeconds(60),
            automationId: "JobExplorerView");
        var job = FindNamed(tree, "Default Job")
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有 Default Job。");
        Expand(job);
        var lot = job.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem)).FirstOrDefault()
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Default Job 下没有 Lot。");
        lot.DoubleClick();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
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
        if (edit is not null && edit.Patterns.Value.IsSupported)
        {
            edit.Focus();
            edit.Patterns.Value.Pattern.SetValue(text);
            return;
        }
        throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉不支持写入。");
    }

    private static List<string> ReadPaths(AutomationElement combo)
    {
        var paths = new List<string>();
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            string path = "";
            foreach (var text in item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)))
            {
                if (!string.IsNullOrWhiteSpace(text.Name) && text.Name != "Vega.UI.Jobs.SimpleRecipe")
                {
                    path = text.Name;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(item.Name)
                && item.Name != "Vega.UI.Jobs.SimpleRecipe")
                path = item.Name;
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
