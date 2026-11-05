using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Galaxy.Platform.Component;
using Galaxy.Platform.IComponent;
using Galaxy.Platform.ILogging;
using Galaxy.Platform.IMediator;
using Galaxy.Platform.Mediator;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Recipes;
using Vega.UI.Common;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("Authored map regression", DisableParallelization = true)]
public sealed class MapCollection : ICollectionFixture<MapDispatcher> { }
public sealed class MapDispatcher : IDisposable
{
    readonly Thread thread;
    Application app = null!;
    public MapDispatcher()
    {
        using var ready = new ManualResetEventSlim();
        thread = new Thread(() => { app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; ready.Set(); app.Run(); }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("WPF startup");
    }
    public void Run(Action<AssertionRecorder> body)
    {
        var r = new AssertionRecorder();
        try { app.Dispatcher.Invoke(() => { using var services = new MapServices(); body(r); MapKit.Eq(r, "No product errors", "", string.Join(";", services.Errors)); }); }
        catch (Exception e) { r.Fail("unexpected", "execution", "Production invocation completed", "no unexpected exception", e.ToString()); throw; }
        finally { r.Flush(); }
    }
    public void Dispose() { app.Dispatcher.Invoke(app.Shutdown); if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("WPF shutdown"); }
}
public class MapProxy : DispatchProxy
{
    public Dictionary<Type, object> Services = new();
    public List<string> Errors = new();
    readonly AnalysisConfig analysis = new();
    readonly GeneralConfig general = new();
    readonly WaferRunConfig run = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method?.Name)
        {
            case "CreateInstance":
                var type = method.IsGenericMethod ? method.GetGenericArguments()[0] : args?.FirstOrDefault() as Type;
                return type != null && Services.TryGetValue(type, out var service) ? service : null;
            case "get_AnalysisConfig": return analysis;
            case "get_GeneralConfig": return general;
            case "get_WaferRunConfig": return run;
            case "GetSimpleToolType": return "Vega200";
            case "GetAllGrants": case "GetToolSpecificFeatures": case "GetRootConditions": return new List<string>();
        }
        if (method?.Name.Contains("Error") == true) Errors.Add(string.Join(" ", args ?? Array.Empty<object>()));
        return method?.ReturnType.IsValueType == true && method.ReturnType != typeof(void) ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
public sealed class MapServices : IDisposable
{
    readonly object lazy;
    readonly (FieldInfo Field, object? Value)[] saved;
    public List<string> Errors { get; }
    public MapServices()
    {
        var log = DispatchProxy.Create<ILogService, MapProxy>();
        Errors = ((MapProxy)(object)log).Errors;
        var configs = DispatchProxy.Create<IConfigsManager, MapProxy>();
        var activator = DispatchProxy.Create<IXActivator, MapProxy>();
        ((MapProxy)(object)activator).Services = new() { [typeof(ILogService)] = log, [typeof(IConfigsManager)] = configs, [typeof(IMediatorService)] = new MediatorService() };
        lazy = typeof(XActivator).GetField("s_activator", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        saved = lazy.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => (f, f.GetValue(lazy))).ToArray();
        if (saved.Length != 3 || saved.Any(x => x.Field.Name is not ("_state" or "_factory" or "_value"))) throw new NotSupportedException("Unsupported Lazy layout");
        try { foreach (var x in saved) x.Field.SetValue(lazy, x.Field.Name == "_value" ? activator : null); }
        catch { Dispose(); throw; }
    }
    public void Dispose() { foreach (var x in saved) x.Field.SetValue(lazy, x.Value); }
}
internal static class MapKit
{
    internal const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    internal static object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, Flags)!.Invoke(target, args);
    internal static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags)!.GetValue(target)!;
    internal static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags)!.SetValue(target, value);
    internal static T New<T>()
    {
        var c = typeof(T).GetConstructors(Flags).Where(c => c.GetParameters().All(p => p.IsOptional || p.ParameterType == typeof(string))).OrderBy(c => c.GetParameters().Length).First();
        return (T)c.Invoke(c.GetParameters().Select(p => p.IsOptional ? p.DefaultValue : "Regression").ToArray());
    }
    static string Text(object? value) => value is IEnumerable items && value is not string ? string.Join(",", items.Cast<object?>().Select(Text)) : value?.ToString() ?? "null";
    internal static void Eq(AssertionRecorder r, string description, object? expected, object? actual)
    {
        string e = Text(expected), a = Text(actual), id = "3703-" + (r.Count + 1);
        if (e != a) { r.Fail(id, "behavior", description, e, a); Assert.Equal(e, a); }
        else r.Pass(id, "behavior", description, e, a);
    }
    internal static DefectCategoryItemContent Cat(string name = "LPD") => new("DW2", "Default", name, "Size", 0xFF00BFFF) { ChannelMode = ChannelMode.DW2_Normal };
    internal static BinningItemContent Bin(DefectCategoryItemContent c) => new() { Category = c, MinValue = 0, MaxValue = 3, Bins = new() { new(1, 0, .5, true, 0xFF0000FF), new(2, .5, 1, true, 0xFF00FF00) } };
    internal static DefectInfo Def(double size, string name = "LPD", SizeType type = SizeType.LPD_SIZED) => new() { CollectionName = "Default", ChannelMode = ChannelMode.DW2_Normal, DefectName = name, SizeType = type, Attributes = new() { ["Size"] = size, ["X"] = 0, ["Y"] = 0 } };
    internal static WaferMapViewModel Map(DefectCategoryItemContent c, BinningItemContent? b)
    {
        var m = new WaferMapViewModel(DisplayMode.resultNormalMap) { LayoutType = LayoutTypes.Result };
        m.WaferMapImageViewModel.SetWaferInfos(150000, 3000);
        m.OverviewImageViewModel?.SetWaferInfos(150000, 3000);
        var item = new DefectCategoryItem(c); m.CategoryItems.Add(item);
        if (b != null) m.BinningItems.Add(b);
        Call(m, "UpdateColorBar", item, b);
        return m;
    }
    internal static List<DefectGroup> Generate(WaferMapViewModel m, BinningItemContent b, params DefectInfo[] defects) => (List<DefectGroup>)Call(m, "GenerateDefectGroups", defects.ToList(), b)!;
    internal static void Add(WaferMapViewModel m, DefectCategoryItemContent c, List<DefectGroup> groups, params DefectInfo[] defects) => Call(m, "AddUnbinnedDefectGroups", new DefectCategoryItem(c), defects.ToList(), groups);
    internal static double[] Sizes(IEnumerable<DefectGroup> groups) => groups.SelectMany(g => g.Defects).Select(d => d.Attributes["Size"]).OrderBy(x => x).ToArray();
    internal static IEnumerable<Drawing> Flat(Drawing d) => d is DrawingGroup g ? g.Children.SelectMany(Flat) : new[] { d };
    internal static int CrossLines(Drawing d) => Flat(d).OfType<GeometryDrawing>().Count(g => g.Geometry is LineGeometry l && l.StartPoint.X != l.EndPoint.X && l.StartPoint.Y != l.EndPoint.Y);
}
