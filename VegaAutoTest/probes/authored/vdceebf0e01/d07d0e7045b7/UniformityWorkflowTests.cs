using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Utility.Helper;
using Xunit;
using static Vega.AutoTest.Authored.UniformityTestData;

namespace Vega.AutoTest.Authored;

[Collection("Uniformity regression")]
public sealed class UniformityWorkflowTests : L2TestBase, IDisposable
{
    private string _bin = "", _fixture = "";
    private readonly Dictionary<string, byte[]> _original = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    public UniformityWorkflowTests() : base("uniformity-3722-ui.txt") { }

    [Fact]
    public void ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics()
    {
        try
        {
            string root = Environment.GetEnvironmentVariable("VEGA_SW_ROOT")
                ?? throw new InvalidOperationException("VEGA_SW_ROOT must identify the supplied commit runtime.");
            _bin = Path.Combine(root, "Vega", "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64");
            foreach (string p in Directory.GetFiles(_bin, "UniformityCalibrationData.json", SearchOption.AllDirectories))
                _original[p] = File.ReadAllBytes(p);
            string marker = "SYNTHETIC-3722-" + Guid.NewGuid().ToString("N");
            _fixture = Path.Combine(AppContext.BaseDirectory, marker + ".json");
            File.WriteAllText(_fixture, LegacyJson(marker + "-A"));
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            MainWindow!.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.MENU, VirtualKeyShort.KEY_C);
            OpenNode("uniformity", "均匀性");
            var import = WaitElement(ControlType.Button, e => e.Name.StartsWith("Import", StringComparison.OrdinalIgnoreCase) || e.Name.StartsWith("导入"));
            import.AsButton().Invoke();
            Window? dialog = null;
            Wait(() => (dialog = MainWindow.ModalWindows.FirstOrDefault()) != null, "import file dialog");
            var edits = dialog!.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
            var filename = edits.FirstOrDefault(e => e.Properties.AutomationId.ValueOrDefault == "1148")
                ?? edits.FirstOrDefault(e => e.Properties.AutomationId.ValueOrDefault == "1001")
                ?? edits.Last();
            filename.AsTextBox().Text = _fixture;
            dialog.FindFirstDescendant(cf => cf.ByAutomationId("1")).AsButton().Invoke();
            WaitMarker(marker + "-A");
            CheckStatistics("import", 10, 100);
            string dataPath = Directory.GetFiles(_bin, "UniformityCalibrationData.json", SearchOption.AllDirectories)
                .Single(p => File.ReadAllText(p).Contains(marker, StringComparison.Ordinal));
            CheckSaved(dataPath, "import", 30);
            Leave(marker + "-A");
            var changed = JsonHelper.FromJson<ObservableCollection<UniformityCaliAssociatedResult>>(File.ReadAllText(dataPath))!;
            foreach (var row in changed)
            {
                row.WaferID = marker + "-B";
                row.RecipePath = "synthetic/" + marker + "-B";
                if (row.CalibrationMode == CalibrationMode.Verify)
                    foreach (var m in row.UniformityMeasurementResults)
                        foreach (var p in m.SizeRadiusAndPeakDataList) p.PeakData *= 2;
            }
            File.WriteAllText(dataPath, JsonHelper.ToJson(changed));
            OpenNode("uniformity", "均匀性");
            WaitMarker(marker + "-B");
            Check(Assertions, "reload.marker", Boxes().Any(b => b.Text == marker + "-B"),
                "externally changed wafer ID displayed without Save or restart", marker + "-B");
            CheckStatistics("reload", 20, 100);
            Leave(marker + "-B");
            CheckSaved(dataPath, "navigation-save", 60);
        }
        catch (Exception e)
        {
            Assertions.Fail("workflow.execution", "execution", "actual Uniformity import/navigation workflow",
                "import and reload business results", e.ToString());
            throw;
        }
        finally { Assertions.Flush(); }
    }

    private TextBox[] Boxes() => MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
        .Where(e => !e.Properties.IsOffscreen.ValueOrDefault).Select(e => e.AsTextBox()).ToArray();
    private static void Wait(Func<bool> condition, string description)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(45))
        {
            if (condition()) return;
            Thread.Sleep(100);
        }
        throw new TimeoutException(description);
    }
    private AutomationElement WaitElement(ControlType type, Func<AutomationElement, bool> match)
    {
        AutomationElement? found = null;
        Wait(() => (found = MainWindow!.FindAllDescendants(cf => cf.ByControlType(type))
            .FirstOrDefault(e => !e.Properties.IsOffscreen.ValueOrDefault && match(e))) != null, "visible " + type);
        return found!;
    }
    private void OpenNode(params string[] names)
    {
        var node = WaitElement(ControlType.TreeItem, e => names.Any(n => e.Name.Contains(n, StringComparison.OrdinalIgnoreCase)));
        node.AsTreeItem().Select();
        node.DoubleClick();
    }
    private void WaitMarker(string marker) => Wait(() => Boxes().Any(b => b.Text == marker), "displayed result " + marker);
    private void Leave(string marker)
    {
        OpenNode("queue", "队列");
        Wait(() => !Boxes().Any(b => b.Text == marker), "navigation away from Uniformity result");
    }
    private void CheckStatistics(string id, double sd, double percent)
    {
        double[] Read() => Boxes().Select(b => double.TryParse(b.Text, NumberStyles.Float,
            CultureInfo.InvariantCulture, out double n) ? n : double.NaN).ToArray();
        Wait(() => Read().Count(n => n == sd) == 3 && Read().Count(n => n == percent) == 3, "three channel statistics");
        var values = Read();
        Check(Assertions, id + ".three-stddev", values.Count(n => n == sd) == 3,
            "DN/DW1/DW2 sample SD = " + sd, string.Join(",", values));
        Check(Assertions, id + ".three-percent", values.Count(n => n == percent) == 3,
            "DN/DW1/DW2 measured range/mean percent = " + percent, string.Join(",", values));
    }
    private void CheckSaved(string path, string id, double peak)
    {
        var rows = JsonHelper.FromJson<ObservableCollection<UniformityCaliAssociatedResult>>(File.ReadAllText(path))!;
        var verify = rows.Single(r => r.CalibrationMode == CalibrationMode.Verify);
        Check(Assertions, id + ".channels", verify.UniformityMeasurementResults.Count == 3, "3 channels", verify.UniformityMeasurementResults.Count.ToString());
        foreach (var m in verify.UniformityMeasurementResults)
        {
            Check(Assertions, id + ".boundary." + m.ChannelName, m.SizeMaxMeasuredRadius == 147,
                "147, excluding default tail", m.SizeMaxMeasuredRadius.ToString());
            Check(Assertions, id + ".tail." + m.ChannelName,
                Values(m.SizeRadiusAndPeakDataList).SequenceEqual(new[]
                    { (10d, peak / 3), (146d, peak * 2 / 3), (147d, peak), (148d, peak), (149d, peak), (150d, peak), (151d, peak) }),
                "original three measurements plus four constant tail points", JsonHelper.ToJson(m.SizeRadiusAndPeakDataList));
            Check(Assertions, id + ".legacy." + m.ChannelName, m.RadiusAndPeakDataList.Count == 0,
                "legacy data migrated before persistence", JsonHelper.ToJson(m));
        }
    }
    public new void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { base.Dispose(); }
        finally
        {
            if (Directory.Exists(_bin))
                foreach (string p in Directory.GetFiles(_bin, "UniformityCalibrationData.json", SearchOption.AllDirectories))
                    if (!_original.ContainsKey(p)) File.Delete(p);
            foreach (var p in _original) File.WriteAllBytes(p.Key, p.Value);
            if (File.Exists(_fixture)) File.Delete(_fixture);
        }
    }
}
