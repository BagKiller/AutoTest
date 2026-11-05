#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public abstract class DedicatedConfigurationUiBase : L2TestBase
{
    protected DedicatedConfigurationUiBase(string evidence) : base(evidence) { }
    protected static bool ModeLabel(AutomationElement e) => new[] {
        "Notch Scan Mode", "缺口扫描模式", "NotchScanMode" }.Contains(e.Name);
    protected static void WaitUntil(Func<bool> predicate, string failure)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (predicate()) return;
            Thread.Sleep(100);
        }
        throw new Xunit.Sdk.XunitException(failure);
    }
    protected void OpenEdgeDetection()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
        MainWindow!.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.MENU, VirtualKeyShort.KEY_F);
        AutomationElement? section = null;
        WaitUntil(() =>
        {
            section = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                .FirstOrDefault(e => new[] { "Edge Detection", "边缘检测" }.Contains(e.Name));
            return section != null;
        }, "Edge Detection configuration tree item did not appear.");
        section!.Click();
        WaitUntil(() => MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit)).Length > 0,
            "Edge Detection configuration values did not render.");
    }
    protected void Check(string id, bool condition, string expected, string actual)
    {
        if (condition) Assertions.Pass(id, "UI business output", id, expected, actual);
        else Assertions.Fail(id, "UI business output", id, expected, actual);
        Assert.True(condition, $"{id}: expected {expected}; actual {actual}");
    }
    protected ComboBox? FindModeCombo()
    {
        var label = MainWindow!.FindAllDescendants().FirstOrDefault(ModeLabel);
        for (var parent = label?.Parent; parent != null && !parent.Equals(MainWindow); parent = parent.Parent)
        {
            var combos = parent.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox));
            if (combos.Length == 1) return combos[0].AsComboBox();
            if (combos.Length > 1) return null;
        }
        return null;
    }
}

public sealed class DedicatedAlignmentV100ConfigurationUi : DedicatedConfigurationUiBase
{
    public DedicatedAlignmentV100ConfigurationUi() : base("wi3716-v100-configuration.txt") { }
    [Fact]
    public void V100OffersBothScanModesInConfigurationEditor()
    {
        try
        {
            OpenEdgeDetection();
            ComboBox? combo = null;
            WaitUntil(() => (combo = FindModeCombo()) != null, "Notch Scan Mode combo did not render.");
            Check("mode.editor", combo != null, "Notch Scan Mode combo", combo?.Name ?? "missing");
            Check("mode.editable", combo!.IsEnabled, "enabled", combo.IsEnabled.ToString());
            string selected = combo.SelectedItem?.Name ?? "";
            Check("mode.current", new[] { "Inline", "Dedicated" }.Contains(selected), "Inline or Dedicated", selected);
            combo.Expand();
            try
            {
                var options = combo.Items.Select(item => item.Name).Distinct().OrderBy(x => x).ToArray();
                Check("mode.options", options.SequenceEqual(new[] { "Dedicated", "Inline" }),
                    "Dedicated,Inline", string.Join(",", options));
            }
            finally { combo.Collapse(); }
        }
        finally { Assertions.Flush(); }
    }
}

public sealed class DedicatedAlignmentOtherModelsConfigurationUi : DedicatedConfigurationUiBase
{
    public DedicatedAlignmentOtherModelsConfigurationUi() : base("wi3716-other-model-configuration.txt") { }
    [Fact]
    public void OtherModelsHideV100ScanModeSetting()
    {
        try
        {
            OpenEdgeDetection();
            var fields = MainWindow!.FindAllDescendants();
            Check("otherModels.values", fields.Any(e => e.ControlType == ControlType.Edit),
                "configuration values rendered", "Edit count=" + fields.Count(e => e.ControlType == ControlType.Edit));
            int count = fields.Count(ModeLabel);
            Check("otherModels.modeHidden", count == 0, "no NotchScanMode setting", count.ToString());
        }
        finally { Assertions.Flush(); }
    }
}
