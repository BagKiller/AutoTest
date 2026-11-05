using System;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Vega.Utility.Common.Properties;
using Xunit;

namespace Vega.AutoTest.Authored;

public class AclutStopUiTests : L2TestBase
{
    public AclutStopUiTests() : base("aclut-stop-ui.txt") { }
    private AutomationElement[] Elements() => new[] { MainWindow! }.Concat(MainWindow!.ModalWindows).SelectMany(w => w.FindAllDescendants()).ToArray();
    private AutomationElement? Question() => Elements().FirstOrDefault(e => e.Name == Resources.ACLutStopNotExecuting);
    private int Stops() => Elements().Count(e => e.ControlType == ControlType.Text && e.Name.StartsWith("Stop Calibration for ", StringComparison.Ordinal) && e.Name.EndsWith(" end.", StringComparison.Ordinal));
    private void Check(string id, bool ok, string expected, object actual)
    {
        if (ok) Assertions.Pass(id, "uia-business", id, expected, actual.ToString()!);
        else { Assertions.Fail(id, "uia-business", id, expected, actual.ToString()!); Assert.True(ok, id); }
    }
    private bool Until(Func<bool> predicate, int seconds = 20)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        do { if (predicate()) return true; Thread.Sleep(100); } while (DateTime.UtcNow < end);
        return predicate();
    }
    private void Button(params string[] names)
    {
        var found = Elements().LastOrDefault(e => e.ControlType == ControlType.Button && names.Contains(e.Name));
        if (found == null) { Assertions.Fail("button", "uia-action", "Required operator action", string.Join("/", names), "missing"); Assert.True(false, "Required button missing"); }
        found!.AsButton().Invoke();
    }
    [Fact]
    public void Idle_stop_confirmation_honors_cancel_and_confirm()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
        var calibration = MainWindow!.FindAllDescendants().FirstOrDefault(e => e.Name == Resources.Calibration && (e.ControlType == ControlType.Button || e.ControlType == ControlType.TabItem || e.ControlType == ControlType.TreeItem));
        if (calibration == null) { Assertions.Fail("navigation", "uia-action", "Open Calibration", Resources.Calibration, "missing"); Assert.True(false, "Calibration navigation missing"); }
        calibration!.Click();
        AutomationElement? node = null;
        bool opened = Until(() => { node = Elements().FirstOrDefault(e => e.ControlType == ControlType.TreeItem && (e.Name.Contains("Lut", StringComparison.OrdinalIgnoreCase) || e.Name.Contains("LUT", StringComparison.Ordinal))); return node != null; });
        if (!opened) { Assertions.Fail("aclut-navigation", "uia-action", "Open ACLut", "ACLut tree node", "missing"); Assert.True(false, "ACLut node missing"); }
        node!.DoubleClick();
        Button(Resources.Stop, "Stop", "停止");
        bool initial = Until(() => Question() != null || Stops() > 0);
        if (initial && Question() != null) Button("OK", "Ok", "确定", "是", "Yes");
        Check("initial-stop", Until(() => Stops() > 0), "operator Stop reaches motor-stop workflow log", Stops());
        Thread.Sleep(4000);
        int baseline = Stops();
        Button(Resources.Stop, "Stop", "停止");
        Check("idle-question", Until(() => Question() != null), Resources.ACLutStopNotExecuting, Question()?.Name ?? "missing");
        Button(Resources.Cancel, "Cancel", "取消");
        Check("cancel-dismisses", Until(() => Question() == null), "confirmation dismissed", Question()?.Name ?? "dismissed");
        Thread.Sleep(500);
        Check("cancel-preserves", Stops() == baseline, "cancel does not execute another stop", Stops());
        Button(Resources.Stop, "Stop", "停止");
        bool asked = Until(() => Question() != null);
        if (!asked) { Assertions.Fail("repeat-question", "uia-action", "Confirm idle stop", Resources.ACLutStopNotExecuting, "missing"); Assert.True(false, "Confirmation missing"); }
        Button("OK", "Ok", "确定", "是", "Yes");
        Check("confirm-executes", Until(() => Stops() > baseline), "confirm executes another motor-stop workflow", Stops());
    }
}
