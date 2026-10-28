// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Galaxy.Data.Wafers;
using Vega.Data.Measurements;
using Vega.Utility.Helper;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// T1 序列化兼容（flat-wafer-alignment-design-v4 §8.1 / Step 1.5）：
/// 旧 JSON 缺 NotchType 字段 → 默认 Notch（PI 4989 的 EdgeScanInfo 显式初始化）；Flat 往返保留；
/// 主平边长度与既有字段读写互不影响。
/// 注：CollectionsContent 全对象图构造依赖 ToolFeaturesHelper（Galaxy 容器），按 v4 §8.2
/// 不在单测中绕造，其缺省/往返行为由同一 Newtonsoft 缺字段机制保证 + Step 6 UI 验证覆盖。
/// 服务对未知枚举值的显式拒绝见 SetupWaferAlignment(EdgeScanInfo)（人工 Review + T10 覆盖失败出口）。
/// </summary>
public class EdgeScanInfoSerializationTests
{
    [Fact]
    public void MissingNotchTypeField_DefaultsToNotch()
    {
        // 旧版本快照 JSON：只有既有字段，无 NotchType。
        const string legacyJson = """
            {"Pitch":25.0,"SamplePerTrack":3600,"Step":1,"TrackCount":100,"CowSearchMethod":2}
            """;

        var info = JsonHelper.FromJson<EdgeScanInfo>(legacyJson);

        Assert.NotNull(info);
        Assert.Equal(NotchTypes.Notch, info!.NotchType);
        Assert.Equal(0.0, info.PrimaryFlatLength);
        Assert.Equal(25.0, info.Pitch);
        Assert.Equal(3600, info.SamplePerTrack);
    }

    [Fact]
    public void FlatNotchTypeAndPrimaryFlatLength_RoundTripsThroughJson()
    {
        var info = new EdgeScanInfo
        {
            Pitch = 25.0,
            SamplePerTrack = 3600,
            Step = 1,
            NotchType = NotchTypes.Flat,
            PrimaryFlatLength = 57500.0,
            WaferDiameter = 150000.0,
        };

        var restored = JsonHelper.FromJson<EdgeScanInfo>(JsonHelper.ToJson(info));

        Assert.NotNull(restored);
        Assert.Equal(NotchTypes.Flat, restored!.NotchType);
        Assert.Equal(57500.0, restored.PrimaryFlatLength);
        Assert.Equal(150000.0, restored.WaferDiameter);
        Assert.Equal(25.0, restored.Pitch);
    }
}