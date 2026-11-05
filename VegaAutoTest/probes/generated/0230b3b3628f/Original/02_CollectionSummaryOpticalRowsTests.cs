// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Commons;
using Vega.Data.Recipes;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   CollectionSummaryContent.BuildOpticalSummaryRows 光学信息行投影的单测：
///   Collection Info 行追加激光功率与偏振、无键轨道行的 DN/宽通道 N 系与 W 系字段映射
///   （陷阱值验证不串读）、多通道每段一行（\n 换行）与通道顺序、禁用通道过滤、
///   零启用通道、行序、collectors 为 null、键唯一、Edge Exclusion 单位与空值兜底（FormatEdgeExclusion）。
///   勿用 ChannelMode.DW1_Oblique 构造 DFCollectorSettingContent：其 ctor 访问
///   CollectorTypeExtensions.EnabledCollectors（配置服务，测试宿主不可用）；
///   非 DW1 模式被 || 短路不触发。DW1 与 DW2 走同一宽通道分支（IsNarrowCollector==false），
///   DW1 实际显示由仿真验证，不为测试引入假配置或反射。
/// </summary>
public class CollectionSummaryOpticalRowsTests
{
    private static DFCollectorSettingContent BuildChannel(
        ChannelMode channelMode,
        string wMask,
        string nMask,
        WCollectorPolarizerType wPolarizer,
        NCollectorPolarizerType nPolarizer,
        bool isEnabled = true)
    {
        return new DFCollectorSettingContent(channelMode)
        {
            WMask = wMask,
            NMask = nMask,
            WPolarizer = wPolarizer,
            NPolarizer = nPolarizer,
            IsEnabled = isEnabled,
        };
    }

    private static List<KeyValuePair<string, string>> BuildRows(
        ChannelMode channelMode,
        string wMask,
        string nMask,
        WCollectorPolarizerType wPolarizer,
        NCollectorPolarizerType nPolarizer,
        bool isEnabled = true)
        => CollectionSummaryContent.BuildOpticalSummaryRows(
            IncidenceType.Oblique,
            ThroughputType.HighSensitivity,
            PolarizerType.P,
            100,
            [BuildChannel(channelMode, wMask, nMask, wPolarizer, nPolarizer, isEnabled)]);

    private static string TrackRowValue(List<KeyValuePair<string, string>> rows)
        => rows.Single(row => row.Key == string.Empty).Value;

    [Theory]
    [InlineData(ChannelMode.DN_Oblique)]
    [InlineData(ChannelMode.DN_Normal)]
    public void NarrowChannel_SegmentUsesNFields_IgnoresWFields(ChannelMode channelMode)
    {
        List<KeyValuePair<string, string>> rows = BuildRows(
            channelMode, wMask: "W-Leak", nMask: "N-X1",
            wPolarizer: WCollectorPolarizerType.S, nPolarizer: NCollectorPolarizerType.P);

        string collectorName = channelMode.ToString().Split('_')[0];
        // 陷阱值 "S" 是单字母，会误伤 "HighSensitivity" 等正文，断言收窄到轨道行本身
        string trackRow = TrackRowValue(rows);
        Assert.Equal($"{collectorName} {nameof(NCollectorPolarizerType.P)} N-X1", trackRow);
        Assert.DoesNotContain("W-Leak", trackRow);
        Assert.DoesNotContain(nameof(WCollectorPolarizerType.S), trackRow);
    }

    [Theory]
    [InlineData(ChannelMode.DW2_Oblique)]
    [InlineData(ChannelMode.DW2_Normal)]
    public void WideChannel_SegmentUsesWFields_IgnoresNFields(ChannelMode channelMode)
    {
        List<KeyValuePair<string, string>> rows = BuildRows(
            channelMode, wMask: "W1W2-X5", nMask: "N-Leak",
            wPolarizer: WCollectorPolarizerType.POL1, nPolarizer: NCollectorPolarizerType.P);

        string collectorName = channelMode.ToString().Split('_')[0];
        Assert.Equal($"{collectorName} {nameof(WCollectorPolarizerType.POL1)} W1W2-X5", TrackRowValue(rows));
        Assert.DoesNotContain(rows, row => row.Value.Contains("N-Leak"));
    }

    [Fact]
    public void MultipleEnabledChannels_OneSegmentPerLine_InCollectorOrder()
    {
        List<KeyValuePair<string, string>> rows = CollectionSummaryContent.BuildOpticalSummaryRows(
            IncidenceType.Oblique,
            ThroughputType.HighSensitivity,
            PolarizerType.P,
            100,
            [
                BuildChannel(ChannelMode.DW2_Oblique, "W1W2-X5", "N-Leak", WCollectorPolarizerType.POL1, NCollectorPolarizerType.P),
                BuildChannel(ChannelMode.DN_Oblique, "W-Leak", "N-X1", WCollectorPolarizerType.S, NCollectorPolarizerType.P),
            ]);

        Assert.Equal(
            $"DW2 {nameof(WCollectorPolarizerType.POL1)} W1W2-X5\nDN {nameof(NCollectorPolarizerType.P)} N-X1",
            TrackRowValue(rows));
    }

    [Fact]
    public void DisabledChannel_IsFiltered()
    {
        List<KeyValuePair<string, string>> rows = BuildRows(
            ChannelMode.DN_Oblique, wMask: "W-Leak", nMask: "N-X1",
            wPolarizer: WCollectorPolarizerType.S, nPolarizer: NCollectorPolarizerType.P,
            isEnabled: false);

        Assert.DoesNotContain(rows, row => row.Key == string.Empty);
        Assert.DoesNotContain(rows, row => row.Value.Contains("N-X1"));
        Assert.DoesNotContain(rows, row => row.Value.Contains("W-Leak"));
    }

    [Fact]
    public void CollectionInfo_AppendsLaserPowerAndPolarization_WithoutPlaceholder()
    {
        List<KeyValuePair<string, string>> rows = CollectionSummaryContent.BuildOpticalSummaryRows(
            IncidenceType.Normal,
            ThroughputType.UltraHighSensitivity,
            PolarizerType.C,
            87.5,
            null);

        KeyValuePair<string, string> first = rows[0];
        Assert.Equal("Collection Info", first.Key);
        Assert.Equal($"Normal {ThroughputType.UltraHighSensitivity} 87.5% {nameof(PolarizerType.C)}", first.Value);
        Assert.DoesNotContain("...", first.Value);
    }

    [Fact]
    public void Rows_FollowExpectedOrder()
    {
        List<KeyValuePair<string, string>> rows = BuildRows(
            ChannelMode.DN_Oblique, wMask: "W-Leak", nMask: "N-X1",
            wPolarizer: WCollectorPolarizerType.S, nPolarizer: NCollectorPolarizerType.P);

        List<string> keys = rows.Select(row => row.Key).ToList();
        List<string> expected =
        [
            "Collection Info",
            string.Empty,
        ];
        Assert.Equal(expected, keys);
    }

    [Fact]
    public void AllChannelsDisabled_YieldCollectionInfoRowOnly()
    {
        List<KeyValuePair<string, string>> rows = CollectionSummaryContent.BuildOpticalSummaryRows(
            IncidenceType.Oblique,
            ThroughputType.HighSensitivity,
            PolarizerType.P,
            100,
            [
                BuildChannel(ChannelMode.DW2_Oblique, "W1W2-X5", "N-Leak", WCollectorPolarizerType.POL1, NCollectorPolarizerType.P, isEnabled: false),
                BuildChannel(ChannelMode.DN_Oblique, "W-Leak", "N-X1", WCollectorPolarizerType.S, NCollectorPolarizerType.P, isEnabled: false),
            ]);

        List<string> expected = ["Collection Info"];
        Assert.Equal(expected, rows.Select(row => row.Key).ToList());
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(3.0, "3mm")]
    [InlineData(2.5, "2.5mm")]
    public void FormatEdgeExclusion_AppendsUnit_AndFallsBackToEmpty(double? value, string expected)
    {
        Assert.Equal(expected, CollectionSummaryContent.FormatEdgeExclusion(value));
    }

    [Fact]
    public void NullCollectors_YieldCollectionLevelRowsOnly()
    {
        List<KeyValuePair<string, string>> rows = CollectionSummaryContent.BuildOpticalSummaryRows(
            IncidenceType.Oblique,
            ThroughputType.HighSensitivity,
            PolarizerType.P,
            100,
            null);

        List<string> expected = ["Collection Info"];
        Assert.Equal(expected, rows.Select(row => row.Key).ToList());
    }

    [Fact]
    public void Keys_AreUnique()
    {
        List<KeyValuePair<string, string>> rows = BuildRows(
            ChannelMode.DN_Oblique, wMask: "W-Leak", nMask: "N-X1",
            wPolarizer: WCollectorPolarizerType.S, nPolarizer: NCollectorPolarizerType.P);

        Assert.Equal(rows.Count, rows.Select(row => row.Key).Distinct().Count());
    }
}