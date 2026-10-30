using System.Text.Json;
using Vega.AutoTest.Harness;
using Xunit;

public class AssertionRecorderTests
{
    [Fact]
    public void RepeatedFlushAndLaterRecordsDoNotDuplicateEvidence()
    {
        var directory = Path.Combine(Path.GetTempPath(), "vega-recorder-" + Guid.NewGuid().ToString("N"));
        try
        {
            var recorder = new AssertionRecorder(directory);
            recorder.Fail("workflow", "execution", "fixture", "ready", "blocked");
            var first = recorder.Flush();
            Assert.Equal(first, recorder.Flush());
            recorder.Pass("later", "logic", "fixture", "x", "x");
            Assert.Equal(first, recorder.Flush());
            Assert.Single(Directory.GetFiles(directory, "assert-*.json"));
            using var json = JsonDocument.Parse(File.ReadAllText(first));
            Assert.Equal(2, json.RootElement.GetArrayLength());
            var other = new AssertionRecorder(directory);
            other.Pass("other", "logic", "fixture", "x", "x");
            Assert.NotEqual(first, other.Flush());
            Assert.Equal(2, Directory.GetFiles(directory, "assert-*.json").Length);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
