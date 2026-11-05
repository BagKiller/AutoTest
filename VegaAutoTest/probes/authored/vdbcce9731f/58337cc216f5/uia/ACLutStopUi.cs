using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public class ACLutIdleStopUi : L2TestBase
{
    public ACLutIdleStopUi() : base("_wi3608_idle_stop.txt") { }

    [Fact]
    public void Idle_stop_cancel_preserves_displayed_calibration_rows()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
        FindAction(@"^(Calibration|校准)$").Click();
        FindAction(@"AC\s*LUT|AC\s*Lut|AC查找表").Click();
        var stop = FindAction(@"^(Stop|停止)$", ControlType.Button);
        string before = TableRows();
        stop.Click();
        Window? question = null;
        WaitUntil(() =>
        {
            question = MainWindow!.ModalWindows.FirstOrDefault(w => w.FindAllDescendants().Any(e => e.Name == "Program is not executing, continue to stop?" || e.Name == "程序未在执行，确认停止？"));
            return question != null;
        }, TimeSpan.FromSeconds(15));
        Check(question != null, "idle-stop-question", "localized idle-stop confirmation", question?.Name ?? "absent");
        var cancel = question!.FindAllDescendants().FirstOrDefault(e => e.ControlType == ControlType.Button && Regex.IsMatch(e.Name, @"^(Cancel|取消)$"));
        if (cancel == null)
        {
            Assertions.Fail("cancel-action", "workflow", "Cancel idle Stop", "Cancel button", "absent");
            Assert.True(false, "Idle-stop confirmation has no Cancel button.");
        }
        cancel!.AsButton().Invoke();
        bool closed = WaitUntil(() => !MainWindow!.ModalWindows.Any(w => w.FindAllDescendants().Any(e => e.Name == "Program is not executing, continue to stop?" || e.Name == "程序未在执行，确认停止？")), TimeSpan.FromSeconds(10));
        Check(closed, "idle-stop-cancel-closes", "confirmation closed", closed.ToString());
        string after = TableRows();
        Check(before == after, "idle-stop-preserves-rows", before, after);
    }
    private AutomationElement FindAction(string pattern, ControlType? type = null)
    {
        AutomationElement? found = null;
        WaitUntil(() =>
        {
            found = MainWindow!.FindAllDescendants().FirstOrDefault(e =>
                (type.HasValue ? e.ControlType == type.Value : e.ControlType == ControlType.Button || e.ControlType == ControlType.TreeItem || e.ControlType == ControlType.TabItem)
                && e.IsEnabled && Regex.IsMatch(e.Name, pattern, RegexOptions.IgnoreCase));
            return found != null;
        }, TimeSpan.FromSeconds(30));
        if (found == null)
        {
            Assertions.Fail("screen-action", "workflow", pattern, "enabled screen action", "absent");
            Assert.True(false, "Missing screen action: " + pattern);
        }
        return found!;
    }
    private string TableRows()
    {
        var grids = MainWindow!.FindAllDescendants().Where(e => e.ControlType == ControlType.DataGrid).ToArray();
        if (grids.Length == 0)
        {
            Assertions.Fail("results-grid", "workflow", "ACLut results table", "visible results grid", "absent");
            Assert.True(false, "ACLut results grid unavailable.");
        }
        return string.Join("\n", grids.SelectMany(g => g.FindAllDescendants()).Where(e => e.ControlType == ControlType.DataItem || e.ControlType == ControlType.Text).Select(e => e.Name));
    }
    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var timer = Stopwatch.StartNew();
        do { if (condition()) return true; Thread.Sleep(100); } while (timer.Elapsed < timeout);
        return false;
    }
    private void Check(bool ok, string id, string expected, string actual)
    {
        if (ok) Assertions.Pass(id, "ui-business", id, expected, actual);
        else Assertions.Fail(id, "ui-business", id, expected, actual);
        Assert.True(ok, id + ": " + actual);
    }
}
