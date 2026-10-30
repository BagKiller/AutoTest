// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Net.Http;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Newtonsoft.Json.Linq;

namespace Vega.AutoTest.Harness;

/// <summary>
/// 观察：设备状态双通道读取。L1 API（1.5s 超时）优先，API 不可用回退 UIA 状态栏刮取。
/// 通道切换写取证日志，供回归证据与通道统计。
/// </summary>
public sealed class DeviceStateReader
{
    private readonly EvidenceLog _log;
    private string _stateChannel = "";
    public string Channel => _stateChannel;

    public DeviceStateReader(EvidenceLog log) => _log = log;

    /// <summary>读设备状态：优先 L1 API（ToolState 枚举→状态栏中文映射），异常回退状态栏刮取。</summary>
    public string Read(Window main)
    {
        // L1 通道：1.5s 超时，服务未起/延迟启动期间立刻回退，不拖慢 UIA 轮询
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
            var json = client.GetStringAsync($"{VegaEnv.AgentBaseUrl}query/tool-state").GetAwaiter().GetResult();
            var o = JObject.Parse(json);
            if (o.Value<bool?>("toolStateResolved") == true)
            {
                var mapped = MapToolState(o.Value<string>("toolState"));
                if (mapped is not null)
                {
                    if (_stateChannel != "API")
                    {
                        _stateChannel = "API";
                        LogChannel("API", $"toolState={o.Value<string>("toolState")}");
                    }

                    return mapped;
                }
            }
        }
        catch
        {
            // 服务未就绪（DelayStartSeconds 内）/端口不通 → 回退 UIA
        }

        // UIA 兜底：刮状态栏"设备状态"文本
        if (_stateChannel != "UIA")
        {
            _stateChannel = "UIA";
            LogChannel("UIA", "API unavailable, fallback to status bar scraping");
        }

        return ReadFromStatusBar(main);
    }

    private void LogChannel(string channel, string detail)
        => _log.Step($"  状态通道 -> {channel}（{detail}）");

    /// <summary>ToolState 枚举 → 状态栏中文映射；未知枚举返回 null。</summary>
    public static string? MapToolState(string? toolState) => toolState switch
    {
        "Idle" => "空闲",
        "Busy" => "忙碌",
        "Initializing" => "初始化",
        "Boot" => "启动中",
        "Error" => "错误",
        "Locked" => "锁定",
        _ => null,
    };

    /// <summary>UIA 兜底：从状态栏刮"设备状态"文本（spike LoginVerificationTest 版原样保留）。</summary>
    public static string ReadFromStatusBar(Window main)
    {
        try
        {
            var texts = main.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            for (var i = 0; i < texts.Length - 1; i++)
            {
                if (UiProps.Name(texts[i]) == "设备状态")
                {
                    return UiProps.Name(texts[i + 1]);
                }
            }
        }
        catch
        {
            // 树在变化中
        }

        return string.Empty;
    }
}
