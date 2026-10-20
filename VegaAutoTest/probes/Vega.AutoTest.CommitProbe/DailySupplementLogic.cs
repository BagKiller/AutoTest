using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Vega.Data.Commons;
using Vega.LD.DataCollector;
using Vega.Utility.Common;
using Vega.Data.Recipes;
using Vega.UI.Inspection;
using Vega.AutoTest.Harness;
using Vega.UI.Inspection.Tests;

namespace Vega.AutoTest.CommitProbe;

public sealed class DailyChannelSaveLogic
{
    [Fact]
    public void Vega100_LpdFilteringAndSave_PreservesBareAndHiddenKeys()
        => DailySupplementLogic.Check("channels100.lpd-filter-save", () => {
            RuntimeHelpers.RunClassConstructor(typeof(WorkItem3644Logic).TypeHandle);
            Assert.Equal("Vega100", Vega.Utility.Helper.ToolFeaturesHelper.ToolType);
            Assert.Contains("Vega100", Vega.Utility.Helper.ToolFeaturesHelper.AllGrants);
            var activator = DispatchProxy.Create<Galaxy.Platform.IComponent.IXActivator, SummaryBinningEnabledTests.TestProxy>();
            ((SummaryBinningEnabledTests.TestProxy)(object)activator).Services = new Dictionary<Type, object> {
                [typeof(Galaxy.Platform.IAccessControl.IAccessControlService)] = new ModelMarkersService(),
                [typeof(Galaxy.Platform.ILogging.ILogService)] = new Vega.AutoTest.Spike.StubLogService(),
                [typeof(Galaxy.Platform.INavigations.INavigationService)] = DispatchProxy.Create<Galaxy.Platform.INavigations.INavigationService, SummaryBinningEnabledTests.TestProxy>(),
                [typeof(IConfigsManager)] = DispatchProxy.Create<IConfigsManager, SummaryBinningEnabledTests.TestProxy>()
            };
            var overrideType = typeof(SummaryBinningEnabledTests).GetNestedType("LazyOverride", BindingFlags.NonPublic)!;
            using var hostServices = (IDisposable)Activator.CreateInstance(overrideType,
                BindingFlags.Instance | BindingFlags.Public, null,
                new object[] { typeof(Galaxy.Platform.Component.XActivator), "s_activator", activator }, null)!;
            var content = new LPDAlgoContent();
            content.PIRNormFacDic["DW1"] = 11;
            content.SIRNormFacDic["DW1"] = 12;
            var vm = new LPDAlgoViewModel(content, true);
            Assert.DoesNotContain(vm.PIRNormFacList, x => x.Channel == "DW1");
            Assert.DoesNotContain(vm.SIRNormFacList, x => x.Channel == "DW1");
            var pir = Assert.Single(vm.PIRNormFacList.Where(x => x.Channel == "DW2"));
            var sir = Assert.Single(vm.SIRNormFacList.Where(x => x.Channel == "DW2"));
            pir.NormFactor = 7; sir.NormFactor = 8;
            vm.OkButtonClick();
            Assert.Equal(7, content.PIRNormFacDic["DW2"]); Assert.Equal(8, content.SIRNormFacDic["DW2"]);
            Assert.Equal(11, content.PIRNormFacDic["DW1"]); Assert.Equal(12, content.SIRNormFacDic["DW1"]);
            Assert.False(content.PIRNormFacDic.ContainsKey("DW")); Assert.False(content.SIRNormFacDic.ContainsKey("DW"));
        });

}

public sealed class DailySupplementLogic
{
    internal static void Check(string id, Action action)
    {
        var recorder = new AssertionRecorder();
        try { action(); recorder.Record(id, "logic", id, "behavior holds", "behavior holds", null, null, "PASS"); recorder.Flush(); }
        catch (Exception ex) { recorder.Fail(id, "logic", id, "behavior holds", ex.ToString()); recorder.Flush(); throw; }
    }

    private static QT1426ProxyDataCollector Collector(int tracks, byte[] bytes)
    {
        RuntimeHelpers.RunClassConstructor(typeof(WorkItem3644Logic).TypeHandle);
        var collector = new QT1426ProxyDataCollector(null!, null!, null!, new Vega.AutoTest.Spike.StubLogService());
        // Lifecycle wiring normally supplies XComponent.Logger; this isolated host
        // supplies a no-op log service and never starts the DAQ device or board.
        typeof(QT1426ProxyDataCollector).GetProperty("Logger")!.SetValue(collector, new Vega.AutoTest.Spike.StubLogService());
        var cache = (ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<int, byte[]>>>)
            typeof(QT1426ProxyDataCollector).GetField("_dicDataCache", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(collector)!;
        var channel = cache.GetOrAdd("runA", _ => new()).GetOrAdd((int)ChannelMode.DN_Normal, _ => new());
        for (var i = 0; i < tracks; i++) channel[i] = bytes;
        return collector;
    }

    [Theory]
    [InlineData(3, false, 3)]
    [InlineData(1, true, 1)]
    [InlineData(0, true, 0)]
    public void DirectSend_EmptyBytes_CountConsumedTracks(int tracks, bool cancelled, int expected)
        => Check($"delivery.empty.{tracks}.{cancelled}", () => {
            var collector = Collector(tracks, Array.Empty<byte>());
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            if (cancelled) cts.Cancel();
            Assert.True(collector.GetDataGroup("runA", ChannelMode.DN_Normal, 0, 3, out var data, out var delivered, cts.Token));
            Assert.Empty(data); Assert.Equal(expected, delivered);
        });

    [Fact]
    public void ReturnedBytes_CountAndConcatenation()
        => Check("delivery.bytes", () => {
            var collector = Collector(2, new byte[] { 1, 2 });
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            Assert.True(collector.GetDataGroup("runA", ChannelMode.DN_Normal, 0, 2, out var data, out var delivered, cts.Token));
            Assert.Equal(2, delivered); Assert.Equal(new byte[] { 1, 2, 1, 2 }, data);
        });

    [Fact]
    public async Task StopAcquisition_FencesWaitingReader_WithoutHardware()
    {
        var collector = Collector(0, Array.Empty<byte>());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var waiting = Task.Run(() => collector.GetDataGroup("runA", ChannelMode.DN_Normal, 0, 1, out _, out _, cts.Token));
        await Task.Delay(150);
        Check("run-fence.waiting-before-stop", () => Assert.False(waiting.IsCompleted));
        collector.StopAcquisition();
        var completed = await Task.WhenAny(waiting, Task.Delay(1500));
        Check("run-fence.reader-exits", () => { Assert.Same(waiting, completed); Assert.False(waiting.GetAwaiter().GetResult()); });
    }

    [Fact]
    public void PartialRead_IdleTimeoutIsNotSwallowed()
        => Check("delivery.partial-idle-timeout", () => {
            var collector = Collector(1, Array.Empty<byte>());
            typeof(QT1426ProxyDataCollector).GetField("_daqConfig", BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(collector, new DataCollectorConfig { GetDataIdleTimeoutSeconds = 1 });
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            Assert.Throws<TimeoutException>(() => collector.GetDataGroup("runA", ChannelMode.DN_Normal, 0, 2, out _, out _, cts.Token));
        });

    [Fact]
    public void Vega100_EdgeScanAsset_IsDeployedIntact()
        => Check("edge100.asset-deployed", () => {
            var root = Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!;
            var source = File.ReadAllBytes(Path.Combine(root, "Vega/Resources/Data/Vega100Sim_EdgeScan.bin"));
            var deployed = File.ReadAllBytes(Path.Combine(root, "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Resources/Data/Vega100Sim_EdgeScan.bin"));
            Assert.True(source.Length > 16_000_000); Assert.Equal(0, source.Length % sizeof(ushort));
            Assert.Equal(SHA256.HashData(source), SHA256.HashData(deployed));
        });

    [Fact]
    public void Binning_OriginalProductBehaviorSuite()
    {
        var bin = Path.Combine(Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!, "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64");
        Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(bin, "Vega.UI.Inspection.dll"))),
            SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Vega.UI.Inspection.dll"))));
        using var dispatcher = new SummaryBinningDispatcher();
        var instance = new SummaryBinningEnabledTests(dispatcher);
        foreach (var method in typeof(SummaryBinningEnabledTests).GetMethods().Where(m => m.IsDefined(typeof(FactAttribute))))
        {
            var data = method.GetCustomAttributes<InlineDataAttribute>().ToArray();
            var rows = data.Length == 0 ? new[] { Array.Empty<object>() } : data.Select(d => d.GetData(method).Single()).ToArray();
            for (var i = 0; i < rows.Length; i++)
            {
                var id = "binning." + method.Name + "." + i;
                var recorder = new AssertionRecorder();
                try
                {
                    // xUnit treats [InlineData(null)] as one nullable argument.
                    var arguments = rows[i];
                    if ((arguments == null || arguments.Length == 0) && method.GetParameters().Length == 1)
                        arguments = new object[] { null! };
                    method.Invoke(instance, arguments);
                    recorder.Record(id, "logic", "Original product Binning behavior assertions", "hold", "hold", null, null, "PASS");
                    recorder.Flush();
                }
                catch (Exception ex)
                {
                    recorder.Fail(id, "logic", id, "Original behavior assertions hold", (ex.InnerException ?? ex).ToString());
                    recorder.Flush();
                    throw;
                }
            }
        }
    }
}
