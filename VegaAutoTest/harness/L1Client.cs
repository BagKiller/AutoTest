// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Net.Http;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 观察：L1 AgentControlService HTTP 通道（GET json 只读）。
/// </summary>
public sealed class L1Client
{
    /// <summary>轮询 L1 控制服务 ping（最多 timeoutSeconds），用于等待新实例的 DelayStartSeconds。</summary>
    public static bool WaitForReady(int timeoutSeconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                _ = client.GetStringAsync($"{VegaEnv.AgentBaseUrl}ping").GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                Thread.Sleep(2000);
            }
        }

        return false;
    }

    /// <summary>GET 指定端点并返回原始 json（调用方自担异常）。</summary>
    public static string GetJson(string endpoint)
    {
        var (_, body) = Get(endpoint);
        return body;
    }

    /// <summary>GET 并带回 HTTP 状态码。网络失败时 Status=0。</summary>
    public static (int Status, string Body) Get(string endpoint)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var response = client.GetAsync($"{VegaEnv.AgentBaseUrl}{endpoint}").GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return ((int)response.StatusCode, body);
        }
        catch (Exception ex)
        {
            return (0, ex.Message);
        }
    }

    /// <summary>GET query/tool-state 原始 json（调用方自担异常）。</summary>
    public static string QueryToolStateRaw() => GetJson("query/tool-state");

    /// <summary>GET query/opened-recipe 原始 json（调用方自担异常）。</summary>
    public static string GetOpenedRecipe() => GetJson("query/opened-recipe");
}
