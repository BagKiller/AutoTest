using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>探针：输入无法匹配的文字后点到别的控件，只读下拉框回显，不保存。</summary>
public sealed class ProbeBlur : L2TestBase
{
    public ProbeBlur() : base("_probe_blur.txt") { }

    [Fact]
    public void TypeUnmatchedTextAndLeave()
    {
        LaunchAndPrepare();
        UiFind.Require(MainWindow!, "Job", automationId: "JobButton").Click();
        Thread.Sleep(1500);
        var lot = WaitLot();
        lot.DoubleClick();
        Thread.Sleep(1200);
        var combo = UiFind.Wait(MainWindow!, "Recipe combo", TimeSpan.FromSeconds(60),
            automationId: "LotView_AvailableRecipesComboBox");
        string selected = ReadText(combo);
        SetText(combo, "ZZZNOMATCH");
        Thread.Sleep(500);
        Log.Step("while-typing='" + ReadText(combo) + "'");
        UiFind.Require(MainWindow!, "Disabled", automationId: "LotView_IsLotDisabledCheckBox").Click();
        Thread.Sleep(600);
        string after = ReadText(combo);
        Log.Step("selected-before='" + selected + "' after-leave='" + after + "'");
        StepEvidence.Capture(MainWindow!, "probe-6-blur");
    }

    private AutomationElement WaitLot()
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var lot = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId("WI3689_UI_20261026"));
            if (lot is not null)
                return lot;
            Thread.Sleep(400);
        }
        throw new UiPrerequisiteException("UI_NOT_READY", "任务树里没有 WI3689_UI_20261026。");
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
