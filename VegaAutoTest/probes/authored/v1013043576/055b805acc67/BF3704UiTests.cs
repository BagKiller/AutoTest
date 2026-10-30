using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("BF3704")]
public sealed class BF3704UiWorkflowTests : L2TestBase
{
    public BF3704UiWorkflowTests() : base("bf3704-ui.txt") { }
    private AutomationElement[] Elements(ControlType type) => MainWindow!.FindAllDescendants(cf => cf.ByControlType(type)).Where(e => !e.IsOffscreen).ToArray();
    private AutomationElement Label(params string[] names) => Elements(ControlType.Text).First(e => names.Contains(e.Name));
    private AutomationElement RightOf(AutomationElement label, ControlType type)
    {
        var box = label.BoundingRectangle;
        return Elements(type).Where(e => e.BoundingRectangle.Left >= box.Right - 1 && Math.Abs((e.BoundingRectangle.Top + e.BoundingRectangle.Height / 2) - (box.Top + box.Height / 2)) < 16)
            .OrderBy(e => e.BoundingRectangle.Left).First();
    }
    private string Value(params string[] labels) => RightOf(Label(labels), ControlType.Text).Name;
    private Button Button(params string[] names) => Elements(ControlType.Button).First(e => names.Contains(e.Name)).AsButton();
    private string Stat(int channel, params string[] labels)
    {
        var label = Elements(ControlType.Text).Where(e => labels.Contains(e.Name)).OrderBy(e => e.BoundingRectangle.Left).ElementAt(channel);
        return RightOf(label, ControlType.Text).Name;
    }
    private double Numeric(string text) => double.Parse(text.Trim().TrimEnd('%'), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture);
    private void Check(string id, bool condition, string expected, object? actual) => BF3704Check.That(Assertions, id, condition, expected, actual);
    private void Wait(Func<bool> predicate, int timeout = 15000)
    {
        var timer = Stopwatch.StartNew();
        while (true)
        {
            try { if (predicate()) return; }
            catch (InvalidOperationException) { }
            if (timer.ElapsedMilliseconds > timeout) throw new TimeoutException("UI business state did not settle.");
            Thread.Sleep(100);
        }
    }
    private void CheckContrast(int channel)
    {
        var max = Numeric(Stat(channel, "Max", "最大值"));
        var min = Numeric(Stat(channel, "Min", "最小值"));
        var shown = Stat(channel, "Contrast", "对比度");
        Check("ui-extrema-order-" + channel, max >= min && min >= 0, "max >= min >= 0", max + "/" + min);
        Check("ui-contrast-percent-symbol-" + channel, shown.EndsWith("%", StringComparison.Ordinal), "percentage", shown);
        // Each displayed number rounds to F0. Derive allowable contrast from
        // the entire rounding intervals of the two displayed extrema.
        var lowMax = Math.Max(0, max - 0.5); var highMax = max + 0.5;
        var lowMin = Math.Max(0, min - 0.5); var highMin = min + 0.5;
        var lower = lowMax + highMin > 0 ? 100 * (lowMax - highMin) / (lowMax + highMin) : 0;
        var upper = highMax + lowMin > 0 ? 100 * (highMax - lowMin) / (highMax + lowMin) : 100;
        var contrast = Numeric(shown);
        Check("ui-contrast-business-output-" + channel, contrast >= lower - 0.500001 && contrast <= upper + 0.500001, "contract percentage within F0 rounding intervals", contrast);
    }
    [Fact]
    public void OperatorCanSelectSecondsValidateDurationRecordAndStopWithResults()
    {
        var acquisitionStarted = false;
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            MainWindow!.Patterns.Window.Pattern.SetWindowVisualState(WindowVisualState.Maximized);
            MainWindow.FindFirstDescendant(cf => cf.ByAutomationId("CalibrationButton")).AsButton().Invoke();
            Wait(() => Elements(ControlType.TreeItem).Any(e => IsBfNode(e.Name)));
            Elements(ControlType.TreeItem).First(e => IsBfNode(e.Name)).DoubleClick();
            Wait(() => Elements(ControlType.Text).Any(e => e.Name == "PD1CountAverage/time"));
            Check("ui-initial-pd1-max-no-data", Stat(0, "Max", "最大值") == "N/A", "N/A", Stat(0, "Max", "最大值"));
            Check("ui-initial-pd2-contrast-no-data", Stat(1, "Contrast", "对比度") == "N/A", "N/A", Stat(1, "Contrast", "对比度"));
            var combo = RightOf(Label("Avg (s)", "均值(秒)"), ControlType.ComboBox).AsComboBox();
            combo.Expand();
            var choices = combo.Items.Select(x => x.Text).ToArray();
            Check("ui-seconds-options", choices.SequenceEqual(Enumerable.Range(1, 10).Select(x => x.ToString(CultureInfo.InvariantCulture))), "1 through 10", string.Join(",", choices));
            combo.Select("10");
            Check("ui-ten-second-selection", combo.SelectedItem.Text == "10", "10", combo.SelectedItem.Text);
            combo.Select("1");
            Check("ui-one-second-selection", combo.SelectedItem.Text == "1", "1", combo.SelectedItem.Text);
            var minutes = RightOf(Label("Record (min)", "记录时长(分)"), ControlType.Edit).AsTextBox();
            Check("ui-idle-record-disabled", !Button("Start Record", "开始记录").IsEnabled, "disabled until acquisition", Button("Start Record", "开始记录").IsEnabled);
            minutes.Text = "0";
            Check("ui-invalid-duration-does-not-block-start", Button("Start", "开始").IsEnabled, "acquisition Start enabled", Button("Start", "开始").IsEnabled);
            Button("Start", "开始").Invoke(); acquisitionStarted = true;
            Wait(() => !Button("Start", "开始").IsEnabled && Value("PD1CountAverage/time") != "0");
            Check("ui-zero-duration-record-disabled", !Button("Start Record", "开始记录").IsEnabled, "disabled", Button("Start Record", "开始记录").IsEnabled);
            minutes.Text = "121";
            Wait(() => !Button("Start Record", "开始记录").IsEnabled);
            Check("ui-over-limit-record-disabled", !Button("Start Record", "开始记录").IsEnabled, "disabled", Button("Start Record", "开始记录").IsEnabled);
            minutes.Text = "120";
            Wait(() => Button("Start Record", "开始记录").IsEnabled);
            Check("ui-upper-bound-valid", Button("Start Record", "开始记录").IsEnabled, "enabled at 120", minutes.Text);
            minutes.Text = "1";
            Wait(() => Button("Start Record", "开始记录").IsEnabled);
            Button("Start Record", "开始记录").Invoke();
            Wait(() => Elements(ControlType.Button).Any(e => e.Name == "Stop Record" || e.Name == "停止记录"));
            Check("ui-record-button-switched-command", Button("Stop Record", "停止记录").IsEnabled, "enabled Stop Record", Button("Stop Record", "停止记录").IsEnabled);
            Wait(() => Stat(0, "Max", "最大值") != "N/A" && Stat(1, "Max", "最大值") != "N/A");
            Wait(() => Value("Remaining", "剩余时间") != "01:00", 5000);
            var remaining = Value("Remaining", "剩余时间");
            Check("ui-countdown-business-value", TimeSpan.TryParseExact(remaining, "mm\\:ss", CultureInfo.InvariantCulture, out var duration) && duration > TimeSpan.Zero && duration < TimeSpan.FromMinutes(1), "positive remaining time below one minute", remaining);
            Button("Stop Record", "停止记录").Invoke();
            Wait(() => Elements(ControlType.Button).Any(e => e.Name == "Start Record" || e.Name == "开始记录"));
            var final = new[] { Stat(0, "Max", "最大值"), Stat(0, "Min", "最小值"), Stat(0, "Contrast", "对比度"), Stat(1, "Max", "最大值"), Stat(1, "Min", "最小值"), Stat(1, "Contrast", "对比度") };
            CheckContrast(0); CheckContrast(1);
            Thread.Sleep(1200);
            var retained = new[] { Stat(0, "Max", "最大值"), Stat(0, "Min", "最小值"), Stat(0, "Contrast", "对比度"), Stat(1, "Max", "最大值"), Stat(1, "Min", "最小值"), Stat(1, "Contrast", "对比度") };
            Check("ui-manual-stop-retains-results", final.SequenceEqual(retained), string.Join(",", final), string.Join(",", retained));
            Check("ui-acquisition-continues-after-record-stop", !Button("Start", "开始").IsEnabled, "acquisition still running", Button("Start", "开始").IsEnabled);
            Button("Stop", "停止").Invoke();
            Wait(() => Button("Start", "开始").IsEnabled); acquisitionStarted = false;
            var pd1 = Numeric(Value("PD1CountAverage/time"));
            var pd2 = Numeric(Value("PD2CountAverage/time"));
            var sum = Numeric(Value("Sum", "总和"));
            Check("ui-count-sum", Math.Abs(sum - pd1 - pd2) <= 1.500001, "PD1 + PD2 within three F0 rounding errors", sum + " versus " + pd1 + "+" + pd2);
            Check("ui-acquisition-stop-disables-record", !Button("Start Record", "开始记录").IsEnabled, "disabled", Button("Start Record", "开始记录").IsEnabled);
        }
        catch (Exception e)
        {
            Assertions.Fail("ui-execution-or-mismatch", "uia", "BF operator workflow", "Business actions and comparisons complete", e.ToString());
            throw;
        }
        finally
        {
            try
            {
                if (acquisitionStarted && MainWindow != null)
                {
                    try { Button("Stop", "停止").Invoke(); } catch { }
                }
            }
            finally { Assertions.Flush(); }
        }
    }
    private static bool IsBfNode(string name)
    {
        var compact = name.Replace(" ", "");
        return compact.Equals("BF", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("BFCalibration", StringComparison.OrdinalIgnoreCase)
            || compact.Equals("BF校准", StringComparison.OrdinalIgnoreCase);
    }
}
