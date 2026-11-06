using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.UIA3;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Runner;

/// <summary>Attach to a manually started Simulator and verify its actual login; no test verdict is created.</summary>
internal static class ManualLogin
{
    public static int Run(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
            options.Add(args[i], args[i + 1]);
        var repo = Path.GetFullPath(options["--repo-root"]);
        var pid = int.Parse(options["--pid"]);
        Environment.SetEnvironmentVariable("VEGA_SW_ROOT", repo);
        using var process = Process.GetProcessById(pid);
        var exe = process.MainModule?.FileName ?? throw new InvalidOperationException("Cannot read Vega image");
        if (!string.Equals(Path.GetFullPath(exe), Path.GetFullPath(VegaEnv.VegaExe), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PID does not match the selected Vega executable");
        var target = AuthenticatorPassword.LoadTarget(exe);
        if (!target.Simulator) throw new InvalidOperationException("Manual login is restricted to Simulator");
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
}
