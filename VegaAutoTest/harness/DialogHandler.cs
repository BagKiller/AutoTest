// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 期望弹窗签名：Describe 用于证据日志，Match 在分类之前做签名匹配
/// （Confirm/Prompt 两形态通吃——只看 title/message 文本，不看内部结构）。
/// </summary>
public sealed record DialogExpectation(string Describe, Func<string, string, bool> Match);

/// <summary>
/// 操作：按 DialogClassifier 分类结果执行（点击/登录）。
/// Unknown 窗口 dump 存证、不静默跳过。LoggedInSeen 供 LoginFlow 就绪判据。
/// </summary>
public sealed class DialogHandler
{
    public bool LoggedInSeen { get; private set; }
    public string? LastHandlingError { get; private set; }
    private bool _loginAttempted;

    private EvidenceLog Log { get; }

    public DialogHandler(EvidenceLog log) => Log = log;

    /// <summary>处理主窗口的全部 Window 子窗口（分类 → 执行）。树变化中的瞬时异常忽略。</summary>
    public void HandleAll(Window main)
    {
        try
        {
            var windows = main.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                HandleOne(w);
            }
        }
        catch (UiPrerequisiteException) { throw; }
        catch (Exception error)
        {
            LastHandlingError = error.GetType().Name;
        }
    }

    /// <summary>
    /// 处理主窗口子窗，但跳过与 <paramref name="skip"/> 签名匹配的窗口——
    /// 供 WindowClosed 等「观察期不能动目标窗」的场景（替它点掉 = 伪通过）。
    /// </summary>
    public void HandleAllExcept(Window main, DialogExpectation skip)
    {
        try
        {
            var windows = main.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            foreach (var w in windows)
            {
                var title = UiProps.TextOf(w, "TitleTextBlock");
                var message = UiProps.TextOf(w, "MessageTextBlock");
                if (skip.Match(title, message))
                {
                    continue;
                }

                HandleOne(w);
            }
        }
        catch
        {
            // 树变化中的瞬时异常忽略
        }
    }

    /// <summary>
    /// 轮询主窗口子窗，等待与 <paramref name="expected"/> 签名匹配的弹窗。
    /// 每个窗口先做签名匹配（读 TitleTextBlock/MessageTextBlock）——命中即 dump 全文到证据并返回该窗口；
    /// 未命中的窗口交由 HandleOne 照旧处理（Login/Confirm/Prompt/Question/Unknown）。
    /// 超时返回 null（调用方决定断言）。
    /// </summary>
    public AutomationElement? WaitForDialog(Window main, DialogExpectation expected, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            try
            {
                var windows = main.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
                foreach (var w in windows)
                {
                    var title = UiProps.TextOf(w, "TitleTextBlock");
                    var message = UiProps.TextOf(w, "MessageTextBlock");
                    if (expected.Match(title, message))
                    {
                        Log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 命中期望弹窗（{expected.Describe}）: title='{title}' message='{message}'");
                        Log.Step("  弹窗全文 dump:");
                        UiTreeDumper.Dump(w, maxDepth: 5, Log);
                        return w;
                    }

                    HandleOne(w);
                }
            }
            catch
            {
                // 树变化中的瞬时异常忽略
            }

            Thread.Sleep(1000);
        }

        Log.Step($"!! {timeout.TotalSeconds:F0}s 内未出现期望弹窗（{expected.Describe}）");
        return null;
    }

    private void HandleOne(AutomationElement w)
    {
        switch (DialogClassifier.Classify(w))
        {
            case DialogDecision.Login:
                // One submission per launched session. An unchanged/rejected
                // login dialog must not repeatedly submit credentials.
                if (_loginAttempted) break;
                _loginAttempted = true;
                Log.Step($"发现登录窗口: '{UiProps.Name(w)}'，自动登录 {VegaEnv.UserName}/***");
                TryLogin(w);
                LoggedInSeen = true;
                break;

            case DialogDecision.Confirm:
                Log.Step($"发现弹窗: title='{UiProps.TextOf(w, "TitleTextBlock")}' message='{UiProps.TextOf(w, "MessageTextBlock")}'");
                w.FindFirstDescendant(cf => cf.ByAutomationId("ConfirmButton"))!.AsButton().Invoke();
                Log.Step("  已点击 确定");
                Thread.Sleep(1500);
                break;

            case DialogDecision.Prompt:
                var ok = DialogClassifier.PromptConfirmButton(w);
                if (ok is not null)
                {
                    Log.Step($"发现提示弹窗: '{UiProps.TextOf(w, "TitleTextBlock")}' - '{UiProps.TextOf(w, "MessageTextBlock")}'，点击确认");
                    ok.AsButton().Invoke();
                    Log.Step("  已点击 确认");
                    Thread.Sleep(1500);
                }

                break;

            case DialogDecision.Question:
                var no = DialogClassifier.NegativeButton(w)!;
                Log.Step($"发现询问弹窗: '{UiProps.Name(w)}'，点击 '{UiProps.Name(no)}'");
                no.AsButton().Invoke();
                Log.Step($"  已点击 {UiProps.Name(no)}");
                Thread.Sleep(1500);
                break;

            case DialogDecision.Unknown:
                // 未知窗口：dump 存证，不静默跳过
                Log.Step($"发现未识别窗口: name='{UiProps.Name(w)}' class={w.ClassName}，dump:");
                UiTreeDumper.Dump(w, maxDepth: 3, Log);
                break;
        }
    }

    /// <summary>在登录窗口填入凭据并点确认（VegaLoginWindowOK 优先，末位按钮兜底）。</summary>
    private void TryLogin(AutomationElement loginWindow)
    {
        var edits = loginWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Edit));
        if (edits.Length < 2)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "登录窗口缺少账号或密码控件。");
        var password = AuthenticatorPassword.Resolve(Log);
        edits[0].Patterns.Value.Pattern.SetValue(VegaEnv.UserName);
        edits[1].Patterns.Value.Pattern.SetValue(password);
        Log.Step("  凭据已填");

        var ok = loginWindow.FindFirstDescendant(cf => cf.ByAutomationId("VegaLoginWindowOK"))?.AsButton();
        if (ok is null)
        {
            var buttons = loginWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.Button));
            ok = buttons.Length > 0 ? buttons[^1].AsButton() : null;
        }

        if (ok is null)
            throw new UiPrerequisiteException("LOGIN_NOT_READY", "登录窗口缺少提交控件。");
        ok!.Invoke();
        Log.Step("  登录已提交");
    }
}
