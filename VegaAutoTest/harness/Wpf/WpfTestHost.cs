// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Vega.AutoTest.Harness;

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
/// </summary>
public static class WpfTestHost
{
    public const string ResourceMetadataKey = "VegaWpfResourceDictionaries";

    private static readonly object Gate = new();
    private static Dispatcher? s_dispatcher;
    private static Exception? s_startupError;
    private static IReadOnlyList<string> s_loaded = Array.Empty<string>();

    /// <summary>Product dictionaries actually merged into Application.Resources.</summary>
    public static IReadOnlyList<string> LoadedDictionaries => s_loaded;

    public static void Run(Action body) => Run<object?>(() =>
    {
        body();
        return null;
    });

    /// <summary>
    /// Execute <paramref name="body"/> on the WPF host thread. Exceptions keep their
    /// original type and stack (xUnit assertions stay assertions).
    /// </summary>
    public static T Run<T>(Func<T> body)
    {
        var dispatcher = EnsureStarted();
        if (dispatcher.CheckAccess())
            return body();

        T result = default!;
        ExceptionDispatchInfo? failure = null;
        dispatcher.Invoke(() =>
        {
            try
            {
                result = body();
            }
            catch (Exception error)
            {
                failure = ExceptionDispatchInfo.Capture(error);
            }
        });
        failure?.Throw();
        return result;
    }

    private static Dispatcher EnsureStarted()
    {
        lock (Gate)
        {
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
