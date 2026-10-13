// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Vega.AutoTest.Runner;

/// <summary>写 runs/&lt;runId&gt;/result.json（camelCase，对齐方案 §2.1 契约）。</summary>
public static class ResultWriter
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Include,
        Formatting = Formatting.Indented,
    };

    public static void Write(string path, RunResult result)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonConvert.SerializeObject(result, Settings));
    }
}
