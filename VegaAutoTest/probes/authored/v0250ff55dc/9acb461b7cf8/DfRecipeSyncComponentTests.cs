using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Galaxy.Platform.Component;
using Galaxy.Platform.IComponent;
using Galaxy.Platform.INavigations;
using Galaxy.Platform.INotifications;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.UI.Calibration;
using Xunit;
using R = Vega.Utility.Common.Properties.Resources;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("DF recipe synchronization", DisableParallelization = true)]
public sealed class DfRecipeSyncCollection { }

[Collection("DF recipe synchronization")]
public sealed class DfRecipeSyncComponentTests
{
    [Fact]
    public void EligibilitySelectionAndConfirmationFollowTheContract()
    {
        Run(a =>
        {
            var source = Config("source");
            for (int mask = 0; mask < 8; mask++)
                source.ConfigSizeItems.Add(new DFCaliConfigSizeItem { Size = mask + 1, RecipeID = (mask & 1) != 0 ? 11 : 0, RecipePath = (mask & 2) != 0 ? "source.recipe" : "", SpotDepRgn = (mask & 4) != 0 ? "Region" : "" });
            source.ConfigSizeItems.Add(Size(9, 19, "full.recipe", DFCaliConfigSizeItem.SpotDepRgnFull));
            var configs = new ObservableCollection<DFCaliConfigItem> { source };
            for (int mask = 0; mask < 8; mask++)
            {
                var target = Config(mask.ToString("000"));
                if ((mask & 1) != 0) target.Throughput = Other(source.Throughput);
                if ((mask & 2) != 0) target.Incidence = Other(source.Incidence);
                if ((mask & 4) != 0) target.IncP = Other(source.IncP);
                foreach (double size in new double[] { 9, 8, 7, 6, 5, 4, 3, 2, 1, double.BitIncrement(8) }) target.ConfigSizeItems.Add(Size(size));
                configs.Add(target);
            }
            var channel = Config("channel");
            channel.Channel = Other(source.Channel);
            channel.NMask = "X1";
            channel.WMask = "X2";
            channel.NCP = Other(channel.NCP);
            channel.WCP = Other(channel.WCP);
            channel.FluorescenceFilter = Other(channel.FluorescenceFilter);
            channel.ConfigSizeItems.Add(Size(8));
            configs.Add(channel);
            var sink = new List<SyncRecipeTarget>();
            var vm = new DFCaliSyncRecipeSelectViewModel(configs, source, sink);
            Verify(a, "filter", () => Assert.Equal(new[] { "000", "channel" }, vm.TargetItems.Select(x => x.ConfigItem.Key)));
            Verify(a, "initial", () => { Assert.Empty(vm.CurSyncSizeItems); Assert.All(vm.TargetItems, x => Assert.False(x.IsChecked)); });
            vm.SelectedAllSizesCommand.Execute(null);
            vm.UnSelectedAllSizesCommand.Execute(null);
            foreach (var target in vm.TargetItems)
                foreach (var row in target.SyncSizeItems)
                    Verify(a, "eligibility-" + target.ConfigItem.Key + "-" + row.Item.Size.ToString("R", CultureInfo.InvariantCulture), () =>
                    {
                        bool expected = row.Item.Size == 8 || row.Item.Size == 9;
                        Assert.Equal(expected, row.IsEnabled);
                        Assert.Equal(expected, row.IsChecked);
                        Assert.Contains(row.Item, target.ConfigItem.ConfigSizeItems);
                    });
            var first = vm.TargetItems[0];
            Verify(a, "order", () => Assert.Equal(new double[] { 1, 2, 3, 4, 5, 6, 7, 8, double.BitIncrement(8), 9 }, first.SyncSizeItems.Select(x => x.Item.Size)));
            var notifications = new List<string>();
            vm.PropertyChanged += (_, e) => notifications.Add(e.PropertyName ?? "");
            vm.SelectedTargetItem = first;
            Verify(a, "details", () => { Assert.Same(first.SyncSizeItems, vm.CurSyncSizeItems); Assert.Contains("CurSyncSizeItems", notifications); });
            vm.UnSelectedAllSizesCommand.Execute(null);
            Verify(a, "clear-sizes", () => Assert.All(first.SyncSizeItems, x => Assert.False(x.IsChecked)));
            vm.SelectedAllSizesCommand.Execute(null);
            Verify(a, "select-sizes", () => Assert.Equal(new double[] { 8, 9 }, first.SyncSizeItems.Where(x => x.IsChecked).Select(x => x.Item.Size)));
            vm.SelectedAllTargetsCommand.Execute(null);
            Verify(a, "select-targets", () => Assert.All(vm.TargetItems, x => Assert.True(x.IsChecked)));
            vm.UnSelectedAllTargetsCommand.Execute(null);
            Verify(a, "clear-targets", () => Assert.All(vm.TargetItems, x => Assert.False(x.IsChecked)));
            first.IsChecked = true;
            first.SyncSizeItems.Single(x => x.Item.Size == 9).IsChecked = false;
            first.SyncSizeItems.Single(x => x.Item.Size == 1).IsChecked = true;
            vm.SelectedTargetItem = vm.TargetItems[1];
            vm.UnSelectedAllSizesCommand.Execute(null);
            vm.TargetItems[1].IsChecked = true;
            vm.SelectedTargetItem = null!;
            Verify(a, "clear-details", () => Assert.Empty(vm.CurSyncSizeItems));
            vm.SelectedTargetItem = first;
            Verify(a, "retained-selection", () => Assert.False(first.SyncSizeItems.Single(x => x.Item.Size == 9).IsChecked));
            var stale = new SyncRecipeTarget(source, new[] { source.ConfigSizeItems[0] });
            sink.Add(stale);
            vm.CancelButtonClick();
            vm.CloseButtonClick();
            Verify(a, "cancel-close", () => Assert.Same(stale, Assert.Single(sink)));
            vm.OkButtonClick();
            Verify(a, "result", () =>
            {
                var result = Assert.Single(sink);
                Assert.Same(configs[1], result.ConfigItem);
                Assert.Same(configs[1].ConfigSizeItems.Single(x => x.Size == 8), Assert.Single(result.SizeItems));
                Assert.All(configs.Skip(1).SelectMany(x => x.ConfigSizeItems), x => Assert.Equal("old.recipe", x.RecipePath));
            });
        });
    }

    [Fact]
    public void SetupCopiesOnlyConfirmedSizesAndOwnsIndependentRegionLists()
    {
        Run(a =>
        {
            using var services = new Services();
            foreach (string outcome in new[] { "ok", "cancel", "empty" })
            {
                var config = new DFCaliConfig();
                var owner = new DFCaliSetupViewModel(config);
                var source = Config("source");
                source.ConfigSizeItems = new() { Size(.1, 101, "a.recipe", "A"), Size(.2, 102, "b.recipe", "B"), Size(.4, 104, "d.recipe", "D") };
                var targets = new[] { Config("A"), Config("B"), Config("unchecked") };
                targets[0].ConfigSizeItems = new() { Size(.2), Size(.1), Size(.3), Size(.4) };
                targets[1].ConfigSizeItems.Add(Size(.1));
                targets[2].ConfigSizeItems.Add(Size(.1));
                config.KTConfigItems.Add(source);
                foreach (var target in targets) config.KTConfigItems.Add(target);
                owner.SelectedConfigItem = source;
                var before = targets.SelectMany(x => x.ConfigSizeItems).ToDictionary(x => x, State);
                int calls = 0;
                services.Navigation.Handler = (method, args) =>
                {
                    Assert.Equal("ShowXDialog", method.Name);
                    Assert.Equal(typeof(DFCaliSyncRecipeSelectViewModel), args![0]);
                    var arguments = Assert.IsType<object[]>(args[1]);
                    Assert.Same(config.KTConfigItems, arguments[0]);
                    Assert.Same(source, arguments[1]);
                    var sink = Assert.IsType<List<SyncRecipeTarget>>(arguments[2]);
                    var dialog = new DFCaliSyncRecipeSelectViewModel(config.KTConfigItems, source, sink);
                    calls++;
                    if (outcome != "empty")
                    {
                        dialog.TargetItems[0].IsChecked = true;
                        dialog.TargetItems[1].IsChecked = true;
                        dialog.TargetItems[0].SyncSizeItems.Single(x => x.Item.Size == .4).IsChecked = false;
                        dialog.OkButtonClick();
                    }
                    return outcome == "cancel" ? InteractionResult.Cancel : InteractionResult.Ok;
                };
                owner.SyncRecipeCommand.Execute(null);
                Verify(a, outcome + "-dialog", () => Assert.Equal(1, calls));
                foreach (var target in targets)
                    foreach (var size in target.ConfigSizeItems)
                        Verify(a, outcome + "-" + target.Key + "-" + size.Size, () =>
                        {
                            bool changed = outcome == "ok" && target.Key != "unchecked" && (size.Size == .1 || size.Size == .2);
                            var expected = changed ? size.Size == .1 ? (101L, "a.recipe", "A", "Full Deposit,A,A-extra") : (102L, "b.recipe", "B", "Full Deposit,B,B-extra") : before[size];
                            Assert.Equal(expected, State(size));
                            Assert.Equal((17d, 23d, 29d, true), (size.NormPPM, size.ExpectedPPM, size.ActualSize, size.IsSelect));
                        });
                Verify(a, outcome + "-source", () => Assert.Equal(new[] { "a.recipe", "b.recipe", "d.recipe" }, source.ConfigSizeItems.Select(x => x.RecipePath)));
                if (outcome == "ok")
                {
                    var copied = targets[0].ConfigSizeItems.Single(x => x.Size == .1);
                    var sibling = targets[1].ConfigSizeItems[0];
                    copied.SpotDepRgnList.Add("target-only");
                    Verify(a, "independent-lists", () =>
                    {
                        Assert.NotSame(source.ConfigSizeItems[0].SpotDepRgnList, copied.SpotDepRgnList);
                        Assert.NotSame(copied.SpotDepRgnList, sibling.SpotDepRgnList);
                        Assert.Equal(new[] { "Full Deposit", "A", "A-extra" }, source.ConfigSizeItems[0].SpotDepRgnList);
                        Assert.Equal(new[] { "Full Deposit", "A", "A-extra" }, sibling.SpotDepRgnList);
                    });
                }
            }
        });
    }

    [Fact]
    public void GuardsReportTheSpecifiedEnglishAndChineseMessagesWithoutWritingTargets()
    {
        Run(a =>
        {
            using var services = new Services();
            var oldCulture = R.Culture;
            try
            {
                foreach (string language in new[] { "en", "zh-Hans" })
                {
                    R.Culture = CultureInfo.GetCultureInfo(language);
                    string title = language == "en" ? "Sync Recipe" : "同步配方";
                    string missing = language == "en" ? "Please select a config item in the calibration queue first." : "请先在校准队列中选择配置项。";
                    string incomplete = language == "en" ? "The selected config item has no size with both recipe and spot deposit region configured. Configure them before syncing." : "所选配置项没有已同时配置配方与光斑沉积区域的尺寸，请先完成配置再同步。";
                    string noTarget = language == "en" ? "No sync target or size was selected." : "未选择任何同步目标或尺寸。";
                    var owner = new DFCaliSetupViewModel(new DFCaliConfig());
                    services.Messages.Clear();
                    owner.SyncRecipeCommand.Execute(null);
                    Verify(a, language + "-missing", () => { Assert.Contains(title, services.Messages); Assert.Contains(missing, services.Messages); });
                    for (int mask = -1; mask < 7; mask++)
                    {
                        var source = Config("incomplete");
                        if (mask >= 0) source.ConfigSizeItems.Add(new DFCaliConfigSizeItem { Size = 1, RecipeID = (mask & 1) != 0 ? 3 : 0, RecipePath = (mask & 2) != 0 ? "r.recipe" : null!, SpotDepRgn = (mask & 4) != 0 ? "Region" : "" });
                        owner.SelectedConfigItem = source;
                        services.Messages.Clear();
                        owner.SyncRecipeCommand.Execute(null);
                        Verify(a, language + "-incomplete-" + mask, () => Assert.Contains(incomplete, services.Messages));
                    }
                    foreach (bool checkedTarget in new[] { false, true })
                    {
                        var source = Config("source"); source.ConfigSizeItems.Add(Size(1, 5, "valid.recipe", "Region"));
                        var target = Config("target"); target.ConfigSizeItems.Add(Size(1));
                        var sink = new List<SyncRecipeTarget> { new(source, source.ConfigSizeItems.ToArray()) };
                        var dialog = new DFCaliSyncRecipeSelectViewModel(new() { source, target }, source, sink);
                        dialog.TargetItems[0].IsChecked = checkedTarget;
                        dialog.TargetItems[0].SyncSizeItems[0].IsChecked = false;
                        services.Messages.Clear();
                        dialog.OkButtonClick();
                        Verify(a, language + "-empty-" + checkedTarget, () => { Assert.Empty(sink); Assert.Contains(noTarget, services.Messages); Assert.Equal("old.recipe", target.ConfigSizeItems[0].RecipePath); });
                    }
                }
            }
            finally { R.Culture = oldCulture; }
        });
    }

    private static DFCaliConfigItem Config(string key) => new() { Key = key, Throughput = ThroughputType.HighSensitivity, Incidence = IncidenceType.Oblique, IncP = PolarizerType.P, Channel = ChannelType.DN };
    private static T Other<T>(T value) where T : struct, Enum => Enum.GetValues<T>().First(x => !x.Equals(value));
    private static DFCaliConfigSizeItem Size(double value, long id = 77, string path = "old.recipe", string region = "Old") => new() { Size = value, RecipeID = id, RecipePath = path, SpotDepRgn = region, SpotDepRgnList = new() { "Full Deposit", region, region + "-extra" }, NormPPM = 17, ExpectedPPM = 23, ActualSize = 29, IsSelect = true };
    private static (long, string, string, string) State(DFCaliConfigSizeItem x) => (x.RecipeID, x.RecipePath, x.SpotDepRgn, string.Join(",", x.SpotDepRgnList));
    private static void Verify(AssertionRecorder a, string id, Action assertion)
    {
        try { assertion(); a.Pass(id, "behavior", id, "stated contract assertions succeed", "assertions succeeded"); }
        catch (Exception e) { a.Fail(id, "behavior", id, "stated contract assertions succeed", e.ToString()); throw; }
    }
    private static void Run(Action<AssertionRecorder> test)
    {
        var a = new AssertionRecorder();
        try { test(a); }
        catch (Exception e) { a.Fail("execution", "behavior", "Production behavior executes", "completed", e.ToString()); throw; }
        finally { a.Flush(); }
    }
    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler = (m, _) => throw new NotSupportedException(m.Name);
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Handler(method!, args);
    }
    private sealed class Services : IDisposable
    {
        private readonly object lazy;
        private readonly (FieldInfo Field, object? Value)[] saved;
        public Proxy Navigation { get; }
        public List<string> Messages { get; } = new();
        public Services()
        {
            var navigation = DispatchProxy.Create<INavigationService, Proxy>();
            Navigation = (Proxy)(object)navigation;
            var notification = DispatchProxy.Create<INotificationService, Proxy>();
            ((Proxy)(object)notification).Handler = (m, args) =>
            {
                Messages.AddRange((args ?? Array.Empty<object>()).OfType<string>());
                if (m.Name == "Prompt" && args![4] is IEnumerable actions)
                    return actions.Cast<object>().First().GetType().GetField("Item1")!.GetValue(actions.Cast<object>().First());
                return m.ReturnType.IsValueType && m.ReturnType != typeof(void) ? Activator.CreateInstance(m.ReturnType) : null;
            };
            var activator = DispatchProxy.Create<IXActivator, Proxy>();
            ((Proxy)(object)activator).Handler = (m, args) =>
            {
                if (m.Name != "CreateInstance") throw new NotSupportedException(m.Name);
                var type = m.IsGenericMethod ? m.GetGenericArguments()[0] : args![0] as Type;
                return type == typeof(INavigationService) ? navigation : type == typeof(INotificationService) ? notification : null;
            };
            lazy = typeof(XActivator).GetField("s_activator", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            saved = lazy.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Select(f => (f, f.GetValue(lazy))).ToArray();
            if (saved.Length != 3 || saved.Any(x => x.Field.Name is not ("_state" or "_factory" or "_value"))) throw new NotSupportedException("Unsupported Lazy layout");
            try { foreach (var x in saved) x.Field.SetValue(lazy, x.Field.Name == "_value" ? activator : null); }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var x in saved) x.Field.SetValue(lazy, x.Value); }
    }
}
