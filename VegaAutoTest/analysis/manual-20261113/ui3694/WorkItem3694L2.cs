using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3694 没有编号复现步骤。描述要求看 manual 分档和 Job Summary。
/// 这一步只打开本机已有配方的分档编辑器，并查看作业页有没有附图里的 25 片汇总。
/// </summary>
public sealed class WorkItem3694L2 : L2TestBase
{
    public WorkItem3694L2() : base("_wi3694.txt") { }

    [Fact]
    public void OpenBinningEditorAndLookForIllustratedSummary()
    {
        try
        {
            LaunchAndPrepare();
            OpenRecipe();
            StepEvidence.Capture(MainWindow!, "step-1-recipe");
            var binning = FindBinning();
            string editor;
            if (binning is null)
            {
                editor = "Default Recipe 元素列表没有分档";
            }
            else
            {
                var expand = binning.Patterns.ExpandCollapse;
                if (expand.IsSupported && expand.Pattern.ExpandCollapseState.Value != ExpandCollapseState.Expanded)
                    expand.Pattern.Expand();
                Thread.Sleep(500);
                string range = ReadName(UiFind.Optional(binning, "Range", automationId: "BinningItemView_BinningRangeComboBox"));
                string grid = ReadGrid(UiFind.Optional(MainWindow!, "Bins", automationId: "BinningItemView_BinsDataGrid"));
                editor = "range=" + range + "; bins=" + grid;
            }
            Log.Step("分档编辑器 " + editor);
            StepEvidence.Capture(MainWindow!, "step-2-binning");

            UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
            Thread.Sleep(1500);
            Dialogs.HandleAll(MainWindow!);
            StepEvidence.Capture(MainWindow!, "step-3-job");
            string page = string.Join(" | ", UiFind.All(MainWindow!, "Texts", controlType: ControlType.Text)
                .Select(item => item.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .Take(40));
            bool illustrated = page.Contains("Wafer Summary", StringComparison.OrdinalIgnoreCase)
                && page.Contains("25 Wafer", StringComparison.OrdinalIgnoreCase);
            Log.Step("作业页 " + page);
            if (binning is null || !illustrated)
                throw new UiPrerequisiteException("UI_NOT_READY",
                    "工单没有给出可导入的作业。当前配方：" + editor + "。作业页没有附图中的 25 片 Wafer Summary。作业页文字：" + page);
            Assertions.Pass("binning-editor", "UI", "打开 manual 分档编辑器并记录当前档表", "editor text", editor);
            Assertions.Pass("job-summary", "UI", "本机作业页有附图中的 25 片 Wafer Summary", "present", page);
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private void OpenRecipe()
    {
        if (UiFind.Optional(MainWindow!, "Recipe list", automationId: "RecipeView_ListView") is not null)
            return;
        var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
        page.OpenInspection();
        var recipeNode = page.FindRecipeNode(TimeSpan.FromSeconds(90));
        if (recipeNode is null)
            throw new UiPrerequisiteException("UI_NOT_READY", "配方库中没有可用配方节点。");
        recipeNode.DoubleClick();
        UiFind.Wait(MainWindow!, "Recipe element list", TimeSpan.FromSeconds(90), automationId: "RecipeView_ListView");
        Dialogs.HandleAll(MainWindow!);
    }

    private AutomationElement? FindBinning()
    {
        var list = UiFind.Require(MainWindow!, "Recipe list", automationId: "RecipeView_ListView");
        foreach (var percent in new[] { 0d, 50d, 100d })
        {
            if (list.Patterns.Scroll.IsSupported)
                list.Patterns.Scroll.Pattern.SetScrollPercent(-1, percent);
            Thread.Sleep(300);
            var found = UiFind.Optional(list, "Binning", automationId: "RecipeView_BinningExpander")
                ?? UiFind.Optional(MainWindow!, "Binning", automationId: "RecipeView_BinningExpander");
            if (found is not null)
                return found;
        }
        return null;
    }

    private static string ReadName(AutomationElement? element)
    {
        if (element is null)
            return "(missing)";
        var combo = element.AsComboBox();
        return combo.SelectedItem is { } item ? item.Name : element.Name;
    }

    private static string ReadGrid(AutomationElement? grid)
    {
        if (grid is null)
            return "(missing)";
        var cells = grid.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
            .Select(item => item.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .Take(24);
        var text = string.Join(", ", cells);
        return text.Length == 0 ? "(empty)" : text;
    }
}
