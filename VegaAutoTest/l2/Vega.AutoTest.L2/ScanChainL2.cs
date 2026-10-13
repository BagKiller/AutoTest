// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Net.Http;
using System.Text;
using FlaUI.Core.AutomationElements;
using Newtonsoft.Json.Linq;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// C.3 扫描主链正式用例（需求主流程第 10-14 步：创建 Recipe → 设参 → 执行检测 → 等待完成 → 获取结果）。
/// 混合通道：L1 写命令驱动业务（recipe/create → wafer/load → scan/start → 轮询），
/// L2 UIA 负责 Full Initialize 菜单触发与扫描路径上的模态弹窗自动清理。
/// 断言（缺陷模型，无 Total Die/OK/NG）：IsScanSuccessful=true、IsScanDone=true、
/// resultState=Pass、EffectiveDefectCount 合理（>0 且 < 1e6 防全图噪点异常）。
/// </summary>
public sealed class ScanChainL2 : L2TestBase
{
    public ScanChainL2() : base("_scanchain_l2.txt") { }

    [Fact]
    public void CreateLoadScanAndCollectResult()
    {
        // ---- 拉起 UI + 登录就绪（Vega 进程承载 L1 服务，必须先拉起再等 L1）----
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        // ---- 前置：L1 在线（随 Vega DelayStartSeconds=5 上线）+ 硬件命令开关 ----
        Assert.True(L1Client.WaitForReady(90), "L1 AgentControlService 90s 未就绪");
        var scan = new ScanFlow(Log);
        RequireHardwareCommandsEnabled();

        // ---- toolState 到 Idle（Full Initialize 由启动链自动做过；若仍 Busy 则菜单补一次）----
        if (!scan.WaitToolStateIdle(TimeSpan.FromMinutes(2)))
        {
            Log.Step("toolState 2 分钟未 Idle，主动 Invoke InitializeFullMenuItem");
            InvokeFullInitialize();
            Assert.True(scan.WaitToolStateIdle(TimeSpan.FromMinutes(10)),
                "Full Initialize 后 10 分钟 toolState 未达 Idle");
        }

        scan.RequireReadiness();

        // ---- 步骤 10：创建 Recipe ----
        var recipeId = scan.CreateRecipe($"AUTOTEST_SCAN_{DateTime.Now:HHmmss}");

        // ---- 步骤 11：上片（对应手动放片；仿真不走 EFEM）----
        scan.LoadWafer(slot: 1, diameter: 300);

        Assert.True(scan.WaitCanRun(TimeSpan.FromMinutes(2), out var readiness),
            $"上片后 canRunCurrentRecipe 仍 false: {readiness}");
        Log.Step("canRunCurrentRecipe=true ✔");

        // ---- 步骤 12-13：执行检测 + 等待完成（弹窗由 DialogHandler 守护清理）----
        var taskId = scan.StartScan(recipeId, scanNum: 1);
        var terminal = scan.PollToTerminal(taskId, TimeSpan.FromMinutes(10),
            onPoll: () => Dialogs.HandleAll(MainWindow!));

        var state = terminal["state"]?.Value<string>();
        Assert.True(state == "completed",
            $"扫描终态 {state}（预期 completed），error={terminal["error"]}，详情: {terminal}");

        // ---- 步骤 14：获取结果（落库延迟兜底等待 5 分钟）----
        var run = scan.GetWaferRunResult(terminal, TimeSpan.FromMinutes(5));
        Assert.True(run is not null, "任务终态无 waferRuns 且兜底链 5 分钟未找到本次 WaferRun");

        var success = ScanFlow.ReadCaseInsensitive(run!, "IsScanSuccessful")?.Value<bool>();
        var scanDone = ScanFlow.ReadCaseInsensitive(run!, "IsScanDone")?.Value<bool>();
        var resultState = ScanFlow.ReadCaseInsensitive(run!, "resultState")?.Value<string>();
        var defectCount = ScanFlow.ReadCaseInsensitive(run!, "EffectiveDefectCount")?.Value<int?>();

        Log.Step($"结果: success={success} scanDone={scanDone} state={resultState} defects={defectCount}");

        Assertions.Pass("ScanChain.IsScanSuccessful", "oracle-crosscheck",
            "扫描完成且成功", "true", success?.ToString());
        Assertions.Pass("ScanChain.ResultState", "oracle-crosscheck",
            "结果判定状态", "Pass", resultState);
        Assertions.Pass("ScanChain.EffectiveDefectCount", "oracle-crosscheck",
            "有效缺陷计数合理（仿真 BSS 源图噪声量级）", "1..1000000", defectCount?.ToString());

        Assert.True(success == true, $"IsScanSuccessful={success}，详情: {run}");
        Assert.True(scanDone == true, $"IsScanDone={scanDone}，详情: {run}");
        Assert.True(resultState == "Pass", $"resultState={resultState}，详情: {run}");
        Assert.True(defectCount is > 0 and < 1_000_000,
            $"EffectiveDefectCount={defectCount} 不在合理量级，详情: {run}");
    }

    /// <summary>读 L1 readiness；开关未开则给出一键修复指引并判败（不静默跳过）。</summary>
    private void RequireHardwareCommandsEnabled()
    {
        var (_, body) = L1Client.Get("query/run-readiness");
        var enabled = body.Length > 2 &&
                      JObject.Parse(body)["allowHardwareCommands"]?.Value<bool>() == true;
        if (!enabled)
        {
            Log.Step("!! AllowHardwareCommands=false：写命令将 403。请置 Resources/Configs/" +
                     "AgentControlService.json 的 AllowHardwareCommands=true 后重启 Vega");
        }

        Assert.True(enabled, "AllowHardwareCommands=false，扫描主链写命令不可用");
    }

    /// <summary>OperationMenuItem 展开 → Invoke InitializeFullMenuItem（wi3547 同款展开模式）。</summary>
    private void InvokeFullInitialize()
    {
        var operationMenu = Waits.ForElement(MainWindow!, "OperationMenuItem",
            TimeSpan.FromSeconds(30), Log);
        Assert.NotNull(operationMenu);
        try
        {
            if (operationMenu!.Patterns.ExpandCollapse.IsSupported)
            {
                operationMenu.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(500);
            }
        }
        catch
        {
            // 展开失败不致命，直查兜底
        }

        var item = operationMenu!.FindFirstDescendant(cf => cf.ByAutomationId("InitializeFullMenuItem"));
        Assert.NotNull(item);
        Log.Step("Invoke InitializeFullMenuItem");
        item!.AsMenuItem().Invoke();
    }
}
