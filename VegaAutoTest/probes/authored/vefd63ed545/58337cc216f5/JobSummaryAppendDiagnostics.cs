using System;
using System.Collections.Generic;
using System.Text;
using Vega.BL.DataReporting;
using Vega.Data.Commons;
using Vega.Utility.Common;
using Xunit;
using static Vega.AutoTest.Authored.JobSummaryProbe;

namespace Vega.AutoTest.Authored;

public class JobSummaryAppendDiagnostics
{
    [Fact]
    public void AppendingPreservesRowsAndOnlyWarnsForMissingAnchors() => Run(a =>
    {
        var logger = Logger(out var log);
        string warning(int tables, string missing, string ids) => Log("Warning", "ExtendedChannelDefectSummaryBuilder",
            $"AppendToHtmlFile inserted no row waferRunId=42 helperInstance={Fingerprint} lotId=7 summaryTables={tables} firstMissingAnchor={missing} fileTbodyIds=[{ids}]");
        var duplicate = Section(A, Row) + Section(B);
        var cases = new[]
        {
            (2, Section(A) + Section(B), Section(A, Row) + Section(B, Row), ""),
            (2, duplicate, duplicate, ""),
            (2, Section("foreign1") + Section("foreign2"), Section("foreign1") + Section("foreign2"),
                warning(2, A + "-tbody", "foreign1-tbody,foreign2-tbody")),
            (2, Section(A), Section(A, Row), Log("Warning", "ExtendedChannelDefectSummaryBuilder",
                $"AppendToHtmlFile partial anchor miss waferRunId=42 helperInstance={Fingerprint} lotId=7 summaryTables=2 inserted=1 dedupSkipped=0 firstMissingAnchor={B}-tbody")),
            (1, $"<tbody id=\"{A}-tbody\">", $"<tbody id=\"{A}-tbody\">", warning(1, A + "-tbody", A + "-tbody")),
            (0, Section("foreign"), Section("foreign"), warning(0, "<none>", "foreign-tbody")),
            (2, Row, Row, "")
        };
        for (int i = 0; i < cases.Length; i++)
        {
            log.Entries.Clear();
            var builder = Builder(logger, cases[i].Item1);
            var html = new StringBuilder(cases[i].Item2);
            Call(builder, "AppendToHtmlFile", html, 42L);
            Equal(a, $"append-html-{i}", cases[i].Item3, html.ToString());
            Equal(a, $"append-log-{i}", cases[i].Item4, Joined(log));
        }
    });

    [Fact]
    public void DescriptorReinitializationUsesInformationForLotChangesAndVerboseForSameLot() => Run(a =>
    {
        var logger = Logger(out var log);
        var configs = System.Reflection.DispatchProxy.Create<IConfigsManager, SummaryLogProxy>();
        var builder = new ExtendedChannelDefectSummaryBuilder(configs, logger, Fingerprint);
        var recipe = EmptyRecipe();
        var fields = new Dictionary<SummaryChannelKey, IReadOnlyList<JobSummaryDefectField>>();
        var calls = new[] { ("A", true, "Information"), ("A", false, "Verbose"), ("B", false, "Information"), ("B", true, "Verbose") };
        for (int i = 0; i < calls.Length; i++)
        {
            log.Entries.Clear();
            var html = new StringBuilder("sentinel");
            builder.InitializeWaferTableHeaders(recipe, html, calls[i].Item1, fields, calls[i].Item2);
            string state = $"lotId={calls[i].Item1} summaryTables=0";
            Equal(a, $"initialization-state-{i}", state, (string)Call(builder, "DescribeAnchorState")!);
            Equal(a, $"initialization-html-{i}", "sentinel", html.ToString());
            Equal(a, $"initialization-log-{i}", Log(calls[i].Item3, "ExtendedChannelDefectSummaryBuilder",
                $"JobSummary table descriptors initialized: {state} channels=0 writeHtml={calls[i].Item2} helperInstance={Fingerprint}"), Joined(log));
        }
    });
}
