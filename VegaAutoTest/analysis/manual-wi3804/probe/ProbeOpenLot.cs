using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>探针：只打开 Default Job 的第一个 Lot，读出 Recipe 下拉的当前文本和全部项。</summary>
public sealed class ProbeOpenLot : L2TestBase
{
    public ProbeOpenLot() : base("_probe_open_lot.txt") { }

    [Fact]
    public void OpenLotAndReadRecipeCombo()
    {
        LaunchAndPrepare();
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
        Log.Step("Lot id=" + (lot.AutomationId ?? "") + " name=" + (lot.Name ?? ""));
        lot.DoubleClick();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
        var combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");
        ExpandCombo(combo);
        var paths = ReadPaths(combo);
        Log.Step("selected='" + ReadText(combo) + "' count=" + paths.Count + " items=" + string.Join(" | ", paths));
        StepEvidence.Capture(MainWindow!, "probe-1-lot");
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
        if (combo.Patterns.Value.IsSupported)
            return combo.Patterns.Value.Pattern.Value ?? "";
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is not null && edit.Patterns.Value.IsSupported)
            return edit.Patterns.Value.Pattern.Value ?? "";
        return combo.Name ?? "";
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
