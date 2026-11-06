using Vega.AutoTest.Harness;
using Xunit;

public class LoginReadinessTests
{
    [Theory]
    [InlineData("Vega500 (1.0) - su", "su", true, "空闲", true)]
    [InlineData("Vega500 (1.0)", "su", true, "空闲", false)]
    [InlineData("Vega500 (1.0) - other", "su", true, "空闲", false)]
    [InlineData("Vega500 (1.0) - su", "su", false, "空闲", false)]
    [InlineData("Vega500 (1.0) - su", "su", true, "忙碌", false)]
    public void RequiresAuthenticatedAccountEnabledMenuAndIdle(string title, string user, bool menu, string state, bool expected)
        => Assert.Equal(expected, LoginFlow.IsAuthenticatedReady(title, user, menu, state));
}
