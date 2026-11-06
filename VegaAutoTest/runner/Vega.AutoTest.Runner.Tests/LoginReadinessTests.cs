using Vega.AutoTest.Harness;
using Xunit;

public class LoginReadinessTests
{
    [Theory]
    [InlineData("")]
    [InlineData("--pid")]
    [InlineData("--repo-root|C:\\definitely-missing-vega-root|--pid|12")]
    [InlineData("--pid|abc|--repo-root|.")]
    [InlineData("--pid|0|--repo-root|.")]
    [InlineData("--repo-root|.|--pid|12|--extra")]
    public void ManualLoginRejectsMalformedArgumentsWithUsage(string joined)
    {
        var args = joined.Length == 0 ? Array.Empty<string>() : joined.Split('|');
        Assert.False(Vega.AutoTest.Runner.ManualLogin.TryParse(args, out _, out _, out var usage));
        Assert.StartsWith("usage: login", usage);
    }

    [Fact]
    public void ManualLoginAcceptsArgumentsInAnyOrder()
    {
        Assert.True(Vega.AutoTest.Runner.ManualLogin.TryParse(new[] { "--pid", "42", "--repo-root", "." },
            out var repo, out var pid, out _));
        Assert.Equal(42, pid);
        Assert.Equal(Path.GetFullPath("."), repo);
    }

    [Theory]
    [InlineData("Vega500 (1.0) - su", "su", true, "空闲", true)]
    [InlineData("Vega500 (1.0)", "su", true, "空闲", false)]
    [InlineData("Vega500 (1.0) - other", "su", true, "空闲", false)]
    [InlineData("Vega500 (1.0) - su", "su", false, "空闲", false)]
    [InlineData("Vega500 (1.0) - su", "su", true, "忙碌", false)]
    public void RequiresAuthenticatedAccountEnabledMenuAndIdle(string title, string user, bool menu, string state, bool expected)
        => Assert.Equal(expected, LoginFlow.IsAuthenticatedReady(title, user, menu, state));
}
