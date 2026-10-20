using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Galaxy.Platform.Component;
using Galaxy.Platform.IComponent;
using Galaxy.Platform.IAccessControl;
using Galaxy.Platform.ILogging;
using Galaxy.Platform.INavigations;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.UI.Inspection;
using Vega.UI.Inspection.Tests;
using Vega.Utility.Common;
using Vega.Utility.Helper;
using BatchVM = Vega.UI.RecipeBatchEditor.ViewModels.CollectionViewViewModel;

namespace Vega.AutoTest.CommitProbe;

[CollectionDefinition("WI3661", DisableParallelization = true)]
public sealed class Wi3661Collection : ICollectionFixture<Wi3661Dispatcher>;

public sealed class Wi3661Dispatcher : IDisposable
{
    private readonly Thread thread;
    private Dispatcher dispatcher = null!;
    public Wi3661Dispatcher()
    {
        using var ready = new ManualResetEventSlim();
        thread = new Thread(() => {
            // In-process WPF test context only: no Window.Show and no desktop input.
            RuntimeHelpers.RunClassConstructor(typeof(WorkItem3644Logic).TypeHandle);
            var app = new Vega.UI.RecipeBatchEditor.App();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Vega.UI.Common;component/Themes/General.xaml", UriKind.Relative) });
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set(); Dispatcher.Run();
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(ready.Wait(TimeSpan.FromSeconds(20)));
    }
    public void Run(Action action) => dispatcher.Invoke(action);
    public void Dispose() { dispatcher.InvokeShutdown(); Assert.True(thread.Join(10000)); }
}

[Collection("WI3661")]
public sealed class WorkItem3661Logic
{
    private readonly Wi3661Dispatcher dispatcher;
    private static bool SingleWide => ToolFeaturesHelper.IsVega100;
    public WorkItem3661Logic(Wi3661Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        RuntimeHelpers.RunClassConstructor(typeof(WorkItem3644Logic).TypeHandle);
        _ = ToolFeaturesHelper.ToolType; _ = ToolFeaturesHelper.AllGrants;
        var product = Path.Combine(Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!, "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64");
        foreach (var name in new[] { "Vega.UI.Inspection.dll", "Vega.Data.Commons.dll", "Vega.Utility.Common.dll", "RecipeBatchEditor/RecipeBatchEditor.dll" })
        {
            var loaded = Path.Combine(AppContext.BaseDirectory, Path.GetFileName(name));
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(product, name))), SHA256.HashData(File.ReadAllBytes(loaded)));
        }
    }
    private static void Eq<T>(string id, T expected, T actual)
    {
        var r = new AssertionRecorder();
        r.Record("wi3661." + id, "logic", id, JsonConvert.SerializeObject(expected), JsonConvert.SerializeObject(actual), null, null,
            EqualityComparer<T>.Default.Equals(expected, actual) ? "PASS" : "FAIL"); r.Flush(); Assert.Equal(expected, actual);
    }
    private void Run(string id, Action action)
    {
        try { dispatcher.Run(() => { using var services = Services(); action(); }); }
        catch (Exception ex) { var r = new AssertionRecorder(); r.Fail("wi3661." + id, "logic", id, "scenario completes", ex.ToString()); r.Flush(); throw; }
    }
    private static IDisposable Services()
    {
        var activator = DispatchProxy.Create<IXActivator, SummaryBinningEnabledTests.TestProxy>();
        ((SummaryBinningEnabledTests.TestProxy)(object)activator).Services = new Dictionary<Type, object> {
            [typeof(IAccessControlService)] = new ModelMarkersService(),
            [typeof(ILogService)] = new Vega.AutoTest.Spike.StubLogService(),
            [typeof(INavigationService)] = DispatchProxy.Create<INavigationService, SummaryBinningEnabledTests.TestProxy>(),
            [typeof(IConfigsManager)] = DispatchProxy.Create<IConfigsManager, Wi3661ConfigProxy>()
        };
        var t = typeof(SummaryBinningEnabledTests).GetNestedType("LazyOverride", BindingFlags.NonPublic)!;
        return (IDisposable)Activator.CreateInstance(t, BindingFlags.Public | BindingFlags.Instance, null,
            new object[] { typeof(XActivator), "s_activator", activator }, null)!;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        yield return root;
        foreach (var c in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var d in Descendants(c)) yield return d;
    }
    private static DataGrid Grid(DependencyObject view, string path) => Descendants(view).OfType<DataGrid>().First(g =>
        BindingOperations.GetBinding(g, ItemsControl.ItemsSourceProperty)?.Path.Path == path);
    private static string Cell(DataGrid grid, string path, object item)
    {
        var col = grid.Columns.OfType<DataGridTextColumn>().Single(c => (c.Binding as Binding)?.Path.Path == path);
        var cell = new DataGridCell { DataContext = item };
        var element = (TextBlock)typeof(DataGridTextColumn).GetMethod("GenerateElement", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(col, new object[] { cell, item })!;
        cell.Content = element;
        var panel = new System.Windows.Controls.Grid(); panel.Children.Add(cell);
        panel.Measure(new Size(900, 80)); panel.Arrange(new Rect(0, 0, 900, 80)); panel.UpdateLayout();
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        element.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
        return element.Text;
    }
    private static void Channels(string id, IEnumerable<string> names)
    {
        Eq(id + ".bare-DW2", true, names.Contains("DW2"));
        Eq(id + ".DW1-filter", !SingleWide, names.Contains("DW1"));
        Eq(id + ".DN", true, names.Contains("DN"));
    }
    private static string Wide => SingleWide ? "DW" : "DW2";

    [Fact]
    public void ResultSummary_CompiledView_RendersLabelsWithoutChangingKeys() => Run("summary", () => {
        var content = new WaferSummaryContent(true);
        content.BaseSummaryDic = new() { ["DW2 Mask"] = "Full", ["DW2 Polarizer"] = "P", ["DW2 Ideal Gain"] = "123", ["DW2-0"] = "4", ["DN Mask"] = "Full" };
        var before = JsonConvert.SerializeObject(content);
        var vm = new WaferSummaryViewModel(true); vm.LoadData(content);
        var view = new WaferSummaryView { DataContext = vm };
        var grid = Grid(view, "WaferSummaryItems");
        foreach (var item in vm.WaferSummaryItems.Where(x => content.BaseSummaryDic.ContainsKey(x.Key)))
            Eq("summary.render." + item.Key, SingleWide ? item.Key.Replace("DW2", "DW") : item.Key, Cell(grid, "Key", item));
        Eq("summary.bare-keys-unchanged", before, JsonConvert.SerializeObject(content));
    });

    [Fact]
    public void Lpd_PirSir_CompiledView_FilterEditSaveAndHiddenData() => Run("lpd", () => {
        var data = new LPDAlgoContent(); data.PIRNormFacDic["DW1"] = 11; data.SIRNormFacDic["DW1"] = 12;
        var vm = new LPDAlgoViewModel(data, true); var view = new LPDAlgoView { DataContext = vm };
        Channels("lpd.pir", vm.PIRNormFacList.Select(x => x.Channel)); Channels("lpd.sir", vm.SIRNormFacList.Select(x => x.Channel));
        var pir = vm.PIRNormFacList.Single(x => x.Channel == "DW2"); var sir = vm.SIRNormFacList.Single(x => x.Channel == "DW2");
        Eq("lpd.pir.render", Wide, Cell(Grid(view, "PIRNormFacList"), "Channel", pir));
        Eq("lpd.sir.render", Wide, Cell(Grid(view, "SIRNormFacList"), "Channel", sir));
        pir.NormFactor = 7; sir.NormFactor = 8; vm.OkButtonClick();
        Eq("lpd.save.pir-DW2", 7d, data.PIRNormFacDic["DW2"]); Eq("lpd.save.sir-DW2", 8d, data.SIRNormFacDic["DW2"]);
        Eq("lpd.hidden.pir-DW1", 11d, data.PIRNormFacDic["DW1"]); Eq("lpd.hidden.sir-DW1", 12d, data.SIRNormFacDic["DW1"]);
        Eq("lpd.no-display-key", false, data.PIRNormFacDic.ContainsKey("DW"));
        File.WriteAllText(Path.Combine(RunContext.Dir, "lpd-saved.json"), JsonConvert.SerializeObject(data, Formatting.Indented));
    });

    [Fact]
    public void HardwareParameters_CompiledView_FilterAndSaveBareKeys() => Run("hardware", () => {
        var data = new HardwareParamsContent(); data.HardwareParamsDic["DW1"].MinDefect = 11;
        var hidden = JsonConvert.SerializeObject(data.HardwareParamsDic["DW1"]);
        var vm = new DFHardwareParametersViewModel(data, true); vm.OnNavigatingTo(); vm.IsUserCanEditRecipe = true;
        var view = new DFHardwareParametersView { DataContext = vm };
        Channels("hardware", vm.HardwareParamsList.Select(x => x.Channel));
        var row = vm.HardwareParamsList.Single(x => x.Channel == "DW2");
        Eq("hardware.render", Wide, Cell(Grid(view, "HardwareParamsList"), "Channel", row));
        row.MinDefect = 0.123; vm.OkButtonClick(); vm.OnNavigatingFrom();
        Eq("hardware.save-DW2", .123, data.HardwareParamsDic["DW2"].MinDefect);
        Eq("hardware.hidden-preserved", hidden, JsonConvert.SerializeObject(data.HardwareParamsDic["DW1"]));
        Eq("hardware.no-display-key", false, data.HardwareParamsDic.ContainsKey("DW"));
        File.WriteAllText(Path.Combine(RunContext.Dir, "hardware-saved.json"), JsonConvert.SerializeObject(data, Formatting.Indented));
    });

    [Fact]
    public void Calculator_CompiledView_FilterAndNoWriteBack() => Run("calculator", () => {
        var data = new HardwareParamsContent(); var before = JsonConvert.SerializeObject(data);
        var vm = new DFHardwareParamsCalculatorViewModel(data, new(), 50, new()); vm.OnNavigatingTo();
        var view = new DFHardwareParamsCalculatorView { DataContext = vm };
        Channels("calculator", vm.HardwareParamsList.Select(x => x.Channel));
        var row = vm.HardwareParamsList.Single(x => x.Channel == "DW2");
        Eq("calculator.render", Wide, Cell(Grid(view, "HardwareParamsList"), "Channel", row));
        row.SingleThreshold = 1234; vm.OkButtonClick(); vm.CancelButtonClick(); vm.OnNavigatingFrom();
        Eq("calculator.does-not-save", before, JsonConvert.SerializeObject(data));
    });

    [Fact]
    public void BatchEditor_CompiledWindow_FilterSaveAndJsonRoundTrip() => Run("batch", () => {
        var data = new CollectionContent("WI3661"); data.DarkField.HardwareParamsContent.HardwareParamsDic["DW1"].SingleThreshold = 111;
        data.DarkField.ChannelSettingContent.DW1.ThresMin = .111;
        var vm = new BatchVM(new BatchProbeLogger()); vm.Initialize("WI3661", "WI3661", data);
        var view = new Vega.UI.RecipeBatchEditor.Views.CollectionViewWindow("WI3661", "WI3661", null) { DataContext = vm };
        Channels("batch.hardware", vm.HardwareParamsList.Select(x => x.Key)); Channels("batch.channels", vm.ChannelSettings.Select(x => x.CollectorName));
        var row = vm.HardwareParamsList.Single(x => x.Key == "DW2"); var channel = vm.ChannelSettings.Single(x => x.CollectorName == "DW2");
        Eq("batch.hardware.render", Wide, Cell(Grid(view, "HardwareParamsList"), "Key", row));
        Eq("batch.channel.render", Wide, Cell(Grid(view, "ChannelSettings"), "CollectorName", channel));
        row.SingleThreshold = 2345; channel.ThresMin = .456;
        var current = (CollectionContent)typeof(BatchVM).GetField("_currentCollectionContent", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(vm)!;
        typeof(BatchVM).GetMethod("SaveHardwareParamsList", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, new object[] { current.DarkField.HardwareParamsContent });
        typeof(BatchVM).GetMethod("SaveChannelSettingsToRecipe", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(vm, new object[] { current.DarkField.ChannelSettingContent });
        Eq("batch.save-DW2-hardware", 2345d, current.DarkField.HardwareParamsContent.HardwareParamsDic["DW2"].SingleThreshold);
        Eq("batch.save-DW2-channel", .456, current.DarkField.ChannelSettingContent.DW2.ThresMin);
        Eq("batch.hidden-hardware", 111d, current.DarkField.HardwareParamsContent.HardwareParamsDic["DW1"].SingleThreshold);
        Eq("batch.hidden-channel", .111, current.DarkField.ChannelSettingContent.DW1.ThresMin);
        Eq("batch.no-display-key", false, current.DarkField.HardwareParamsContent.HardwareParamsDic.ContainsKey("DW"));
        var json = JsonConvert.SerializeObject(current, Formatting.Indented);
        File.WriteAllText(Path.Combine(RunContext.Dir, "batch-saved-collection.json"), json);
        var roundTrip = JsonConvert.DeserializeObject<CollectionContent>(json)!;
        Eq("batch.json-DW2-roundtrip", 2345d, roundTrip.DarkField.HardwareParamsContent.HardwareParamsDic["DW2"].SingleThreshold);
        view.Close();
    });
}

public sealed class BatchProbeLogger : Microsoft.Extensions.Logging.ILogger<BatchVM>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
    {
        if (error != null) File.AppendAllText(Path.Combine(RunContext.Dir, "_batch-init-errors.txt"), formatter(state, error) + "\n" + error + "\n");
    }
}

public class Wi3661ConfigProxy : DispatchProxy
{
    private readonly Dictionary<Type, object> values = new();
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null) return null;
        var type = method.ReturnType;
        if (type == typeof(string)) return string.Empty;
        if (type.IsValueType) return Activator.CreateInstance(type);
        if (method.Name.StartsWith("get_") && type.GetConstructor(Type.EmptyTypes) != null)
        {
            if (!values.TryGetValue(type, out var value)) values[type] = value = Activator.CreateInstance(type)!;
            return value;
        }
        return null;
    }
}
