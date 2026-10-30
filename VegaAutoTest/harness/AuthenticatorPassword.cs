// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace Vega.AutoTest.Harness;

/// <summary>当前 Vega 的机型条件。认证器必须按同一组条件生成超级用户密码。</summary>
public sealed record AuthenticatorLoginTarget(
    string Model, bool Simulator, bool Gem, string Generation, int MachineId, string RootCondition);

/// <summary>
/// 从官方认证器读取超级用户密码。只操作指定的 Authenticator.exe，
/// 密码留在内存，不写日志、剪贴板或仓库。
/// </summary>
public static class AuthenticatorPassword
{
    public static string Resolve(EvidenceLog log)
        => Resolve(log, () => Read(log));

    public static string Resolve(EvidenceLog log, Func<string> readFromAuthenticator)
    {
        _ = log;
        return VegaEnv.PasswordOverride ?? readFromAuthenticator();
    }

    public static string Read(EvidenceLog log)
    {
        var target = LoadTarget(VegaEnv.VegaExe);
        log.Step($"认证器对齐 {target.RootCondition}，机器ID={target.MachineId}");
        return ReadFromAuthenticator(VegaEnv.AuthenticatorExe, target, log);
    }

    public static AuthenticatorLoginTarget LoadTarget(string vegaExe)
    {
        var directory = Path.GetDirectoryName(vegaExe)
            ?? throw new UiPrerequisiteException("LOGIN_NOT_READY", "无法定位被测 Vega 目录。");
        var configPath = Path.Combine(directory, "Resources", "ComponentConfig.json");
        if (!File.Exists(configPath))
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "被测程序缺少 Resources\\ComponentConfig.json。");

        var json = File.ReadAllText(configPath);
        string? userDataFolder;
        try
        {
            userDataFolder = ReadUserDataFolder(json);
        }
        catch (Exception error)
        {
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "无法读取被测机型配置：" + error.Message);
        }

        var machineId = FirstMachineId(ReadExisting(MachineIdCandidates(directory, userDataFolder)));
        try
        {
            return FromComponent(json, machineId);
        }
        catch (Exception error)
        {
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "无法对齐认证器机型：" + error.Message);
        }
    }

    public static AuthenticatorLoginTarget FromComponent(string json, int machineId)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json, JsonOptions);
        var collection = document.RootElement.GetProperty("ToolConfigCollection");
        var selected = collection.GetProperty("SelectedToolType").GetString() ?? "";
        System.Text.Json.JsonElement? tool = null;
        foreach (var item in collection.GetProperty("ToolConfigs").EnumerateArray())
        {
            if (item.GetProperty("ToolType").GetString() == selected)
            {
                tool = item;
                break;
            }
        }

        if (tool is null)
            throw new InvalidOperationException($"ComponentConfig 没有选中机型 {selected}。");

        var root = tool.Value.GetProperty("RootCondition").GetString() ?? "";
        var parts = root.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new InvalidOperationException("RootCondition 为空。");

        var generation = parts.FirstOrDefault(part => part.StartsWith("Gen", StringComparison.Ordinal)) ?? "Gen2";
        return new AuthenticatorLoginTarget(
            parts[0],
            parts.Any(part => part == "Simulator"),
            parts.Any(part => part == "Gem"),
            generation,
            machineId,
            root);
    }

    public static string? ReadUserDataFolder(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json, JsonOptions);
        var collection = document.RootElement.GetProperty("ToolConfigCollection");
        var selected = collection.GetProperty("SelectedToolType").GetString();
        foreach (var item in collection.GetProperty("ToolConfigs").EnumerateArray())
        {
            if (item.GetProperty("ToolType").GetString() != selected) continue;
            return item.TryGetProperty("UserDataFolder", out var folder) ? folder.GetString() : null;
        }

        return null;
    }

    public static IEnumerable<string> MachineIdCandidates(string vegaDirectory, string? userDataFolder)
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(userDataFolder))
            yield return Path.Combine(local, "Vega", userDataFolder, "MachineConfiguration.json");
        yield return Path.Combine(local, "Vega", "Resources", "MachineConfiguration.json");
        yield return Path.Combine(vegaDirectory, "Resources", "MachineConfiguration", "General.json");
    }

    public static int FirstMachineId(IEnumerable<string> jsonDocuments)
    {
        foreach (var json in jsonDocuments)
        {
            var id = TryReadMachineId(json);
            if (id is not null) return id.Value;
        }

        return 0;
    }

    public static int? TryReadMachineId(string json)
    {
        using var document = System.Text.Json.JsonDocument.Parse(json, JsonOptions);
        return FindMachineId(document.RootElement);
    }

    private static IEnumerable<string> ReadExisting(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path)) yield return File.ReadAllText(path);
        }
    }

    private static int? FindMachineId(System.Text.Json.JsonElement element)
    {
        switch (element.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Object:
                if (element.TryGetProperty("Name", out var name) && name.GetString() == "Machine ID"
                    && element.TryGetProperty("Value", out var value) && TryValue(value, out var id))
                    return id;
                foreach (var property in element.EnumerateObject())
                {
                    var found = FindMachineId(property.Value);
                    if (found is not null) return found;
                }

                return null;
            case System.Text.Json.JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindMachineId(item);
                    if (found is not null) return found;
                }

                return null;
            default:
                return null;
        }
    }

    private static bool TryValue(System.Text.Json.JsonElement value, out int id)
    {
        if (value.ValueKind == System.Text.Json.JsonValueKind.Number) return value.TryGetInt32(out id);
        return int.TryParse(value.GetString(), out id);
    }

    private static readonly System.Text.Json.JsonDocumentOptions JsonOptions = new()
    {
        CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static string ReadFromAuthenticator(string exe, AuthenticatorLoginTarget target, EvidenceLog log)
    {
        if (!File.Exists(exe))
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器不存在：" + exe);

        using var automation = new UIA3Automation();
        var app = EnsureRunning(exe, log);
        var window = WaitForWindow(app, automation);
        if (window is null)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器窗口没有出现。");

        SelectTool(window, target, log);
        var tab = SelectSuperuserTab(window);
        var edits = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        if (edits.Length < 2)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器超级用户页缺少机器ID或密码框。");

        edits[0].Patterns.Value.Pattern.SetValue(target.MachineId.ToString());
        Thread.Sleep(300);
        var generate = tab.FindAllDescendants(cf => cf.ByControlType(ControlType.Button)).FirstOrDefault();
        if (generate is null)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器超级用户页缺少生成按钮。");
        generate.AsButton().Invoke();
        Thread.Sleep(400);
        ThrowIfGenerateFailed(app, automation);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var password = UiProps.TryReadValue(edits[1]);
            if (!string.IsNullOrWhiteSpace(password) && password != "<读取失败>")
            {
                log.Step("已从认证器读取超级用户密码");
                return password;
            }

            Thread.Sleep(200);
        }

        throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器没有给出超级用户密码。");
    }

    private static Application EnsureRunning(string exe, EvidenceLog log)
    {
        var full = Path.GetFullPath(exe);
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(full)))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, full, StringComparison.OrdinalIgnoreCase))
                    return Application.Attach(process);
            }
            catch
            {
                // 读不到模块路径的进程不是这次要操作的认证器
            }
        }

        log.Step("启动认证器");
        var start = new ProcessStartInfo
        {
            FileName = full,
            WorkingDirectory = Path.GetDirectoryName(full)!,
            UseShellExecute = false
        };
        var dotnet = @"C:\AutoTest\runtime\dotnet";
        if (Directory.Exists(dotnet)) start.Environment["DOTNET_ROOT"] = dotnet;
        return Application.Launch(start);
    }

    private static Window? WaitForWindow(Application app, UIA3Automation automation)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            try
            {
                var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(2));
                if (window is not null && window.Title is "认证器" or "Authenticator") return window;
            }
            catch
            {
                // 窗口尚未创建
            }

            Thread.Sleep(500);
        }

        return null;
    }

    private static void SelectTool(Window window, AuthenticatorLoginTarget target, EvidenceLog log)
    {
        var tool = FindCombo(window, target.Model);
        Select(tool, target.Model, log);
        Thread.Sleep(400);
        if (!target.Model.Contains("Vega", StringComparison.OrdinalIgnoreCase)) return;

        SetChecked(window, "Simulator?", target.Simulator, log);
        SetChecked(window, "Gem?", target.Gem, log);
        var generation = FindCombo(window, target.Generation);
        Select(generation, target.Generation, log);
    }

    private static AutomationElement FindCombo(AutomationElement window, string itemText)
    {
        foreach (var combo in window.FindAllDescendants(cf => cf.ByControlType(ControlType.ComboBox)))
        {
            try
            {
                combo.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(200);
                var items = combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem));
                var matched = items.Any(item => string.Equals(ComboBoxReader.FindChildText(item) ?? UiProps.Name(item), itemText, StringComparison.OrdinalIgnoreCase));
                try { combo.Patterns.ExpandCollapse.Pattern.Collapse(); } catch { /* 收起失败不改变选项 */ }
                if (matched) return combo;
            }
            catch
            {
                // 这个下拉还没展开，继续找下一个
            }
        }

        throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器下拉里没有 " + itemText + "。");
    }

    private static void Select(AutomationElement combo, string display, EvidenceLog log)
    {
        if (!ComboBoxReader.SelectByDisplay(combo, display, log.Step))
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器里没有选项 " + display + "。");
        try { combo.Patterns.ExpandCollapse.Pattern.Collapse(); } catch { /* 下拉已关闭 */ }
    }

    private static void SetChecked(AutomationElement window, string name, bool value, EvidenceLog log)
    {
        var box = window.FindAllDescendants(cf => cf.ByControlType(ControlType.CheckBox))
            .FirstOrDefault(candidate => UiProps.Name(candidate) == name);
        if (box is null)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器里没有 " + name + "。");

        var toggle = box.Patterns.Toggle.Pattern;
        var want = value ? ToggleState.On : ToggleState.Off;
        if (toggle.ToggleState != want)
        {
            toggle.Toggle();
            log.Step($"认证器 {name} -> {(value ? "勾选" : "取消")}");
            Thread.Sleep(300);
        }
    }

    private static AutomationElement SelectSuperuserTab(AutomationElement window)
    {
        var tab = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
            .FirstOrDefault(item => UiProps.Name(item) is "超级用户密码" or "Superuser Password");
        if (tab is null)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器没有超级用户密码页。");
        tab.Patterns.SelectionItem.Pattern.Select();
        Thread.Sleep(300);
        return tab;
    }

    private static void ThrowIfGenerateFailed(Application app, UIA3Automation automation)
    {
        foreach (var window in app.GetAllTopLevelWindows(automation))
        {
            if (window.Title is not ("Error" or "错误")) continue;
            try
            {
                window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button))?.AsButton().Invoke();
            }
            catch
            {
                // 错误框关不上也不把其中的文本写进日志
            }

            throw new UiPrerequisiteException("LOGIN_NOT_READY", "认证器生成超级用户密码失败。");
        }
    }
}
