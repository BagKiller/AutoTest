// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 期望原语（C.1）：把「某个弹窗应该出现 / 应该关闭」表达为可执行期望，
/// 命中/落空自动落结构化断言（kind="ui-expect"）+ 取证日志。
/// 原语只记录与返回、不抛异常——硬失败由调用方 Assert，判定语义保持诚实。
/// </summary>
public sealed class UiExpect
{
    private readonly DialogHandler _dialogs;
    private readonly AssertionRecorder _assertions;
    private readonly EvidenceLog _log;

    public UiExpect(DialogHandler dialogs, AssertionRecorder assertions, EvidenceLog log)
    {
        _dialogs = dialogs;
        _assertions = assertions;
        _log = log;
    }

    /// <summary>
    /// 期望弹窗出现：timeout 内签名命中 → PASS 断言并返回该窗口；超时 → FAIL 断言并返回 null。
    /// 等待期间非目标弹窗照常托管（WaitForDialog 语义），目标窗先于旁路窗做签名匹配。
    /// </summary>
    public AutomationElement? WindowAppears(string id, DialogExpectation expected,
        TimeSpan timeout, Window main)
    {
        var window = _dialogs.WaitForDialog(main, expected, timeout);
        if (window is not null)
        {
            _assertions.Pass(id, "ui-expect", $"弹窗出现: {expected.Describe}",
                expected.Describe, "命中");
        }
        else
        {
            _assertions.Fail(id, "ui-expect", $"弹窗出现: {expected.Describe}",
                expected.Describe, $"{timeout.TotalSeconds:F0}s 内未出现");
        }

        return window;
    }

    /// <summary>
    /// 负期望：timeout 内签名不得出现 → PASS；一旦命中 → FAIL 并返回该窗口。
    /// 「不应弹出」必须走这条，禁止复用 <see cref="WindowAppears"/>（超时会被记成 FAIL）。
    /// </summary>
    public AutomationElement? WindowDoesNotAppear(string id, DialogExpectation unexpected,
        TimeSpan timeout, Window main)
    {
        var window = _dialogs.WaitForDialog(main, unexpected, timeout);
        if (window is null)
        {
            _assertions.Pass(id, "ui-expect", $"弹窗不出现: {unexpected.Describe}",
                "无此窗", $"{timeout.TotalSeconds:F0}s 内未出现");
        }
        else
        {
            _assertions.Fail(id, "ui-expect", $"弹窗不出现: {unexpected.Describe}",
                "无此窗", $"弹出: '{UiProps.Name(window)}'");
        }

        return window;
    }

    /// <summary>
    /// 期望弹窗关闭：timeout 内主窗口子窗中不再有签名命中者 → PASS；仍在 → FAIL。
    /// 观察期间目标窗本身绝不被托管（替它点掉 = 伪通过），其他新弹窗照常托管避免卡流程。
    /// </summary>
    public bool WindowClosed(string id, DialogExpectation signature, TimeSpan timeout, Window main)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (!IsPresent(main, signature))
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 弹窗已关闭（{signature.Describe}）✔");
                _assertions.Pass(id, "ui-expect", $"弹窗关闭: {signature.Describe}",
                    "窗口消失", $"{sw.Elapsed.TotalSeconds:F0}s 内消失");
                return true;
            }

            _dialogs.HandleAllExcept(main, signature);
            Thread.Sleep(1000);
        }

        _log.Step($"!! {timeout.TotalSeconds:F0}s 内弹窗未关闭（{signature.Describe}）");
        _assertions.Fail(id, "ui-expect", $"弹窗关闭: {signature.Describe}",
            "窗口消失", $"{timeout.TotalSeconds:F0}s 后仍在");
        return false;
    }

    /// <summary>主窗口子窗中是否存在签名命中的弹窗。瞬时树异常按「仍在」处理（保守，防伪通过）。</summary>
    private bool IsPresent(Window main, DialogExpectation signature)
    {
        try
        {
            foreach (var w in main.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
            {
                var title = UiProps.TextOf(w, "TitleTextBlock");
                var message = UiProps.TextOf(w, "MessageTextBlock");
                if (signature.Match(title, message))
                {
                    return true;
                }
            }
        }
        catch
        {
            return true;
        }

        return false;
    }
}

/// <summary>常用弹窗签名工厂：contains 语义（大小写敏感；中英文按被测程序实际语言写）。</summary>
public static class DialogExpectations
{
    public static DialogExpectation TitleContains(string fragment)
        => new($"title~'{fragment}'", (t, _) => (t ?? "").Contains(fragment));

    public static DialogExpectation MessageContains(string fragment)
        => new($"message~'{fragment}'", (_, m) => (m ?? "").Contains(fragment));

    public static DialogExpectation TitleOrMessageContains(string fragment)
        => new($"title|message~'{fragment}'",
            (t, m) => (t ?? "").Contains(fragment) || (m ?? "").Contains(fragment));

    public static DialogExpectation Any(params DialogExpectation[] parts)
        => new(string.Join(" | ", parts.Select(p => p.Describe)),
            (t, m) => parts.Any(p => p.Match(t, m)));
}
