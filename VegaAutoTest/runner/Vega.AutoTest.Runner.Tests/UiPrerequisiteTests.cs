using Vega.AutoTest.Harness;
using Vega.AutoTest.Runner;
using Xunit;

public class UiPrerequisiteTests
{
    [Theory]
    [InlineData("MAIN_WINDOW_TIMEOUT")]
    [InlineData("LOGIN_NOT_READY")]
    [InlineData("DEVICE_NEVER_IDLE")]
    [InlineData("UI_NOT_READY")]
    [InlineData("VEGA_PROCESS_EXITED")]
    public void TypedPrerequisiteAndRecordedFailureRemainBlocked(string code)
    {
        var error = new UiPrerequisiteException(code, "fixture details");
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = error.ToString() });
        var assertion = new AssertionRecord("workflow", "execution", "workflow", "ready", error.ToString(), null, null, "FAIL");
        var result = VerdictResolver.ResolveLayer(new LayerConfig(), run, [assertion], true, "uia");
        Assert.Equal("BLOCKED", result.Verdict);
        Assert.Equal(code, result.Code);
    }

    [Fact]
    public void PrerequisiteDoesNotHideAnotherBusinessFailure()
    {
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = new UiPrerequisiteException("UI_NOT_READY", "fixture").ToString() });
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = "Assert.Equal() Failure" });
        Assert.Equal("FAIL", VerdictResolver.ResolveLayer(new LayerConfig(), run, [], true, "uia").Verdict);
    }

    [Theory]
    [InlineData("Assert.Equal() Failure: [VEGA_UI_PRECONDITION:UI_NOT_READY] fixture")]
    [InlineData("System.InvalidOperationException: wrapper\n[VEGA_UI_PRECONDITION:UI_NOT_READY] fixture")]
    [InlineData("[VEGA_UI_PRECONDITION:UNKNOWN] fixture")]
    public void QuotedOrWrappedMarkersAreNotPrerequisites(string message)
        => Assert.Null(UiPrerequisiteException.ReadCode(message));

    [Fact]
    public void ArchivedBusyReadinessFailureStaysBlocked()
    {
        var message = "Xunit.Sdk.FailException: 登录/就绪超时，最后状态 '忙碌'";
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = message + "\n   at Vega.AutoTest.Harness.LoginFlow.WaitReady()" });
        var recorded = message + "\n   at Vega.AutoTest.Authored.UniformityWorkflowTests.Import()";
        var records = new[]
        {
            new AssertionRecord("workflow.execution", "execution", "workflow", "import and reload business results", recorded, null, null, "FAIL"),
            new AssertionRecord("workflow.execution", "execution", "workflow", "import and reload business results", recorded, null, null, "FAIL"),
        };
        var result = VerdictResolver.ResolveLayer(new LayerConfig(), run, records, true, "uia");
        Assert.Equal("BLOCKED", result.Verdict);
        Assert.Equal("DEVICE_NEVER_IDLE", result.Code);
    }

    [Fact]
    public void BusinessAssertionQuotingReadinessTextStaysFail()
    {
        var message = "Assert.Equal() Failure: 登录/就绪超时，最后状态 '忙碌'";
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = message });
        Assert.Equal("FAIL", VerdictResolver.ResolveLayer(new LayerConfig(), run, [], true, "logic").Verdict);
    }

    [Fact]
    public void PositiveButInsufficientCountHasAccurateCode()
    {
        var records = Enumerable.Range(0, 65).Select(i => new AssertionRecord(i.ToString(), "logic", "fixture", "x", "x", null, null, "PASS")).ToArray();
        var result = VerdictResolver.ResolveLayer(new LayerConfig { MinAssertions = 70 }, new TestRunner.LayerRun(), records, true, "logic");
        Assert.Equal("BLOCKED", result.Verdict);
        Assert.Equal("ASSERTION_COUNT_BELOW_MINIMUM", result.Code);
    }

    [Fact]
    public void VegaProcessExitDuringReadinessIsBlockedNotBusinessFail()
    {
        var error = new UiPrerequisiteException("VEGA_PROCESS_EXITED", "Vega 进程在就绪等待期间退出");
        var run = new TestRunner.LayerRun();
        run.Tests.Add(new TestEntry { Outcome = "Failed", Message = error.ToString() });
        // 生成测试按 model_author 约定 recorder.Fail 后 rethrow：Actual 即 e.ToString()。
        var records = new[]
        {
            new AssertionRecord("workflow.execution", "execution", "workflow", "import and reload business results", error.ToString(), null, null, "FAIL"),
        };
        var result = VerdictResolver.ResolveLayer(new LayerConfig(), run, records, true, "uia");
        Assert.Equal("BLOCKED", result.Verdict);
        Assert.Equal("VEGA_PROCESS_EXITED", result.Code);
    }
}
