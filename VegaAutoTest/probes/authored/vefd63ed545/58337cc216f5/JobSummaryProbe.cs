using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text;
using Galaxy.Platform.ILogging;
using Vega.AutoTest.Harness;
using Vega.BL.DataReporting;
using Vega.Data.Commons;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

public class SummaryLogProxy : DispatchProxy
{
    public readonly List<string> Entries = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method!.Name == "get_AnalysisConfig") return new AnalysisConfig { DisplayMode = ChannelDisplayMode.All };
        if (new[] { "Information", "Verbose", "Warning", "Error", "Debug", "StackTracedError" }.Contains(method.Name))
            Entries.Add(method.Name + "|" + string.Join("|", (args ?? Array.Empty<object>()).Take(2)));
        return method.ReturnType == typeof(void) || !method.ReturnType.IsValueType ? null : Activator.CreateInstance(method.ReturnType);
    }
}

internal static class JobSummaryProbe
{
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    internal const string A = "lot7_DefectSummary-C-DC-False-Alpha";
    internal const string B = "lot7_DefectSummary-C-DC-False-Beta";
    internal const string Fingerprint = "37310000000000000000000000000001";
    internal static readonly string Row = "<tr data-wafer-run-id=\"42\">\n<td>W42</td>\n<td>G</td>\n<td>S/5-D/5</td>\n</tr>\n".Replace("\n", Environment.NewLine);
    internal static string Section(string id, string rows = "") => $"<tbody id=\"{id}-tbody\">{rows}</tbody>";
    internal static string Log(string level, string owner, string message) => level + "|" + owner + "|" + message;
    internal static string Joined(SummaryLogProxy log) => string.Join("\n", log.Entries);
    internal static void Run(Action<AssertionRecorder> action)
    {
        var recorder = new AssertionRecorder();
        try { action(recorder); }
        catch (Exception ex)
        {
            recorder.Fail("execution", "execution", "Regression execution completed", "No unexpected failure", ex.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }
    internal static void Equal<T>(AssertionRecorder recorder, string id, T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
            recorder.Pass(id, "behavior", id, Convert.ToString(expected) ?? "null", Convert.ToString(actual) ?? "null");
        else recorder.Fail(id, "behavior", id, Convert.ToString(expected) ?? "null", Convert.ToString(actual) ?? "null");
        Assert.Equal(expected, actual);
    }
    internal static object? Call(object? owner, Type type, string name, params object?[] args)
    {
        try { return type.GetMethod(name, Flags)!.Invoke(owner, args); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    internal static object? Call(object owner, string name, params object?[] args) => Call(owner, owner.GetType(), name, args);
    internal static object New(Type type)
    {
        var value = RuntimeHelpers.GetUninitializedObject(type);
        foreach (var field in type.GetFields(Flags).Where(f => !f.IsStatic))
        {
            var ft = field.FieldType;
            if (ft.Namespace?.StartsWith("System.Collections", StringComparison.Ordinal) == true &&
                !ft.IsInterface && ft.GetConstructor(Type.EmptyTypes) != null)
                field.SetValue(value, Activator.CreateInstance(ft));
        }
        return value;
    }
    internal static void Set(object owner, string name, object? value)
    {
        var property = owner.GetType().GetProperty(name, Flags);
        if (property?.GetSetMethod(true) != null) { property.SetValue(owner, value); return; }
        var field = owner.GetType().GetField(name, Flags) ?? owner.GetType().GetField("<" + name + ">k__BackingField", Flags);
        if (field == null) throw new MissingMemberException(owner.GetType().FullName, name);
        field.SetValue(owner, value);
    }
    internal static void SetList(object owner, string propertyName, params object?[] items)
    {
        var type = owner.GetType().GetProperty(propertyName, Flags)!.PropertyType;
        var concrete = type.IsInterface ? typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]) : type;
        var list = (IList)Activator.CreateInstance(concrete)!;
        foreach (var item in items) list.Add(item);
        Set(owner, propertyName, list);
    }
    internal static ILogService Logger(out SummaryLogProxy capture)
    {
        var logger = DispatchProxy.Create<ILogService, SummaryLogProxy>();
        capture = (SummaryLogProxy)(object)logger;
        return logger;
    }
    internal static void WireLogger(object owner, ILogService logger)
    {
        foreach (var field in owner.GetType().GetFields(Flags).Where(f => !f.IsStatic && f.FieldType == typeof(ILogService)))
            field.SetValue(owner, logger);
    }
    internal static Recipe EmptyRecipe()
    {
        var recipe = (Recipe)New(typeof(Recipe));
        var collections = New(typeof(Recipe).GetProperty("CollectionElement")!.PropertyType);
        SetList(collections, "Collections");
        SetList(recipe, "RecipeElements", null, collections);
        return recipe;
    }
    internal static ExtendedChannelDefectSummaryBuilder Builder(ILogService logger, int tables)
    {
        var configs = DispatchProxy.Create<IConfigsManager, SummaryLogProxy>();
        var builder = new ExtendedChannelDefectSummaryBuilder(configs, logger, Fingerprint);
        Set(builder, "_lotId", "7");
        var summaries = (Dictionary<string, List<ChannelSummary>>)typeof(ExtendedChannelDefectSummaryBuilder)
            .GetField("_waferSummaries", Flags)!.GetValue(builder)!;
        summaries["fixture"] = Enumerable.Range(0, tables).Select(i => new ChannelSummary
        {
            SummaryType = SummaryType.DefectSummary, CollectionName = "C", ChannelMode = ChannelMode.DC,
            ChannelName = i == 0 ? "Alpha" : "Beta", Headers = new List<string>(),
            RowDataList = new List<WaferRowData> { new() { WaferRunId = 42, WaferId = "W42", Grade = "G", SrcDest = "S/5-D/5" } }
        }).ToList();
        return builder;
    }
    internal static JobSummaryHelper Helper(ILogService logger, ExtendedChannelDefectSummaryBuilder builder)
    {
        // Supply only collaborators consumed by the selected private component paths; no container or singleton replacement.
        var helper = (JobSummaryHelper)New(typeof(JobSummaryHelper));
        WireLogger(helper, logger);
        Set(helper, "_helperInstanceId", Fingerprint);
        Set(helper, "_extendedBuilder", builder);
        return helper;
    }
    internal static JobRunInfo Job()
    {
        var job = (JobRunInfo)New(typeof(JobRunInfo));
        Set(job, "JobName", "Job3731");
        Set(job, "StartTime", new DateTime(2026, 10, 29, 14, 18, 39));
        SetList(job, "LotRuns");
        return job;
    }
    internal static (JobRunInfo Job, object Lot, WaferRunInfo Wafer) Fixture()
    {
        var job = Job();
        var lotType = typeof(JobRunInfo).GetProperty("LotRuns")!.PropertyType.GetGenericArguments()[0];
        var lot = New(lotType);
        Set(lot, "ID", 7L); Set(lot, "LotName", "Lot3731");
        Set(lot, "RecipeSnapshot", EmptyRecipe());
        var wafer = (WaferRunInfo)New(typeof(WaferRunInfo));
        Set(wafer, "ID", 42L); Set(wafer, "WaferID", "W42"); Set(wafer, "Grade", "G");
        Set(wafer, "SourceCassetteID", "S"); Set(wafer, "DestCassetteID", "D");
        Set(wafer, "SourceSlotNumber", 5); Set(wafer, "DestSlotNumber", 5);
        SetList(lot, "WaferRunIDs", 42L); SetList(lot, "WaferRuns", wafer);
        SetList(job, "LotRuns", lot);
        return (job, lot, wafer);
    }
}
