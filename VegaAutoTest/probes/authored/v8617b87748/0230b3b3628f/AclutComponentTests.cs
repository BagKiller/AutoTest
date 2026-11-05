using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Galaxy.Data.Calibrations;
using Galaxy.Platform.Utilities;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.UI.Calibration.ACLutCalibration;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("ACLut components", DisableParallelization = true)]
public class AclutCollection { }

public class AclutProxy : DispatchProxy
{
    public Func<MethodInfo, object?[], object?> Call = null!;
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args ?? Array.Empty<object?>());
    public static object Create(Type type, Func<MethodInfo, object?[], object?> call)
    {
        var proxy = DispatchProxy.Create(type, typeof(AclutProxy));
        ((AclutProxy)proxy).Call = call;
        return proxy;
    }
}

internal static class AclutChecks
{
    public static void Run(Action<AssertionRecorder> body)
    {
        var a = new AssertionRecorder();
        try { body(a); }
        finally { a.Flush(); }
    }
    public static void Check(AssertionRecorder a, string id, bool ok, string expected, object? actual)
    {
        if (ok) a.Pass(id, "behavior", id, expected, Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "null");
        else { a.Fail(id, "behavior", id, expected, Convert.ToString(actual, CultureInfo.InvariantCulture) ?? "null"); Assert.True(ok, id); }
    }
    public static readonly Lazy<Dispatcher> UI = new(() =>
    {
        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown }; ready.SetResult(app.Dispatcher); Dispatcher.Run(); });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return ready.Task.GetAwaiter().GetResult();
    });
}

internal sealed class AclutFixture : IDisposable
{
    public readonly ACLutCalibrationViewModel VM = (ACLutCalibrationViewModel)RuntimeHelpers.GetUninitializedObject(typeof(ACLutCalibrationViewModel));
    public readonly ACLutCaliConfig Config = new() { RMinAC1 = 0, RMaxAC1 = 100, RMinAC2 = 0, RMaxAC2 = 3, MotorSettleMs = 0 };
    public readonly List<double> Moves = new();
    public double Position = 50, AC2 = 1, Slope = 2;
    public Func<double>? Sample;
    public Action? Peak, Reset;
    private double target1, target2;
    public AclutFixture()
    {
        _ = AclutChecks.UI.Value;
        VM.LogTextList = new ObservableCollection<string>();
        VM.CurACLutResultItems = new ObservableCollection<ACLutResultItem>();
        VM.RMinAC1Value = 0; VM.RMaxAC1Value = 100;
        VM.AC1MaxStepValue = 1; VM.AC2MaxStepValue = 1;
        VM.RAC1Value = .5; VM.RAC2Value = 1; VM.CentroidToleranceValue = .001; VM.AverageCount = 1;
        Put("_motorSettleMs", 0); Put("_calibrationConfig", Config);
        Put("_curExecuteModelLock", new object()); Put("_aCLutAssociatedResult", new ACLutAssociatedResult());
        Put("_cancellationTokenSource", new CancellationTokenSource());
        var logger = VM.GetType().GetProperty("Logger")!;
        logger.SetValue(VM, AclutProxy.Create(logger.PropertyType, (m, _) => Default(m.ReturnType)));
        foreach (var name in new[] { "_ac1Motor", "_ac2Motor" })
        {
            bool first = name == "_ac1Motor";
            Put(name, AclutProxy.Create(Field(name).FieldType, (m, args) => Motor(first, m, args)));
        }
        Put("_bssControllor", AclutProxy.Create(Field("_bssControllor").FieldType, (m, args) => m.Name switch
        {
            "ResetAutoAdjustPeakAnchors" => ResetAnchors(),
            "GetCCDCentroidXInfo" => args.Length > 1 && args[1] is true ? (100d, 0d, 0d) : Centroid(),
            "AutoAdjustCameraPeak" => AdjustPeak(),
            _ => throw new InvalidOperationException("Unexpected BSS call: " + m.Name)
        }));
    }
    private object? ResetAnchors() { Reset?.Invoke(); return null; }
    private object AdjustPeak() { Peak?.Invoke(); return (1, 150); }
    private object Centroid() { double x = Sample?.Invoke() ?? 100 + Slope * (Position - 50); return (x, (x - 100) / 2, (x - 100) / 2); }
    private object? Motor(bool first, MethodInfo m, object?[] args)
    {
        switch (m.Name)
        {
            case "GetCurPosition": return first ? Position : AC2;
            case "SetTargetPosition": if (first) target1 = (double)args[0]!; else target2 = (double)args[0]!; break;
            case "MovePosition":
                double next = args.Length > 0 && args[0] is double d ? d : first ? target1 : target2;
                if (first) { Position = next; Moves.Add(next); } else AC2 = next;
                break;
            case "IsMoving": return false;
            case "IsError": return false;
            case "IsReadyToMove": case "IsStateMatching": case "IsHomed": return true;
            case "GetErrInfo": case "GetProtectInfo": return "";
        }
        if (m.ReturnType.Name == "DeviceCmdResult")
        {
            var result = m.ReturnType.IsValueType ? Activator.CreateInstance(m.ReturnType)! : RuntimeHelpers.GetUninitializedObject(m.ReturnType);
            var p = m.ReturnType.GetProperty("ResultState")!;
            var success = Enum.Parse(p.PropertyType, "Succeed");
            if (p.SetMethod != null) p.SetValue(result, success);
            else m.ReturnType.GetField("<ResultState>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(result, success);
            return result;
        }
        return Default(m.ReturnType);
    }
    private static object? Default(Type t) => t == typeof(void) ? null : t == typeof(Task) ? Task.CompletedTask : t.IsValueType ? Activator.CreateInstance(t) : null;
    private FieldInfo Field(string name)
    {
        for (Type? t = VM.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new MissingFieldException(name);
    }
    public void Put(string name, object? value) => Field(name).SetValue(VM, value);
    public object? Get(string name) => Field(name).GetValue(VM);
    public object? Invoke(string name, params object?[] args)
    {
        var method = VM.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!;
        try { return method.Invoke(VM, args); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); throw; }
    }
    public (double, double, double) Adjust(CancellationToken token = default) => ((double, double, double))Invoke("AjustAC1ToTarget", Config, token)!;
    public void Dispose() { (Get("_cancellationTokenSource") as CancellationTokenSource)?.Dispose(); }
}

[Collection("ACLut components")]
public class AclutMotionRegressionTests
{
    [Fact]
    public void Signed_linear_plants_converge_for_both_probe_directions() => AclutChecks.Run(a =>
    {
        foreach (double slope in new[] { 2d, -2d }) foreach (double start in new[] { 48d, 52d }) foreach (bool reverse in new[] { false, true })
        {
            using var f = new AclutFixture { Slope = slope, Position = start }; f.VM.ReverseOrientationAC1 = reverse;
            var result = f.Adjust(); string id = $"linear/{slope}/{start}/{reverse}";
            double first = start + ((slope * (start - 50) > 0) != reverse ? -.5 : .5);
            AclutChecks.Check(a, id + "/moves", f.Moves.Count == 2, "two moves", f.Moves.Count);
            AclutChecks.Check(a, id + "/probe", f.Moves[0] == first, first.ToString(CultureInfo.InvariantCulture), f.Moves[0]);
            AclutChecks.Check(a, id + "/correction", f.Moves[1] == 50, "50", f.Moves[1]);
            AclutChecks.Check(a, id + "/result", result.Item1 == 50, "50", result.Item1);
            AclutChecks.Check(a, id + "/residual", result.Item2 == 0 && result.Item3 == 0, "zero centroid residuals", result);
            AclutChecks.Check(a, id + "/memory", (int)f.Get("_ac1SensitivitySign")! == Math.Sign(slope), Math.Sign(slope).ToString(), f.Get("_ac1SensitivitySign"));
        }
    });
    [Fact]
    public void Direction_memory_survives_adjacent_points_and_recovers_after_slope_reversal() => AclutChecks.Run(a =>
    {
        using var f = new AclutFixture();
        var rows = new[] { (52d, 2d, false, 51.5), (48d, 2d, true, 48.5), (52d, -2d, false, 52.5), (48d, -2d, false, 48.5) };
        for (int i = 0; i < rows.Length; i++)
        {
            var r = rows[i]; f.Position = r.Item1; f.Slope = r.Item2; f.VM.ReverseOrientationAC1 = r.Item3; f.Moves.Clear();
            var result = f.Adjust();
            AclutChecks.Check(a, $"memory/{i}/probe", f.Moves[0] == r.Item4, r.Item4.ToString(CultureInfo.InvariantCulture), f.Moves[0]);
            AclutChecks.Check(a, $"memory/{i}/target", result.Item1 == 50, "50", result.Item1);
            AclutChecks.Check(a, $"memory/{i}/sign", (int)f.Get("_ac1SensitivitySign")! == Math.Sign(r.Item2), Math.Sign(r.Item2).ToString(), f.Get("_ac1SensitivitySign"));
        }
    });
    [Fact]
    public void Invalid_sensitivity_cancellation_and_range_limits_stop_motion() => AclutChecks.Run(a =>
    {
        foreach (double sample in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, 104d, 104.002 })
        {
            using var f = new AclutFixture { Position = 52 }; int reads = 0; f.Sample = () => reads++ == 0 ? 104 : sample;
            var ex = Record.Exception(() => f.Adjust()); string text = double.IsFinite(sample) ? "sensitivity too small" : "not a finite number";
            AclutChecks.Check(a, $"bad/{sample}/exception", ex is Exception && ex.Message.Contains(text), text, ex?.Message);
            AclutChecks.Check(a, $"bad/{sample}/motion", f.Moves.SequenceEqual(new[] { 51.5 }), "only probe to 51.5", string.Join(",", f.Moves));
            AclutChecks.Check(a, $"bad/{sample}/memory", (int)f.Get("_ac1SensitivitySign")! == 0, "invalid measurement does not establish direction", f.Get("_ac1SensitivitySign"));
        }
        using (var f = new AclutFixture { Position = 52 })
        {
            using var cts = new CancellationTokenSource(); cts.Cancel(); var ex = Record.Exception(() => f.Adjust(cts.Token));
            AclutChecks.Check(a, "cancel/type", ex is OperationCanceledException, "OperationCanceledException", ex?.GetType());
            AclutChecks.Check(a, "cancel/motion", f.Moves.Count == 0, "no moves", f.Moves.Count);
        }
        foreach (double minimum in new[] { 51.75, 50.25 })
        {
            using var f = new AclutFixture { Position = 52 }; f.VM.RMinAC1Value = minimum;
            var ex = Record.Exception(() => f.Adjust()); var expected = minimum == 51.75 ? Array.Empty<double>() : new[] { 51.5 };
            AclutChecks.Check(a, $"range/{minimum}/exception", ex?.Message.Contains("out of range") == true, "out of range", ex?.Message);
            AclutChecks.Check(a, $"range/{minimum}/moves", f.Moves.SequenceEqual(expected), string.Join(",", expected), string.Join(",", f.Moves));
            AclutChecks.Check(a, $"range/{minimum}/cancel", ((CancellationTokenSource)f.Get("_cancellationTokenSource")!).IsCancellationRequested, "cancelled", f.Get("_cancellationTokenSource"));
        }
        foreach (double start in new[] { 49.975, 50d, 50.025 })
        {
            using var f = new AclutFixture { Position = start }; f.VM.CentroidToleranceValue = .1; var result = f.Adjust();
            AclutChecks.Check(a, $"tolerance/{start}", f.Moves.Count == 0 && result.Item1 == start, "in-tolerance position retained without motion", result.Item1);
        }
    });
}
