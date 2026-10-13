// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Vega.AutoTest.Harness;

/// <summary>
/// L1 扫描主链原语：建 Recipe → 上片 → readiness 确认 → scan/start → 轮询 → 结果获取。
/// 全部走 AgentControlService HTTP（写命令需 AllowHardwareCommands=true）。
/// 断言口径：缺陷检测模型（IsScanSuccessful / EffectiveDefectCount / counters），
/// Vega 无 Total Die / OK / NG 概念。
///
/// 已知时序（2026-09-22 实测三轮）：scan-task 终态时 WaferRun 往往尚未落库
/// （DataReportingService 异步分片 + GridFS 上传 ~3.9 万缺陷可拖 2-4 分钟），
/// 因此结果获取默认走 recent-wafer-runs → wafer-run 详情的兜底链，
/// 插件侧 waferRuns 摘要（后台 6 分钟 watch 回填）作为可选快路径。
/// </summary>
public sealed class ScanFlow
{
    /// <summary>readiness 五服务：定位失败则后续全白搭，优先暴露。</summary>
    private static readonly string[] ExpectedServices =
    {
        "gem", "waferHandler", "recipe", "measurement", "toolState",
    };

    private readonly EvidenceLog _log;

    public ScanFlow(EvidenceLog log)
    {
        _log = log;
    }

    /// <summary>查询 run-readiness，校验五服务全定位成功。失败抛 Xunit 异常。</summary>
    public JObject RequireReadiness()
    {
        var (status, body) = L1Client.Get("query/run-readiness");
        Xunit.Assert.True(status == 200, $"run-readiness HTTP {status}: {body}");
        var d = JObject.Parse(body);
        var services = d["services"] as JObject;
        var missing = ExpectedServices
            .Where(s => services?[s]?.Value<bool>() != true)
            .ToList();
        Xunit.Assert.True(missing.Count == 0,
            $"L1 服务定位失败: {string.Join(",", missing)}（readiness: {body}）");
        _log.Step($"readiness: toolState={d["toolState"]} blockers=[{string.Join(",", d["blockers"] ?? new JArray())}]");
        return d;
    }

    /// <summary>轮询直到 toolState=Idle 且 blockers 只剩 no_wafer_on_stage（或全空）。</summary>
    public bool WaitToolStateIdle(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastBlockers = string.Empty;
        while (sw.Elapsed < timeout)
        {
            var (_, body) = L1Client.Get("query/run-readiness");
            if (body.Length > 2)
            {
                try
                {
                    var d = JObject.Parse(body);
                    var state = d["toolState"]?.Value<string>();
                    var blockers = string.Join(",", d["blockers"] ?? new JArray());
                    if (blockers != lastBlockers)
                    {
                        _log.Step($"  [{sw.Elapsed.TotalSeconds:F0}s] toolState={state} blockers=[{blockers}]");
                        lastBlockers = blockers;
                    }

                    if (state == "Idle" && !blockers.Contains("tool_state_not_idle") &&
                        !blockers.Contains("gem_state_query_failed") &&
                        !blockers.Contains("job_run_state_not_idle"))
                    {
                        _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] toolState=Idle ✔");
                        return true;
                    }
                }
                catch
                {
                    // 半截 json，下轮
                }
            }

            Thread.Sleep(3000);
        }

        return false;
    }

    /// <summary>建 Recipe（等价 UI 新建，立即入库）。返回 recipeId。</summary>
    public long CreateRecipe(string name, bool open = true)
    {
        var r = PostJson("cmd/recipe/create",
            new JObject { ["name"] = name, ["folderId"] = 0, ["open"] = open });
        _log.Step($"recipe/create({name}) => {r.Body}");
        Xunit.Assert.True(r.Ok, $"recipe/create 失败: HTTP {r.Status} {r.Body}");
        var id = ((JObject)JToken.Parse(r.Body))["recipe"]?["ID"]?.Value<long>();
        Xunit.Assert.True(id.HasValue, $"recipe/create 响应缺 recipe.ID: {r.Body}");
        return id!.Value;
    }

    /// <summary>手动上片（仿真不走 EFEM）。alreadyLoaded 视为成功。</summary>
    public void LoadWafer(int slot = 1, int diameter = 300)
    {
        var r = PostJson("cmd/wafer/load",
            new JObject { ["slot"] = slot, ["diameter"] = diameter, ["portNum"] = 0 });
        _log.Step($"wafer/load(slot={slot}) => {r.Body}");
        Xunit.Assert.True(r.Ok, $"wafer/load 失败: HTTP {r.Status} {r.Body}");
    }

    /// <summary>轮询 readiness 直到 canRunCurrentRecipe=true。</summary>
    public bool WaitCanRun(TimeSpan timeout, out string lastBody)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var (_, b) = L1Client.Get("query/run-readiness");
            lastBody = b;
            if (b.Length > 2)
            {
                try
                {
                    if (JObject.Parse(b)["canRunCurrentRecipe"]?.Value<bool>() == true)
                    {
                        return true;
                    }
                }
                catch
                {
                    // 半截 json
                }
            }

            Thread.Sleep(2000);
        }

        lastBody = L1Client.GetJson("query/run-readiness");
        return false;
    }

    /// <summary>启动扫描（异步）。返回 taskId。</summary>
    public string StartScan(long recipeId, int scanNum = 1)
    {
        var r = PostJson("cmd/scan/start",
            new JObject { ["recipeId"] = recipeId, ["scanNum"] = scanNum });
        _log.Step($"scan/start(recipeId={recipeId}, scanNum={scanNum}) => {r.Body}");
        Xunit.Assert.True(r.Ok, $"scan/start 失败: HTTP {r.Status} {r.Body}");
        var taskId = ((JObject)JToken.Parse(r.Body))["taskId"]?.Value<string>();
        Xunit.Assert.True(!string.IsNullOrWhiteSpace(taskId), $"scan/start 响应缺 taskId: {r.Body}");
        return taskId!;
    }

    /// <summary>轮询 scan-task 到终态。onPoll 每次状态变化回调（用例里挂弹窗清理）。</summary>
    public JObject PollToTerminal(string taskId, TimeSpan timeout, Action? onPoll = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lastState = string.Empty;
        while (sw.Elapsed < timeout)
        {
            onPoll?.Invoke();

            var (_, body) = L1Client.Get($"query/scan-task?id={taskId}");
            if (body.Length > 2)
            {
                try
                {
                    var d = JObject.Parse(body);
                    var state = d["state"]?.Value<string>();
                    if (state != lastState)
                    {
                        _log.Step($"  [{sw.Elapsed.TotalSeconds:F0}s] scan-task state={state}");
                        lastState = state;
                    }

                    if (state != "running")
                    {
                        return d;
                    }
                }
                catch
                {
                    // 半截 json
                }
            }

            Thread.Sleep(5000);
        }

        Xunit.Assert.Fail($"扫描任务 {taskId} 在 {timeout.TotalMinutes:F0} 分钟内未到终态");
        return null!; // 不可达
    }

    /// <summary>
    /// 获取本次扫描的 WaferRun 详情。优先任务摘要，落库延迟时走兜底链
    /// （recent-wafer-runs 按 RecipeName 匹配 → query/wafer-run 详情）。
    /// 未找到返回 null（调用方断言）。
    /// </summary>
    public JObject? GetWaferRunResult(JObject terminalTask, TimeSpan fallbackWait)
    {
        // 快路径：插件后台 watch 已回填
        var embedded = terminalTask["waferRuns"]?.First as JObject;
        if (embedded is not null)
        {
            _log.Step($"waferRuns 内嵌摘要命中: id={embedded["waferRunId"]}");
            return embedded;
        }

        // 兜底：轮询 recent-wafer-runs 直到出现匹配项或超时
        var recipeId = terminalTask["RecipeId"]?.Value<long>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < fallbackWait)
        {
            var (_, listBody) = L1Client.Get("query/recent-wafer-runs?limit=5");
            if (listBody.Length > 2)
            {
                try
                {
                    var runs = ((JObject)JToken.Parse(listBody))["runs"] as JArray;
                    var candidate = runs?.FirstOrDefault(r =>
                        r["RecipeName"]?.Value<string>()?.Contains("AUTOTEST_SCAN") == true);
                    if (candidate is not null)
                    {
                        var id = candidate["waferRunId"]?.Value<long>();
                        var (_, detail) = L1Client.Get($"query/wafer-run?id={id}");
                        _log.Step($"兜底命中 waferRunId={id}: {detail}");
                        return detail.Length > 2 ? JObject.Parse(detail) : null;
                    }
                }
                catch
                {
                    // 半截 json
                }
            }

            Thread.Sleep(5000);
        }

        _log.Step($"兜底 {fallbackWait.TotalSeconds:F0}s 内未在 recent-wafer-runs 找到本次 WaferRun");
        return null;
    }

    /// <summary>POST json 写命令；网络失败与 HTTP 错误都结构化返回。</summary>
    private static (bool Ok, int Status, string Body) PostJson(string endpoint, JToken payload)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var content = new StringContent(payload.ToString(Newtonsoft.Json.Formatting.None),
                Encoding.UTF8, "application/json");
            var response = client.PostAsync($"{VegaEnv.AgentBaseUrl}{endpoint}", content)
                .GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return ((int)response.StatusCode is >= 200 and < 300, (int)response.StatusCode, body);
        }
        catch (Exception ex)
        {
            return (false, 0, ex.Message);
        }
    }

    /// <summary>
    /// 插件序列化混用 camelCase（匿名投影小写字段）与 C# 属性名（run.XXX 直传大写字段），
    /// 统一按大小写不敏感取值。
    /// </summary>
    public static JToken? ReadCaseInsensitive(JObject obj, string key)
        => obj.TryGetValue(key, out var v) ? v
         : obj.Properties().FirstOrDefault(p => string.Equals(p.Name, key, StringComparison.OrdinalIgnoreCase))?.Value;
}
