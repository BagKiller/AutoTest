using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public sealed class ForceAbortAuditUiTests : CommitUiBase
{
    public ForceAbortAuditUiTests() : base("_wi3735_force_abort_audit.txt") { }

    [Fact]
    public void ForceAbortRecordsOneShowingAndOneTimestampedChoiceForCancelAndOk()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            OpenLogs();
            var baseline = AuditRows();
            CloseLogWindows();
            foreach (var choice in new[] { "Cancel", "Ok" })
            {
                ClickId("OperationMenuItem");
                var action = FindId("ForceAbortAllJobsMenuItem");
                var label = NameOf(action).Replace("&", "").Trim();
                Require("Force Abort enabled", action.IsEnabled, "Enabled Force Abort All Jobs menu action");
                action.Click();
                AutomationElement? dialog = null;
                Until(choice + " confirmation", () =>
                {
                    dialog = Roots().FirstOrDefault(root => root.Properties.NativeWindowHandle.ValueOrDefault
                        != MainWindow!.Properties.NativeWindowHandle.ValueOrDefault
                        && TextOf(root).Contains(label, StringComparison.Ordinal)
                        && Buttons(root).Any(button => IsChoice(NameOf(button), "Cancel")));
                    return dialog != null;
                });
                Check(choice + "/confirmation", TextOf(dialog!).Contains(label, StringComparison.Ordinal),
                    "Confirmation identifies " + label, TextOf(dialog!));
                var button = Buttons(dialog!).FirstOrDefault(b => IsChoice(NameOf(b), choice));
                Require(choice + " button", button != null && button.IsEnabled, "Enabled " + choice + " button");
                button!.AsButton().Invoke();
                OpenLogs();
                string[] added = Array.Empty<string>();
                const string showing = "[Audit] ForceAbortAllJobs: showing confirmation dialog.";
                var chosen = "[Audit] ForceAbortAllJobs: user chose " + choice + ".";
                Until(choice + " audit rows", () =>
                {
                    added = Difference(AuditRows(), baseline);
                    return added.Any(r => r.Contains(showing, StringComparison.Ordinal))
                        && added.Any(r => r.Contains(chosen, StringComparison.Ordinal));
                });
                var debug = added.Where(r => r.Contains(showing, StringComparison.Ordinal)).ToArray();
                var information = added.Where(r => r.Contains(chosen, StringComparison.Ordinal)).ToArray();
                Check(choice + "/showing-log", debug.Length == 1 && debug[0].Contains("HorizontalToolBar")
                    && Regex.IsMatch(debug[0], @"\b(Debug|DBG)\b|调试", RegexOptions.IgnoreCase),
                    "Exactly one HorizontalToolBar Debug showing record", string.Join("\n", debug));
                Check(choice + "/choice-log", information.Length == 1 && information[0].Contains("HorizontalToolBar")
                    && Regex.IsMatch(information[0], @"\b(Information|Info|INF)\b|信息", RegexOptions.IgnoreCase),
                    "Exactly one HorizontalToolBar Information record: " + chosen, string.Join("\n", information));
                Check(choice + "/timestamps", debug.Concat(information).All(row =>
                    Regex.IsMatch(row, @"\b\d{2}:\d{2}:\d{2}(?:[.,]\d+)?\b")),
                    "Both audit records expose a timestamp including seconds", string.Join("\n", debug.Concat(information)));
                baseline = AuditRows();
                CloseLogWindows();
            }
        }
        finally { Assertions.Flush(); }
    }

    private static AutomationElement[] Buttons(AutomationElement root) =>
        root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));

    private static bool IsChoice(string name, string choice)
    {
        var normalized = name.Replace("&", "").Trim().ToLowerInvariant();
        return (choice == "Cancel" ? new[] { "cancel", "取消" } : new[] { "ok", "确定", "确认", "是", "yes" })
            .Contains(normalized);
    }

    private void OpenLogs()
    {
        Menu("ExplorerMenuItem", "ShowLogsMenuItem");
        Until("Log rows available", () => Roots().Any(root => root.FindAllDescendants(cf =>
            cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.ListItem))).Length > 0));
        foreach (var combo in Roots().SelectMany(root => root.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox))))
        {
            var identity = NameOf(combo) + " " + combo.Properties.AutomationId.ValueOrDefault;
            if (!Regex.IsMatch(identity, "level|severity|级别|等级", RegexOptions.IgnoreCase)) continue;
            var box = combo.AsComboBox();
            box.Expand();
            var all = box.Items.FirstOrDefault(item => new[] { "all", "全部", "所有" }
                .Contains(NameOf(item).Trim().ToLowerInvariant()));
            if (all != null) box.Select(NameOf(all));
            else box.Collapse();
        }
    }

    private string[] AuditRows() => Roots().SelectMany(root => root.FindAllDescendants(cf =>
        cf.ByControlType(ControlType.DataItem).Or(cf.ByControlType(ControlType.ListItem))))
        .Select(TextOf).Where(text => text.Contains("[Audit] ForceAbortAllJobs:", StringComparison.Ordinal)).ToArray();

    private void CloseLogWindows()
    {
        foreach (var root in Roots())
            if (root.Properties.ControlType.Value == ControlType.Window
                && root.Properties.NativeWindowHandle.ValueOrDefault != MainWindow!.Properties.NativeWindowHandle.ValueOrDefault
                && Regex.IsMatch(NameOf(root), "log|日志", RegexOptions.IgnoreCase)) root.AsWindow().Close();
    }

    private static string[] Difference(IEnumerable<string> current, IEnumerable<string> previous)
    {
        var counts = previous.GroupBy(row => row).ToDictionary(group => group.Key, group => group.Count());
        var added = new List<string>();
        foreach (var row in current)
        {
            if (counts.TryGetValue(row, out var count) && count > 0) counts[row] = count - 1;
            else added.Add(row);
        }
        return added.ToArray();
    }
}
