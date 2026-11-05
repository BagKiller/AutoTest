using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Vega.UI.Calibration;
using Vega.UI.Calibration.ACLutCalibration;
using Xunit;

namespace Vega.AutoTest.Authored;

public class AclutInterfaceProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Handler = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
        => Handler(method!, args ?? Array.Empty<object?>());
}

internal static class AclutSupport
{
    internal static object Proxy(Type type, Func<MethodInfo, object?[], object?> handler)
    {
        var value = DispatchProxy.Create(type, typeof(AclutInterfaceProxy));
        ((AclutInterfaceProxy)value).Handler = handler;
        return value;
    }
    internal static FieldInfo Field(object target, string name)
    {
        for (Type? t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f;
        }
        throw new MissingFieldException(target.GetType().FullName, name);
    }
    internal static void Set(object target, string name, object? value) => Field(target, name).SetValue(target, value);
    internal static object? Call(object target, string name, params object?[] args)
    {
        for (Type? t = target.GetType(); t != null; t = t.BaseType)
        {
            var m = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length);
            if (m == null) continue;
            try { return m.Invoke(target, args); }
            catch (TargetInvocationException e) when (e.InnerException != null)
            { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
        }
        throw new MissingMethodException(target.GetType().FullName, name);
    }
    internal static void Check(AssertionRecorder a, bool ok, string id, object? expected, object? actual)
    {
        if (ok) a.Pass(id, "behavior", id, Convert.ToString(expected, CultureInfo.InvariantCulture) ?? "null", Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "null");
        else a.Fail(id, "behavior", id, Convert.ToString(expected, CultureInfo.InvariantCulture) ?? "null", Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "null");
        Assert.True(ok, id + ": expected " + expected + "; actual " + actual);
    }
    internal static void Near(AssertionRecorder a, double expected, double actual, string id)
        => Check(a, double.IsFinite(actual) && Math.Abs(expected - actual) <= 1e-9, id, expected, actual);
    internal static void Run(Action<AssertionRecorder> test)
    {
        var a = new AssertionRecorder();
        try { test(a); }
        finally { a.Flush(); }
    }
    internal static async Task RunAsync(Func<AssertionRecorder, Task> test)
    {
        var a = new AssertionRecorder();
        try { await test(a); }
        finally { a.Flush(); }
    }
    // Adapt result envelopes, not calibration mathematics. Missing/unhandled calls fail the fixture.
    internal static object? MotorResult(Type type, double position)
    {
        if (type == typeof(void)) return null;
        if (type == typeof(double)) return position;
        if (type == typeof(float)) return (float)position;
        if (type == typeof(bool)) return true;
        if (type == typeof(string)) return "";
        if (type.IsEnum) return Enum.GetNames(type).Contains("Succeed") ? Enum.Parse(type, "Succeed") : Activator.CreateInstance(type);
        if (type.IsPrimitive) return Activator.CreateInstance(type);
        var value = type.IsValueType ? Activator.CreateInstance(type)! : RuntimeHelpers.GetUninitializedObject(type);
        for (Type? t = type; t != null; t = t.BaseType)
        foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (f.FieldType.IsEnum) f.SetValue(value, MotorResult(f.FieldType, position));
            else if (f.FieldType == typeof(double) && (f.Name.Contains("value", StringComparison.OrdinalIgnoreCase) || f.Name.Contains("pos", StringComparison.OrdinalIgnoreCase))) f.SetValue(value, position);
            else if (f.FieldType == typeof(bool)) f.SetValue(value, f.Name.Contains("success", StringComparison.OrdinalIgnoreCase));
        }
        return value;
    }
    internal static ACLutCalibrationViewModel Bare()
    {
        var vm = (ACLutCalibrationViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ACLutCalibrationViewModel));
        vm.LogTextList = new();
        vm.AC1MaxStepValue = vm.AC2MaxStepValue = 1;
        vm.RAC1Value = vm.RAC2Value = .25;
        vm.RMinAC1Value = -100;
        vm.RMaxAC1Value = 100;
        vm.CentroidToleranceValue = .001;
        vm.AverageCount = 1;
        vm.Logger = (Galaxy.Platform.ILogging.ILogService)Proxy(typeof(Galaxy.Platform.ILogging.ILogService), (m, _) => m.ReturnType == typeof(void) ? null : throw new NotSupportedException(m.Name));
        return vm;
    }
}

internal sealed class AclutPlant
{
    internal readonly ACLutCalibrationViewModel Vm = AclutSupport.Bare();
    internal readonly List<double> Moves = new();
    internal double Position, Bias = 4, Slope = 2;
    internal double? ProbeReading;
    private double _target;
    private int _reads;
    internal AclutPlant()
    {
        var motorField = AclutSupport.Field(Vm, "_ac1Motor");
        motorField.SetValue(Vm, AclutSupport.Proxy(motorField.FieldType, Motor));
        var bssField = AclutSupport.Field(Vm, "_bssControllor");
        bssField.SetValue(Vm, AclutSupport.Proxy(bssField.FieldType, (m, args) =>
        {
            if (m.Name != "GetCCDCentroidXInfo") throw new NotSupportedException(m.Name);
            bool baseline = args.OfType<bool>().LastOrDefault();
            if (baseline) return (0d, 0d, 0d);
            var sum = ++_reads >= 2 && ProbeReading.HasValue ? ProbeReading.Value : Bias + Slope * Position;
            return (sum, sum / 2, sum / 2);
        }));
        AclutSupport.Set(Vm, "_motorSettleMs", 0);
        AclutSupport.Set(Vm, "_nExecuteStatus", 1);
    }
    private object? Motor(MethodInfo m, object?[] args)
    {
        if (m.Name == "SetTargetPosition") _target = Convert.ToDouble(args[0], CultureInfo.InvariantCulture);
        else if (m.Name == "MovePosition") { Position = _target; Moves.Add(Position); }
        else if (!new[] { "Pos", "Status", "State", "Busy", "Moving", "Protect", "Err", "Ready", "Complete" }.Any(s => m.Name.Contains(s, StringComparison.OrdinalIgnoreCase)))
            throw new NotSupportedException("Unhandled motor API: " + m.Name);
        var parameters = m.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
            if (parameters[i].ParameterType.IsByRef)
                args[i] = AclutSupport.MotorResult(parameters[i].ParameterType.GetElementType()!, Position);
        if (m.ReturnType == typeof(bool) && (m.Name.Contains("Busy") || m.Name.Contains("Moving"))) return false;
        return AclutSupport.MotorResult(m.ReturnType, Position);
    }
    internal (double angle, double x1, double x2) Adjust(CancellationToken token = default)
    {
        var method = typeof(ACLutCalibrationViewModel).GetMethod("AjustAC1ToTarget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var config = Activator.CreateInstance(method.GetParameters()[0].ParameterType);
        return ((double, double, double))AclutSupport.Call(Vm, "AjustAC1ToTarget", config, token)!;
    }
}

public class ACLutMotionRegression
{
    [Fact]
    public void Signed_probe_converges_and_direction_memory_recovers_after_slope_reversal() => AclutSupport.Run(a =>
    {
        int row = 0;
        foreach (double slope in new[] { -2d, 2d })
        foreach (double bias in new[] { -4d, 4d })
        foreach (bool reverse in new[] { false, true })
        {
            var p = new AclutPlant { Slope = slope, Bias = bias };
            p.Vm.ReverseOrientationAC1 = reverse;
            var result = p.Adjust();
            string id = "linear-" + ++row;
            double expectedProbe = ((bias > 0) ^ reverse) ? -.25 : .25;
            AclutSupport.Near(a, expectedProbe, p.Moves[0], id + "-initial-probe");
            AclutSupport.Near(a, -bias / slope, result.angle, id + "-analytic-root");
            AclutSupport.Near(a, 0, p.Bias + p.Slope * p.Position, id + "-residual");
            AclutSupport.Check(a, (int)AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm)! == Math.Sign(slope), id + "-remember", Math.Sign(slope), AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm));
            double oldPosition = p.Position;
            int start = p.Moves.Count;
            p.Slope = -slope;
            result = p.Adjust();
            AclutSupport.Near(a, oldPosition - Math.Sign(2 * bias) * Math.Sign(slope) * .25, p.Moves[start], id + "-remembered-probe");
            AclutSupport.Near(a, bias / slope, result.angle, id + "-reversed-root");
            AclutSupport.Near(a, 0, p.Bias + p.Slope * p.Position, id + "-reversed-residual");
            AclutSupport.Check(a, (int)AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm)! == -Math.Sign(slope), id + "-relearn", -Math.Sign(slope), AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm));
        }
    });

    [Fact]
    public void Invalid_sensitivity_cancellation_bounds_and_already_centered_inputs() => AclutSupport.Run(a =>
    {
        foreach (double reading in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 4d })
        {
            var p = new AclutPlant { ProbeReading = reading };
            var e = Record.Exception(() => p.Adjust());
            string fragment = reading == 4 ? "sensitivity too small" : "sensitivity is not a finite number";
            AclutSupport.Check(a, e != null && e.Message.Contains(fragment), "sensitivity-" + reading, fragment, e?.Message);
            AclutSupport.Check(a, p.Moves.Count == 1, "no-invalid-extrapolation-" + reading, 1, p.Moves.Count);
            AclutSupport.Check(a, (int)AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm)! == 0, "no-invalid-memory-" + reading, 0, AclutSupport.Field(p.Vm, "_ac1SensitivitySign").GetValue(p.Vm));
        }
        var cancelled = new AclutPlant();
        var error = Record.Exception(() => cancelled.Adjust(new CancellationToken(true)));
        AclutSupport.Check(a, error is OperationCanceledException, "cancel-type", nameof(OperationCanceledException), error?.GetType().Name);
        AclutSupport.Check(a, cancelled.Moves.Count == 0, "cancel-no-motion", 0, cancelled.Moves.Count);
        var bounded = new AclutPlant();
        bounded.Vm.RMinAC1Value = -.1;
        error = Record.Exception(() => bounded.Adjust());
        AclutSupport.Check(a, error != null && error.Message.Contains("out of range"), "probe-bounds", "out of range", error?.Message);
        AclutSupport.Check(a, bounded.Moves.Count == 0, "bounds-no-motion", 0, bounded.Moves.Count);
        var centered = new AclutPlant { Bias = .0005 };
        var result = centered.Adjust();
        AclutSupport.Near(a, 0, result.angle, "centered-position");
        AclutSupport.Check(a, centered.Moves.Count == 0, "centered-no-motion", 0, centered.Moves.Count);
    });
}

public class ACLutLifecycleContracts
{
    [Fact]
    public void Step_flags_reject_nonfinite_nonpositive_and_oversize_values() => AclutSupport.Run(a =>
    {
        var vm = AclutSupport.Bare();
        foreach (int axis in new[] { 1, 2 })
        foreach (double value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity, .1, 1, 1.01 })
        {
            vm.RAC1Value = vm.RAC2Value = .25;
            if (axis == 1) vm.RAC1Value = value; else vm.RAC2Value = value;
            bool invalid = value != .1 && value != 1;
            bool flag = axis == 1 ? vm.IsAC1StepOutOfRange : vm.IsAC2StepOutOfRange;
            string? message = axis == 1 ? vm.RAC1ErrorMessage : vm.RAC2ErrorMessage;
            string id = "step-" + axis + "-" + value;
            AclutSupport.Check(a, flag == invalid, id, invalid, flag);
            AclutSupport.Check(a, invalid ? !string.IsNullOrWhiteSpace(message) : message == null, id + "-message", invalid ? "error" : "null", message);
            AclutSupport.Check(a, !(axis == 1 ? vm.IsAC2StepOutOfRange : vm.IsAC1StepOutOfRange), id + "-other-axis", false, axis == 1 ? vm.IsAC2StepOutOfRange : vm.IsAC1StepOutOfRange);
        }
        vm.RAC1Value = vm.RAC2Value = .1;
        vm.AC1MaxStepValue = vm.AC2MaxStepValue = .05;
        AclutSupport.Check(a, vm.IsAC1StepOutOfRange && vm.IsAC2StepOutOfRange, "lower-max-revalidates", true, vm.IsAC1StepOutOfRange && vm.IsAC2StepOutOfRange);
        vm.AC1MaxStepValue = vm.AC2MaxStepValue = 1;
        AclutSupport.Check(a, !vm.IsAC1StepOutOfRange && !vm.IsAC2StepOutOfRange, "restore-max-revalidates", false, vm.IsAC1StepOutOfRange || vm.IsAC2StepOutOfRange);
    });

    [Fact]
    public void Cancellation_source_is_created_reused_and_replaced_after_stop() => AclutSupport.Run(a =>
    {
        var vm = AclutSupport.Bare();
        var token = (CancellationToken)AclutSupport.Call(vm, "EnsureCancellationTokenSource")!;
        var first = (CancellationTokenSource)AclutSupport.Field(vm, "_cancellationTokenSource").GetValue(vm)!;
        try
        {
            AclutSupport.Check(a, token.CanBeCanceled && !token.IsCancellationRequested, "new-token", "usable", token);
            var reused = (CancellationToken)AclutSupport.Call(vm, "EnsureCancellationTokenSource")!;
            AclutSupport.Check(a, token == reused && ReferenceEquals(first, AclutSupport.Field(vm, "_cancellationTokenSource").GetValue(vm)), "reuse-live-source", true, token == reused);
            first.Cancel();
            AclutSupport.Check(a, token.IsCancellationRequested, "stop-propagates", true, token.IsCancellationRequested);
            var fresh = (CancellationToken)AclutSupport.Call(vm, "EnsureCancellationTokenSource")!;
            AclutSupport.Check(a, fresh != token && fresh.CanBeCanceled && !fresh.IsCancellationRequested, "restart-fresh-token", true, fresh != token && !fresh.IsCancellationRequested);
            var error = Record.Exception(() => first.Cancel());
            AclutSupport.Check(a, error is ObjectDisposedException, "replaced-source-disposed", nameof(ObjectDisposedException), error?.GetType().Name);
        }
        finally { ((CancellationTokenSource?)AclutSupport.Field(vm, "_cancellationTokenSource").GetValue(vm))?.Dispose(); }
    });

    [Fact]
    public Task Pmt_abort_calls_service_even_with_null_or_cancelled_source() => AclutSupport.RunAsync(async a =>
    {
        foreach (int state in new[] { 0, 1, 2 })
        {
            var vm = (PMTCalibrationViewModel)RuntimeHelpers.GetUninitializedObject(typeof(PMTCalibrationViewModel));
            using var source = state == 0 ? null : new CancellationTokenSource();
            if (state == 2) source!.Cancel();
            AclutSupport.Set(vm, "_cancellationTokenSource", source);
            AclutSupport.Set(vm, "_currentCaliID", "fixture-id");
            vm.IsCalibrationRunning = true;
            int calls = 0;
            object? received = null;
            var property = vm.GetType().GetProperty("CalibrationService")!;
            property.SetValue(vm, AclutSupport.Proxy(property.PropertyType, (m, args) =>
            {
                if (m.Name != "Abort") throw new NotSupportedException(m.Name);
                calls++; received = args[0];
                return m.ReturnType == typeof(Task) ? Task.CompletedTask : m.ReturnType == typeof(void) ? null : AclutSupport.MotorResult(m.ReturnType, 0);
            }));
            var error = await Record.ExceptionAsync(async () => await (Task)AclutSupport.Call(vm, "AbortCalibrationAsync")!);
            AclutSupport.Check(a, error == null, "pmt-completes-" + state, "no exception", error?.ToString());
            AclutSupport.Check(a, calls == 1, "pmt-service-once-" + state, 1, calls);
            AclutSupport.Check(a, Equals(received, "fixture-id"), "pmt-id-" + state, "fixture-id", received);
            AclutSupport.Check(a, !vm.IsCalibrationRunning, "pmt-idle-" + state, false, vm.IsCalibrationRunning);
            if (source != null) AclutSupport.Check(a, source.IsCancellationRequested, "pmt-cancels-" + state, true, source.IsCancellationRequested);
        }
    });

    [Fact]
    public void Operator_resources_resolve_in_English_and_Simplified_Chinese() => AclutSupport.Run(a =>
    {
        string[][] rows = {
            new[] { "ACLutSaveIncompleteConfirm", "Current results are incomplete (from an aborted calibration). Overwrite last saved data?", "当前结果不完整（来自中止的校准）。覆盖上一次保存的数据？" },
            new[] { "ACLutStopNotExecuting", "Program is not executing, continue to stop?", "程序未在执行，确认停止？" },
            new[] { "AnotherCalibrationRunning", "Another calibration is currently running.", "另一个校准正在运行。" },
            new[] { "ErrorMsgFmt", "Error Msg: {0}", "错误信息：{0}" },
            new[] { "ExportResult", "Export result", "导出结果" },
            new[] { "ExportResultSuccessFmt", "Export result: {0} Success", "导出结果：{0} 成功" },
            new[] { "ExportResultFailedFmt", "Failed to export result: {0}", "导出结果失败：{0}" },
            new[] { "SaveCaliResultFailed", "Save cali result failed.", "保存校准结果失败。" }
        };
        foreach (var row in rows)
        for (int language = 1; language <= 2; language++)
        {
            var actual = Vega.Utility.Common.Properties.Resources.ResourceManager.GetString(row[0], CultureInfo.GetCultureInfo(language == 1 ? "en-US" : "zh-Hans"));
            AclutSupport.Check(a, actual == row[language], row[0] + "-" + language, row[language], actual);
        }
    });
}
