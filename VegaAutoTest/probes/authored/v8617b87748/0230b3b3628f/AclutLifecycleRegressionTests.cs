using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Galaxy.Data.Calibrations;
using Galaxy.Platform.Utilities;
using Vega.Data.Calibration;
using Vega.Utility.Common.Properties;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("ACLut components")]
public class AclutLifecycleRegressionTests
{
    [Fact]
    public void Step_validation_reacts_to_values_and_maximum_changes() => AclutChecks.Run(a =>
    {
        var rows = new[] { (double.NaN, true), (double.NegativeInfinity, true), (double.PositiveInfinity, true), (-1d, true), (0d, true), (double.Epsilon, false), (.5, false), (1d, false), (1.0001, true) };
        foreach (int axis in new[] { 1, 2 })
        {
            using var f = new AclutFixture();
            foreach (var row in rows)
            {
                if (axis == 1) f.VM.RAC1Value = row.Item1; else f.VM.RAC2Value = row.Item1;
                bool flag = axis == 1 ? f.VM.IsAC1StepOutOfRange : f.VM.IsAC2StepOutOfRange;
                string? error = axis == 1 ? f.VM.RAC1ErrorMessage : f.VM.RAC2ErrorMessage;
                string id = $"step/{axis}/{row.Item1}";
                AclutChecks.Check(a, id + "/flag", flag == row.Item2, row.Item2.ToString(), flag);
                AclutChecks.Check(a, id + "/message", row.Item2 ? error == Resources.ACStepOutOfRange : error == null, row.Item2 ? Resources.ACStepOutOfRange : "null", error);
                AclutChecks.Check(a, id + "/isolation", !(axis == 1 ? f.VM.IsAC2StepOutOfRange : f.VM.IsAC1StepOutOfRange), "other axis remains valid", axis);
            }
            if (axis == 1) { f.VM.RAC1Value = .5; f.VM.AC1MaxStepValue = .25; } else { f.VM.RAC2Value = .5; f.VM.AC2MaxStepValue = .25; }
            AclutChecks.Check(a, $"max/{axis}/lower", axis == 1 ? f.VM.IsAC1StepOutOfRange : f.VM.IsAC2StepOutOfRange, "lower maximum invalidates step", axis);
            if (axis == 1) f.VM.AC1MaxStepValue = .5; else f.VM.AC2MaxStepValue = .5;
            AclutChecks.Check(a, $"max/{axis}/equal", !(axis == 1 ? f.VM.IsAC1StepOutOfRange : f.VM.IsAC2StepOutOfRange), "step equal to maximum is valid", axis);
        }
    });
    [Fact]
    public void Cancellation_source_is_reused_or_replaced_without_a_stale_token() => AclutChecks.Run(a =>
    {
        using var f = new AclutFixture(); ((CancellationTokenSource)f.Get("_cancellationTokenSource")!).Dispose(); f.Put("_cancellationTokenSource", null);
        var token = (CancellationToken)f.Invoke("EnsureCancellationTokenSource")!; var first = (CancellationTokenSource)f.Get("_cancellationTokenSource")!;
        AclutChecks.Check(a, "cts/new", token.CanBeCanceled && !token.IsCancellationRequested, "fresh cancellable token", token);
        var reused = (CancellationToken)f.Invoke("EnsureCancellationTokenSource")!;
        AclutChecks.Check(a, "cts/reuse", ReferenceEquals(first, f.Get("_cancellationTokenSource")) && reused == token, "same live source and token", reused);
        first.Cancel(); var next = (CancellationToken)f.Invoke("EnsureCancellationTokenSource")!;
        AclutChecks.Check(a, "cts/replace", !ReferenceEquals(first, f.Get("_cancellationTokenSource")) && !next.IsCancellationRequested, "cancelled source replaced", next);
        var ex = Record.Exception(() => first.Cancel());
        AclutChecks.Check(a, "cts/dispose", ex is ObjectDisposedException, "replaced source disposed", ex?.GetType());
        ((CancellationTokenSource)f.Get("_cancellationTokenSource")!).Cancel();
        AclutChecks.Check(a, "cts/propagate", next.IsCancellationRequested, "returned token observes cancellation", next.IsCancellationRequested);
        ((CancellationTokenSource)f.Get("_cancellationTokenSource")!).Dispose(); f.Put("_cancellationTokenSource", null);
        var final = (CancellationToken)f.Invoke("EnsureCancellationTokenSource")!;
        AclutChecks.Check(a, "cts/restart", final.CanBeCanceled && !final.IsCancellationRequested, "fresh token after completed-chain cleanup", final);
    });
    private static string Signature(IEnumerable<ACLutResultItem> rows) => string.Join("|", rows.Select(x => FormattableString.Invariant($"{x.Index}:{x.AC2Angle}:{x.AC1Angle}:{x.DeltaX}")));
    [Fact]
    public void Scan_completion_cancellation_and_failure_preserve_the_history_contract() => AclutChecks.Run(a =>
    {
        string path = PathHelper.GetUserPath("Resources", "CalibrationDataResult", "ACLutCalibrationData.json");
        byte[]? original = File.Exists(path) ? File.ReadAllBytes(path) : null; Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            foreach (string mode in new[] { "complete", "cancel", "failure" })
            {
                using var f = new AclutFixture(); using var cts = new CancellationTokenSource(); f.Put("_ac1SensitivitySign", -1);
                var history = new ACLutAssociatedResult(); history.ACLutResultItems.Add(new ACLutResultItem { Index = 1, AC2Angle = 99, AC1Angle = 77, DeltaX = 3 });
                ACLutCaliResult.SaveAssociatedResult(history); byte[] baseline = File.ReadAllBytes(path);
                if (mode == "cancel") f.Peak = cts.Cancel;
                if (mode == "failure") f.Peak = () => throw new InvalidOperationException("synthetic camera failure");
                CalibrationStatus status = CalibrationStatus.Unstarted;
                var ex = Record.Exception(() => { status = f.VM.CalculateLUTAsync(cts.Token).GetAwaiter().GetResult(); });
                bool complete = mode == "complete"; string expected = complete ? "1:0:50:0|2:1:50:0|3:2:50:0" : "1:1:50:0";
                bool outcome = mode == "failure" ? ex?.Message.Contains("synthetic camera failure") == true : ex == null && status == (complete ? CalibrationStatus.Calibrated : CalibrationStatus.Failed);
                AclutChecks.Check(a, mode + "/outcome", outcome, mode == "failure" ? "camera failure propagated" : complete ? "Calibrated" : "Failed", ex?.Message ?? status.ToString());
                AclutChecks.Check(a, mode + "/table", Signature(f.VM.CurACLutResultItems) == expected, expected, Signature(f.VM.CurACLutResultItems));
                AclutChecks.Check(a, mode + "/provenance", (bool)f.Get("_curResultsComplete")! == complete, complete.ToString(), f.Get("_curResultsComplete"));
                string disk = Signature(ACLutCaliResult.InitFromHisFile().ACLutResultItems);
                AclutChecks.Check(a, mode + "/history", disk == (complete ? expected : "1:99:77:3"), complete ? expected : "1:99:77:3", disk);
                AclutChecks.Check(a, mode + "/unlock", (int)f.Get("_interLockRunning")! == 0, "scan interlock released", f.Get("_interLockRunning"));
                AclutChecks.Check(a, mode + "/reset", (int)f.Get("_ac1SensitivitySign")! == 0, "old direction forgotten at scan entry", f.Get("_ac1SensitivitySign"));
                if (!complete)
                {
                    AclutChecks.Check(a, mode + "/bytes", baseline.SequenceEqual(File.ReadAllBytes(path)), "previous history bytes unchanged", disk);
                    AclutChecks.UI.Value.Invoke(() => f.Invoke("SaveCalibrationResult", false));
                    disk = Signature(ACLutCaliResult.InitFromHisFile().ACLutResultItems);
                    AclutChecks.Check(a, mode + "/explicit-persist", disk == expected, expected, disk);
                    var saved = (ACLutAssociatedResult)f.Get("_aCLutAssociatedResult")!;
                    AclutChecks.Check(a, mode + "/copy", !ReferenceEquals(saved.ACLutResultItems[0], f.VM.CurACLutResultItems[0]), "persisted model owns a copy", ReferenceEquals(saved.ACLutResultItems[0], f.VM.CurACLutResultItems[0]));
                }
            }
        }
        finally { if (original == null) File.Delete(path); else File.WriteAllBytes(path, original); }
    });
    [Fact]
    public void Manual_scan_dispatches_off_the_dispatcher_and_cleans_up_after_cancellation() => AclutChecks.Run(a =>
    {
        using var f = new AclutFixture(); using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        var source = (CancellationTokenSource)f.Get("_cancellationTokenSource")!; int worker = 0;
        f.Reset = () => { worker = Environment.CurrentManagedThreadId; entered.Set(); if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("scan gate"); };
        try
        {
            AclutChecks.UI.Value.Invoke(f.VM.CalculateLUT);
            AclutChecks.Check(a, "dispatch/source", f.Get("_cancellationTokenSource") != null, "source exists on command return", f.Get("_cancellationTokenSource"));
            AclutChecks.Check(a, "dispatch/reuse", ReferenceEquals(source, f.Get("_cancellationTokenSource")), "prepare source reused", f.Get("_cancellationTokenSource"));
            bool started = entered.Wait(TimeSpan.FromSeconds(5));
            AclutChecks.Check(a, "dispatch/worker", started && worker != AclutChecks.UI.Value.Thread.ManagedThreadId, "scan executes off dispatcher", worker);
            var pulse = AclutChecks.UI.Value.InvokeAsync(() => true).Task;
            AclutChecks.Check(a, "dispatch/pulse", pulse.Wait(TimeSpan.FromSeconds(2)) && pulse.Result, "dispatcher processes work during blocked scan", pulse.Status);
            source.Cancel(); release.Set();
            bool done = SpinWait.SpinUntil(() => f.Get("_cancellationTokenSource") == null && f.VM.CanCalibration, TimeSpan.FromSeconds(5));
            AclutChecks.Check(a, "dispatch/completion", done, "cancelled worker completes cleanup", done);
            AclutChecks.Check(a, "dispatch/null", f.Get("_cancellationTokenSource") == null, "disposed source cleared", f.Get("_cancellationTokenSource"));
            AclutChecks.Check(a, "dispatch/actions", f.VM.CanCalibration && f.VM.CanExport && f.VM.CanSave, "actions restored", $"{f.VM.CanCalibration}/{f.VM.CanExport}/{f.VM.CanSave}");
            AclutChecks.Check(a, "dispatch/motion", f.Moves.Count == 0, "cancel before first scan point causes no moves", f.Moves.Count);
        }
        finally { release.Set(); SpinWait.SpinUntil(() => f.Get("_caliLockRunning") is 0, TimeSpan.FromSeconds(12)); }
    });
    [Fact]
    public void Operator_resources_resolve_in_English_and_Simplified_Chinese() => AclutChecks.Run(a =>
    {
        var rows = new[] {
            ("ACLutSaveIncompleteConfirm", "Current results are incomplete (from an aborted calibration). Overwrite last saved data?", "当前结果不完整（来自中止的校准）。覆盖上一次保存的数据？"),
            ("ACLutStopNotExecuting", "Program is not executing, continue to stop?", "程序未在执行，确认停止？"),
            ("AnotherCalibrationRunning", "Another calibration is currently running.", "另一个校准正在运行。"),
            ("ErrorMsgFmt", "Error Msg: probe.xlsx", "错误信息：probe.xlsx"),
            ("ExportResult", "Export result", "导出结果"),
            ("ExportResultSuccessFmt", "Export result: probe.xlsx Success", "导出结果：probe.xlsx 成功"),
            ("ExportResultFailedFmt", "Failed to export result: probe.xlsx", "导出结果失败：probe.xlsx"),
            ("SaveCaliResultFailed", "Save cali result failed.", "保存校准结果失败。") };
        foreach (var row in rows) foreach (string language in new[] { "en-US", "zh-Hans" })
        {
            string? format = Resources.ResourceManager.GetString(row.Item1, CultureInfo.GetCultureInfo(language));
            string actual = format == null ? "<missing>" : string.Format(CultureInfo.InvariantCulture, format, "probe.xlsx"); string expected = language == "en-US" ? row.Item2 : row.Item3;
            AclutChecks.Check(a, row.Item1 + "/" + language, actual == expected, expected, actual);
        }
    });
}
