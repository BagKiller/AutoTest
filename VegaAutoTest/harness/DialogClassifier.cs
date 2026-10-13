// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Vega.AutoTest.Harness;

/// <summary>弹窗分类结果：类别 + 需要点击的目标（登录窗口则为目标窗口本身）。</summary>
public enum DialogDecision
{
    /// <summary>登录窗口（Edit≥2 且无 ConfirmButton）。</summary>
    Login,

    /// <summary>确认类提示（含 ConfirmButton）。</summary>
    Confirm,

    /// <summary>PromptNotification 类提示（含 TitleTextBlock，点其中"确认"按钮）。</summary>
    Prompt,

    /// <summary>询问类（确认+取消 / 是+否 按钮对，点否定项）。</summary>
    Question,

    /// <summary>未识别窗口（调用方 dump 存证，不得静默跳过）。</summary>
    Unknown,
}

/// <summary>
/// 观察：弹窗纯分类（只读不点）。判据顺序固定从严到宽：
/// Login → Confirm → Prompt → Question → Unknown。
/// 分类逻辑可独立验证；点击执行在 DialogHandler。
/// </summary>
public static class DialogClassifier
{
    public static DialogDecision Classify(AutomationElement window)
    {
        // 1. 登录窗口：含 2 个以上输入框（优先判定：登录窗口也可能带按钮对）
        var edits = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        var hasConfirm = window.FindFirstDescendant(cf => cf.ByAutomationId("ConfirmButton")) is not null;
        if (edits.Length >= 2 && !hasConfirm)
        {
            return DialogDecision.Login;
        }

        // 2. 确认类提示：含 ConfirmButton
        if (hasConfirm)
        {
            return DialogDecision.Confirm;
        }

        var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        var named = buttons.Select(b => (El: b, Name: UiProps.Name(b)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToList();

        // 3. PromptNotification 类：含 TitleTextBlock 且有"确认"按钮可点。
        //    注意：XY Auto Calibration 类窗口也带 TitleTextBlock 但按钮对是 是/否（无"确认"），
        //    对齐 spike 已验证的 fall-through 语义：这类窗口落入下面的 Question 判据点否定项。
        if (window.FindFirstDescendant(cf => cf.ByAutomationId("TitleTextBlock")) is not null &&
            named.Any(x => x.Name == "确认"))
        {
            return DialogDecision.Prompt;
        }

        // 4. 询问类：确认+取消 或 是+否 按钮对（XY Auto Calibration 等）
        if (named.Any(x => x.Name is "确认" or "是") &&
            named.Any(x => x.Name is "取消" or "否"))
        {
            return DialogDecision.Question;
        }

        return DialogDecision.Unknown;
    }

    /// <summary>Question 类窗口的否定项（取消/否），无则 null。</summary>
    public static AutomationElement? NegativeButton(AutomationElement window)
    {
        var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        return buttons.Select(b => (El: b, Name: UiProps.Name(b)))
            .FirstOrDefault(x => x.Name is "取消" or "否").El;
    }

    /// <summary>Prompt 类窗口的"确认"按钮，无则 null。</summary>
    public static AutomationElement? PromptConfirmButton(AutomationElement window)
    {
        var buttons = window.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
        return buttons.Select(b => (El: b, Name: UiProps.Name(b)))
            .FirstOrDefault(x => x.Name == "确认").El;
    }
}
