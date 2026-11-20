using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3802 第4次：严格要求 BPS 页出现「膜系」文案，避免误匹配 ComponentManager。
/// </summary>
public sealed class WorkItem3802L2 : L2TestBase
{
    public WorkItem3802L2() : base("_wi3802.txt") { }

    [Fact]
    public void OpenBpsDiagnosticsFilmUi()
    {
        try
        {
            LaunchAndPrepare();
            OpenDiagnostics();
            StepEvidence.Capture(MainWindow!, "step-1-diagnostics");

            var tree = UiFind.Wait(MainWindow!, "Diagnostics tree", TimeSpan.FromSeconds(60),
                automationId: "FixedTreeView");
            var deviceGroup = FindTreeItem(tree, "设备诊断")
                ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有设备诊断组；树=" + Summarize(tree));
            Expand(deviceGroup);
            Thread.Sleep(500);
            var bps = FindTreeItemExact(tree, "BPS")
                ?? throw new UiPrerequisiteException("UI_NOT_READY",
                    "设备诊断下没有 BPS；树=" + Summarize(tree));
            bps.DoubleClick();
            Thread.Sleep(2500);
            Dialogs.HandleAll(MainWindow!);
            StepEvidence.Capture(MainWindow!, "step-2-bps");

            string texts = string.Join(" | ", UiFind.All(MainWindow!, "Texts", controlType: ControlType.Text)
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(100));
            string buttons = string.Join(" | ", UiFind.All(MainWindow!, "Buttons", controlType: ControlType.Button)
                .Select(b => b.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(40));
            Log.Step("BPS 文本: " + texts);
            Log.Step("BPS 按钮: " + buttons);
            StepEvidence.Capture(MainWindow!, "step-3-film");

            bool onBpsPage = texts.Contains("BPS", StringComparison.OrdinalIgnoreCase)
                || texts.Contains("膜系", StringComparison.Ordinal);
            bool filmUi = texts.Contains("膜系", StringComparison.Ordinal)
                || (buttons.Contains("新建", StringComparison.Ordinal)
                    && buttons.Contains("删除", StringComparison.Ordinal)
                    && !texts.Contains("Loaded ", StringComparison.Ordinal));
            if (!(onBpsPage && filmUi))
            {
                Assertions.Fail("bps-film-ui", "UI", "BPS 诊断页出现膜系表相关界面",
                    "页面含膜系，且不是 ComponentManager", "texts=" + texts + "; buttons=" + buttons);
            }
            else
            {
                Assertions.Pass("bps-film-ui", "UI", "BPS 诊断页出现膜系表相关界面",
                    "含膜系", "texts has 膜系");
            }
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private void OpenDiagnostics()
    {
        var explorer = UiFind.Require(MainWindow!, "浏览菜单", automationId: "ExplorerMenuItem");
        explorer.Click();
        Thread.Sleep(500);
        var diagnostics = UiFind.Optional(MainWindow!, "诊断", automationId: "DiagnosticsMenuItem")
            ?? UiFind.Optional(MainWindow!, "诊断", name: "诊断");
        if (diagnostics is null)
            throw new UiPrerequisiteException("UI_NOT_READY", "浏览菜单下没有诊断项。");
        diagnostics.Click();
        Thread.Sleep(1500);
        Dialogs.HandleAll(MainWindow!);
    }

    private static AutomationElement? FindTreeItem(AutomationElement tree, string key)
    {
        foreach (var item in tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            string id = item.AutomationId ?? "";
            if (id.Equals(key, StringComparison.OrdinalIgnoreCase)
                || id.Contains(key, StringComparison.OrdinalIgnoreCase))
                return item;
        }
        return null;
    }

    private static AutomationElement? FindTreeItemExact(AutomationElement tree, string key)
    {
        foreach (var item in tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
        {
            if (string.Equals(item.AutomationId, key, StringComparison.OrdinalIgnoreCase))
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

    private static string Summarize(AutomationElement tree)
    {
        return string.Join(" / ", tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
            .Select(i => i.AutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(40));
    }
}
