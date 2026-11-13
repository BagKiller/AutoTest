using System.Diagnostics;
using System.Reflection;
using System.Windows;
using Vega.AutoTest.Harness;

[assembly: AssemblyMetadata("VegaWpfResourceDictionaries",
    "/Vega.AutoTest.WpfHost.Contracts;component/Startup.xaml")]

namespace WpfHostContracts;

// Synthetic harness resource only; it is never a product/acceptance dictionary.
public class StartupDictionary : ResourceDictionary
{
    public StartupDictionary()
    {
        if (Environment.GetEnvironmentVariable("WPF_CONTRACT_MODE") is "startup" or "shared-budget")
            Thread.Sleep(1400);
    }
}

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void MustTimeout(Action action)
    {
        try { action(); }
        catch (WpfHostTimeoutException) { return; }
        throw new InvalidOperationException("Expected WpfHostTimeoutException");
    }

    public static int Main(string[] args)
    {
        var mode = args.Single();
        Environment.SetEnvironmentVariable("WPF_CONTRACT_MODE", mode);
        Environment.SetEnvironmentVariable(WpfTestHost.TimeoutVariable, mode == "shared-budget" ? "2" : "1");
        var watch = Stopwatch.StartNew();
        switch (mode)
        {
            case "startup":
                var bodyRan = false;
                MustTimeout(() => WpfTestHost.Run(() => { bodyRan = true; }));
                Check(watch.Elapsed < TimeSpan.FromSeconds(2.5), "Startup exceeded deadline");
                // Late startup cannot resurrect the timed-out host or signal a disposed event.
                Thread.Sleep(800);
                MustTimeout(() => WpfTestHost.Run(() => { bodyRan = true; }));
                Check(!bodyRan, "Late initializer resurrected poisoned host");
                break;
            case "work":
                WpfTestHost.Run(() => Check(Application.Current is not null, "No Application"));
                watch.Restart();
                MustTimeout(() => WpfTestHost.Run(() => Thread.Sleep(3000)));
                Check(watch.Elapsed < TimeSpan.FromSeconds(2.5), "Work exceeded deadline");
                watch.Restart();
                MustTimeout(() => WpfTestHost.Run(() => { }));
                Check(watch.Elapsed < TimeSpan.FromMilliseconds(500), "Poisoned host did not fail immediately");
                break;
            case "shared-budget":
                MustTimeout(() => WpfTestHost.Run(() => Thread.Sleep(1400)));
                Check(watch.Elapsed < TimeSpan.FromSeconds(2.7), "Startup and body received separate budgets");
                break;
            case "async":
                var thread = WpfTestHost.Run(() => Environment.CurrentManagedThreadId);
                var answer = WpfTestHost.RunAsync(async () =>
                {
                    await Task.Delay(20);
                    Check(thread == Environment.CurrentManagedThreadId, "Continuation lost STA dispatcher");
                    return 42;
                });
                Check(answer == 42, "Async result changed");
                Check(WpfTestHost.LoadedDictionaries.Count == 1, "Dictionary was not loaded");
                try { WpfTestHost.Run(() => throw new ArgumentException("original fixture exception")); }
                catch (ArgumentException error)
                {
                    Check(error.Message == "original fixture exception", "Original exception changed");
                    Console.WriteLine("PASS " + mode);
                    return 0;
                }
                throw new InvalidOperationException("Exception was swallowed");
            default:
                throw new ArgumentException("Unknown contract mode");
        }
        Console.WriteLine("PASS " + mode);
        return 0;
    }
}
