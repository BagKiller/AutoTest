using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Vega.AutoTest.Harness;
using Xunit;
using static Vega.AutoTest.Authored.ExceptionTestSupport;

namespace Vega.AutoTest.Authored;

[Collection("App exception regression")]
public sealed class UnobservedTaskRegressionTests
{
    [Fact]
    public void Background_handler_observes_storm_without_dispatching_user_feedback()
    {
        var recorder = new AssertionRecorder();
        using var ready = new ManualResetEventSlim();
        using var stop = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        var posted = new ConcurrentQueue<DispatcherOperation>();
        global::Vega.App? app = null;
        Dispatcher? dispatcher = null;
        Exception? ownerError = null;
        Exception? workerError = null;
        bool allObserved = true;
        int attempts = 0, throttled = 0, total = 0;
        var owner = new Thread(() =>
        {
            try
            {
                app = new global::Vega.App();
                dispatcher = Dispatcher.CurrentDispatcher;
                dispatcher.Hooks.OperationPosted += Capture;
                ready.Set();
                // Deliberately do not pump: a finalizer-side handler must not need UI progress.
                stop.Wait();
            }
            catch (Exception error) { ownerError = error; ready.Set(); }
            finally
            {
                if (dispatcher != null)
                {
                    dispatcher.Hooks.OperationPosted -= Capture;
                    dispatcher.InvokeShutdown();
                }
            }
        }) { IsBackground = true };
        owner.SetApartmentState(ApartmentState.STA);
        void Capture(object? sender, DispatcherHookEventArgs args) => posted.Enqueue(args.Operation);
        Thread? worker = null;
        try
        {
            owner.Start();
            Check(recorder, "owner-ready", ready.Wait(TimeSpan.FromSeconds(15)) && ownerError == null,
                "STA App constructed without startup", ownerError?.ToString() ?? "ready=" + ready.IsSet);
            var throttler = AppType.GetField("_exceptionThrottler", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(app)!;
            var statistics = throttler.GetType().GetMethod("GetStatistics")!;
            var handler = Method("OnUnobservedTaskException");
            worker = new Thread(() =>
            {
                try
                {
                    var error = new AggregateException(Diagnostic());
                    do
                    {
                        var args = new UnobservedTaskExceptionEventArgs(error);
                        handler.Invoke(app, new object?[] { null, args });
                        allObserved &= args.Observed;
                        attempts++;
                        object?[] values = { 0, 0, false };
                        statistics.Invoke(throttler, values);
                        total = (int)values[0]!;
                        throttled = (int)values[1]!;
                    } while (throttled == 0 && attempts < 2048);
                    // Exercise SetObserved again after the rate limiter has suppressed an event.
                    var suppressed = new UnobservedTaskExceptionEventArgs(error);
                    handler.Invoke(app, new object?[] { null, suppressed });
                    allObserved &= suppressed.Observed;
                }
                catch (Exception error) { workerError = error; }
                finally { completed.Set(); }
            }) { IsBackground = true };
            worker.Start();
            Check(recorder, "background-return", completed.Wait(TimeSpan.FromSeconds(15)),
                "handler returns while owner dispatcher is not pumping", "completed=" + completed.IsSet);
            Check(recorder, "handler-success", workerError == null, "no escaped exception", workerError?.ToString() ?? "none");
            Check(recorder, "all-observed", allObserved, "every event observed, including suppressed events", allObserved.ToString());
            Check(recorder, "storm-suppressed", throttled > 0 && total >= throttled,
                "a repeated exception storm is throttled with coherent statistics",
                $"attempts={attempts}, total={total}, throttled={throttled}");
            Check(recorder, "no-ui-dispatch", posted.IsEmpty,
                "zero operations posted to the App dispatcher", "posted=" + posted.Count);
        }
        catch (Exception error)
        {
            recorder.Fail("execution", "behavior", "Unobserved handler regression", "all checks pass", error.ToString());
            throw;
        }
        finally
        {
            // Release an old implementation's synchronous Invoke without executing its feedback callback.
            foreach (var operation in posted) operation.Abort();
            worker?.Join(TimeSpan.FromSeconds(3));
            stop.Set();
            owner.Join(TimeSpan.FromSeconds(3));
            recorder.Flush();
        }
    }
}
