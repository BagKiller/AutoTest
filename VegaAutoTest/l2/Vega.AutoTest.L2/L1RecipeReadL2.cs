// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Text.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// C.3 首条 L1 API 主通道用例（只读冒烟）。
/// 设计文档 P1 的完整闭环是「建 Recipe → 设参 → 扫描 → 取结果」，但 6.2 写命令
/// （recipe/create、scan/start 等）尚未实现；已注册的 recipe/open 又受
/// AllowHardwareCommands 保护（入库配置为 false）。本用例先把<strong>已存在的
/// GET 主通道</strong>收成正式登记用例，断言 18901 四条只读端点可消费。
/// 写命令扩表后另开 C.3b，不要在这里伪造扫描。
/// </summary>
public sealed class L1RecipeReadL2 : L2TestBase
{
    public L1RecipeReadL2() : base("_l1_recipe_read.txt") { }

    [Fact]
    public void RecipeReadPath()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        var ready = L1Client.WaitForReady(90);
        if (!ready)
        {
            Assertions.Fail("l1.ping", "l1-query", "18901 ping 就绪", "ok", "timeout");
            Assert.Fail("L1 API 90s 内未就绪（插件未注册或 DelayStartSeconds 未到）");
            return;
        }

        Assertions.Pass("l1.ping", "l1-query", "18901 ping 就绪", "ok", "ok");

        RecordToolState();
        var firstId = RecordRecipeList();
        RecordRecipeBody(firstId);
    }

    private void RecordToolState()
    {
        var (status, body) = L1Client.Get("query/tool-state");
        if (status != 200)
        {
            Assertions.Fail("l1.tool-state", "l1-query", "GET query/tool-state", "200", status.ToString());
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var state = doc.RootElement.TryGetProperty("toolState", out var ts) ? ts.GetString() : null;
            var resolved = doc.RootElement.TryGetProperty("toolStateResolved", out var r) && r.GetBoolean();
            Log.Step($"tool-state: {state} resolved={resolved}");
            if (string.IsNullOrWhiteSpace(state) && !resolved)
            {
                Assertions.Fail("l1.tool-state", "l1-query", "tool-state 可解析", "有 toolState 或 resolved", body);
                return;
            }

            Assertions.Pass("l1.tool-state", "l1-query", "GET query/tool-state", "200+toolState", $"{status}/{state}");
        }
        catch (Exception ex)
        {
            Assertions.Fail("l1.tool-state", "l1-query", "tool-state JSON 可解析", "json", ex.Message);
        }
    }

    private long? RecordRecipeList()
    {
        var (status, body) = L1Client.Get("query/recipe-list?folderId=0");
        if (status != 200)
        {
            Assertions.Fail("l1.recipe-list", "l1-query", "GET query/recipe-list", "200", status.ToString());
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var count = doc.RootElement.TryGetProperty("count", out var c) ? c.GetInt32() : -1;
            Log.Step($"recipe-list count={count}");
            if (count < 0)
            {
                Assertions.Fail("l1.recipe-list", "l1-query", "recipe-list 含 count", ">=0", body);
                return null;
            }

            Assertions.Pass("l1.recipe-list", "l1-query", "GET query/recipe-list", "count>=0", count.ToString());
            if (count == 0 || !doc.RootElement.TryGetProperty("recipes", out var recipes)
                || recipes.ValueKind != JsonValueKind.Array || recipes.GetArrayLength() == 0)
            {
                return null;
            }

            var first = recipes[0];
            return first.TryGetProperty("ID", out var id) ? id.GetInt64() : null;
        }
        catch (Exception ex)
        {
            Assertions.Fail("l1.recipe-list", "l1-query", "recipe-list JSON 可解析", "json", ex.Message);
            return null;
        }
    }

    private void RecordRecipeBody(long? idFromList)
    {
        var (openedStatus, openedBody) = L1Client.Get("query/opened-recipe");
        if (openedStatus == 200)
        {
            Assertions.Pass("l1.opened-recipe", "l1-query", "GET query/opened-recipe", "200", "200");
            return;
        }

        Log.Step($"opened-recipe HTTP {openedStatus}，回退 query/recipe?id=");
        if (idFromList is null)
        {
            Assertions.Skip("l1.opened-recipe", "l1-query", "无已打开配方且列表为空，无法回退按 id 读",
                "opened-recipe 404 且 recipe-list 空");
            return;
        }

        var (status, body) = L1Client.Get($"query/recipe?id={idFromList.Value}");
        if (status != 200 || string.IsNullOrWhiteSpace(body))
        {
            Assertions.Fail("l1.recipe-by-id", "l1-query",
                $"GET query/recipe?id={idFromList.Value}", "200", status.ToString());
            return;
        }

        Assertions.Pass("l1.recipe-by-id", "l1-query",
            $"GET query/recipe?id={idFromList.Value}", "200", "200");
    }
}
