using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Galaxy.Platform.ILogging;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.PD.IDAQDevice;
using Vega.UI.Calibration;
using Xunit;

namespace Vega.AutoTest.Authored;

[CollectionDefinition("BF3704", DisableParallelization = true)]
public sealed class BF3704Collection : ICollectionFixture<BF3704WpfHost> { }

public sealed class BF3704WpfHost : IDisposable
{
    private readonly Thread thread;
    private readonly TaskCompletionSource<Dispatcher> ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Dispatcher dispatcher;
    public BF3704WpfHost()
    {
        thread = new Thread(() =>
        {
            try
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
                ready.SetResult(app.Dispatcher);
                Dispatcher.Run();
            }
            catch (Exception e) { ready.TrySetException(e); }
        }) { IsBackground = true, Name = "BF3704 WPF" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        dispatcher = ready.Task.WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
    }
    public Task Run(Func<Task> body) => dispatcher.InvokeAsync(body).Task.Unwrap();
    public void Dispose()
    {
        dispatcher.Invoke(() => Application.Current.Shutdown());
        thread.Join(TimeSpan.FromSeconds(10));
    }
}

public static class BF3704Check
{
    public static void That(AssertionRecorder r, string id, bool condition, string expected, object? actual)
    {
        var observed = actual?.ToString() ?? "null";
        if (!condition)
        {
            r.Fail(id, "behavior", id, expected, observed);
            throw new Xunit.Sdk.XunitException(id + ": expected " + expected + "; actual " + observed);
        }
        r.Pass(id, "behavior", id, expected, observed);
    }
    public static void Number(AssertionRecorder r, string id, double expected, double? actual, double tolerance = 0.0000001)
        => That(r, id, actual.HasValue && double.IsFinite(actual.Value) && Math.Abs(actual.Value - expected) <= tolerance,
            expected.ToString("R", System.Globalization.CultureInfo.InvariantCulture), actual);
    public static async Task Until(Func<bool> predicate, int milliseconds = 5000)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate())
        {
            if (timer.ElapsedMilliseconds >= milliseconds) throw new TimeoutException("Production behavior did not reach the required state.");
            await Task.Delay(10);
        }
    }
    public static void Run(Action<AssertionRecorder> body)
    {
        var r = new AssertionRecorder();
        try { body(r); }
        catch (Exception e) { r.Fail("unexpected-or-mismatch", "execution", "Test execution", "All behavioral checks complete", e.ToString()); throw; }
        finally { r.Flush(); }
    }
    public static async Task RunAsync(Func<AssertionRecorder, Task> body)
    {
        var r = new AssertionRecorder();
        try { await body(r); }
        catch (Exception e) { r.Fail("unexpected-or-mismatch", "execution", "Test execution", "All behavioral checks complete", e.ToString()); throw; }
        finally { r.Flush(); }
    }
}

public class BF3704Proxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    public static T Make<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, BF3704Proxy>();
        ((BF3704Proxy)(object)proxy).Handler = handler;
        return proxy;
    }
}

// The only substituted input is the DAQ boundary. Calculations, commands, validation,
// timing, accumulation and cancellation execute in the compiled product view model.
public sealed class BF3704Rig
{
    private readonly ConcurrentQueue<(double[] A, double[] B)> batches = new();
    private (double[] A, double[] B)? active;
    public ConcurrentQueue<string> Messages { get; } = new();
    public BFCalibrationViewModel VM { get; }
    public bool ThrowOnRead { get; set; }
    public int Stops { get; private set; }
    public BF3704Rig()
    {
        var logger = BF3704Proxy.Make<ILogService>((method, args) =>
        {
            Messages.Enqueue(method.Name + ": " + string.Join(" | ", (args ?? Array.Empty<object?>()).Select(x => x?.ToString() ?? "null")));
            if (method.ReturnType == typeof(void)) return null;
            if (method.ReturnType == typeof(Task)) return Task.CompletedTask;
            return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
        });
        var daq = BF3704Proxy.Make<IDAQBoard>((method, args) =>
        {
            if (method.Name == nameof(IDAQBoard.StopDAQ))
            {
                Stops++;
                return method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
            }
            if (method.Name != nameof(IDAQBoard.GetDICDACValues)) throw new InvalidOperationException("Unexpected DAQ operation: " + method.Name);
            if (ThrowOnRead) throw new InvalidOperationException("BF3704 injected DAQ read failure");
            if ((CollectorBFType)args![0]! == CollectorBFType.PD1)
            {
                active = batches.TryDequeue(out var pair) ? pair : null;
                return active.HasValue ? Points(active.Value.A) : null;
            }
            var answer = active.HasValue ? Points(active.Value.B) : null;
            active = null;
            return answer;
        });
        VM = new BFCalibrationViewModel { Logger = logger, DAQBoard = daq };
    }
    private static List<Point> Points(double[] values) => values.Select((y, x) => new Point(x, y)).ToList();
    public async Task Batch(double[] a, double[] b, double expectedA, double expectedB)
    {
        if (VM.PD1CountAverage == expectedA && VM.PD2CountAverage == expectedB)
            throw new InvalidOperationException("Each scripted batch must have a distinguishable output.");
        batches.Enqueue((a, b));
        await BF3704Check.Until(() => VM.PD1CountAverage == expectedA && VM.PD2CountAverage == expectedB);
    }
    public bool HasMessage(string text) => Messages.Any(x => x.Contains(text, StringComparison.Ordinal));
    public async Task Cleanup(Task? acquisition, Task? recording)
    {
        if (VM.StopDicRecordCommand.CanExecute(null)) VM.StopDicRecordCommand.Execute(null);
        VM.StartCommand.Cancel();
        if (acquisition != null) await acquisition.WaitAsync(TimeSpan.FromSeconds(10));
        if (recording != null) await recording.WaitAsync(TimeSpan.FromSeconds(10));
    }
}
