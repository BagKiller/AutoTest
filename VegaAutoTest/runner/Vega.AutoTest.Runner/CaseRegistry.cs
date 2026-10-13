// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Vega.AutoTest.Runner;

/// <summary>cases/registry.json 中单个 layer 的配置。</summary>
public sealed class LayerConfig
{
    [JsonProperty("project")] public string Project { get; set; } = string.Empty;

    [JsonProperty("filter")] public string Filter { get; set; } = string.Empty;

    [JsonProperty("timeoutSeconds")] public int TimeoutSeconds { get; set; } = 600;

    [JsonProperty("requiresVegaExe")] public bool RequiresVegaExe { get; set; }

    [JsonProperty("requiresL1Api")] public bool RequiresL1Api { get; set; }

    [JsonProperty("minAssertions")] public int MinAssertions { get; set; }
}

/// <summary>cases/registry.json 中单个用例的登记。</summary>
public sealed class CaseConfig
{
    [JsonProperty("id")] public string Id { get; set; } = string.Empty;

    [JsonProperty("title")] public string Title { get; set; } = string.Empty;

    [JsonProperty("workItems")] public List<int> WorkItems { get; set; } = new();

    [JsonProperty("tags")] public List<string> Tags { get; set; } = new();

    [JsonProperty("layers")] public Dictionary<string, LayerConfig> Layers { get; set; } = new();

    [JsonProperty("evidenceFiles")] public List<string> EvidenceFiles { get; set; } = new();
}

/// <summary>cases/registry.json 顶层结构。</summary>
public sealed class RegistryConfig
{
    [JsonProperty("schemaVersion")] public string SchemaVersion { get; set; } = "1.0";

    [JsonProperty("cases")] public List<CaseConfig> Cases { get; set; } = new();
}

/// <summary>加载 cases/registry.json，按 workItemId 查 case（显式登记，不让模型猜）。</summary>
public static class CaseRegistry
{
    public static RegistryConfig Load(string autoTestRoot)
        => JsonConvert.DeserializeObject<RegistryConfig>(
               File.ReadAllText(Path.Combine(autoTestRoot, "cases", "registry.json")))
           ?? throw new InvalidOperationException("registry.json 解析结果为空");

    public static CaseConfig? FindByWorkItem(RegistryConfig registry, int workItemId)
        => registry.Cases.FirstOrDefault(c => c.WorkItems.Contains(workItemId));
}
