using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vega.BL.DataReporting;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

public class ExtendedSummaryMonitoringTests
{
    private const string Fingerprint = "fp-3731";
    private const string DefectId = "lot17_DefectSummary-C-DW1_Oblique-False-DW1-tbody";
    private const string BinId = "lot17_BinSummaryBins-C-DW1_Oblique-False-DW1-tbody";
    private const string Prior = "<tr data-wafer-run-id=\"41\"><td>Slot4</td></tr>";
    private const string Marker = "data-wafer-run-id=\"42\"";
    private static string Body(string id, string rows = "") => "<tbody id=\"" + id + "\">" + rows + "</tbody>";

    private static ChannelSummary Descriptor(SummaryType type, string category) => new()
    {
        SummaryType = type, CategoryName = category, ChannelName = "DW1", CollectionName = "C",
        ChannelMode = ChannelMode.DW1_Oblique, IsAdvanced = false, Headers = new List<string> { "Count" },
        RowDataList = new List<WaferRowData>
        {
            new() { WaferRunId = 42, WaferId = "Slot5", Grade = "Pass", SrcDest = "1/5-1/5", RowData = new List<double> { 7 } }
        }
    };

    private static Dictionary<string, List<ChannelSummary>> Descriptors(ExtendedChannelDefectSummaryBuilder builder)
        => (Dictionary<string, List<ChannelSummary>>)SummaryTest.Get(builder, "_waferSummaries");

    [Fact]
    public void AppendWarningsExplainMissingAnchorsWithoutChangingInsertionOrDeduplication()
    {
        SummaryTest.Run(a =>
        {
            string healthy = Body(DefectId, Prior) + Body(BinId);
            string duplicate = "<tr " + Marker + "><td>Slot5</td></tr>";
            var rows = new (string Name, string Html, bool Empty, int MarkerCount, bool Changed, string Warning, string[] InsertedBodies)[]
            {
                ("healthy", healthy, false, 2, true, "", new[] { DefectId, BinId }),
                ("dedup", Body(DefectId, Prior + duplicate) + Body(BinId), false, 1, false, "", Array.Empty<string>()),
                ("missing", Body("legacy-tbody", Prior), false, 0, false,
                    "AppendToHtmlFile inserted no row waferRunId=42 helperInstance=fp-3731 lotId=17 summaryTables=2 firstMissingAnchor=" + DefectId + " fileTbodyIds=[legacy-tbody]", Array.Empty<string>()),
                ("partial", Body(DefectId, Prior) + Body("legacy-tbody"), false, 1, true,
                    "AppendToHtmlFile partial anchor miss waferRunId=42 helperInstance=fp-3731 lotId=17 summaryTables=2 inserted=1 dedupSkipped=0 firstMissingAnchor=" + BinId, new[] { DefectId }),
                ("unclosed", "<tbody id=\"" + DefectId + "\">" + Prior, false, 0, false,
                    "AppendToHtmlFile inserted no row waferRunId=42 helperInstance=fp-3731 lotId=17 summaryTables=2 firstMissingAnchor=" + DefectId + " fileTbodyIds=[" + DefectId + "]", Array.Empty<string>()),
                ("empty-state", Body("legacy-tbody", Prior), true, 0, false,
                    "AppendToHtmlFile inserted no row waferRunId=42 helperInstance=fp-3731 lotId=17 summaryTables=0 firstMissingAnchor=<none> fileTbodyIds=[legacy-tbody]", Array.Empty<string>()),
                ("dedup-before-missing", Body("legacy-tbody", Prior + duplicate), false, 1, false, "", Array.Empty<string>())
            };
            foreach (var row in rows)
            {
                var logger = SummaryTest.Logger(out var log);
                var builder = new ExtendedChannelDefectSummaryBuilder(null!, logger, Fingerprint);
                SummaryTest.Set(builder, "_lotId", "17");
                if (!row.Empty) Descriptors(builder).Add("fixture", new List<ChannelSummary>
                    { Descriptor(SummaryType.DefectSummary, ""), Descriptor(SummaryType.BinSummary, "Bins") });
                var sb = new StringBuilder(row.Html);
                SummaryTest.Call(builder, "AppendToHtmlFile", sb, 42L);
                string output = sb.ToString();
                var warnings = log.Entries.Where(e => e.Level == "Warning").ToList();
                SummaryTest.Check(a, SummaryTest.Occurrences(output, Marker) == row.MarkerCount,
                    row.Name + " row-marker count", row.MarkerCount, SummaryTest.Occurrences(output, Marker));
                SummaryTest.Check(a, (output != row.Html) == row.Changed,
                    row.Name + " mutation policy", row.Changed, output != row.Html);
                int expectedWarnings = row.Warning.Length == 0 ? 0 : 1;
                SummaryTest.Check(a, warnings.Count == expectedWarnings, row.Name + " warning frequency", expectedWarnings, warnings.Count);
                bool diagnostic = expectedWarnings == 0 ? log.Entries.Count == 0
                    : log.Entries.Count == 1 && warnings[0].Text.Contains("ExtendedChannelDefectSummaryBuilder", StringComparison.Ordinal)
                        && warnings[0].Text.Contains(row.Warning, StringComparison.Ordinal);
                SummaryTest.Check(a, diagnostic, row.Name + " diagnostic context or healthy silence",
                    row.Warning.Length == 0 ? "No logs" : row.Warning, string.Join(" | ", log.Entries));
                SummaryTest.Check(a, SummaryTest.Occurrences(output, Prior) == 1,
                    row.Name + " previously completed wafer remains intact", 1, SummaryTest.Occurrences(output, Prior));
                foreach (string id in row.InsertedBodies)
                {
                    int start = output.IndexOf("<tbody id=\"" + id + "\">", StringComparison.Ordinal);
                    int end = output.IndexOf("</tbody>", start, StringComparison.Ordinal);
                    string body = output.Substring(start, end - start);
                    SummaryTest.Check(a, SummaryTest.Occurrences(body, Marker) == 1,
                        row.Name + " new wafer belongs to " + id, 1, SummaryTest.Occurrences(body, Marker));
                }
            }
        });
    }

    [Fact]
    public void DescriptorInitializationTracksLotTransitionsAndClearsPreviousState()
    {
        SummaryTest.Run(a =>
        {
            var logger = SummaryTest.Logger(out var log);
            var configs = DispatchProxy.Create<IConfigsManager, SummaryConfigProxy>();
            ((SummaryConfigProxy)(object)configs).Analysis.DisplayMode = ChannelDisplayMode.All;
            var recipe = new Recipe();
            recipe.CollectionElement.Collections.Clear();
            var builder = new ExtendedChannelDefectSummaryBuilder(configs, logger, Fingerprint);
            var fields = new Dictionary<SummaryChannelKey, IReadOnlyList<JobSummaryDefectField>>();
            var rows = new (string? Lot, string Level, bool WriteHtml)[]
            {
                ("17", "Information", true), ("17", "Verbose", false),
                ("18", "Information", false), (null, "Information", false), ("", "Verbose", true)
            };
            foreach (var row in rows)
            {
                Descriptors(builder).Clear();
                Descriptors(builder).Add("stale", new List<ChannelSummary> { Descriptor(SummaryType.DefectSummary, "") });
                log.Entries.Clear();
                var sb = new StringBuilder("sentinel");
                builder.InitializeWaferTableHeaders(recipe, sb, row.Lot!, fields, row.WriteHtml);
                string state = (string)SummaryTest.Call(builder, "DescribeAnchorState")!;
                string expectedState = "lotId=" + (row.Lot ?? "") + " summaryTables=0";
                SummaryTest.Check(a, state == expectedState, "Initialization normalizes lot and clears stale descriptors", expectedState, state);
                SummaryTest.Check(a, log.Entries.Count == 1 && log.Entries[0].Level == row.Level,
                    "Lot transition selects diagnostic severity", row.Level + " once", string.Join(" | ", log.Entries));
                string expected = "JobSummary table descriptors initialized: " + expectedState
                    + " channels=0 writeHtml=" + row.WriteHtml + " helperInstance=" + Fingerprint;
                SummaryTest.Check(a, log.Entries[0].Text.Contains(expected, StringComparison.Ordinal),
                    "Initialization diagnostic includes state and originating helper", expected, log.Entries[0].Text);
                SummaryTest.Check(a, sb.ToString() == "sentinel", "Empty-channel initialization adds no HTML", "sentinel", sb.ToString());
            }
        });
    }
}
