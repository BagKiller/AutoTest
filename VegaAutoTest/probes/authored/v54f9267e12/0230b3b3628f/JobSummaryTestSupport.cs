using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Galaxy.Platform.ILogging;
using Vega.AutoTest.Harness;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

internal static class SummaryTest
{
    internal static void Run(Action<AssertionRecorder> test)
    {
        var recorder = new AssertionRecorder();
        try { test(recorder); }
        catch (Exception ex)
        {
            recorder.Fail("execution", "execution", "Production regression execution", "Successful completion", ex.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    internal static void Check(AssertionRecorder recorder, bool condition, string description, object expected, object actual)
    {
        string id = "assert-" + (recorder.Count + 1);
        if (condition) recorder.Pass(id, "behavior", description, expected.ToString()!, actual.ToString()!);
        else recorder.Fail(id, "behavior", description, expected.ToString()!, actual.ToString()!);
        Assert.True(condition, description + ": expected " + expected + "; actual " + actual);
    }

    internal static object? Call(object target, string method, params object?[] args)
    {
        Type type = target as Type ?? target.GetType();
        var member = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length);
        return member.Invoke(target is Type ? null : target, args);
    }

    internal static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    internal static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);

    internal static T Bare<T>() => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    internal static int Occurrences(string text, string token) => text.Split(new[] { token }, StringSplitOptions.None).Length - 1;
    internal static ILogService Logger(out SummaryLogCapture capture)
    {
        var logger = DispatchProxy.Create<ILogService, SummaryLogCapture>();
        capture = (SummaryLogCapture)(object)logger;
        return logger;
    }
}

public class SummaryLogCapture : DispatchProxy
{
    public List<(string Level, string Text)> Entries { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        Entries.Add((method!.Name, string.Join(" ", args ?? Array.Empty<object>())));
        return method.ReturnType == typeof(void) ? null
            : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}

public class SummaryConfigProxy : DispatchProxy
{
    public AnalysisConfig Analysis { get; } = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_AnalysisConfig") return Analysis;
        throw new NotSupportedException("Unexpected configuration access: " + method.Name);
    }
}
