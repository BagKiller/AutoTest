using Vega.AutoTest.Harness;
using Vega.AutoTest.Runner;
using Xunit;

public class TimeoutEvidenceTests
{
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
