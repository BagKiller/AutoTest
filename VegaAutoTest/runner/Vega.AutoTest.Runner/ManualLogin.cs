using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.UIA3;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>Attach to a manually started Simulator and verify its actual login; no test verdict is created.</summary>
public static class ManualLogin
{
    public static int Run(string[] args)
    {
        if (!TryParse(args, out var repo, out var pid, out var usage))
        {
            Console.Error.WriteLine(usage);
            Console.WriteLine(JsonSerializer.Serialize(new { state = "USAGE_ERROR", reason = usage }));
            return 3;
        }
        Environment.SetEnvironmentVariable("VEGA_SW_ROOT", repo);
        using var process = Process.GetProcessById(pid);
        var exe = process.MainModule?.FileName ?? throw new InvalidOperationException("Cannot read Vega image");
        if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(VegaEnv.VegaExe), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PID does not match the selected Vega executable");
        var target = AuthenticatorPassword.LoadTarget(exe);
        if (!target.Simulator) throw new InvalidOperationException("Manual login is restricted to Simulator");
        // FlaUI 4.0 Application.Dispose only releases the Process handle (checked
        // 2026-11-06); it does not close Vega, so the logged-in session stays open.
        using var app = Application.Attach(pid);
        using var automation = new UIA3Automation();
        var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(10))
            ?? throw new InvalidOperationException("Vega main window is not available");
        var log = new EvidenceLog();
        var dialogs = new DialogHandler(log);
        var states = new DeviceStateReader(log);
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(90) && !process.HasExited)
        {
            dialogs.HandleAll(main);
            var title = main.Title;
            var menu = main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeMenuItem"));
            var deviceState = states.Read(main);
            if (LoginFlow.IsAuthenticatedReady(title, VegaEnv.UserName,
                menu?.Properties.IsEnabled.ValueOrDefault == true, deviceState))
            {
                Console.WriteLine(JsonSerializer.Serialize(new { state = "LOGGED_IN", pid, title, target.Model, deviceState }));
                return 0;
            }
            Thread.Sleep(1000);
        }
        Console.WriteLine(JsonSerializer.Serialize(new { state = "LOGIN_NOT_CONFIRMED", pid,
            reason = dialogs.LastHandlingError ?? "No authenticated window with enabled menu" }));
        return 2;
    }

    /// <summary>Requires exactly --repo-root &lt;existing dir&gt; and --pid &lt;positive int&gt;.</summary>
    public static bool TryParse(string[] args, out string repo, out int pid, out string usage)
    {
        const string Usage = "usage: login --repo-root <path> --pid <existing Vega pid>";
        repo = "";
        pid = 0;
        usage = Usage;
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is not ("--repo-root" or "--pid") || i + 1 >= args.Length || options.ContainsKey(args[i]))
            {
                usage = Usage + $" (unexpected or incomplete argument '{args[i]}')";
                return false;
            }
            options[args[i]] = args[++i];
        }

        if (!options.TryGetValue("--repo-root", out var root) || !Directory.Exists(root))
        {
            usage = Usage + " (--repo-root is missing or does not exist)";
            return false;
        }
        if (!options.TryGetValue("--pid", out var text) || !int.TryParse(text, out pid) || pid <= 0)
        {
            usage = Usage + " (--pid must be a positive integer)";
            return false;
        }

        repo = Path.GetFullPath(root);
        return true;
    }
}
