namespace Vega.AutoTest.Harness;

/// <summary>A failed UI prerequisite, before the requested business assertion.</summary>
public sealed class UiPrerequisiteException : Exception
{
    private const string Prefix = "[VEGA_UI_PRECONDITION:";
    private static readonly HashSet<string> Codes =
        ["MAIN_WINDOW_TIMEOUT", "LOGIN_NOT_READY", "DEVICE_NEVER_IDLE", "UI_NOT_READY", "VEGA_PROCESS_EXITED"];

    public UiPrerequisiteException(string code, string detail) : base($"{Prefix}{Validate(code)}] {detail}") { }

    private static string Validate(string code) => Codes.Contains(code) ? code
        : throw new ArgumentException("Unknown UI prerequisite code", nameof(code));

    // Only a top-level typed exception/message is accepted. A nested exception
    // or a business assertion quoting a prerequisite must retain its own verdict.
    public static string? ReadCode(string? message)
    {
        var first = (message ?? "").Split('\n', 2)[0].Trim();
        var type = typeof(UiPrerequisiteException).FullName!;
        if (first.StartsWith(type, StringComparison.Ordinal))
        {
            first = first[type.Length..].TrimStart();
            if (!first.StartsWith(':')) return null;
            first = first[1..].TrimStart();
        }
        if (!first.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var end = first.IndexOf(']', Prefix.Length);
        if (end < 0) return null;
        var code = first[Prefix.Length..end];
        return Codes.Contains(code) ? code : null;
    }
}
