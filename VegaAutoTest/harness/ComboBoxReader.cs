// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Vega.AutoTest.Harness;

/// <summary>
/// WPF ComboBox 显示值读写（方案 §8.1）。
/// DisplayMemberPath 下 ListItem 的 UIA Name 是 CLR 类型名，真正的显示文本在
/// 子 TextBlock；闭合态 popup 子树未生成，必须先 Expand 再读 IsSelected 项。
/// 下沉到 harness，避免每个用例各写一遍后漂移。
/// </summary>
public static class ComboBoxReader
{
    /// <summary>读当前选中项的显示文本。读不到时回落 ComboBox 自身 Name。</summary>
    public static string ReadSelectedDisplay(AutomationElement combo)
    {
        var expanded = TryExpand(combo, 600);
        try
        {
            foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
            {
                try
                {
                    if (item.Patterns.SelectionItem.Pattern.IsSelected.Value)
                    {
                        return FindChildText(item) ?? UiProps.Name(item);
                    }
                }
                catch
                {
                    // 单项 pattern 异常，跳过
                }
            }

            return UiProps.Name(combo);
        }
        finally
        {
            if (expanded)
            {
                TryCollapse(combo, 300);
            }
        }
    }

    /// <summary>
    /// 按显示文本选中一项（忽略大小写）。返回是否命中。
    /// </summary>
    public static bool SelectByDisplay(AutomationElement combo, string display,
        Action<string>? log = null)
    {
        TryExpand(combo, 800);

        var names = new List<string>();
        foreach (var item in combo.FindAllDescendants(cf => cf.ByControlType(ControlType.ListItem)))
        {
            var text = FindChildText(item) ?? UiProps.Name(item);
            names.Add(text);
            if (string.Equals(text, display, StringComparison.OrdinalIgnoreCase))
            {
                item.Patterns.SelectionItem.Pattern.Select();
                log?.Invoke($"已选择 '{display}'");
                Thread.Sleep(500);
                return true;
            }
        }

        log?.Invoke($"下拉未找到 '{display}'（候选项: {string.Join(", ", names)}）");
        return false;
    }

    public static string? FindChildText(AutomationElement el)
    {
        try
        {
            var text = el.FindFirstDescendant(cf => cf.ByControlType(ControlType.Text));
            return text is null ? null : UiProps.Name(text);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryExpand(AutomationElement combo, int settleMs)
    {
        try
        {
            combo.Patterns.ExpandCollapse.Pattern.Expand();
            Thread.Sleep(settleMs);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCollapse(AutomationElement combo, int settleMs)
    {
        try
        {
            combo.Patterns.ExpandCollapse.Pattern.Collapse();
            Thread.Sleep(settleMs);
        }
        catch
        {
            // 收起失败不致命
        }
    }
}
