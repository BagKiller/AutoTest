// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Newtonsoft.Json;

namespace Vega.AutoTest.Runner;

/// <summary>
/// P3.5 契约字段自检（外挂化集成技术方案 §7 阶段表）：Runner 落盘前校验
/// result.json 关键字段是否在说它声称要说的事。契约偏离（仪表读错）比用例
/// 写错更危险——不会报错，只会让结论被误读。
/// <para>
/// 返回违例列表而不抛异常：调用方（<see cref="Program"/>.Finish）据此把 verdict
/// 改写为 ERROR/CONTRACT_VIOLATION 并**照常落盘**——坏仪表的盘比没有盘更可诊断，
/// 且 L0 靠 result.json 取结构化原因（方案 §9 决策 2：宿主崩溃也要写出 result.json）。
/// </para>
/// </summary>
public static class ResultContractCheck
{
    /// <summary>返回空列表 = 契约自检通过。</summary>
    public static List<string> Validate(RunResult result)
    {
        var violations = new List<string>();

        // §2.1：run 级墙钟耗时不得留 0（含早退路径，Finish 统一赋值）
        if (result.DurationMs <= 0)
        {
            violations.Add($"durationMs={result.DurationMs}（契约要求 > 0，落盘前必须赋值）");
        }

        // §2.4：builtFromCurrentSource 真值表——只能是 rebuilt && buildSucceeded
        // 未重编（rebuilt=false）时恒 false；重编失败时恒 false
        if (!result.Target.Rebuilt && result.Target.BuiltFromCurrentSource)
        {
            violations.Add("target.rebuilt=false 但 builtFromCurrentSource=true（真值表违例：未重编只能是 false）");
        }

        // §2.5：channels 三字段各答一问
        // l2Uia 只答"是否执行了 uia 层"，与断言结果无关
        var hasUiaFailures = result.Tests.Any(t => t.Outcome == "Failed")
                             && result.Verdict is "FAIL" or "ERROR";
        if (!result.Channels.L2Uia.Available && hasUiaFailures && result.Summary.Passed > 0)
        {
            // 极端组合：无 uia 通道却有断言通过——多半是字段复用
            violations.Add("channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用）");
        }

        // l1Api.available=true 时必须真的消费到 oracle 断言（或本次跑的层无 oracle 概念）
        if (result.Channels.L1Api.Available)
        {
            var oracleConsumed = result.Assertions.Any(a =>
                a.Kind is "oracle-crosscheck" or "l1-query" && a.Result != "SKIP");
            if (!oracleConsumed && result.Assertions.Count > 0)
            {
                violations.Add("channels.l1Api.available=true 但断言里没有任何非 SKIP 的 oracle-crosscheck/l1-query（l1Api 只答 18901 是否被消费）");
            }
        }

        // assertions.available=false 时必须带原因
        if (!result.Channels.Assertions.Available && string.IsNullOrWhiteSpace(result.Channels.Assertions.Reason))
        {
            violations.Add("channels.assertions.available=false 但 reason 为空（不可诊断）");
        }

        // summary 与 assertions 明细一致（防两处口径漂移）
        if (result.Assertions.Count > 0)
        {
            var expected = (
                Total: result.Assertions.Count,
                Passed: result.Assertions.Count(a => a.Result == "PASS"),
                Failed: result.Assertions.Count(a => a.Result == "FAIL"),
                Skipped: result.Assertions.Count(a => a.Result == "SKIP"));
            if (result.Summary.Total != expected.Total
                || result.Summary.Passed != expected.Passed
                || result.Summary.Failed != expected.Failed
                || result.Summary.Skipped != expected.Skipped)
            {
                violations.Add($"summary=({result.Summary.Total},{result.Summary.Passed},{result.Summary.Failed},{result.Summary.Skipped}) 与 assertions 明细 ({expected.Total},{expected.Passed},{expected.Failed},{expected.Skipped}) 不一致");
            }
        }

        if (violations.Count > 0)
        {
            Console.Error.WriteLine($"[contract] 契约自检违例: {string.Join("; ", violations)}");
        }

        return violations;
    }
}
