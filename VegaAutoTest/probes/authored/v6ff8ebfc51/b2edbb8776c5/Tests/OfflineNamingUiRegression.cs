#nullable enable
using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("OfflineNamingDesktop", DisableParallelization = true)]
public class OfflineNamingDesktopCollection { }

[Collection("OfflineNamingDesktop")]
public class OfflineNamingUiRegression : L2TestBase
{
    private UIA3Automation? _automation;
    public OfflineNamingUiRegression() : base("offline-job-name-ui.txt") { }

    [Fact]
    public void BothReachableDialogsRejectIllegalAndOverlengthNamesWithoutClosing()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            using var automation = new UIA3Automation();
            _automation = automation;
            foreach (bool recipe in new[] { true, false })
            {
                string entry = recipe ? "recipe" : "workspace";
                OpenEntry(recipe);
                string prefix = recipe ? "RunCurRecipeOfflineJob_" : "WorkSpaceOfflineJob_";
                var dialog = Probe(() => FindDialog(prefix), 10000);
                if (dialog is null)
                    throw new UiPrerequisiteException("OFFLINE_DIALOG_NOT_OPENED", entry + ": offline dialog did not become available after the screen action.");
                NamingOracle.Is(Assertions, entry + "-visible-label",
                    dialog.Root.FindAllDescendants(cf => cf.ByControlType(ControlType.Text)).Any(e =>
                        e.Name == "JobName:" || e.Name == "Job 名称:"), "localized JobName label", "visible dialog text inspected");
                NamingOracle.Is(Assertions, entry + "-prefill", Regex.IsMatch(dialog.Name.Text,
                    "^" + Regex.Escape(prefix) + "[0-9]{17}$"), prefix + " plus 17 timestamp digits", dialog.Name.Text);

                string[] invalid = "\\/:*?\"<>|".Select(c => "Bad" + c + "Name").Append(new string('A', 101)).ToArray();
                for (int i = 0; i < invalid.Length; i++)
                {
                    dialog.Name.Text = invalid[i];
                    Button(dialog.Root, "OK", "Ok", "确定", "确认").Invoke();
                    string[] expected = i == 9
                        ? new[] { "Job name exceeds 100 characters.", "Job 名称超过 100 个字符。" }
                        : new[] { "Job name contains characters not allowed in file names (\\ / : * ? \" < > |).",
                            "Job 名称含文件名非法字符（\\ / : * ? \" < > |）。" };
                    var warning = Probe(() => Roots().FirstOrDefault(root => root.FindAllDescendants(
                        cf => cf.ByControlType(ControlType.Text)).Any(e => expected.Contains(e.Name))), 5000);
                    NamingOracle.Is(Assertions, entry + "-warning-" + i, warning is not null,
                        string.Join(" OR ", expected), warning is null ? "expected warning absent" : "expected warning displayed");
                    Button(warning!, "OK", "Ok", "确定", "确认").Invoke();
                    bool dismissed = Until(() => !Roots().Any(root => root.FindAllDescendants(
                        cf => cf.ByControlType(ControlType.Text)).Any(e => expected.Contains(e.Name))), 5000);
                    if (!dismissed)
                        NamingOracle.Is(Assertions, entry + "-dismiss-warning-" + i, false, "warning dismissed", "warning still displayed");
                    NamingOracle.Equal(Assertions, entry + "-input-retained-" + i, invalid[i], dialog.Name.Text);
                    int handle = dialog.Root.Properties.NativeWindowHandle.Value;
                    NamingOracle.Is(Assertions, entry + "-blocked-close-" + i,
                        Roots().Any(root => root.Properties.NativeWindowHandle.Value == handle),
                        "offline configuration remains open after rejected OK", "dialog handle=" + handle);
                }
                dialog.Name.Text = "";
                NamingOracle.Equal(Assertions, entry + "-clear-input", "", dialog.Name.Text);
                int dialogHandle = dialog.Root.Properties.NativeWindowHandle.Value;
                Button(dialog.Root, "Cancel", "取消").Invoke();
                NamingOracle.Is(Assertions, entry + "-cancel-closes", Until(() => !Roots().Any(root =>
                    root.Properties.NativeWindowHandle.Value == dialogHandle), 5000), "dialog closed after Cancel", "dialog handle=" + dialogHandle);
            }
        }
        finally { Assertions.Flush(); }
    }

    private AutomationElement[] Roots()
    {
        int process = MainWindow!.Properties.ProcessId.Value;
        return _automation!.GetDesktop().FindAllChildren().Where(e => e.Properties.ProcessId.Value == process).ToArray();
    }

    private sealed record NameDialog(AutomationElement Root, TextBox Name);

    private NameDialog? FindDialog(string prefix)
    {
        int mainHandle = MainWindow!.Properties.NativeWindowHandle.Value;
        foreach (var root in Roots().Where(e => e.Properties.NativeWindowHandle.Value != mainHandle))
        {
            var name = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit))
                .Select(e => e.AsTextBox()).FirstOrDefault(e => e.Text.StartsWith(prefix, StringComparison.Ordinal));
            if (name is not null) return new NameDialog(root, name);
        }
        return null;
    }

    private Button Button(AutomationElement root, params string[] names)
    {
        var button = root.FindAllDescendants(cf => cf.ByControlType(ControlType.Button))
            .FirstOrDefault(e => names.Contains(e.Name));
        if (button is null)
            NamingOracle.Is(Assertions, "dialog-action-" + string.Join("-", names), false,
                "visible dialog action button", "button absent");
        return button!.AsButton();
    }

    private AutomationElement? FindId(string id) => Roots().Select(root =>
        root.FindFirstDescendant(cf => cf.ByAutomationId(id))).FirstOrDefault(e => e is not null);

    private void OpenEntry(bool recipe)
    {
        MainWindow!.Focus();
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.MENU,
            recipe ? VirtualKeyShort.KEY_N : VirtualKeyShort.KEY_J);
        string gridId = recipe ? "RecipeLibraryView_dataGrid" : "JobLibraryView_dataGrid";
        var grid = Probe(() => MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(gridId)), 2000);
        var row = grid?.FindAllDescendants(cf => cf.ByControlType(ControlType.DataItem))
            .FirstOrDefault(e => !e.Properties.IsOffscreen.Value);
        row?.DoubleClick();
        string menuId = recipe ? "NavigationTreeView_RunCurrentRecipeOfflineMenuItem" : "NavigationTreeView_StartOfflineMenuItem";
        for (int round = 0; round < 3; round++)
        {
            var items = MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem))
                .Where(e => !e.Properties.IsOffscreen.Value && e.IsEnabled).Take(30).ToArray();
            foreach (var item in items)
            {
                item.DoubleClick();
                item.RightClick();
                var menu = Probe(() => FindId(menuId), 500);
                if (menu is not null && menu.IsEnabled)
                {
                    menu.AsMenuItem().Invoke();
                    return;
                }
                Keyboard.Type(VirtualKeyShort.ESCAPE);
            }
        }
        throw new UiPrerequisiteException("OFFLINE_WORKSPACE_FIXTURE_MISSING",
            "Prepare at least one saved runnable " + (recipe ? "recipe" : "Workspace Job with an enabled lot and resolvable recipe") +
            "; no enabled offline context-menu action was reachable through its workspace tree.");
    }

    private static T? Probe<T>(Func<T?> action, int milliseconds) where T : class
    {
        var timer = Stopwatch.StartNew();
        do
        {
            var result = action();
            if (result is not null) return result;
            Thread.Sleep(100);
        } while (timer.ElapsedMilliseconds < milliseconds);
        return null;
    }

    private static bool Until(Func<bool> action, int milliseconds)
    {
        var timer = Stopwatch.StartNew();
        do
        {
            if (action()) return true;
            Thread.Sleep(100);
        } while (timer.ElapsedMilliseconds < milliseconds);
        return false;
    }
}
