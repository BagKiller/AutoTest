using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

/// <summary>
/// Compile-time contract for the call shapes generated tests use (2026-11-10 batch
/// failed with CS1744/CS7036). These lambdas are never executed; they must compile.
/// </summary>
public class UiFindSignatureTests
{
    [Fact]
    public void GeneratedCallShapesCompile()
    {
        Action<AutomationElement> shapes = root =>
        {
            _ = UiFind.Optional(root, "Existing wafer result", automationId: "Id");
            _ = UiFind.Optional(root, "original text", name: "x", controlType: ControlType.Text);
            _ = UiFind.All(root, "Haze page selectors", controlType: ControlType.ComboBox).ToArray();
            _ = UiFind.Require(root, "Attach inspection", automationId: "AttachInspectionButton");
            _ = UiFind.Wait(root, "Haze calibration navigation", name: "Haze", controlType: ControlType.TreeItem);
            _ = UiFind.Wait(root, "with timeout", automationId: "Id", timeout: TimeSpan.FromSeconds(5));
            _ = UiFind.Wait(root, "timeout third", TimeSpan.FromSeconds(5), automationId: "Id");
        };
        Assert.NotNull(shapes);
        Assert.Equal(TimeSpan.FromSeconds(15), UiFind.DefaultWait);
    }
}
