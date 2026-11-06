using Vega.AutoTest.Harness;
using Vega.AutoTest.Runner;
using Xunit;

public class TimeoutEvidenceTests
{
    [Fact]
    public void PipeHeldByLeftoverDescendantDoesNotBlockTheRunner()
    {
        // 2026-11-05 Vega100-PIC: the test finished in 38s, but a descendant kept
        // the redirected pipe open; reading to EOF hung until the 780s kill.
        var never = new TaskCompletionSource<string>().Task;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var text = TestRunner.StreamText(Task.FromResult("Passed!  - Failed: 0"), never, TimeSpan.FromMilliseconds(200));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Contains("Passed!", text);
        Assert.Contains("stderr incomplete", text);
    }

    [Fact]
    public void CompletedStreamsAreReturnedUnchanged()
    {
        var text = TestRunner.StreamText(Task.FromResult("out"), Task.FromResult("err"), TimeSpan.FromSeconds(1));
        Assert.Equal("out" + Environment.NewLine + "err", text);
    }

    [Fact]
    public void TimeoutKillsOwnedChildAndRetainsHostLog()
    {
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        var directory = Path.Combine(temporaryRoot, "vega-timeout-contract-" + Guid.NewGuid().ToString("N"));
        var oldScratch = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_ARTIFACTS_ROOT");
        Directory.CreateDirectory(directory);
        try
        {
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_ARTIFACTS_ROOT", Path.Combine(directory, "scratch"));
            File.WriteAllText(Path.Combine(directory, "Delayed.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net8.0-windows</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup>
                  <Target Name="DelayedFixture" BeforeTargets="VSTest">
                    <Message Text="timeout-fixture-started" Importance="high" />
                    <Exec Command="powershell -NoProfile -NonInteractive -Command &quot;Start-Sleep -Seconds 20&quot;" />
                  </Target>
                </Project>
                """);
            var layerDirectory = Path.Combine(directory, "layer");
            var trxDirectory = Path.Combine(layerDirectory, "trx");
            var result = TestRunner.RunLayer(directory, "logic", new LayerConfig
            {
                Project = "Delayed.csproj", Filter = "FullyQualifiedName~TimeoutFixture", TimeoutSeconds = 1,
            }, layerDirectory, trxDirectory);
            Assert.True(result.TimedOut);
            var log = File.ReadAllText(Path.Combine(trxDirectory, "logic.testhost.log"));
            Assert.Contains("timed out", log);
        }
        finally
        {
            Environment.SetEnvironmentVariable("VEGA_AUTOTEST_ARTIFACTS_ROOT", oldScratch);
            var resolved = Path.GetFullPath(directory);
            if (!resolved.StartsWith(temporaryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture cleanup escaped its temporary parent");
            Directory.Delete(resolved, true);
        }
    }
}
