using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3803 探针：打开诊断「光学」，查看本机 Simulator 是否暴露慢门相关控件。
/// 完整验收（关慢门后跑 wafer 看重开日志）需要可操作的慢门与 wafer 运行证据，本探针只定位界面材料。
/// </summary>
public sealed class WorkItem3803L2 : L2TestBase
{
    public WorkItem3803L2() : base("_wi3803.txt") { }

    [Fact]
    public void OpenOpticsAndLookForSlowShutter()
    {
        try
        {
            LaunchAndPrepare();
            OpenDiagnostics();
            StepEvidence.Capture(MainWindow!, "step-1-diagnostics");

            var tree = UiFind.Wait(MainWindow!, "Diagnostics tree", TimeSpan.FromSeconds(60),
                automationId: "FixedTreeView");
            var deviceGroup = FindTreeItem(tree, "设备诊断")
                ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有设备诊断；树=" + Summarize(tree));
            Expand(deviceGroup);
            Thread.Sleep(400);
            var optics = FindTreeItemExact(tree, "光学")
                ?? FindTreeItem(tree, "光学")
                ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有光学节点；树=" + Summarize(tree));
            optics.DoubleClick();
            Thread.Sleep(2000);
            Dialogs.HandleAll(MainWindow!);
            StepEvidence.Capture(MainWindow!, "step-2-optics");

            string texts = string.Join(" | ", UiFind.All(MainWindow!, "Texts", controlType: ControlType.Text)
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(100));
            string buttons = string.Join(" | ", UiFind.All(MainWindow!, "Buttons", controlType: ControlType.Button)
                .Select(b => b.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(50));
            Log.Step("光学文本: " + texts);
            Log.Step("光学按钮: " + buttons);
            StepEvidence.Capture(MainWindow!, "step-3-shutter-scan");

            bool shutterUi = ContainsAny(texts, "慢门", "SlowShutter", "Shutter", "快门")
                || ContainsAny(buttons, "慢门", "SlowShutter", "Shutter", "快门", "打开", "关闭");
            if (!shutterUi)
            {
                throw new UiPrerequisiteException("UI_NOT_READY",
                    "光学页没有可操作的慢门控件，无法在 Simulator 上按验收标准手动关慢门再跑 wafer。texts="
                    + texts + "; buttons=" + buttons);
            }
            Assertions.Pass("optics-shutter-ui", "UI", "光学诊断页出现慢门相关控件",
                "慢门/Shutter", "seen");
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

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));

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
