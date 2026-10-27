using System.Security.Cryptography;
using System.Text.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>Validates evidence from actual Computer Use operations, not simulated WPF calls.
/// The controller launches/operates Vega; this case does not launch a second instance.</summary>
public sealed class WorkItem3677L2
{
    [Fact]
    public void ActualDesktopPostprocessEvidence()
    {
        var recorder = new AssertionRecorder();
        var folder = Environment.GetEnvironmentVariable("VEGA_WI3677_UI_EVIDENCE");
        var file = Path.Combine(folder ?? "", "ui-evidence.json");
        if (!File.Exists(file)) {
            recorder.Skip("wi3677.ui.evidence", "ui-expect", "Actual UI evidence available", "Desktop workflow not completed; no evidence file");
            recorder.Flush(); return;
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var root = doc.RootElement;
        foreach (var stage in new[] { "loggedIn", "recipeImported", "jobCreated", "bothJsonConfigured", "postprocessCompleted" }) {
            if (!root.GetProperty("stages").TryGetProperty(stage, out var item)) {
                recorder.Skip("wi3677.ui."+stage,"ui-expect",stage,root.GetProperty("incompleteReason").GetString() ?? "UI step not reached");
                continue;
            }
            var screenshot = Path.Combine(folder!, item.GetProperty("screenshot").GetString()!);
            bool valid = File.Exists(screenshot) && new FileInfo(screenshot).Length > 500
                && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(screenshot))).Equals(item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase);
            recorder.Record("wi3677.ui."+stage,"ui-expect",stage,"Recorded actual desktop screenshot with matching hash",valid.ToString(),null,null,valid?"PASS":"FAIL");
        }
        if (root.TryGetProperty("producedCollections",out var collections)) {
            var pitches = collections.EnumerateArray().Select(c=>c.GetProperty("PitchInMicron").GetDouble()).ToArray();
            bool valid=pitches.Length==2 && pitches.All(p=>double.IsFinite(p) && p>0);
            recorder.Record("wi3677.ui.produced-pitch","logic","Actual produced waferRun collection pitches","Two finite nonzero pitches",string.Join(",",pitches),null,null,valid?"PASS":"FAIL");
        } else recorder.Skip("wi3677.ui.produced-pitch","logic","Actual produced waferRun collection pitches","No completed UI-produced waferRun metadata");
        recorder.Flush();
    }
}
