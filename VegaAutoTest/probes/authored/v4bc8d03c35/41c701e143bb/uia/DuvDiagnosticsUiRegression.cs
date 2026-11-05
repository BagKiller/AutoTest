using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

public class DuvDiagnosticsUiRegression : L2TestBase
{
    public DuvDiagnosticsUiRegression() : base("_wi3737_duv_diagnostics.txt") { }
    [Fact]
    public void ModeSelectionIsDuvOnlyAndRetainedWithinTheSession()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs,States,Log).Watch(Vega).WaitReady(MainWindow!,TimeSpan.FromMinutes(3));
            Named(@"^(Diagnostics|诊断|系统诊断)$","diagnostics").Click();
            foreach(var item in MainWindow!.FindAllDescendants(cf=>cf.ByControlType(ControlType.TreeItem)))
                if(Regex.IsMatch(item.Name,@"Data\s*Collection|数据采集",RegexOptions.IgnoreCase)) item.AsTreeItem().Expand();
            Named(@"Edge.*Analy|边缘.*分析","edgeAnalysis").Click();
            var type=Wait(()=>Combos().FirstOrDefault(c=>Names(c).Any(n=>n.Contains("DUV",StringComparison.OrdinalIgnoreCase))),"edgeDataType").AsComboBox();
            var types=Names(type);
            int duv=Array.FindIndex(types,n=>n.Contains("DUV",StringComparison.OrdinalIgnoreCase));
            int bf=Array.FindIndex(types,n=>n.Contains("BF",StringComparison.OrdinalIgnoreCase));
            int edge=Array.FindIndex(types,n=>!n.Contains("DUV",StringComparison.OrdinalIgnoreCase) && !n.Contains("BF",StringComparison.OrdinalIgnoreCase));
            if(duv<0 || bf<0 || edge<0) Check(false,"dataTypeOptions","Edge, DUV, BF",string.Join(",",types));
            type.Select(duv);
            var mode=Wait(()=>FindMode(),"duvMode").AsComboBox();
            Check(Eventually(()=>mode.SelectedItem?.Name=="3"),"ui.mode.default","3",mode.SelectedItem?.Name ?? "none");
            var options=Names(mode);
            Check(options.SequenceEqual(new[]{"1","2","3"}),"ui.mode.options","1,2,3",string.Join(",",options));
            mode.Select(0);
            Check(Eventually(()=>mode.SelectedItem?.Name=="1"),"ui.mode.select","1",mode.SelectedItem?.Name ?? "none");
            type.Select(edge);
            Check(Eventually(()=>FindMode() is null),"ui.mode.hiddenForEdge","hidden",FindMode() is null?"hidden":"visible");
            type.Select(bf);
            Check(Eventually(()=>FindMode() is null),"ui.mode.hiddenForBf","hidden",FindMode() is null?"hidden":"visible");
            type.Select(duv);
            var retained=Wait(()=>FindMode(),"retainedDuvMode").AsComboBox();
            Check(Eventually(()=>retained.SelectedItem?.Name=="1"),"ui.mode.sessionRetention","1",retained.SelectedItem?.Name ?? "none");
        }
        finally { Assertions.Flush(); }
    }
    private ComboBox[] Combos()
        => MainWindow!.FindAllDescendants(cf=>cf.ByControlType(ControlType.ComboBox)).Where(e=>!e.Properties.IsOffscreen.Value && e.IsEnabled).Select(e=>e.AsComboBox()).ToArray();
    private static string[] Names(ComboBox box)
    {
        box.Expand();
        try { return box.Items.Select(i=>i.Name).ToArray(); }
        finally { box.Collapse(); }
    }
    private ComboBox? FindMode()
        => Combos().FirstOrDefault(c=>Names(c).SequenceEqual(new[]{"1","2","3"}));
    private AutomationElement Named(string pattern,string id)
        => Wait(()=>MainWindow!.FindAllDescendants().FirstOrDefault(e=>!e.Properties.IsOffscreen.Value && e.IsEnabled && Regex.IsMatch(e.Name,pattern,RegexOptions.IgnoreCase) && (e.ControlType==ControlType.Button || e.ControlType==ControlType.MenuItem || e.ControlType==ControlType.TreeItem)),id);
    private AutomationElement Wait(Func<AutomationElement?> find,string id)
    {
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed<TimeSpan.FromSeconds(30))
        {
            var element=find();if(element is not null) return element;
            Thread.Sleep(150);
        }
        Check(false,"ui.locate."+id,"control available","timeout");
        throw new InvalidOperationException(id);
    }
    private static bool Eventually(Func<bool> condition)
    {
        var timer=Stopwatch.StartNew();
        while(timer.Elapsed<TimeSpan.FromSeconds(10))
        {
            if(condition()) return true;
            Thread.Sleep(150);
        }
        return false;
    }
    private void Check(bool ok,string id,string expected,string actual)
    {
        if(ok) Assertions.Pass(id,"UI behavior",id,expected,actual);
        else { Assertions.Fail(id,"UI behavior",id,expected,actual);throw new XunitException($"{id}: expected {expected}; actual {actual}"); }
    }
}
