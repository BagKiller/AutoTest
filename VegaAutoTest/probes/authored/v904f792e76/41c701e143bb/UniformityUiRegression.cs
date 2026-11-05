using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Newtonsoft.Json;
using Vega.AutoTest.Harness;
using Xunit;
using static Vega.AutoTest.Authored.UniformitySupport;

namespace Vega.AutoTest.Authored;

public class UniformityUiRegression : L2TestBase
{
    public UniformityUiRegression() : base("uniformity-dual-statistics.txt") { }
    [Fact]
    public void ImportedCalAndVerifyShowBothMeasuredStatisticsForAllThreeChannels()
    {
        string directory = Path.Combine(AppContext.BaseDirectory,"UniformityUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string input = Path.Combine(directory,"input.json");
        var fixture = Fixture();
        File.WriteAllText(input,JsonConvert.SerializeObject(fixture));
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs,States,Log).Watch(Vega).WaitReady(MainWindow!,TimeSpan.FromMinutes(3));
            Activate("Calibration","校准");
            Activate("Uniformity Calibration","Uniformity","均匀性校准");
            Activate("Export","导出");
            Choose(directory,true);
            string backup = Wait(() => Directory.GetFiles(directory,"UniformityCali_*.json").SingleOrDefault(),"exported calibration backup");
            try
            {
                Import(input);
                for (int mode = 0; mode < 2; mode++)
                {
                    SelectMode(mode == 0 ? "Cal" : "Verify");
                    Wait(() => Visible().FirstOrDefault(x => x.Name == fixture[mode].Key),"imported result " + fixture[mode].Key).Click();
                    SpinWait.SpinUntil(() => Labels(false).Length >= 6 && Labels(true).Length >= 6,TimeSpan.FromSeconds(10));
                    var std = Labels(false); var percent = Labels(true);
                    Check(Assertions,$"ui-{mode}-std-panels",std.Length == 6,6,std.Length);
                    Check(Assertions,$"ui-{mode}-percent-panels",percent.Length == 6,6,percent.Length);
                    for (int index = 0; index < 6; index++)
                    {
                        int scale = mode * 3 + index % 3 + 1;
                        string expectedStd = index < 3 ? (scale * .00001).ToString("E2",CultureInfo.CurrentCulture) : (scale * 10d).ToString("F3",CultureInfo.CurrentCulture);
                        string expectedPercent = (index < 3 ? 2d : 20d).ToString("F3",CultureInfo.CurrentCulture);
                        SpinWait.SpinUntil(() => FollowingText(std[index]) == expectedStd && FollowingText(percent[index]) == expectedPercent,TimeSpan.FromSeconds(5));
                        string observedStd = FollowingText(std[index]), observedPercent = FollowingText(percent[index]);
                        Check(Assertions,$"ui-{mode}-{index}-std",observedStd == expectedStd,expectedStd,observedStd);
                        Check(Assertions,$"ui-{mode}-{index}-percent",observedPercent == expectedPercent,expectedPercent,observedPercent);
                    }
                }
            }
            finally { Import(backup); }
        }
        finally { Assertions.Flush(); }
    }
    private AutomationElement[] Visible() => MainWindow!.FindAllDescendants().Where(x => !x.Properties.IsOffscreen.Value).ToArray();
    private T Wait<T>(Func<T?> read, string description) where T : class
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(30))
        {
            T? value = read(); if (value != null) return value;
            Thread.Sleep(100);
        }
        Assertions.Fail("ui-action","workflow",description,"available","not found");
        throw new InvalidOperationException(description + " was not available");
    }
    private void Activate(params string[] names)
    {
        var element = Wait(() => Visible().FirstOrDefault(x => names.Contains(x.Name)),string.Join("/",names));
        if (element.ControlType == ControlType.Button) element.AsButton().Invoke();
        else if (element.ControlType == ControlType.TreeItem) element.DoubleClick();
        else element.Click();
    }
    private void Import(string path) { Activate("Import","导入"); Choose(path,false); }
    private void Choose(string path, bool folder)
    {
        var dialog = Wait(() => MainWindow!.ModalWindows.FirstOrDefault(),"file chooser");
        var edits = dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        var edit = edits.FirstOrDefault(x => new[] { "File name:","文件名:","文件名(N):","Folder:","文件夹:" }.Contains(x.Name)) ?? edits.Last();
        edit.AsTextBox().Text = path;
        string[] buttons = folder ? new[] { "Select Folder","选择文件夹","选择文件夹(S)" } : new[] { "Open","打开","打开(O)" };
        Wait(() => dialog.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault(x => buttons.Contains(x.Name)),"chooser confirmation").AsButton().Invoke();
        Wait(() => MainWindow!.ModalWindows.Length == 0 ? MainWindow : null,"chooser completion");
    }
    private void SelectMode(string mode)
    {
        foreach (var element in Visible().Where(x => x.ControlType == ControlType.ComboBox && x.Properties.IsEnabled.Value))
        {
            var combo = element.AsComboBox(); combo.Expand();
            var item = combo.Items.FirstOrDefault(x => x.Name == mode);
            if (item != null) { combo.Select(mode); return; }
            combo.Collapse();
        }
        Assertions.Fail("ui-mode","workflow","select calibration mode",mode,"mode option not found");
        throw new InvalidOperationException("Calibration mode option was not found: " + mode);
    }
    private AutomationElement[] Labels(bool percent) => Visible().Where(x => x.ControlType == ControlType.Text &&
        (percent ? new[] { "Percent","百分比","百分比(%)" } : new[] { "StdDev","Std Dev","Standard Deviation","标准差" }).Contains(x.Name.Trim()))
        .OrderBy(x => x.BoundingRectangle.Top).ThenBy(x => x.BoundingRectangle.Left).ToArray();
    private string FollowingText(AutomationElement label)
    {
        var box = Visible().Where(x => x.ControlType == ControlType.Edit &&
            x.BoundingRectangle.Left > label.BoundingRectangle.Left && Math.Abs(x.BoundingRectangle.Top-label.BoundingRectangle.Top) < 20)
            .OrderBy(x => x.BoundingRectangle.Left).First();
        return box.AsTextBox().Text;
    }
}
