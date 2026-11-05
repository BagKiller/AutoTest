using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public class LpdHeaderUiTests : L2TestBase
{
    public LpdHeaderUiTests() : base("_wi3757_lpd_header_ui.txt") { }

    private AutomationElement? Unit(string name) => MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.RadioButton))
        .FirstOrDefault(e => e.Name == name && !e.Properties.IsOffscreen.ValueOrDefault);

    private void OpenCollectionEditor()
    {
        var visited = new HashSet<string>();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (Unit("µmLSE") != null) return;
            var nodes = MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            foreach (var node in nodes)
            {
                if (node.Patterns.ExpandCollapse.IsSupported &&
                    node.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed)
                    node.AsTreeItem().Expand();
            }
            var collection = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(e => !e.Properties.IsOffscreen.ValueOrDefault && !visited.Contains(e.Name) &&
                    (e.Name.Contains("Collection", StringComparison.OrdinalIgnoreCase) || e.Name.Contains("采集") || e.Name.Contains("集合")));
            if (collection != null)
            {
                visited.Add(collection.Name);
                collection.DoubleClick();
            }
            Thread.Sleep(200);
        }
        Assertions.Fail("collection-editor", "workflow", "Open the active recipe's DF collection editor",
            "Visible LPD unit controls", "No editable DF collection reached in 30 seconds");
        Assert.True(false, "An editable DF recipe must be available in the recipe explorer.");
    }

    [Fact]
    public void SelectingLpdUnitsUpdatesTheVisibleSizingThresholdHeader()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            OpenCollectionEditor();
            foreach (string unit in new[] { "µmLSE", "nPPM", "rPPM" })
            {
                var control = Unit(unit);
                if (control == null)
                {
                    Assertions.Fail("unit-control", "workflow", "Select LPD unit", unit, "Radio button unavailable");
                    Assert.True(false, "LPD unit control unavailable: " + unit);
                }
                control!.Click();
                string english = "Sizing Thresholds (" + unit + ")";
                string chinese = "尺寸阈值（" + unit + "）";
                bool matched = false;
                var timer = Stopwatch.StartNew();
                while (!matched && timer.Elapsed < TimeSpan.FromSeconds(5))
                {
                    matched = MainWindow!.FindAllDescendants().Any(e =>
                        !e.Properties.IsOffscreen.ValueOrDefault && (e.Name == english || e.Name == chinese));
                    if (!matched) Thread.Sleep(100);
                }
                if (matched) Assertions.Pass("header-" + unit, "business", "Rendered threshold unit follows LPD selection", english + " / " + chinese, "Matching visible header");
                else Assertions.Fail("header-" + unit, "business", "Rendered threshold unit follows LPD selection", english + " / " + chinese, "Matching visible header absent");
                Assert.True(matched, "Threshold header did not follow LPD selection: " + unit);
            }
        }
        finally { Assertions.Flush(); }
    }
}
