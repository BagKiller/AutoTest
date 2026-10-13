// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using Newtonsoft.Json;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 记住当前界面：主窗口截图 + UIA 结构指纹。
/// 闭环用它对比修复前后，发现 AutomationId 被误增删或明显位移。
/// 坐标相对主窗口左上角，并按 8px 取整，避免 DPI 抖动把同一控件判成移动。
/// </summary>
public static class UiSnapshot
{
    public const string JsonName = "ui-snapshot.json";
    public const string PngName = "ui-snapshot.png";

    public static void Save(AutomationElement? root)
    {
        if (root is null)
        {
            return;
        }

        var nodes = new List<SnapshotNode>();
        var origin = SafeRect(root);
        Walk(root, origin, 0, new HashSet<IntPtr>(), nodes);

        var payload = new SnapshotFile
        {
            CapturedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            WindowName = UiProps.Name(root),
            Nodes = nodes,
        };
        File.WriteAllText(
            Path.Combine(RunContext.Dir, JsonName),
            JsonConvert.SerializeObject(payload, Formatting.Indented));

        try
        {
            using var image = Capture.Element(root);
            image.ToFile(Path.Combine(RunContext.Dir, PngName));
        }
        catch
        {
            // 截图失败不丢掉结构指纹；对比时缺图只说明没有像素证据
        }
    }

    private static void Walk(
        AutomationElement el, Rect origin, int depth, HashSet<IntPtr> visited, List<SnapshotNode> nodes)
    {
        if (depth > 8 || nodes.Count >= 800)
        {
            return;
        }

        try
        {
            var handle = el.Properties.NativeWindowHandle.ValueOrDefault;
            if (handle != IntPtr.Zero && !visited.Add(handle))
            {
                return;
            }

            var rect = SafeRect(el);
            nodes.Add(new SnapshotNode
            {
                Depth = depth,
                ControlType = el.ControlType.ToString(),
                AutomationId = UiProps.Id(el),
                Name = Truncate(UiProps.Name(el), 80),
                ClassName = el.ClassName ?? string.Empty,
                X = Quantize(rect.X - origin.X),
                Y = Quantize(rect.Y - origin.Y),
                W = Quantize(rect.W),
                H = Quantize(rect.H),
            });

            if (depth == 8)
            {
                return;
            }

            foreach (var child in el.FindAllChildren())
            {
                Walk(child, origin, depth + 1, visited, nodes);
            }
        }
        catch
        {
            // 单节点失败不影响其余指纹
        }
    }

    private static Rect SafeRect(AutomationElement el)
    {
        try
        {
            var r = el.BoundingRectangle;
            return new Rect(r.X, r.Y, r.Width, r.Height);
        }
        catch
        {
            return new Rect(0, 0, 0, 0);
        }
    }

    private static int Quantize(double value) => (int)(Math.Round(value / 8.0) * 8);

    private static string Truncate(string s, int len)
        => string.IsNullOrEmpty(s) || s.Length <= len ? s : s[..len];

    private readonly record struct Rect(double X, double Y, double W, double H);

    private sealed class SnapshotFile
    {
        [JsonProperty("capturedAt")] public string CapturedAt { get; set; } = "";
        [JsonProperty("windowName")] public string WindowName { get; set; } = "";
        [JsonProperty("nodes")] public List<SnapshotNode> Nodes { get; set; } = new();
    }

    private sealed class SnapshotNode
    {
        [JsonProperty("depth")] public int Depth { get; set; }
        [JsonProperty("controlType")] public string ControlType { get; set; } = "";
        [JsonProperty("automationId")] public string AutomationId { get; set; } = "";
        [JsonProperty("name")] public string Name { get; set; } = "";
        [JsonProperty("className")] public string ClassName { get; set; } = "";
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("w")] public int W { get; set; }
        [JsonProperty("h")] public int H { get; set; }
    }
}
