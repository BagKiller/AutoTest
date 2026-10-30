using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Xunit;
using static Vega.AutoTest.Authored.ExceptionTestSupport;

namespace Vega.AutoTest.Authored;

[Collection("App exception regression")]
public sealed class FallbackLogRegressionTests
{
    private const int Limit = 10 * 1024 * 1024;
    private const string Marker = "[FallbackLog] capped at size limit; further entries skipped";

    [Fact]
    public void Fallback_preserves_diagnostics_serializes_writes_caps_and_survives_write_failures()
    {
        var recorder = new AssertionRecorder();
        try
        {
            using var fixture = new HourlyLogFixture();
            var error = Diagnostic();
            string previous = "";
            foreach (var source in new[] { "Task Exception", "UI Thread Exception", "Non-UI Thread Exception" })
            {
                Write(source, error, new IOException("notification subscriber failed"));
                fixture.RequireSameHour();
                string text = fixture.Read();
                Check(recorder, "append-" + source, text.StartsWith(previous, StringComparison.Ordinal)
                    && text.Length > previous.Length, "previous records retained and a new record appended", "bytes=" + new FileInfo(fixture.Path).Length);
                string suffix = $"[{source}] {error}{Environment.NewLine}  (log service error: notification subscriber failed){Environment.NewLine}";
                Check(recorder, "diagnostic-" + source, text.EndsWith(suffix, StringComparison.Ordinal),
                    "source, Unicode, original exception, inner exception, stack and logger failure retained", text.Substring(previous.Length));
                previous = text;
            }
            Write("null-logger-error", error, null);
            fixture.RequireSameHour();
            string added = fixture.Read().Substring(previous.Length);
            Check(recorder, "optional-logger-error", added.EndsWith($"[null-logger-error] {error}{Environment.NewLine}", StringComparison.Ordinal)
                && !added.Contains("(log service error:"), "null logger error omits only its annotation", added);

            fixture.Reset(0);
            Parallel.For(0, 24, i => Write("parallel-" + i.ToString("D2"), new Exception("payload-" + i.ToString("D2")), null));
            fixture.RequireSameHour();
            string concurrent = fixture.Read();
            var lines = concurrent.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Check(recorder, "parallel-count", lines.Length == 24, "24 complete entries", "lines=" + lines.Length);
            for (int i = 0; i < 24; i++)
            {
                string suffix = $"[parallel-{i:D2}] System.Exception: payload-{i:D2}";
                Check(recorder, "parallel-entry-" + i, lines.Count(line => line.EndsWith(suffix, StringComparison.Ordinal)) == 1,
                    "exactly one complete entry for this writer", suffix);
            }

            foreach (long initialLength in new long[] { Limit - 1, Limit, Limit + 1 })
            {
                fixture.Reset(initialLength);
                Write("boundary-first", new Exception("first-boundary-payload"), null);
                fixture.RequireSameHour();
                long afterFirst = new FileInfo(fixture.Path).Length;
                string tail = ReadTail(fixture.Path, initialLength);
                if (initialLength < Limit)
                {
                    Check(recorder, "below-limit-accepted", afterFirst > Limit && tail.Contains("first-boundary-payload") && !tail.Contains(Marker),
                        "entry starting below 10 MiB is appended in full", tail);
                    Write("boundary-drop", new Exception("discard-this-payload"), null);
                }
                else
                {
                    Check(recorder, "at-or-above-limit-" + initialLength, tail.Contains(Marker) && !tail.Contains("first-boundary-payload"),
                        "at or above 10 MiB writes a discard marker instead of the exception", tail);
                }
                long markedLength = new FileInfo(fixture.Path).Length;
                for (int i = 0; i < 3; i++) Write("boundary-repeat", new Exception("discard-this-payload"), null);
                fixture.RequireSameHour();
                tail = ReadTail(fixture.Path, initialLength);
                Check(recorder, "one-marker-" + initialLength,
                    tail.Split(new[] { Marker }, StringSplitOptions.None).Length - 1 == 1
                    && !tail.Contains("discard-this-payload") && new FileInfo(fixture.Path).Length == markedLength,
                    "one marker per capped file; later entries do not grow the file", tail);
            }

            fixture.Reset(0);
            using (var locked = new FileStream(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Write("locked-file", error, new IOException("logger failed"));
            fixture.RequireSameHour();
            Check(recorder, "write-failure-swallowed", new FileInfo(fixture.Path).Length == 0,
                "exclusive file lock does not escape or produce a partial record", "length=" + new FileInfo(fixture.Path).Length);
            Write("format-failure", new BrokenFormattingException(), null);
            Check(recorder, "format-failure-swallowed", new FileInfo(fixture.Path).Length == 0,
                "exception formatting failure does not escape or append", "length=" + new FileInfo(fixture.Path).Length);
            Write("recovered", error, null);
            fixture.RequireSameHour();
            Check(recorder, "recovery", fixture.Read().Contains("[recovered] " + error),
                "subsequent valid writes still succeed", fixture.Read());
        }
        catch (Exception error)
        {
            recorder.Fail("execution", "behavior", "Fallback writer regression", "all checks pass", error.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    private static string ReadTail(string path, long offset)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Position = offset;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private sealed class BrokenFormattingException : Exception
    {
        public override string ToString() => throw new IOException("exception formatting failed");
    }
}
