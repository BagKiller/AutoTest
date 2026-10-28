using Xunit;
using Vega.AutoTest.Runner;
using Vega.AutoTest.Harness;

public class FailureClassificationTests
{
    [Theory]
    [InlineData("Assert.Equal() Failure: Values differ\nExpected: 1\nActual: 0")]
    [InlineData("Xunit.Sdk.TrueException: Assert.True() Failure")]
    [InlineData("Assert.True() Failure: device should be idle")]
    public void LogicAssertionRemainsFail(string message)
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = message });
        var verdict = VerdictResolver.ResolveLayer(new LayerConfig(), run, [], true, "logic");
        Assert.Equal("FAIL", verdict.Verdict);
        Assert.Equal("ASSERTION_FAILED", verdict.Code);
    }

    [Fact]
    public void WrappedInitializationExceptionRemainsError()
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = "System.TypeInitializationException: initializer\n---- Assert.Equal() Failure" });
        Assert.Equal("ERROR", VerdictResolver.ResolveLayer(new LayerConfig(), run, [], true, "logic").Verdict);
    }

    [Fact]
    public void HeadlessMixedResultsDoNotRequireUia()
    {
        var result = new RunResult { DurationMs = 1, Verdict = "FAIL", Summary = new SummaryInfo { Passed = 1 } };
        result.Tests.Add(new TestEntry { Outcome = "Failed", Message = "Assert.Equal() Failure" });
        result.Channels.Assertions.Available = true;
        Assert.Empty(ResultContractCheck.Validate(result));
    }

    [Theory]
    [InlineData("Xunit.Sdk.TrueException: Missing dependency node\n at Fixture.Test()", "FAIL")]
    [InlineData("System.Collections.Generic.KeyNotFoundException: Missing dependency node\n at Fixture.Test()", "ERROR")]
    [InlineData("Xunit.Sdk.TrueException: Missing dependency node\n at Other.Test()", "ERROR")]
    [InlineData("Xunit.Sdk.TrueException: Different failure\n at Fixture.Test()", "ERROR")]
    [InlineData("Xunit.Sdk.TrueException: Missing dependency node suffix\n at Fixture.Test()", "ERROR")]
    public void CustomMessageRequiresMatchingTypedTestEvidence(string actual, string expected)
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Name = "Fixture.Test", Outcome = "Failed", Message = "Missing dependency node" });
        var assertion = new AssertionRecord("id", "logic", "graph", "valid", actual, null, null, "FAIL");
        Assert.Equal(expected, VerdictResolver.ResolveLayer(new LayerConfig(), run, [assertion], true, "logic").Verdict);
    }
}
