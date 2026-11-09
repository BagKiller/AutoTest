// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Vega.AutoTest.Harness;

/// <summary>Raised when a required control cannot be located; carries a summary of what was there.</summary>
public sealed class UiElementNotFoundException : Exception
{
    public UiElementNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Null-safe control lookup for UI tests. Searches all descendants (not only direct
/// children), prefers AutomationId, and never dereferences a missing Name/AutomationId.
/// A required control that is absent raises <see cref="UiElementNotFoundException"/> with
/// the nearby tree; it is never silently skipped.
/// </summary>
public static class UiFind
{
    private const int SummaryLimit = 40;

    /// <summary>First matching descendant, or null. Criteria that are null are ignored.</summary>
    public static AutomationElement? Optional(AutomationElement root, string? automationId = null,
        string? name = null, ControlType? controlType = null, bool nameContains = false)
        => All(root, automationId, name, controlType, nameContains).FirstOrDefault();

    /// <summary>All matching descendants, in tree order. Transient UIA errors exclude only that element.</summary>
    public static IReadOnlyList<AutomationElement> All(AutomationElement root, string? automationId = null,
        string? name = null, ControlType? controlType = null, bool nameContains = false)
    {
        if (automationId is null && name is null && controlType is null)
            throw new ArgumentException("Specify at least one of automationId, name or controlType");

        AutomationElement[] candidates;
        try
        {
            candidates = automationId is not null
                ? root.FindAllDescendants(cf => cf.ByAutomationId(automationId))
                : controlType is { } type
                    ? root.FindAllDescendants(cf => cf.ByControlType(type))
                    : root.FindAllDescendants();
        }
        catch
        {
            return Array.Empty<AutomationElement>();
        }

        return candidates.Where(element => Matches(element, automationId, name, controlType, nameContains)).ToArray();
    }

    /// <summary>First matching descendant; throws with the surrounding tree when absent.</summary>
    public static AutomationElement Require(AutomationElement root, string description, string? automationId = null,
        string? name = null, ControlType? controlType = null, bool nameContains = false)
        => Optional(root, automationId, name, controlType, nameContains)
           ?? throw new UiElementNotFoundException(
               $"UI element not found: {description} (automationId={automationId ?? "-"}, name={name ?? "-"}"
               + $"{(nameContains ? " [contains]" : string.Empty)}, type={controlType?.ToString() ?? "-"}). "
               + "Nearby elements: " + Summary(root));

    /// <summary>Wait up to <paramref name="timeout"/> for a required control.</summary>
    public static AutomationElement Wait(AutomationElement root, string description, TimeSpan timeout,
        string? automationId = null, string? name = null, ControlType? controlType = null, bool nameContains = false)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            var found = Optional(root, automationId, name, controlType, nameContains);
            if (found is not null)
                return found;
            if (DateTime.UtcNow >= deadline)
                return Require(root, description, automationId, name, controlType, nameContains);
            Thread.Sleep(300);
        }
    }

    private static bool Matches(AutomationElement element, string? automationId, string? name,
        ControlType? controlType, bool nameContains)
    {
        try
        {
            if (automationId is not null && UiProps.Id(element) != automationId)
                return false;
            if (controlType is { } type && element.Properties.ControlType.ValueOrDefault != type)
                return false;
            if (name is null)
                return true;
            var actual = UiProps.Name(element);
            return nameContains
                ? actual.Contains(name, StringComparison.Ordinal)
                : string.Equals(actual, name, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static string Summary(AutomationElement root)
    {
        try
        {
            var parts = root.FindAllDescendants().Take(SummaryLimit).Select(element =>
            {
                string type;
                try { type = element.Properties.ControlType.ValueOrDefault.ToString(); }
                catch { type = "?"; }
                var id = UiProps.Id(element);
                return $"{type} '{UiProps.Name(element)}'{(id.Length > 0 ? " #" + id : string.Empty)}";
            });
            return "[" + string.Join("; ", parts) + "]";
        }
        catch (Exception error)
        {
            return "[tree unavailable: " + error.GetType().Name + "]";
        }
    }
}
