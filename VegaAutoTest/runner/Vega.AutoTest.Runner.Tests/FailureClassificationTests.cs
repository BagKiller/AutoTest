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

    [Fact]
    public void RecordedBusinessMismatchRethrownAsCustomExceptionIsFail()
    {
        // 2026-11-06 duv-fit-gate preflight: recorder.Fail then custom rethrow.
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Name = "Vega.AutoTest.Authored.DuvFitGateRegressionTests.Reject", Outcome = "Failed",
            Message = "IQR leaves five rejection reason: expected TooFewKept; actual OneSided\nat Vega.AutoTest.Authored.DuvProbe.Check(...)" });
        var records = new[]
        {
            new AssertionRecord("duv-21", "behavior", "IQR leaves five rejected", "False", "False", null, null, "PASS"),
            new AssertionRecord("duv-22", "behavior", "IQR leaves five rejection reason", "TooFewKept", "OneSided", null, null, "FAIL"),
        };
        var verdict = VerdictResolver.ResolveLayer(new LayerConfig(), run, records, true, "logic");
        Assert.Equal("FAIL", verdict.Verdict);
        Assert.Equal("ASSERTION_FAILED", verdict.Code);
    }

    [Theory]
    // Description matches but the values do not appear in the failure: not that comparison.
    [InlineData("IQR leaves five rejection reason: unexpected crash")]
    // Description is only quoted later in an infrastructure error.
    [InlineData("System.IO.FileNotFoundException : IQR leaves five rejection reason TooFewKept OneSided")]
    public void UnrelatedFailureIsNotRelabelledAsBusinessFail(string message)
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Name = "T", Outcome = "Failed", Message = message });
        var record = new AssertionRecord("duv-22", "behavior", "IQR leaves five rejection reason", "TooFewKept", "OneSided", null, null, "FAIL");
        Assert.Equal("ERROR", VerdictResolver.ResolveLayer(new LayerConfig(), run, [record], true, "logic").Verdict);
    }

    [Fact]
    public void RecordedExceptionDumpCannotTurnAnErrorIntoFail()
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Name = "T", Outcome = "Failed", Message = "Galaxy init: expected loaded; actual System.InvalidOperationException: x" });
        var record = new AssertionRecord("x", "logic", "Galaxy init", "loaded", "System.InvalidOperationException: x\n   at A.B()", null, null, "FAIL");
        Assert.Equal("ERROR", VerdictResolver.ResolveLayer(new LayerConfig(), run, [record], true, "logic").Verdict);
    }

    [Theory]
    // 2026-11-06 section-pane-reset: a logic test constructing Vega.MainWindow without
    // theme resources was reported as MAIN_WINDOW_TIMEOUT because of the word "MainWindow".
    [InlineData("System.TypeInitializationException : The type initializer for 'Vega.MainWindow' threw an exception.")]
    [InlineData("System.InvalidOperationException : 主窗口 ViewModel 未初始化")]
    [InlineData("System.TimeoutException : waiting for device idle")]
    public void KeywordsWithoutTypedPrerequisiteAreNotEnvironmentBlocks(string message)
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = message });
        var verdict = VerdictResolver.ResolveLayer(new LayerConfig(), run, [], true, "logic");
        Assert.Equal("ERROR", verdict.Verdict);
        Assert.Equal("HARNESS_EXCEPTION", verdict.Code);
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
