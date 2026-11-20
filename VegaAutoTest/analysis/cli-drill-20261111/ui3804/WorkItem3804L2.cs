using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3804 正式用例（第4次）：过滤后匹配项保留；已选中项按设计恒留在视图内。
/// </summary>
public sealed class WorkItem3804L2 : L2TestBase
{
    public WorkItem3804L2() : base("_wi3804.txt") { }

    [Fact]
    public void RecipeComboFiltersByTypedText()
    {
        try
        {
            LaunchAndPrepare();
            OpenLot();
            var combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
                automationId: "LotView_AvailableRecipesComboBox");
            StepEvidence.Capture(MainWindow!, "step-1-lot");

            ExpandCombo(combo);
            var allBefore = ReadRecipePaths(combo);
            string selected = ReadComboText(combo);
            Log.Step("过滤前项数=" + allBefore.Count + " selected='" + selected + "' 样例=" + string.Join(" | ", allBefore.Take(6)));
            if (allBefore.Count < 2)
                throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉可过滤项不足 2。");

            string needle = PickNeedle(allBefore, selected);
            SetComboText(combo, needle);
            Thread.Sleep(900);
            ExpandCombo(combo);
            var filtered = ReadRecipePaths(combo);
            Log.Step("输入='" + needle + "' 后项数=" + filtered.Count + " 项=" + string.Join(" | ", filtered.Take(10)));
            StepEvidence.Capture(MainWindow!, "step-2-filtered");

            bool shrunk = filtered.Count > 0 && filtered.Count < allBefore.Count;
            bool othersMatch = filtered.All(p =>
                p.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || string.Equals(p, selected, StringComparison.OrdinalIgnoreCase));
            bool hasMatch = filtered.Any(p => p.Contains(needle, StringComparison.OrdinalIgnoreCase));
            if (!(shrunk && othersMatch && hasMatch))
            {
                Assertions.Fail("filter-match", "UI",
                    "输入部分路径后列表收缩为匹配项（已选中项可按设计保留）",
                    "count<" + allBefore.Count + "; match '" + needle + "' or selected '" + selected + "'",
                    "count=" + filtered.Count + "; items=" + string.Join(" | ", filtered.Take(12)));
            }
            else
            {
                Assertions.Pass("filter-match", "UI",
                    "输入部分路径后列表收缩为匹配项（已选中项可按设计保留）",
                    "count<" + allBefore.Count + "; match '" + needle + "' or selected",
                    "count=" + filtered.Count);
            }

            SetComboText(combo, "");
            Thread.Sleep(900);
            ExpandCombo(combo);
            var restored = ReadRecipePaths(combo);
            Log.Step("清空后项数=" + restored.Count);
            StepEvidence.Capture(MainWindow!, "step-3-cleared");
            if (restored.Count < allBefore.Count)
            {
                Assertions.Fail("filter-clear", "UI", "清空输入后恢复全量列表",
                    "count>=" + allBefore.Count, "count=" + restored.Count);
            }
            else
            {
                Assertions.Pass("filter-clear", "UI", "清空输入后恢复全量列表",
                    "count>=" + allBefore.Count, "count=" + restored.Count);
            }
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private void OpenLot()
    {
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
        var tree = UiFind.Wait(MainWindow!, "Job explorer", TimeSpan.FromSeconds(60),
            automationId: "JobExplorerView");
        var job = FindNamedTreeItem(tree, "Default Job")
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有 Default Job。");
        Expand(job);
        var lot = FirstChildTreeItem(job)
            ?? throw new UiPrerequisiteException("UI_NOT_READY", "Default Job 下没有 Lot。");
        Log.Step("打开 Lot id=" + (lot.AutomationId ?? ""));
        lot.DoubleClick();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
    }

    private static string PickNeedle(IReadOnlyList<string> paths, string selected)
    {
        foreach (var path in paths)
        {
            if (string.Equals(path, selected, StringComparison.OrdinalIgnoreCase))
                continue;
            var parts = path.Split(new[] { '/', '\\', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts.OrderByDescending(p => p.Length))
            {
                if (part.Length < 3) continue;
                int hits = paths.Count(p => p.Contains(part, StringComparison.OrdinalIgnoreCase));
                if (hits >= 1 && hits < paths.Count)
                    return part.Length > 16 ? part[..16] : part;
            }
            if (path.Length >= 4)
                return path[..Math.Min(12, path.Length)];
        }
        var fallback = paths[0];
        return fallback.Length >= 4 ? fallback[..4] : fallback;
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

    private static string ReadComboText(AutomationElement combo)
    {
        if (combo.Patterns.Value.IsSupported)
            return combo.Patterns.Value.Pattern.Value ?? "";
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is not null && edit.Patterns.Value.IsSupported)
            return edit.Patterns.Value.Pattern.Value ?? "";
        return combo.Name ?? "";
    }

    private static void SetComboText(AutomationElement combo, string text)
    {
        combo.Focus();
        Thread.Sleep(150);
        var edit = combo.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
        if (edit is not null && edit.Patterns.Value.IsSupported)
        {
            edit.Focus();
            edit.Patterns.Value.Pattern.SetValue(text);
            return;
        }
        if (combo.Patterns.Value.IsSupported)
        {
            combo.Patterns.Value.Pattern.SetValue(text);
            return;
        }
        throw new UiPrerequisiteException("UI_NOT_READY", "Recipe 下拉不支持 Value 写入。");
    }

    private static List<string> ReadRecipePaths(AutomationElement combo)
    {
        var items = combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
        var paths = new List<string>();
        foreach (var item in items)
        {
            string path = "";
            var texts = item.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            foreach (var t in texts)
            {
                if (!string.IsNullOrWhiteSpace(t.Name) && t.Name != "Vega.UI.Jobs.SimpleRecipe")
                {
                    path = t.Name;
                    break;
                }
            }
            if (string.IsNullOrWhiteSpace(path)
                && item.Patterns.Value.IsSupported
                && !string.IsNullOrWhiteSpace(item.Patterns.Value.Pattern.Value))
                path = item.Patterns.Value.Pattern.Value;
            if (string.IsNullOrWhiteSpace(path)
                && !string.IsNullOrWhiteSpace(item.Name)
                && item.Name != "Vega.UI.Jobs.SimpleRecipe")
                path = item.Name;
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static AutomationElement? FindNamedTreeItem(AutomationElement root, string automationId)
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

    private static AutomationElement? FirstChildTreeItem(AutomationElement parent)
    {
        var children = parent.FindAllChildren(cf => cf.ByControlType(ControlType.TreeItem));
        return children.Length > 0 ? children[0] : null;
    }
}
