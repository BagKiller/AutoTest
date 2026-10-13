// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 观察与取证原语：递归 dump UIA 树（限制深度与数量），用于定位规则调试与未知弹窗存证。
/// </summary>
public static class UiTreeDumper
{
    public static void Dump(AutomationElement root, int maxDepth, EvidenceLog log)
    {
        try
        {
            DumpCore(root, 0, maxDepth, new HashSet<IntPtr>(), log);
        }
        catch (Exception ex)
        {
            log.Step($"  dump failed: {ex.Message}");
        }
    }

    private static void DumpCore(AutomationElement el, int depth, int maxDepth, HashSet<IntPtr> visited, EvidenceLog log)
    {
        if (depth > maxDepth || visited.Count > 400)
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

            var indent = new string(' ', depth * 2);
            log.Step($"{indent}[{el.ControlType}] name='{Truncate(UiProps.Name(el), 40)}' id='{Truncate(UiProps.Id(el), 40)}' class={el.ClassName}");

            if (depth == maxDepth)
            {
                return;
            }

            foreach (var child in el.FindAllChildren())
            {
                DumpCore(child, depth + 1, maxDepth, visited, log);
            }
        }
        catch
        {
            // 单节点失败不影响整体
        }
    }

    private static string Truncate(string s, int len)
        => string.IsNullOrEmpty(s) || s.Length <= len ? s : s[..len] + "…";
}
