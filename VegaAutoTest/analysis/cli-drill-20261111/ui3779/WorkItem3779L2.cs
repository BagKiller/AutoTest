using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3779 基线探针：Vega100-PIC Simulator 诊断 ComponentManager 不应出现 DAQDevice_2。
/// </summary>
public sealed class WorkItem3779L2 : L2TestBase
{
    public WorkItem3779L2() : base("_wi3779.txt") { }

    [Fact]
    public void Vega100BaselineHasNoDaqDevice2()
    {
        try
        {
            LaunchAndPrepare();
            OpenDiagnostics();
            StepEvidence.Capture(MainWindow!, "step-1-diagnostics");

            var tree = UiFind.Wait(MainWindow!, "Diagnostics tree", TimeSpan.FromSeconds(60),
                automationId: "FixedTreeView");
            var cm = FindTreeItem(tree, "ComponentManager")
                ?? throw new UiPrerequisiteException("UI_NOT_READY", "没有 ComponentManager；树=" + Summarize(tree));
            cm.Click();
            Thread.Sleep(2000);
            Dialogs.HandleAll(MainWindow!);
            StepEvidence.Capture(MainWindow!, "step-2-component-manager");

            string texts = string.Join("\n", UiFind.All(MainWindow!, "Texts", controlType: ControlType.Text)
                .Select(t => t.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct());
            string grid = string.Join("\n", UiFind.All(MainWindow!, "DataItems", controlType: ControlType.DataItem)
                .Select(i => i.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .Take(200));
            Log.Step("ComponentManager 文本片段:\n" + string.Join("\n", texts.Split('\n').Take(80)));
            Log.Step("DataItem 片段:\n" + string.Join("\n", grid.Split('\n').Take(80)));
            StepEvidence.Capture(MainWindow!, "step-3-scan");

            bool hasDaq2 = texts.Contains("DAQDevice_2", StringComparison.OrdinalIgnoreCase)
                || grid.Contains("DAQDevice_2", StringComparison.OrdinalIgnoreCase);
            string tool = texts.Split('\n').FirstOrDefault(l => l.Contains("Vega100", StringComparison.OrdinalIgnoreCase))
                ?? texts.Split('\n').FirstOrDefault(l => l.Contains("设备类型", StringComparison.OrdinalIgnoreCase))
                ?? "(未读到机型行)";
            Log.Step("机型线索=" + tool + "; hasDAQDevice_2=" + hasDaq2);

            if (hasDaq2)
            {
                Assertions.Fail("no-daqdevice-2", "UI",
                    "Vega100-PIC 基线 ComponentManager 无 DAQDevice_2 实例",
                    "absent", "present; tool=" + tool);
            }
            else
            {
                Assertions.Pass("no-daqdevice-2", "UI",
                    "Vega100-PIC 基线 ComponentManager 无 DAQDevice_2 实例",
                    "absent", "absent; tool=" + tool);
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

    private static string Summarize(AutomationElement tree)
    {
        return string.Join(" / ", tree.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
            .Select(i => i.AutomationId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(40));
    }
}
