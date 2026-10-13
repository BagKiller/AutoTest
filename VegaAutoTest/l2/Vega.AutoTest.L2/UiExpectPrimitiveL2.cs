// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// C.1 uia.expect 原语自检用例（不关联工作项，不进 registry）：
/// 验证 WindowAppears / WindowClosed 的判定语义与断言落盘。
/// 走真实启动（原语操作的是真 UIA 树），但不依赖任何业务弹窗时机——
/// 期望目标用「永不出现的签名」，负路径稳定可回归。
/// </summary>
public sealed class UiExpectPrimitiveL2 : L2TestBase
{
    public UiExpectPrimitiveL2() : base("_uiexpect_l2.txt") { }

    /// <summary>
    /// 负路径：10s 内不可能出现的签名 → WindowAppears 必须返回 null 且落 FAIL 断言；
    /// WindowClosed 对「不存在」的窗立即 PASS（不在场 = 已关闭）。
    /// 两者合计 2 条 kind="ui-expect" 断言（1 FAIL + 1 PASS）落 assert-*.json。
    /// </summary>
    [Fact]
    public void ExpectPrimitives_NegativePath()
    {
        LaunchAndPrepare();

        var expect = new UiExpect(Dialogs, Assertions, Log);
        var never = DialogExpectations.TitleContains("__ NEVER_APPEARS __");

        // 1. WindowAppears：必超时 → null + FAIL 断言
        var found = expect.WindowAppears("expect.appears.negative", never,
            TimeSpan.FromSeconds(10), MainWindow!);
        Assert.Null(found);

        // 2. WindowClosed：目标窗从未存在 → 立即 PASS
        var closed = expect.WindowClosed("expect.closed.negative", never,
            TimeSpan.FromSeconds(5), MainWindow!);
        Assert.True(closed);

        // 3. 断言落盘内容自检：1 FAIL（appears）+ 1 PASS（closed）
        Assert.Equal(2, Assertions.Count);

        Log.Step("=== UiExpect 负路径自检完成：appears FAIL / closed PASS 均按预期 ===");
    }
}
