// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3547 正式 L2 回归用例：操作菜单「强制中断所有任务」替换原中断/停止四项。
/// 验收标准（工作项标题 + PR 4803 实现口径）：
///   1. OperationMenuItem 下存在 ForceAbortAllJobsMenuItem（强制中断所有任务）；
///   2. 原 AbortCurrentJobMenuItem / AbortAllJobsMenuItem / StopCurrentJobMenuItem /
///      StopAllJobsMenuItem 四项不再出现在菜单里。
/// 纯 UI 结构断言：不点击、零硬件、零数据副作用。
/// XAML 依据（上游 477aa845）：Vega/MainWindow.xaml 操作菜单段。
/// </summary>
public sealed class WorkItem3547L2 : L2TestBase
{
    /// <summary>修复后应存在的菜单项。</summary>
    private const string ForceAbortId = "ForceAbortAllJobsMenuItem";

    /// <summary>修复后应消失的四项。</summary>
    private static readonly string[] RemovedIds =
    {
        "AbortCurrentJobMenuItem",
        "AbortAllJobsMenuItem",
        "StopCurrentJobMenuItem",
        "StopAllJobsMenuItem",
    };

    public WorkItem3547L2() : base("_wi3547_l2.txt") { }

    [Fact]
    public void OperationMenuForceAbortReplacesAbortStop()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        // OperationMenuItem 是 MainWindow 的顶层菜单（非 Window 子窗），树稳定后直接按 id 找。
        // WPF Menu 的子项在不展开时也挂在 Automation 树上（WPF MenuItem 支持 ExpandCollapse，
        // 但子项的 UIA 可见性不依赖视觉展开），这里先直查；找不到再展开一次兜底。
        var operationMenu = Waits.ForElement(MainWindow!, "OperationMenuItem",
            TimeSpan.FromSeconds(30), Log);
        Assert.NotNull(operationMenu);

        TryExpand(operationMenu!);

        // 断言 1：强制中断按钮存在
        var forceAbort = FindMenuItem(operationMenu!, ForceAbortId);
        Log.Step($"强制中断按钮 {ForceAbortId}: {(forceAbort is not null ? $"存在 name='{UiProps.Name(forceAbort)}'" : "未找到")}");
        if (forceAbort is not null)
        {
            Assertions.Pass(ForceAbortId, "ui-value",
                "操作菜单存在强制中断所有任务按钮", ForceAbortId, "存在");
        }
        else
        {
            Assertions.Fail(ForceAbortId, "ui-value",
                "操作菜单存在强制中断所有任务按钮", ForceAbortId, "未找到");
        }

        // 断言 2-5：旧的中断/停止四项已删除
        foreach (var removedId in RemovedIds)
        {
            var still = FindMenuItem(operationMenu!, removedId);
            Log.Step($"旧按钮 {removedId}: {(still is not null ? "仍存在" : "已删除")}");
            if (still is null)
            {
                Assertions.Pass(removedId, "ui-value",
                    "旧中断/停止菜单项已删除", $"{removedId} 不存在", "已删除");
            }
            else
            {
                Assertions.Fail(removedId, "ui-value",
                    "旧中断/停止菜单项已删除", $"{removedId} 不存在", "仍存在");
            }
        }

        // 硬判定：修复后 5 条断言全 PASS（明细已落 AssertionRecorder）。
        // 旧二进制（PR 4803 之前）必然：强制中断不在 + 四个旧按钮全在。
        Assert.True(forceAbort is not null,
            $"{ForceAbortId} 未找到：操作菜单未替换为强制中断（PR 4803 未包含在此二进制中？）");
        var leftovers = new System.Collections.Generic.List<string>();
        foreach (var removedId in RemovedIds)
        {
            if (FindMenuItem(operationMenu!, removedId) is not null)
            {
                leftovers.Add(removedId);
            }
        }

        Assert.True(leftovers.Count == 0,
            $"旧中断/停止菜单项仍存在: {string.Join(", ", leftovers)}");
    }

    /// <summary>在 OperationMenuItem 子树里按 AutomationId 找菜单项（含任意深度）。</summary>
    private static AutomationElement? FindMenuItem(AutomationElement operationMenu, string id)
        => operationMenu.FindFirstDescendant(cf => cf.ByAutomationId(id));

    /// <summary>展开顶层菜单（ExpandCollapse 优先，失败忽略——子树断言不依赖视觉展开）。</summary>
    private void TryExpand(AutomationElement menu)
    {
        try
        {
            if (menu.Patterns.ExpandCollapse.IsSupported)
            {
                menu.Patterns.ExpandCollapse.Pattern.Expand();
                Log.Step("OperationMenuItem 已展开（ExpandCollapse）");
                Thread.Sleep(500);
                return;
            }
        }
        catch
        {
            // 展开失败不致命
        }

        // 子项树断言不依赖展开，找不到时再 dump 存证
        Log.Step("OperationMenuItem 不支持 ExpandCollapse，跳过展开");
        UiTreeDumper.Dump(menu, maxDepth: 2, Log);
    }
}
