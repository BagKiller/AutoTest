using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.DailyUi20261028;

// The daily agent performs the actual UI using computer-use. This test audits
// its current-session observations; a missing receipt is BLOCKED, never PASS.
public class TargetUiAcceptance
{
    public static readonly Dictionary<string, string[]> Required = new()
    {
        ["00b3004a"] = ["authorized-login", "failed-scan-same-wafer-rerun", "ramdisk-algorithm-path-match", "defect-output-comparison"],
        ["6b4cf4af"] = ["authorized-login", "real-copy-destination-failure", "single-warning-history", "status-refresh-and-error-history"],
        ["227ace85"] = ["authorized-login", "v100-ht-oblique-s-setup", "recipe-editor-bps-recommendation", "save-reopen-bps"],
        ["bd9ccb55"] = ["authorized-login", "pmt-missing-invalid-condition", "fallback-conditions", "warning-observed"],
        ["19c18be5"] = ["authorized-login", "v100-dn-dw-setting", "recipe-without-selected-channel", "routing-and-result-channel-comparison"],
        ["9fa008a9"] = ["authorized-login", "original-waferrun-import", "spa-edit-save-reopen", "dca-defect-type-comparison"],
        ["d117a21d"] = ["authorized-login", "uniformity-verify-open", "known-small-nonzero-stddev-display"],
        ["eba9945a"] = ["authorized-login", "flat-notch-recipe-configuration", "offset-alarm-and-alignment", "diagnostic-result-image-comparison"],
        ["b428717a"] = ["authorized-login", "captured-duv-brightfield-inputs", "masked-nan-wraparound-alignment", "outer-edge-output-comparison"],
    };

    [Fact]
    public void ActualTargetWorkflowObservations()
    {
        var recorder = new AssertionRecorder();
        try
        {
            var path = Environment.GetEnvironmentVariable("VEGA_DAILY_UI_EVIDENCE");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                recorder.Skip("actual-ui.missing", "ui-evidence", "Actual target workflow", "No current-session UI observations; UI remains unverified");
                return;
            }
            var repo = Environment.GetEnvironmentVariable("VEGA_SW_ROOT") ?? throw new InvalidOperationException("VEGA_SW_ROOT required");
            using var git = Process.Start(new ProcessStartInfo("git")
            {
                ArgumentList = { "-c", "safe.directory=" + repo.Replace('\\', '/'), "-C", repo, "rev-parse", "HEAD" },
                RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
            })!;
            var sha = git.StandardOutput.ReadToEnd().Trim(); git.WaitForExit();
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var receipt = document.RootElement;
            var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
            void Check(string id, bool matches, string expected, string actual)
            {
                if (matches) recorder.Pass(id, "ui-evidence", id, expected, actual);
                else recorder.Fail(id, "ui-evidence", id, expected, actual);
                Assert.True(matches, id + ": " + actual);
            }
            Check("ui.target", receipt.GetProperty("sha").GetString() == sha, "This exact SHA", receipt.GetProperty("sha").ToString());
            Check("ui.process", receipt.GetProperty("pid").GetInt32() > 0, "Actual process PID", receipt.GetProperty("pid").ToString());
            var started = DateTimeOffset.Parse(receipt.GetProperty("startedAt").GetString()!);
            var observed = DateTimeOffset.Parse(receipt.GetProperty("observedAt").GetString()!);
            Check("ui.fresh", observed >= started && observed <= DateTimeOffset.Now && DateTimeOffset.Now - observed < TimeSpan.FromHours(2), "Current-session observation", observed.ToString("o"));
            var binary = Path.Combine(repo, "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.dll");
            Check("ui.binary", Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(binary))).Equals(receipt.GetProperty("vegaDllSha256").GetString(), StringComparison.OrdinalIgnoreCase), "Current target DLL fingerprint", receipt.GetProperty("vegaDllSha256").ToString());
            foreach (var id in Required[sha[..8]])
            {
                var observations = receipt.GetProperty("observations").EnumerateArray().Where(o => o.GetProperty("id").GetString() == id).ToArray();
                Check("ui.present." + id, observations.Length == 1, "Exactly one observed acceptance step", observations.Length.ToString());
                var item = observations.Single();
                var file = Path.GetFullPath(Path.Combine(directory, item.GetProperty("evidence").GetString()!));
                Check("ui.evidence." + id, file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(file) && new FileInfo(file).Length > 0
                    && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).Equals(item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase), "Nonempty current screenshot/output with matching fingerprint", file);
                Check("ui.compare." + id, item.GetProperty("passed").GetBoolean()
                    && !string.IsNullOrWhiteSpace(item.GetProperty("expected").GetString()) && !string.IsNullOrWhiteSpace(item.GetProperty("actual").GetString()),
                    item.GetProperty("expected").ToString(), item.GetProperty("actual").ToString());
            }
        }
        finally { recorder.Flush(); }
    }
}
