using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("BF3704")]
public sealed class BF3704AcquisitionStatisticsTests
{
    private readonly BF3704WpfHost host;
    public BF3704AcquisitionStatisticsTests(BF3704WpfHost host) { this.host = host; }
    [Fact]
    public Task RealAcquisitionLoopUsesBatchMeansAndRecordsEveryBatch()
        => BF3704Check.RunAsync(r => host.Run(async () =>
        {
            var rig = new BF3704Rig(); var vm = rig.VM;
            Task? acquisition = null; Task? recording = null;
            try
            {
                vm.DicAverageSeconds = 10;
                acquisition = vm.StartCommand.ExecuteAsync(null);
                var interval = Stopwatch.StartNew();
                await rig.Batch(new[] { 900d }, new[] { 800d }, 900, 800);
                BF3704Check.Number(r, "unrecorded-pd1-average", 900, vm.PD1Average);
                BF3704Check.Number(r, "unrecorded-pd2-average", 800, vm.PD2Average);
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                BF3704Check.That(r, "record-start-clears-old-extrema", vm.PD1Max == null && vm.PD2Max == null, "no recorded samples yet", vm.PD1Max + "/" + vm.PD2Max);
                await rig.Batch(new[] { 0d, 200d }, new[] { 20d, 40d, 60d }, 100, 40);
                BF3704Check.Number(r, "batch-mean-not-raw-maximum", 100, vm.PD1Max);
                BF3704Check.Number(r, "first-batch-minimum-not-stale-or-zero", 100, vm.PD1Min);
                BF3704Check.Number(r, "first-pd2-batch-mean", 40, vm.PD2Min);
                BF3704Check.Number(r, "single-positive-batch-contrast", 0, vm.PD1Contrast);
                await rig.Batch(new[] { 10d, 30d }, new[] { 80d, 100d }, 20, 90);
                BF3704Check.Number(r, "second-batch-extreme-visible-without-polling", 20, vm.PD1Min);
                BF3704Check.Number(r, "pd2-independent-maximum", 90, vm.PD2Max);
                await rig.Batch(new[] { 50d, 70d }, new[] { 10d, 30d }, 60, 20);
                BF3704Check.That(r, "script-fits-ten-second-window", interval.Elapsed < TimeSpan.FromSeconds(8), "all four batches delivered within 8 seconds", interval.Elapsed);
                BF3704Check.Number(r, "pd1-window-mean-of-four-batch-means", 270, vm.PD1Average);
                BF3704Check.Number(r, "pd2-window-unweighted-by-raw-point-count", 237.5, vm.PD2Average);
                BF3704Check.Number(r, "latest-batch-count-sum", 80, vm.PDCountSum);
                BF3704Check.Number(r, "record-excludes-pre-record-pd1", 100, vm.PD1Max);
                BF3704Check.Number(r, "record-pd1-minimum", 20, vm.PD1Min);
                BF3704Check.Number(r, "record-pd1-percent", 66.66666666666667, vm.PD1Contrast);
                BF3704Check.Number(r, "record-excludes-pre-record-pd2", 90, vm.PD2Max);
                BF3704Check.Number(r, "record-pd2-minimum", 20, vm.PD2Min);
                BF3704Check.Number(r, "record-pd2-percent", 63.63636363636363, vm.PD2Contrast);
                vm.StopDicRecordCommand.Execute(null);
                await recording.WaitAsync(TimeSpan.FromSeconds(5));
                BF3704Check.That(r, "manual-stop-leaves-acquisition-running", !vm.IsDicRecording && vm.IsCalibrationRunning, "record stopped; acquisition running", vm.IsDicRecording + "/" + vm.IsCalibrationRunning);
                vm.DicAverageSeconds = 1;
                await Task.Delay(1100);
                await rig.Batch(new[] { 300d }, new[] { 200d }, 300, 200);
                BF3704Check.Number(r, "shortened-pd1-window-ages-out-all-old-batches", 300, vm.PD1Average);
                BF3704Check.Number(r, "shortened-pd2-window", 200, vm.PD2Average);
                BF3704Check.Number(r, "manual-stop-freezes-pd1-result", 100, vm.PD1Max);
                BF3704Check.Number(r, "manual-stop-freezes-pd2-result", 20, vm.PD2Min);
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                BF3704Check.That(r, "second-record-resets-all-six-results", vm.PD1Max == null && vm.PD1Min == null && vm.PD2Max == null && vm.PD2Min == null && vm.PD1Contrast == null && vm.PD2Contrast == null, "all six null", vm.PD1Max);
                await rig.Batch(new[] { 0d, 0d }, new[] { 0d }, 0, 0);
                BF3704Check.Number(r, "zero-is-a-real-recorded-sample", 0, vm.PD1Min);
                BF3704Check.That(r, "all-zero-contrast-is-undefined", vm.PD1Contrast == null && vm.PD2Contrast == null, "null contrasts", vm.PD1Contrast + "/" + vm.PD2Contrast);
                await rig.Batch(new[] { 40d }, new[] { 60d }, 40, 60);
                BF3704Check.Number(r, "zero-first-sample-survives", 0, vm.PD1Min);
                BF3704Check.Number(r, "zero-to-positive-contrast", 100, vm.PD1Contrast);
                vm.StartCommand.Cancel();
                await acquisition.WaitAsync(TimeSpan.FromSeconds(5));
                await recording.WaitAsync(TimeSpan.FromSeconds(5));
                BF3704Check.That(r, "acquisition-stop-cancels-record", !vm.IsCalibrationRunning && !vm.IsDicRecording, "both stopped", vm.IsCalibrationRunning + "/" + vm.IsDicRecording);
                BF3704Check.That(r, "interrupted-summary-uses-recorded-samples", rig.HasMessage("PD1 Max=40 Min=0 Contrast=100%") && rig.HasMessage("PD2 Max=60 Min=0 Contrast=100%"), "summary contains both accumulated channels", string.Join("\n", rig.Messages));
                acquisition = vm.StartCommand.ExecuteAsync(null);
                BF3704Check.That(r, "acquisition-restart-clears-averages-and-results", vm.PD1Average == null && vm.PD2Average == null && vm.PD1Max == null && vm.PD1Min == null && vm.PD2Max == null && vm.PD2Min == null && vm.PD1Contrast == null && vm.PD2Contrast == null, "all averages and recording results null", vm.PD1Average);
                await rig.Batch(new[] { 7d }, new[] { 11d }, 7, 11);
                BF3704Check.Number(r, "restart-pd1-window-has-no-old-sum", 7, vm.PD1Average);
                BF3704Check.Number(r, "restart-pd2-window-has-no-old-sum", 11, vm.PD2Average);
                BF3704Check.That(r, "no-production-errors", !rig.HasMessage("StackTracedError") && !rig.HasMessage("BF3704 injected"), "no product error logs", string.Join("\n", rig.Messages));
            }
            finally { await rig.Cleanup(acquisition, recording); }
        }));
}

[Collection("BF3704")]
public sealed class BF3704RecordingLifecycleTests
{
    private readonly BF3704WpfHost host;
    public BF3704RecordingLifecycleTests(BF3704WpfHost host) { this.host = host; }
    [Fact]
    public Task NoDataStopNavigationAndReadFailureTerminateRecording()
        => BF3704Check.RunAsync(r => host.Run(async () =>
        {
            var rig = new BF3704Rig(); var vm = rig.VM;
            Task? acquisition = null; Task? recording = null;
            try
            {
                vm.RecordMinutes = 1;
                acquisition = vm.StartCommand.ExecuteAsync(null);
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                BF3704Check.That(r, "countdown-initial", vm.DicRecordRemainingTime == "01:00", "01:00", vm.DicRecordRemainingTime);
                await BF3704Check.Until(() => vm.DicRecordRemainingTime != "01:00", 4000);
                var remaining = vm.DicRecordRemainingTime;
                BF3704Check.That(r, "countdown-advances", remaining == "00:59" || remaining == "00:58" || remaining == "00:57", "positive countdown below 01:00", remaining);
                vm.StopDicRecordCommand.Execute(null);
                await recording.WaitAsync(TimeSpan.FromSeconds(5));
                BF3704Check.That(r, "manual-stop-preserves-countdown", vm.DicRecordRemainingTime == remaining, remaining, vm.DicRecordRemainingTime);
                BF3704Check.That(r, "no-data-does-not-manufacture-zero-statistics", vm.PD1Average == null && vm.PD2Average == null && vm.PD1Max == null && vm.PD1Min == null && vm.PD2Max == null && vm.PD2Min == null && vm.PD1Contrast == null && vm.PD2Contrast == null, "all null", vm.PD1Max);
                BF3704Check.That(r, "no-data-interrupted-summary", rig.HasMessage("DicContrastRecord end: interrupted") && rig.HasMessage("PD1 Max=N/A Min=N/A Contrast=N/A%") && rig.HasMessage("PD2 Max=N/A Min=N/A Contrast=N/A%"), "interrupted; both channels N/A", string.Join("\n", rig.Messages));
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                vm.OnNavigatingFrom();
                await recording.WaitAsync(TimeSpan.FromSeconds(5));
                BF3704Check.That(r, "navigation-cancels-record", !vm.IsDicRecording, "false", vm.IsDicRecording);
                BF3704Check.That(r, "navigation-disconnects-daq", rig.Stops == 1, "one StopDAQ call", rig.Stops);
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                rig.ThrowOnRead = true;
                await acquisition.WaitAsync(TimeSpan.FromSeconds(5));
                await recording.WaitAsync(TimeSpan.FromSeconds(5));
                BF3704Check.That(r, "read-failure-stops-acquisition-and-record", !vm.IsCalibrationRunning && !vm.IsDicRecording, "both stopped", vm.IsCalibrationRunning + "/" + vm.IsDicRecording);
                BF3704Check.That(r, "read-failure-observed-by-production", rig.HasMessage("BF3704 injected DAQ read failure"), "injected read failure logged", string.Join("\n", rig.Messages));
                BF3704Check.That(r, "record-disabled-after-failure", !vm.StartDicRecordCommand.CanExecute(null), "false", vm.StartDicRecordCommand.CanExecute(null));
                BF3704Check.That(r, "restart-enabled-after-failure", vm.StartCommand.CanExecute(null), "true", vm.StartCommand.CanExecute(null));
            }
            finally { await rig.Cleanup(acquisition, recording); }
        }));

    [Fact]
    public Task OneMinuteRecordCompletesByRealElapsedTimeWithoutSamples()
        => BF3704Check.RunAsync(r => host.Run(async () =>
        {
            var rig = new BF3704Rig(); var vm = rig.VM;
            Task? acquisition = null; Task? recording = null;
            try
            {
                vm.RecordMinutes = 1;
                acquisition = vm.StartCommand.ExecuteAsync(null);
                var elapsed = Stopwatch.StartNew();
                recording = vm.StartDicRecordCommand.ExecuteAsync(null);
                BF3704Check.That(r, "natural-record-active", vm.IsDicRecording, "true", vm.IsDicRecording);
                await recording.WaitAsync(TimeSpan.FromSeconds(90));
                BF3704Check.That(r, "duration-is-one-minute-not-one-tick", elapsed.Elapsed >= TimeSpan.FromMinutes(1), "at least 60 real seconds", elapsed.Elapsed);
                BF3704Check.That(r, "natural-record-stopped", !vm.IsDicRecording, "false", vm.IsDicRecording);
                BF3704Check.That(r, "completion-countdown-zero", vm.DicRecordRemainingTime == "00:00", "00:00", vm.DicRecordRemainingTime);
                BF3704Check.That(r, "completion-keeps-acquisition-running", vm.IsCalibrationRunning, "true", vm.IsCalibrationRunning);
                BF3704Check.That(r, "completion-summary", rig.HasMessage("DicContrastRecord end: completed"), "completed summary", string.Join("\n", rig.Messages));
                BF3704Check.That(r, "natural-no-data-is-null", vm.PD1Max == null && vm.PD2Max == null && vm.PD1Contrast == null && vm.PD2Contrast == null, "no fabricated samples", vm.PD1Max);
                BF3704Check.That(r, "record-can-be-started-again", vm.StartDicRecordCommand.CanExecute(null), "true", vm.StartDicRecordCommand.CanExecute(null));
            }
            finally { await rig.Cleanup(acquisition, recording); }
        }));
}
