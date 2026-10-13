// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 观察原语：安全读 UIA 属性（树变化中随时可能抛异常）与按 id 读文本。
/// </summary>
public static class UiProps
{
    public static string Name(AutomationElement? e)
    {
        try
        {
            return e?.Properties.Name.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string Id(AutomationElement? e)
    {
        try
        {
            return e?.Properties.AutomationId.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>在 root 下按 AutomationId 找元素并返回其 Name。</summary>
    public static string TextOf(AutomationElement root, string automationId)
        => Name(root.FindFirstDescendant(cf => cf.ByAutomationId(automationId)));

    /// <summary>读值：优先 ValuePattern；自定义控件（NumericUpDownView）兜底找内部 Edit 子元素。</summary>
    public static string TryReadValue(AutomationElement el)
    {
        try
        {
            if (el.Patterns.Value.IsSupported)
            {
                return el.Patterns.Value.Pattern.Value;
            }
        }
        catch
        {
            // 落到子元素方案
        }

        try
        {
            var edit = el.FindFirstDescendant(cf => cf.ByControlType(ControlType.Edit));
            if (edit is not null && edit.Patterns.Value.IsSupported)
            {
                return edit.Patterns.Value.Pattern.Value;
            }

            return Name(el);
        }
        catch
        {
            return "<读取失败>";
        }
    }
}
