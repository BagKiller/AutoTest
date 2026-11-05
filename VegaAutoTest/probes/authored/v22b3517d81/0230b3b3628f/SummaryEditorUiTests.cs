using System;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Vega.AutoTest.Harness;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

public class SummaryEditorUiTests : L2TestBase
{
    public SummaryEditorUiTests() : base("_wi3750_summary_editor.txt") { }
    private void Verify(string id, bool ok, string expected, string actual)
    {
        if (!ok)
        {
            Assertions.Fail(id, "uia-business", id, expected, actual);
            throw new XunitException(id + ": " + actual);
        }
        Assertions.Pass(id, "uia-business", id, expected, actual);
    }
    private AutomationElement Missing(string id)
    {
        Assertions.Fail(id, "uia-workflow", "Required editor control or data", id, "Absent after wait");
        throw new XunitException("Missing editor control or data: " + id);
    }
    private static void Wait(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (!ready() && DateTime.UtcNow < deadline) Thread.Sleep(150);
    }
    private AutomationElement Find(string id)
    {
        AutomationElement? result = null;
        Wait(() => (result = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(id))) != null);
        return result ?? Missing(id);
    }
    private static string[] Texts(AutomationElement e) => e.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
        .Select(t => t.Name).Where(t => !string.IsNullOrEmpty(t)).ToArray();
    private void Expand(string id)
    {
        var e = Find(id);
        if (e.Patterns.ScrollItem.IsSupported) e.Patterns.ScrollItem.Pattern.ScrollIntoView();
        e.Patterns.ExpandCollapse.Pattern.Expand();
    }
    private AutomationElement Grid() => Find("WaferSummaryView_WaferSummaryItemsDataGrid");

    [Fact]
    public void EditingPowerUpdatesCompactSummaryAndSingleWideTextCanScroll()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            MainWindow!.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.ALT, VirtualKeyShort.KEY_N);
            Expand("RecipeView_ResultsLayoutExpander");
            string? original = null;
            Wait(() => (original = Texts(Grid()).FirstOrDefault(s => Regex.IsMatch(s, @"^(Oblique|Normal) [A-Za-z]+(?: |$)"))) != null);
            if (original == null) Missing("current-collection-info");
            string throughput = original!.Split(' ')[1];
            Expand("RecipeView_CollectionsExpander");
            var channelGrid = Find("DFChannelSettingView_CollectorSettingsDataGrid");
            var wideRow = channelGrid.FindAllChildren(cf => cf.ByControlType(ControlType.DataItem))
                .FirstOrDefault(row => Texts(row).Any(t => t == "DW" || t == "DW2"));
            if (wideRow == null) Missing("enabled-model-wide-channel-row");
            var enabled = wideRow!.FindFirstDescendant(cf => cf.ByControlType(ControlType.CheckBox));
            if (enabled == null) Missing("wide-channel-enable-checkbox");
            enabled!.AsCheckBox().IsChecked = true;
            foreach (string gear in new[] { "10", "1", "100" })
            {
                Expand("RecipeView_CollectionsExpander");
                Find("DFSizingView_IncidenceGroupRadioButton").Click();
                Find("DFSizingView_PolarizationComboBox").AsComboBox().Select("P");
                var power = Find("DFSizingView_LaserPowerGearComboBox");
                power.Click();
                power.AsComboBox().Select(gear + "%");
                Expand("RecipeView_ResultsLayoutExpander");
                Grid().Click();
                string expected = "Oblique " + throughput + " " + gear + "% P";
                Wait(() => Texts(Grid()).Contains(expected));
                var observed = Texts(Grid());
                Verify("editor-info-" + gear, observed.Contains(expected), expected, string.Join(" | ", observed));
            }
            var texts = Texts(Grid());
            Verify("removed-independent-rows", !texts.Any(t => t == "Polarization" || t == "Laser Power(%)"
                || t == "BEA OD Filter" || t.EndsWith(" Mask", StringComparison.Ordinal)
                || t.EndsWith(" Polarizer", StringComparison.Ordinal)), "No superseded optical row keys", string.Join(" | ", texts));
            Verify("edge-unit", texts.Any(t => Regex.IsMatch(t, @"^[0-9]+(?:[.,][0-9]+)?mm$")),
                "Numeric Edge Exclusion with mm", string.Join(" | ", texts));
            Verify("wide-value-conversion", texts.Any(t => Regex.IsMatch(t, @"(?m)^DW\s+\S+\s+\S+"))
                && !texts.Any(t => Regex.IsMatch(t, @"(?m)^DW[12]\s")), "Track renders DW and retains polarizer/mask", string.Join(" | ", texts));
            Verify("wide-key-conversion", texts.Contains("DW Ideal Gain") && !texts.Contains("DW2 Ideal Gain"),
                "DW Ideal Gain", string.Join(" | ", texts));
            var bounds = Grid().BoundingRectangle;
            var splitter = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Thumb))
                .FirstOrDefault(e => e.BoundingRectangle.Height > 150 && e.BoundingRectangle.Width <= 16
                    && Math.Abs(e.BoundingRectangle.Left - bounds.Right) < 30);
            if (splitter == null) Missing("result-layout-summary-splitter");
            var start = splitter!.GetClickablePoint();
            Mouse.MoveTo(start);
            Mouse.Down(MouseButton.Left);
            try { Mouse.MoveTo(new Point((int)bounds.Left + 205, start.Y)); }
            finally { Mouse.Up(MouseButton.Left); }
            var grid = Grid();
            Wait(() => grid.Patterns.Scroll.IsSupported && grid.Patterns.Scroll.Pattern.HorizontallyScrollable.Value);
            bool scrollable = grid.Patterns.Scroll.IsSupported && grid.Patterns.Scroll.Pattern.HorizontallyScrollable.Value;
            Verify("summary-overflow-scrollable", scrollable, "Horizontal scrolling after narrowing summary", scrollable.ToString());
            var scroll = grid.Patterns.Scroll.Pattern;
            scroll.SetScrollPercent(0, -1);
            double before = scroll.HorizontalScrollPercent.Value;
            scroll.SetScrollPercent(100, -1);
            Wait(() => scroll.HorizontalScrollPercent.Value >= 99);
            double after = scroll.HorizontalScrollPercent.Value;
            Verify("summary-scroll-action", before <= 1 && after >= 99 && after > before,
                "Horizontal position moves from start to end", before + " -> " + after);
        }
        finally { Assertions.Flush(); }
    }
}
