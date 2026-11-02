using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;
using Vega.AutoTest.Harness;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

public abstract class CommitUiBase : L2TestBase, IDisposable
{
    private readonly UIA3Automation reader = new();
    protected CommitUiBase(string evidence) : base(evidence) { }

    protected AutomationElement[] Roots() => reader.GetDesktop().FindAllChildren(
        cf => cf.ByProcessId(MainWindow!.Properties.ProcessId.Value));

    protected static string NameOf(AutomationElement element) => element.Properties.Name.ValueOrDefault ?? "";
    protected static string TextOf(AutomationElement root) => string.Join(" | ",
        new[] { NameOf(root) }.Concat(root.FindAllDescendants().Select(NameOf))
            .Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());

    protected void Check(string id, bool success, string expected, string actual)
    {
        if (!success)
        {
            Assertions.Fail(id, "UI business behavior", id, expected, actual);
            throw new XunitException(id + ": " + actual);
        }
        Assertions.Pass(id, "UI business behavior", id, expected, actual);
    }

    protected void Require(string id, bool success, string expected)
    {
        if (success) return;
        Assertions.Fail(id, "UI action", id, expected, "Not observed");
        throw new XunitException(id + ": " + expected);
    }

    protected void Until(string id, Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(25))
        {
            if (condition()) return;
            Thread.Sleep(150);
        }
        Require(id, false, "Condition satisfied within 25 seconds");
    }

    protected AutomationElement FindId(string id)
    {
        AutomationElement? found = null;
        Until("Find " + id, () =>
        {
            found = Roots().Select(r => r.FindFirstDescendant(cf => cf.ByAutomationId(id)))
                .FirstOrDefault(e => e != null);
            return found != null;
        });
        return found!;
    }

    protected void ClickId(string id)
    {
        var element = FindId(id);
        Require(id + " enabled", element.IsEnabled, "Enabled action");
        element.Click();
    }

    protected void Menu(string parent, string child)
    {
        ClickId(parent);
        ClickId(child);
    }

    protected void ExpandNavigation()
    {
        for (var depth = 0; depth < 12; depth++)
        {
            var expanded = false;
            foreach (var item in MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)))
            {
                var pattern = item.Patterns.ExpandCollapse.PatternOrDefault;
                if (pattern != null && pattern.ExpandCollapseState.Value == ExpandCollapseState.Collapsed)
                {
                    pattern.Expand();
                    expanded = true;
                }
            }
            if (!expanded) return;
            Thread.Sleep(100);
        }
        Require("Navigation expansion", false, "Finite navigation hierarchy expanded within 12 levels");
    }

    protected static bool IsRetiredLabel(string text) => new[] {
        "ComponentLibrary", "Component Library", "组件库"
    }.Any(label => text.Contains(label, StringComparison.OrdinalIgnoreCase));

    void IDisposable.Dispose()
    {
        try { base.Dispose(); }
        finally { reader.Dispose(); }
    }
}
