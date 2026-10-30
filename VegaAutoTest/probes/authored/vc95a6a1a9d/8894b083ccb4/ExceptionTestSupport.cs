using System;
using System.IO;
using System.Reflection;
using Galaxy.Platform.Utilities.Helpers;
using Vega.AutoTest.Harness;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("App exception regression", DisableParallelization = true)]
public sealed class AppExceptionCollection { }

internal static class ExceptionTestSupport
{
    internal static readonly Type AppType = typeof(global::Vega.App);
    internal static MethodInfo Method(string name) => AppType.GetMethod(name,
        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        ?? throw new MissingMethodException(AppType.FullName, name);

    internal static void Check(AssertionRecorder recorder, string id, bool condition,
        string expected, string actual)
    {
        if (!condition)
        {
            recorder.Fail(id, "behavior", id, expected, actual);
            throw new XunitException(id + ": " + actual);
        }
        recorder.Pass(id, "behavior", id, expected, actual);
    }

    internal static void Write(string source, Exception error, Exception? loggingError) =>
        Method("WriteFallbackLog").Invoke(null, new object?[] { source, error, loggingError });

    internal static Exception Diagnostic()
    {
        try
        {
            throw new InvalidOperationException("outer 晶圆 diagnostic", new IOException("inner diagnostic"));
        }
        catch (Exception error) { return error; }
    }
}

// Isolate generated log artifacts; preserve both hourly files if the clock rolls over.
// No configuration, service registration, authorization, or product source is changed.
internal sealed class HourlyLogFixture : IDisposable
{
    private readonly string[] paths;
    private readonly string?[] backups;
    private readonly FieldInfo marker = ExceptionTestSupport.AppType.GetField(
        "s_fallbackLogCappedMarkerPath", BindingFlags.NonPublic | BindingFlags.Static)!;
    private readonly object? previousMarker;
    internal string Path { get; }

    internal HourlyLogFixture()
    {
        var now = DateTime.Now;
        var root = LogDirectoryResolver.ResolveLogDirectory();
        paths = new string[2];
        backups = new string?[2];
        previousMarker = marker.GetValue(null);
        for (int i = 0; i < paths.Length; i++)
        {
            var hour = now.AddHours(i);
            paths[i] = System.IO.Path.Combine(root, "Vega", "LogFiles",
                $"Log_{hour:yyyyMMdd}", $"Log_Fallback_{hour:yyyyMMddHH}.txt");
        }
        Path = paths[0];
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(paths[i])!);
                if (!File.Exists(paths[i])) continue;
                backups[i] = paths[i] + ".autotest-" + Guid.NewGuid().ToString("N");
                File.Move(paths[i], backups[i]!);
            }
            Reset(0);
        }
        catch { Dispose(); throw; }
    }

    internal void Reset(long length)
    {
        marker.SetValue(null, null);
        using var file = new FileStream(Path, FileMode.Create, FileAccess.Write);
        file.SetLength(length);
    }

    internal string Read() => File.ReadAllText(Path);
    internal void RequireSameHour()
    {
        if (!Path.EndsWith($"Log_Fallback_{DateTime.Now:yyyyMMddHH}.txt", StringComparison.Ordinal))
            throw new InvalidOperationException("Clock crossed the fixture hour; repeat in a stable hour.");
    }

    public void Dispose()
    {
        try
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (backups[i] != null)
                {
                    File.Delete(paths[i]);
                    File.Move(backups[i]!, paths[i]);
                    backups[i] = null;
                }
                else if (i == 0 || File.Exists(paths[i])) File.Delete(paths[i]);
            }
        }
        finally { marker.SetValue(null, previousMarker); }
    }
}
