using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public class DedicatedBoundaryProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => Handler(method!, args ?? Array.Empty<object?>());
}

internal static class DedicatedSupport
{
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static Type ProductType(string assembly, string name)
        => Assembly.Load(assembly).GetType(name, true)!;
    internal static object Bare(Type type) => RuntimeHelpers.GetUninitializedObject(type);
    internal static FieldInfo Field(Type type, string name)
    {
        for (Type? t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    internal static void Set(object target, string name, object? value)
        => Field(target.GetType(), name).SetValue(target, value);
    internal static void Write(object target, string name, object? value)
    {
        var property = target.GetType().GetProperty(name, Flags)!;
        if (property.SetMethod != null) property.SetValue(target, value);
        else Set(target, "<" + name + ">k__BackingField", value);
    }
    internal static object? Call(object target, string name, params object?[] args)
    {
        try { return target.GetType().GetMethod(name, Flags)!.Invoke(target, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }
    internal static object Proxy(Type type, Func<MethodInfo, object?[], object?> handler)
    {
        var proxy = DispatchProxy.Create(type, typeof(DedicatedBoundaryProxy));
        ((DedicatedBoundaryProxy)proxy).Handler = handler;
        return proxy;
    }
    internal static object? Default(MethodInfo method)
    {
        var type = method.ReturnType;
        if (type == typeof(void)) return null;
        if (type == typeof(Task)) return Task.CompletedTask;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            return Completed(type, null);
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
    internal static object Completed(Type taskType, object? value)
    {
        var itemType = taskType.GetGenericArguments()[0];
        value ??= itemType.IsValueType ? Activator.CreateInstance(itemType) : null;
        return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(itemType)
            .Invoke(null, new[] { value })!;
    }
    internal static void Inject(object target, string field, Func<MethodInfo, object?[], object?> handler)
        => Set(target, field, Proxy(Field(target.GetType(), field).FieldType, handler));
    internal static object Sequence()
        => Bare(ProductType("Vega.BL.WaferRun", "Vega.BL.WaferRun.WaferRunSequenceBase"));
    internal static object EdgeConfig()
        => Activator.CreateInstance(ProductType("Vega.Utility.Common", "Vega.Utility.Common.Configs.EdgeDetectionConfig"))!;
    internal static void Config(object sequence, object edge)
    {
        var type = Field(sequence.GetType(), "_configsManager").FieldType;
        object configs;
        if (type.IsInterface)
            configs = Proxy(type, (m, a) => m.Name == "get_EdgeDetectionConfig" ? edge : Default(m));
        else
        {
            configs = Bare(type);
            Write(configs, "EdgeDetectionConfig", edge);
        }
        Set(sequence, "_configsManager", configs);
    }
    internal static void Equal<T>(AssertionRecorder recorder, string id, T expected, T actual)
    {
        bool equal = EqualityComparer<T>.Default.Equals(expected, actual);
        string e = expected is null ? "<null>" : expected.ToString()!;
        string a = actual is null ? "<null>" : actual.ToString()!;
        if (equal) recorder.Pass(id, "behavior", id, e, a);
        else recorder.Fail(id, "behavior", id, e, a);
        Assert.Equal(expected, actual);
    }
}
