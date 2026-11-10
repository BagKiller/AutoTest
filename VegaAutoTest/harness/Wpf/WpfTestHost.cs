// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Vega.AutoTest.Harness;

/// <summary>Raised when work on the WPF test thread does not finish in time (typically a deadlock).</summary>
public sealed class WpfHostTimeoutException : Exception
{
    public WpfHostTimeoutException(string message) : base(message) { }
}

/// <summary>
/// Run WPF code from a logic test on one long-lived STA thread whose Application
/// holds the product's real theme dictionaries.
///
/// This file is NOT compiled into Vega.AutoTest.Harness (which stays free of WPF and
/// Vega.* dependencies). Authored test projects link it, and name the product
/// dictionaries through the assembly metadata "VegaWpfResourceDictionaries"
/// ('|' separated pack paths, e.g. /Vega.UI.Common;component/Themes/General.xaml).
///
/// Rules: one Application per process (WPF limit), created once; all WPF objects are
/// created and used on its thread; a dictionary that fails to load is an explicit
/// error, never replaced by placeholder resources.
///
/// Deadlock guard (2026-11-10): a synchronous wait (.Result, .Wait(), GetAwaiter().GetResult())
/// on async work that resumes on this thread blocks it forever. Every call is bounded
/// (default 120 s, VEGA_AUTOTEST_WPF_TIMEOUT_SECONDS); on timeout the host is marked
/// unusable so later calls fail immediately with the same reason instead of hanging the
/// whole test run. Use <see cref="RunAsync{T}(Func{Task{T}})"/> and await product async APIs:
/// the dispatcher keeps processing their continuations while the test awaits them.
/// </summary>
public static class WpfTestHost
{
    public const string ResourceMetadataKey = "VegaWpfResourceDictionaries";
    public const string TimeoutVariable = "VEGA_AUTOTEST_WPF_TIMEOUT_SECONDS";

    private static readonly object Gate = new();
    private static Dispatcher? s_dispatcher;
    private static Exception? s_startupError;
    private static string? s_poisoned;
    private static IReadOnlyList<string> s_loaded = Array.Empty<string>();

    /// <summary>Product dictionaries actually merged into Application.Resources.</summary>
    public static IReadOnlyList<string> LoadedDictionaries => s_loaded;

    /// <summary>Upper bound for one Run/RunAsync call.</summary>
    public static TimeSpan Timeout
    {
        get
        {
            var text = Environment.GetEnvironmentVariable(TimeoutVariable);
            return int.TryParse(text, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : TimeSpan.FromSeconds(120);
        }
    }

    public static void Run(Action body) => Run<object?>(() =>
    {
        body();
        return null;
    });

    /// <summary>
    /// Execute synchronous <paramref name="body"/> on the WPF host thread. Exceptions keep
    /// their original type and stack (xUnit assertions stay assertions). Do not block on
    /// tasks inside it; use <see cref="RunAsync{T}(Func{Task{T}})"/> for async product APIs.
    /// </summary>
    public static T Run<T>(Func<T> body)
    {
        // Already on the host thread (helper called from inside Run): execute inline.
        if (s_dispatcher is { } current && current.CheckAccess())
            return body();
        return RunAsync(() => Task.FromResult(body()));
    }

    public static void RunAsync(Func<Task> body) => RunAsync<object?>(async () =>
    {
        await body();
        return null;
    });

    /// <summary>
    /// Execute async <paramref name="body"/> on the WPF host thread. Continuations of awaited
    /// product work run on that thread while it stays free to process them.
    /// </summary>
    public static T RunAsync<T>(Func<Task<T>> body)
    {
        var dispatcher = EnsureStarted();
        if (dispatcher.CheckAccess())
            throw new InvalidOperationException("WpfTestHost.Run/RunAsync cannot be nested; call the inner code directly.");

        var operation = dispatcher.InvokeAsync(() =>
        {
            try
            {
                return body();
            }
            catch (Exception error)
            {
                // Synchronous throw: keep it in the task, never in the dispatcher loop.
                return Task.FromException<T>(error);
            }
        }).Task.Unwrap();
        var timeout = Timeout;
        bool completed;
        try
        {
            completed = operation.Wait(timeout);
        }
        catch (AggregateException error) when (error.InnerExceptions.Count == 1)
        {
            ExceptionDispatchInfo.Capture(error.InnerExceptions[0]).Throw();
            throw;
        }

        if (!completed)
        {
            var reason = $"WPF test host work did not finish within {timeout.TotalSeconds:F0}s. "
                + "Typical cause: a synchronous wait (.Result/.Wait()/GetAwaiter().GetResult()) on async work "
                + "that must resume on the WPF thread, or non-UI work run inside WpfTestHost.Run. "
                + "Keep only WPF object creation/inspection inside Run and await product async APIs via RunAsync.";
            lock (Gate)
                s_poisoned ??= reason;
            throw new WpfHostTimeoutException(reason);
        }

        return operation.GetAwaiter().GetResult();
    }

    private static Dispatcher EnsureStarted()
    {
        lock (Gate)
        {
            if (s_poisoned is not null)
                throw new WpfHostTimeoutException("WPF test host is blocked by an earlier call and cannot be reused in this test run: " + s_poisoned);
            if (s_startupError is not null)
                throw new InvalidOperationException("WPF test host failed to start earlier: " + s_startupError.Message, s_startupError);
            if (s_dispatcher is not null)
                return s_dispatcher;

            using var ready = new ManualResetEventSlim();
            Exception? error = null;
            Dispatcher? dispatcher = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var application = Application.Current
                        ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    s_loaded = LoadProductDictionaries(application);
                    dispatcher = Dispatcher.CurrentDispatcher;
                }
                catch (Exception startup)
                {
                    error = startup;
                }
                finally
                {
                    ready.Set();
                }

                if (error is null)
                    Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "Vega WPF test host",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();

            if (error is not null)
            {
                s_startupError = error;
                throw new InvalidOperationException("WPF test host could not load the product resources: " + error.Message, error);
            }

            s_dispatcher = dispatcher!;
            return s_dispatcher;
        }
    }

    private static IReadOnlyList<string> LoadProductDictionaries(Application application)
    {
        var loaded = new List<string>();
        foreach (var path in ConfiguredDictionaries())
        {
            var uri = path.StartsWith("/", StringComparison.Ordinal)
                ? new Uri("pack://application:,,," + path, UriKind.Absolute)
                : new Uri(path, UriKind.RelativeOrAbsolute);
            try
            {
                application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = uri });
                loaded.Add(path);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException(
                    $"Product WPF resource dictionary could not be loaded: {path} ({error.GetType().Name}: {error.Message})", error);
            }
        }

        return loaded;
    }

    private static IEnumerable<string> ConfiguredDictionaries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            IEnumerable<AssemblyMetadataAttribute> attributes;
            try
            {
                attributes = assembly.GetCustomAttributes<AssemblyMetadataAttribute>();
            }
            catch
            {
                continue;
            }

            foreach (var attribute in attributes)
            {
                if (attribute.Key != ResourceMetadataKey || string.IsNullOrWhiteSpace(attribute.Value))
                    continue;
                foreach (var path in attribute.Value.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (seen.Add(path))
                        yield return path;
                }
            }
        }
    }
}
