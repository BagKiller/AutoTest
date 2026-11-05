// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.DataReporting;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// 锁定 JobSummary 增量追加的锚点分类与文件 tbody 解析（WI #3731 监控日志的诊断基础）：
/// 1) ClassifyTableAnchor 三态判定：内容已含本片 data-wafer-run-id = 去重跳过（先于锚点查找）；
///    tbody 锚点或其闭合标签缺失 = AnchorMissing（行数据就绪也不插入，这是"运行完成片未落盘"的静默点）；
///    命中时插入位置必须是该表 &lt;/tbody&gt; 起始处（行插表尾）。
/// 2) ExtractAllTbodyIds：按出现顺序返回文件内全部 tbody id（原始属性值，含 -tbody 后缀，
///    与诊断日志 firstMissingAnchor 的格式统一可直接比对）；无 tbody 时为空集。
/// </summary>
public class JobSummaryAnchorClassificationTests
{
    private const string TableId = "lot123_DefectSummary-Oblique-DW1_Oblique-True-DW1A-Oblique";

    private static string BuildContent(bool includeTbody = true, bool includeClose = true, bool includeWaferRow = false)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<html><body>");
        if (includeWaferRow)
        {
            sb.Append("<tr data-wafer-run-id=\"42\"><td>W</td></tr>");
        }
        if (includeTbody)
        {
            sb.Append($"<tbody id=\"{TableId}-tbody\">");
            sb.Append("<!-- New rows will be appended here -->");
        }
        if (includeClose)
        {
            sb.Append("</tbody>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    [Fact]
    public void AnchorFound_InsertPositionPointsAtTbodyClose()
    {
        var content = BuildContent();
        int expected = content.IndexOf("</tbody>", StringComparison.Ordinal);

        var result = ExtendedChannelDefectSummaryBuilder.ClassifyTableAnchor(content, waferRunId: 42, TableId, out int insertPosition);

        Assert.Equal(ExtendedChannelDefectSummaryBuilder.AnchorClassification.Inserted, result);
        Assert.Equal(expected, insertPosition);
    }

    [Fact]
    public void DedupSkipped_WhenWaferRunIdAlreadyInContent_EvenWithoutAnyAnchor()
    {
        var content = BuildContent(includeTbody: false, includeClose: true, includeWaferRow: true);

        var result = ExtendedChannelDefectSummaryBuilder.ClassifyTableAnchor(content, waferRunId: 42, TableId, out int insertPosition);

        Assert.Equal(ExtendedChannelDefectSummaryBuilder.AnchorClassification.DedupSkipped, result);
        Assert.Equal(-1, insertPosition);
    }

    [Fact]
    public void AnchorMissing_WhenTbodyTagAbsent()
    {
        var content = BuildContent(includeTbody: false, includeClose: true);

        var result = ExtendedChannelDefectSummaryBuilder.ClassifyTableAnchor(content, waferRunId: 42, TableId, out int insertPosition);

        Assert.Equal(ExtendedChannelDefectSummaryBuilder.AnchorClassification.AnchorMissing, result);
        Assert.Equal(-1, insertPosition);
    }

    [Fact]
    public void AnchorMissing_WhenTbodyCloseTagAbsent()
    {
        var content = BuildContent(includeTbody: true, includeClose: false);

        var result = ExtendedChannelDefectSummaryBuilder.ClassifyTableAnchor(content, waferRunId: 42, TableId, out int insertPosition);

        Assert.Equal(ExtendedChannelDefectSummaryBuilder.AnchorClassification.AnchorMissing, result);
        Assert.Equal(-1, insertPosition);
    }

    [Fact]
    public void AnchorMissing_WhenTbodyIdDoesNotMatch()
    {
        var content = BuildContent().Replace(TableId, "lot999_DefectSummary-Other", StringComparison.Ordinal);

        var result = ExtendedChannelDefectSummaryBuilder.ClassifyTableAnchor(content, waferRunId: 42, TableId, out _);

        Assert.Equal(ExtendedChannelDefectSummaryBuilder.AnchorClassification.AnchorMissing, result);
    }

    [Fact]
    public void ExtractAllTbodyIds_ReturnsAllIdsInOrder_KeepingTbodySuffix()
    {
        var content = "<html><tbody id=\"lotA_DefectSummary-tbody\"><!-- x --></tbody>"
            + "<tbody id=\"lotB_BinSummary-tbody\"></tbody></html>";

        var ids = ExtendedChannelDefectSummaryBuilder.ExtractAllTbodyIds(content);

        Assert.Equal(new[] { "lotA_DefectSummary-tbody", "lotB_BinSummary-tbody" }, ids);
    }

    [Fact]
    public void ExtractAllTbodyIds_EmptyWhenNoTbodyPresent()
    {
        var ids = ExtendedChannelDefectSummaryBuilder.ExtractAllTbodyIds("<html><body><p>no table</p></body></html>");

        Assert.Empty(ids);
    }
}