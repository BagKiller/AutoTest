// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Vega.AutoTest.Harness;


/// <summary>
/// #3478 页面对象：检测区域页（InspectionButton → Explorer 树 → 配方文档 → DF 控件）。
/// 操作（OpenInspection / FindRecipeNode）+ 观察（IsDocumentLoaded / ReadRegionValues）
/// + oracle 断言（CrossCheckWithRecipe）。
///
/// 源码依据：
/// - ShowInspection 只打开 Explorer 树（Vega/MainWindowViewModel.cs:406）
/// - 打开配方 = 树节点 MouseDoubleClick → OpenCommand（Vega.UI.NavigationTree/NavigationTreeView.xaml:34）
/// - 节点 AutomationId = Header（同文件 :21）；工作区节点名"任务工作区"/"Job Work Space"
/// - DF 控件 AutomationId 见 DFInspectionRegionView.xaml
/// </summary>
public sealed class InspectionRegionPage
{
    /// <summary>#3478 观察点的核心控件（DFInspectionRegionView.xaml）。</summary>
    public static readonly string[] CoreControlIds =
    {
        "PrimaryInspectionRegion",
        "PriInspRegionStartRadius",
        "PriInspRegionEndRadius",
        "SecInspRegionWidth",
    };

    /// <summary>
    /// 有配方对应物、可做 oracle 交叉验证的观察点。
    /// PrimaryInspectionRegion 是 GroupBox 容器（xaml:55），配方侧无此字段，只做 ui-value 存在性断言；
    /// EdgeExclusion 配方侧存在（DFInspectionRegionContent）但非联动问题观察点，补进来保持 4 字段 oracle 覆盖。
    /// </summary>
    public static readonly string[] OracleCheckableIds =
    {
        "PriInspRegionStartRadius",
        "PriInspRegionEndRadius",
        "SecInspRegionWidth",
        "EdgeExclusion",
    };

    private readonly Window _main;
    private readonly DialogHandler _dialogs;
    private readonly EvidenceLog _log;
    private readonly AssertionRecorder _assertions;

    public InspectionRegionPage(Window main, DialogHandler dialogs, EvidenceLog log,
        AssertionRecorder assertions)
    {
        _main = main;
        _dialogs = dialogs;
        _log = log;
        _assertions = assertions;
    }

    /// <summary>
    /// 等 InspectionButton 启用再点（它由 PreloadInspectionAsync 完成后置 true，
    /// 晚于"菜单可用"，过早点击命令不执行）。
    /// 点击优先 InvokePattern（不依赖前台焦点/坐标；物理 Click 在无人值守环境不可靠）；
    /// 每次点击前清模态弹窗，点击后树 10s 内未出现则重试，最多 3 次；仍失败 dump 主窗口留证。
    /// </summary>
    public void OpenInspection()
    {
        var button = Waits.ForEnabled(_main, "InspectionButton", TimeSpan.FromSeconds(120), _log);
        Assert.NotNull(button);

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            _dialogs.HandleAll(_main); // 清掉可能存在的模态弹窗（如 XY Auto Calibration）
            InvokeOrClick(button!);
            _log.Step($"  已触发 id=InspectionButton（第 {attempt} 次）");

            if (Waits.Until(
                    () => _main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeExplorerView")) is not null,
                    TimeSpan.FromSeconds(10), "RecipeExplorerView", _log))
            {
                return;
            }

            _log.Step("  树未出现，重试");
        }

        _log.Step("!! 3 次触发后 RecipeExplorerView 仍未出现，dump 主窗口:");
        UiTreeDumper.Dump(_main, maxDepth: 3, _log);
    }

    private static void InvokeOrClick(AutomationElement el)
    {
        try
        {
            if (el.Patterns.Invoke.IsSupported)
            {
                el.AsButton().Invoke();
                return;
            }
        }
        catch
        {
            // 落到物理点击
        }

        el.Click();
    }

    /// <summary>
    /// 找 Explorer 树中的默认配方节点。
    /// 实测（dump 确认）：树 id=RecipeExplorerView；工作区节点 id="任务工作区"、
    /// 配方子节点 id=配方路径（节点的 AutomationId = Header，Name 恒为类型名）。
    /// </summary>
    public AutomationElement? FindRecipeNode(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            // 树出现前的轮询里持续处理弹窗（XY Auto Calibration 在就绪后数秒才弹出）
            _dialogs.HandleAll(_main);

            var tree = _main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeExplorerView"));
            if (tree is null)
            {
                Thread.Sleep(2000);
                continue;
            }

            _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] RecipeExplorerView 树已出现");

            // 工作区节点：id="任务工作区"（en 环境为 "Job Work Space"）
            var wsNode = tree.FindFirstDescendant(cf => cf.ByAutomationId("任务工作区"))
                        ?? tree.FindFirstDescendant(cf => cf.ByAutomationId("Job Work Space"));
            if (wsNode is null)
            {
                _log.Step("  工作区节点未找到，dump 树:");
                UiTreeDumper.Dump(tree, maxDepth: 4, _log);
                Thread.Sleep(2000);
                continue;
            }

            try
            {
                wsNode.Patterns.ExpandCollapse.Pattern.Expand();
            }
            catch
            {
                // 可能已展开
            }

            Thread.Sleep(1500);

            var children = wsNode.FindAllChildren();
            foreach (var child in children)
            {
                var id = UiProps.Id(child);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                _log.Step($"  配方子节点: id='{id}' name='{UiProps.Name(child)}'");
                return child;
            }

            _log.Step("  工作区节点下未读到子节点，dump:");
            UiTreeDumper.Dump(wsNode, maxDepth: 3, _log);
            Thread.Sleep(2000);
        }

        _log.Step("!! 未找到配方节点");
        return null;
    }

    /// <summary>文档是否已加载：RecipeView_ListView（RecipeView.xaml:195）出现。</summary>
    public bool IsDocumentLoaded
        => _main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeView_ListView")) is not null;

    /// <summary>等文档区加载：RecipeView_ListView 出现且 DF 控件可见；超时 Assert.Fail。</summary>
    public void WaitForDocument(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        var listViewFound = -1d;
        var dfFound = -1d;

        while (sw.Elapsed < timeout)
        {
            _dialogs.HandleAll(_main);

            if (listViewFound < 0)
            {
                var lv = _main.FindFirstDescendant(cf => cf.ByAutomationId("RecipeView_ListView"));
                if (lv is not null)
                {
                    listViewFound = sw.Elapsed.TotalSeconds;
                    _log.Step($"[{listViewFound:F0}s] RecipeView_ListView 出现（文档已加载）");
                }
            }

            if (listViewFound >= 0)
            {
                var df = _main.FindFirstDescendant(cf => cf.ByAutomationId(CoreControlIds[0]));
                if (df is not null)
                {
                    dfFound = sw.Elapsed.TotalSeconds;
                    _log.Step($"[{dfFound:F0}s] DF 控件 {CoreControlIds[0]} 出现 ✔");
                    return;
                }
            }

            Thread.Sleep(2000);
        }

        _log.Step($"!! {timeout.TotalSeconds:F0}s 内文档未加载完成（ListView={listViewFound >= 0}，DF控件={dfFound >= 0}）");
        Assert.Fail("配方文档未加载完成");
    }

    /// <summary>读取 #3478 观察点控件的当前值。返回成功读到的 id→值；每个命中控件额外记一条 kind="ui-value" 断言。</summary>
    public Dictionary<string, string> ReadRegionValues()
    {
        _log.Step("=== #3478 联动参数当前值 ===");
        var result = new Dictionary<string, string>();

        var targets = CoreControlIds.Concat(OracleCheckableIds).Distinct().ToArray();
        foreach (var target in targets)
        {
            var found = _main.FindFirstDescendant(cf => cf.ByAutomationId(target));
            if (found is null)
            {
                _log.Step($"    {target}: 未找到");
                _assertions.Skip(target, "ui-value", $"控件 {target} 在主窗口树中未找到", "control not found");
                continue;
            }

            var value = UiProps.TryReadValue(found);
            result[target] = value;
            _log.Step($"    {target} = '{value}' (type={found.ControlType})");
            _assertions.Pass(target, "ui-value", $"控件 {target} 可读值", "(present)", value);
        }

        return result;
    }

    /// <summary>
    /// oracle 交叉验证（L1+L2 双通道）：GET query/opened-recipe 拿配方内部值，与 UI 读数对比。
    /// 单位链路源码事实：
    ///   配方内部 mm（如 147.0）→ VM LoadData ×1000（µm: 147000）→ XAML MicronToMMConverter
    ///   Factor=0.001 → 控件显示 mm（147.00）。故 UI 显示值(mm) ≈ 配方内部值(mm)，
    ///   容差 ±0.011mm 吸收 0.01mm 显示舍入（StringFormat 0.00 + Step=0.01）。
    /// 证据（UI 值/内部值/偏差）写取证日志。
    /// API 不可用（服务未起/无打开配方）只告警不断言——那是 L1 通道部署问题，不是 UI 回归失败。
    /// </summary>
    public void CrossCheckWithRecipe(Dictionary<string, string> uiValues)
    {
        _log.Step("=== oracle 交叉验证（UI 显示 mm ≈ 配方内部 mm，±0.011 容差）===");
        JObject? region;

        // 本测试自己拉起的新 Vega 实例，API 要 DelayStartSeconds(60)s 后才监听；
        // UIA 流程 ~30s 就到文档加载完，这里主动等 ping 就绪（最多 90s），不然 oracle 永远跳过
        if (!L1Client.WaitForReady(90))
        {
            _log.Step("!! L1 API 90s 内未就绪，跳过交叉验证");
            foreach (var target in OracleCheckableIds)
            {
                _assertions.Skip($"{target}.crosscheck", "oracle-crosscheck",
                    "UI 显示 mm ≈ 配方内部 mm", "L1 API not ready");
            }

            return;
        }

        try
        {
            var json = L1Client.GetOpenedRecipe();
            var recipe = JObject.Parse(json);

            // Recipe.RecipeElements[1] = CollectionsContent → Collections[0] → DarkField.InspectionRegionContent
            region = recipe["RecipeElements"]?[1]?["Collections"]?.First()?["DarkField"]?["InspectionRegionContent"] as JObject;
            if (region is null)
            {
                _log.Step("!! 配方 JSON 中未找到 Collections[0].DarkField.InspectionRegionContent，跳过交叉验证");
                foreach (var target in OracleCheckableIds)
                {
                    _assertions.Skip($"{target}.crosscheck", "oracle-crosscheck",
                        "UI 显示 mm ≈ 配方内部 mm", "recipe JSON path missing");
                }

                return;
            }
        }
        catch (Exception ex)
        {
            _log.Step($"!! L1 API 拉取配方失败（{ex.GetBaseException().Message}），跳过交叉验证");
            foreach (var target in OracleCheckableIds)
            {
                _assertions.Skip($"{target}.crosscheck", "oracle-crosscheck",
                    "UI 显示 mm ≈ 配方内部 mm", $"L1 API error: {ex.GetBaseException().Message}");
            }

            return;
        }

        // UI 控件 id ↔ 配方 JSON 字段名一一对应；两侧同为 mm
        var checkedCount = 0;
        foreach (var target in OracleCheckableIds)
        {
            var innerToken = region[target];
            string? uiText = null;
            var hasUi = uiValues.TryGetValue(target, out uiText);
            if (innerToken is null || !hasUi || string.IsNullOrEmpty(uiText) ||
                !double.TryParse(uiText, out var uiValue) || !double.TryParse(innerToken.ToString(), out var innerMm))
            {
                _log.Step($"    {target}: UI='{uiText}' 内部={innerToken} → 有一侧缺失/非数值，跳过");
                _assertions.Skip($"{target}.crosscheck", "oracle-crosscheck",
                    "UI 显示 mm ≈ 配方内部 mm", $"one side missing/non-numeric: UI='{uiText}' inner={innerToken}");
                continue;
            }

            var deviation = Math.Abs(uiValue - innerMm);
            var pass = deviation <= 0.011; // 0.01mm 显示舍入
            checkedCount++;
            _log.Step($"    {target}: UI={uiValue}mm | 内部={innerMm}mm | 偏差={deviation:F4}mm | {(pass ? "PASS" : "FAIL")}");
            if (pass)
            {
                _assertions.Pass($"{target}.crosscheck", "oracle-crosscheck",
                    "UI 显示 mm ≈ 配方内部 mm", innerMm.ToString(), uiValue.ToString(),
                    deviation, 0.011);
            }
            else
            {
                _assertions.Fail($"{target}.crosscheck", "oracle-crosscheck",
                    "UI 显示 mm ≈ 配方内部 mm", innerMm.ToString(), uiValue.ToString(),
                    deviation, 0.011);
            }

            Assert.True(pass, $"{target}: UI({uiValue}mm) 与配方内部值({innerMm}mm) 偏差 {deviation:F4}mm 超出 ±0.011mm 容差");
        }

        Assert.True(checkedCount > 0, "交叉验证一个字段都没比对上（UI 与配方 JSON 字段名失配？）");
    }
}
