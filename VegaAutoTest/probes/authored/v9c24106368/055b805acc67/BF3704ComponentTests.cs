using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading.Tasks;
using Vega.UI.Calibration;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("BF3704")]
public sealed class BF3704ContrastTests
{
    [Fact]
    public void ContrastUsesPercentAndDistinguishesZeroFromMissing()
    {
        BF3704Check.Run(r =>
        {
            var type = typeof(BFCalibrationViewModel).Assembly.GetType("Vega.UI.Calibration.DicWindowStatistics", true)!;
            var method = type.GetMethod("ComputeContrast", BindingFlags.Public | BindingFlags.Static)!;
            double? Call(double? max, double? min) => (double?)method.Invoke(null, new object?[] { max, min });
            BF3704Check.Number(r, "contrast-80-20", 60, Call(80, 20));
            BF3704Check.Number(r, "contrast-100-50", 33.333333333333336, Call(100, 50));
            BF3704Check.Number(r, "equal-positive-is-zero", 0, Call(65535, 65535));
            BF3704Check.Number(r, "zero-min-is-100-percent", 100, Call(65535, 0));
            BF3704Check.Number(r, "fractional-batch-means", 50, Call(1.5, 0.5));
            BF3704Check.Number(r, "scale-invariance", 60, Call(8000, 2000));
            foreach (var pair in new (double? Max, double? Min)[] { (null, 5), (5, null), (null, null), (0, 0), (-1, -1), (1, -1) })
                BF3704Check.That(r, "undefined-contrast-" + pair, Call(pair.Max, pair.Min) == null, "null", Call(pair.Max, pair.Min));
        });
    }
}

[Collection("BF3704")]
public sealed class BF3704RollingTests
{
    private sealed class Window
    {
        private readonly object instance;
        private readonly Type type = typeof(BFCalibrationViewModel).Assembly.GetType("Vega.UI.Calibration.RollingWindow", true)!;
        public Window() { instance = Activator.CreateInstance(type, true)!; }
        public double? Mean => (double?)type.GetProperty("Mean")!.GetValue(instance);
        public void Push(double value, long tick, int seconds) => type.GetMethod("Push")!.Invoke(instance, new object[] { value, tick, seconds });
        public void Clear() => type.GetMethod("Clear")!.Invoke(instance, null);
    }
    [Fact]
    public void WindowsUseElapsedSecondsWithExclusiveCutoffAndIndependentState()
    {
        BF3704Check.Run(r =>
        {
            var w = new Window();
            BF3704Check.That(r, "empty-window", w.Mean == null, "null", w.Mean);
            w.Push(100, 0, 1);
            w.Push(20, 100, 1);
            w.Push(40, 999, 1);
            BF3704Check.Number(r, "three-irregularly-spaced-batches", 53.333333333333336, w.Mean);
            w.Push(60, 1000, 1);
            BF3704Check.Number(r, "exact-cutoff-excluded", 40, w.Mean);
            w.Push(80, 1100, 1);
            BF3704Check.Number(r, "next-exact-cutoff-excluded", 60, w.Mean);
            w.Push(120, 5000, 1);
            BF3704Check.Number(r, "long-gap-removes-all-old-samples", 120, w.Mean);
            w.Clear();
            BF3704Check.That(r, "clear-is-no-data", w.Mean == null, "null", w.Mean);
            w.Push(7.5, 9000, 10);
            BF3704Check.Number(r, "clear-resets-sum", 7.5, w.Mean);
            var second = new Window();
            second.Push(900, 9000, 10);
            BF3704Check.Number(r, "instances-do-not-share-state", 7.5, w.Mean);
            BF3704Check.Number(r, "second-instance", 900, second.Mean);
            w.Clear();
            w.Push(10, 0, 10);
            w.Push(30, 500, 10);
            w.Push(50, 1500, 1);
            BF3704Check.Number(r, "shrink-immediately-on-next-batch", 50, w.Mean);
            w.Push(70, 1600, 10);
            BF3704Check.Number(r, "growth-does-not-resurrect-evicted-data", 60, w.Mean);
            for (var seconds = 1; seconds <= 10; seconds++)
            {
                var x = new Window();
                const long origin = 1000000000000;
                x.Push(100, origin, seconds);
                x.Push(20, origin + seconds * 1000L - 1, seconds);
                BF3704Check.Number(r, "seconds-option-inside-" + seconds, 60, x.Mean);
                x.Push(40, origin + seconds * 1000L, seconds);
                BF3704Check.Number(r, "seconds-option-boundary-" + seconds, 30, x.Mean);
            }
            var duplicates = new Window();
            duplicates.Push(0, 10, 1);
            duplicates.Push(12, 10, 1);
            duplicates.Push(6, 11, 1);
            BF3704Check.Number(r, "all-batches-including-same-timestamp", 6, duplicates.Mean);
        });
    }
}

[Collection("BF3704")]
public sealed class BF3704ViewModelContractTests
{
    private readonly BF3704WpfHost host;
    public BF3704ViewModelContractTests(BF3704WpfHost host) { this.host = host; }
    [Fact]
    public Task CommandsValidateTheirOwnInputsAndNotifyTheirConsumers()
        => BF3704Check.RunAsync(r => host.Run(() =>
        {
            var vm = new BFCalibrationViewModel();
            BF3704Check.That(r, "default-duration", vm.RecordMinutes == 5, "5 minutes", vm.RecordMinutes);
            BF3704Check.That(r, "default-window", vm.DicAverageSeconds == 1, "1 second", vm.DicAverageSeconds);
            BF3704Check.That(r, "window-options", vm.DicAverageSecondsOptions.SequenceEqual(new[] { 1,2,3,4,5,6,7,8,9,10 }), "1 through 10 seconds", string.Join(",", vm.DicAverageSecondsOptions));
            BF3704Check.That(r, "idle-start-enabled", vm.StartCommand.CanExecute(null), "true", vm.StartCommand.CanExecute(null));
            BF3704Check.That(r, "idle-record-disabled", !vm.StartDicRecordCommand.CanExecute(null), "false", vm.StartDicRecordCommand.CanExecute(null));
            BF3704Check.That(r, "idle-stop-record-disabled", !vm.StopDicRecordCommand.CanExecute(null), "false", vm.StopDicRecordCommand.CanExecute(null));
            var sumNotifications = 0;
            vm.PropertyChanged += (_, e) => { if (e.PropertyName == "PDCountSum") sumNotifications++; };
            vm.PD1CountAverage = 10.25;
            BF3704Check.Number(r, "sum-after-pd1", 10.25, vm.PDCountSum);
            vm.PD2CountAverage = 20.5;
            BF3704Check.Number(r, "sum-after-pd2", 30.75, vm.PDCountSum);
            BF3704Check.That(r, "sum-notifies-each-channel", sumNotifications == 2, "2 notifications", sumNotifications);
            var startEvents = 0; var recordEvents = 0; var stopEvents = 0; var closeEvents = 0;
            vm.StartCommand.CanExecuteChanged += (_, _) => startEvents++;
            vm.StartDicRecordCommand.CanExecuteChanged += (_, _) => recordEvents++;
            vm.StopDicRecordCommand.CanExecuteChanged += (_, _) => stopEvents++;
            vm.CanCloseChanged += (_, _) => closeEvents++;
            vm.RecordMinutes = 0;
            BF3704Check.That(r, "duration-zero-validation", ((INotifyDataErrorInfo)vm).GetErrors("RecordMinutes").Cast<object>().Any(), "validation error", vm.HasErrors);
            BF3704Check.That(r, "invalid-duration-does-not-block-acquisition", vm.StartCommand.CanExecute(null), "true", vm.StartCommand.CanExecute(null));
            vm.IsCalibrationRunning = true;
            BF3704Check.That(r, "running-acquisition-disables-start", !vm.StartCommand.CanExecute(null), "false", vm.StartCommand.CanExecute(null));
            BF3704Check.That(r, "running-cannot-close", !vm.CanClose, "false", vm.CanClose);
            foreach (var minutes in new[] { -1, 0, 121 })
            {
                vm.RecordMinutes = minutes;
                BF3704Check.That(r, "invalid-duration-gate-" + minutes, !vm.StartDicRecordCommand.CanExecute(null), "false", vm.StartDicRecordCommand.CanExecute(null));
            }
            vm.GainVoltage = -1;
            foreach (var minutes in new[] { 1, 120 })
            {
                vm.RecordMinutes = minutes;
                BF3704Check.That(r, "valid-duration-clears-own-error-" + minutes, !((INotifyDataErrorInfo)vm).GetErrors("RecordMinutes").Cast<object>().Any(), "no RecordMinutes errors", vm.RecordMinutes);
                BF3704Check.That(r, "gain-error-is-independent-" + minutes, vm.HasErrors && vm.StartDicRecordCommand.CanExecute(null), "gain invalid; recording enabled", vm.HasErrors + "/" + vm.StartDicRecordCommand.CanExecute(null));
            }
            vm.IsDicRecording = true;
            BF3704Check.That(r, "recording-prevents-second-record", !vm.StartDicRecordCommand.CanExecute(null), "false", vm.StartDicRecordCommand.CanExecute(null));
            BF3704Check.That(r, "recording-enables-stop", vm.StopDicRecordCommand.CanExecute(null), "true", vm.StopDicRecordCommand.CanExecute(null));
            vm.IsCalibrationRunning = false;
            BF3704Check.That(r, "recording-alone-still-blocks-start", !vm.StartCommand.CanExecute(null), "false", vm.StartCommand.CanExecute(null));
            BF3704Check.That(r, "recording-alone-still-blocks-close", !vm.CanClose, "false", vm.CanClose);
            vm.IsDicRecording = false;
            BF3704Check.That(r, "idle-start-recovers-despite-errors", vm.StartCommand.CanExecute(null), "true", vm.StartCommand.CanExecute(null));
            BF3704Check.That(r, "idle-close-recovers", vm.CanClose, "true", vm.CanClose);
            vm.CanSave = true;
            BF3704Check.That(r, "unsaved-still-blocks-close", !vm.CanClose, "false", vm.CanClose);
            BF3704Check.That(r, "start-command-state-notifications", startEvents == 4, "4 state transitions", startEvents);
            BF3704Check.That(r, "record-command-notifications", recordEvents >= 9, "notifications for duration and running/recording transitions", recordEvents);
            BF3704Check.That(r, "stop-command-notifications", stopEvents == 2, "2 recording transitions", stopEvents);
            BF3704Check.That(r, "close-host-notifications", closeEvents == 5, "4 run/record transitions plus unsaved state", closeEvents);
            var format = typeof(BFCalibrationViewModel).GetMethod("FormatRemaining", BindingFlags.NonPublic | BindingFlags.Static)!;
            foreach (var sample in new[] { (0d,"00:00"), (0.001,"00:01"), (59.1,"01:00"), (60d,"01:00"), (7199.01,"120:00"), (7200d,"120:00") })
            {
                var actual = (string)format.Invoke(null, new object[] { TimeSpan.FromSeconds(sample.Item1) })!;
                BF3704Check.That(r, "remaining-round-up-" + sample.Item1, actual == sample.Item2, sample.Item2, actual);
            }
            return Task.CompletedTask;
        }));

    [Fact]
    public void AddedResourcesHaveEnglishAndSimplifiedChineseBusinessLabels()
    {
        BF3704Check.Run(r =>
        {
            var asm = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Vega.Utility.Common.dll"));
            var manager = new ResourceManager("Vega.Utility.Common.Properties.Resources", asm);
            var labels = new[] {
                ("RecordMinutes", "Record (min)", "记录时长(分)"), ("StartRecord", "Start Record", "开始记录"),
                ("StopRecord", "Stop Record", "停止记录"), ("RemainingTime", "Remaining", "剩余时间"),
                ("Contrast", "Contrast", "对比度"), ("DicAverageSeconds", "Avg (s)", "均值(秒)"),
                ("NA", "N/A", "N/A"), ("PD1CountAverageTime", "PD1CountAverage/time", "PD1CountAverage/time"),
                ("PD2CountAverageTime", "PD2CountAverage/time", "PD2CountAverage/time"), ("PDCountSum", "Sum", "总和") };
            foreach (var label in labels)
            {
                var en = manager.GetString(label.Item1, CultureInfo.GetCultureInfo("en-US"));
                var zh = manager.GetString(label.Item1, CultureInfo.GetCultureInfo("zh-Hans"));
                BF3704Check.That(r, "english-" + label.Item1, en == label.Item2, label.Item2, en);
                BF3704Check.That(r, "chinese-" + label.Item1, zh == label.Item3, label.Item3, zh);
            }
        });
    }
}
