using System;
using System.IO;
using System.Reflection;
using System.Text;
using Vega.BL.DataReporting;
using Vega.Platform.Helpers;
using Xunit;
using static Vega.AutoTest.Authored.JobSummaryProbe;

namespace Vega.AutoTest.Authored;

public class JobSummaryPersistenceDiagnostics
{
    [Fact]
    public void ExtendedPrewriteGuardReportsLossWithoutChangingWriteOrDedupBehavior() => Run(a =>
    {
        var directory = Directory.CreateTempSubdirectory("vega-wi3731-");
        try
        {
            var fixture = Fixture();
            var logger = Logger(out var log);
            string wrap(string content) => "<html><body>Lot3731" + content + "</body></html>";
            var cases = new[]
            {
                (0, wrap(Section("foreign")), wrap(Section("foreign"))),
                (1, wrap(Section("foreign")), wrap(Section("foreign"))),
                (2, wrap(Section(A) + Section(B)), wrap(Section(A, Row) + Section(B, Row))),
                (2, wrap(Section(A, Row) + Section(B)), wrap(Section(A, Row) + Section(B)))
            };
            for (int i = 0; i < cases.Length; i++)
            {
                string path = Path.Combine(directory.FullName, $"case-{i}.html");
                File.WriteAllText(path, cases[i].Item2);
                var builder = Builder(logger, cases[i].Item1);
                var helper = Helper(logger, builder);
                log.Entries.Clear();
                var snapshot = (WaferStaticsSummary)New(typeof(WaferStaticsSummary));
                bool result = (bool)Call(helper, "AddWaferRunToExistingExtendedJobSummary", fixture.Job, fixture.Wafer, path, snapshot)!;
                Equal(a, $"extended-return-{i}", true, result);
                Equal(a, $"extended-disk-{i}", cases[i].Item3, File.ReadAllText(path));
                string expected = "";
                if (i < 2)
                {
                    string missing = i == 0 ? "<none>" : A + "-tbody";
                    expected = Log("Warning", "ExtendedChannelDefectSummaryBuilder",
                        $"AppendToHtmlFile inserted no row waferRunId=42 helperInstance={Fingerprint} lotId=7 summaryTables={cases[i].Item1} firstMissingAnchor={missing} fileTbodyIds=[foreign-tbody]") + "\n" +
                        Log("Error", "JobSummaryHelper", $"JobSummary append produced no row waferRunId=42 waferId=W42 branch=appendRow lot=Lot3731 lotRunId=7 path={path} helperInstance={Fingerprint} builderState=lotId=7 summaryTables={cases[i].Item1}");
                }
                Equal(a, $"extended-guard-log-{i}", expected, Joined(log));
            }
        }
        finally { directory.Delete(true); }
    });

    [Fact]
    public void MalformedShortLotBodiesWarnAndReturnFalseWithoutEditingHtml() => Run(a =>
    {
        var fixture = Fixture();
        var logger = Logger(out var log);
        var helper = Helper(logger, Builder(logger, 0));
        var lotHeader = new StringBuilder();
        Call(helper, "GenerateLotTableToHtml", fixture.Lot, lotHeader);
        string header = lotHeader.ToString().Replace("<tbody>", "").Replace("</tbody>", "");
        var method = typeof(ShortChannelDefectSummaryBuilder).GetMethod("TryAppendWaferToExistingLot", Flags)!;
        var fields = Activator.CreateInstance(typeof(System.Collections.Generic.Dictionary<,>)
            .MakeGenericType(method.GetParameters()[3].ParameterType.GetGenericArguments()))!;
        for (int i = 0; i < 2; i++)
        {
            // Only the malformed-anchor early-return path is selected; no row renderer or global service is used.
            var builder = (ShortChannelDefectSummaryBuilder)New(typeof(ShortChannelDefectSummaryBuilder));
            WireLogger(builder, logger);
            string original = header + (i == 0 ? "" : "<tbody>unclosed");
            var html = new StringBuilder(original);
            log.Entries.Clear();
            bool result = (bool)Call(builder, "TryAppendWaferToExistingLot", html, fixture.Wafer, fixture.Lot, fields,
                New(typeof(WaferStaticsSummary)))!;
            Equal(a, $"short-return-{i}", false, result);
            Equal(a, $"short-unchanged-{i}", original, html.ToString());
            string reason = i == 0 ? "tbody not found after lot cell" : "tbody close tag not found";
            Equal(a, $"short-warning-{i}", Log("Warning", "ShortChannelDefectSummaryBuilder",
                $"TryAppendWaferToExistingLot {reason}, fallback to lot section rebuild lot=Lot3731 waferRunId=42"), Joined(log));
        }
    });

    [Fact]
    public void ExportActionIncludesTheSuppliedHelperFingerprintAndWritesACompleteDocument() => Run(a =>
    {
        var directory = Directory.CreateTempSubdirectory("vega-wi3731-export-");
        try
        {
            var logger = Logger(out var log);
            var helper = Helper(logger, Builder(logger, 0));
            var method = typeof(JobSummaryHelper).GetMethod("DoExportJobSummary", Flags)!;
            object option = Enum.Parse(method.GetParameters()[1].ParameterType, "Extended");
            string path = Path.Combine(directory.FullName, "summary.html");
            Call(helper, "DoExportJobSummary", Job(), option, path);
            string document = File.ReadAllText(path);
            Equal(a, "export-fingerprint", Log("Information", "JobSummaryHelper",
                $"DoExportJobSummary jobRun=Job3731 option=Extended waferCount=0 path={path} helperInstance={Fingerprint}"), Joined(log));
            Equal(a, "export-doctype", true, document.StartsWith("<!DOCTYPE html>", StringComparison.Ordinal));
            Equal(a, "export-business-title", true, document.Contains("<title>Job Summary : Job3731</title>", StringComparison.Ordinal));
            Equal(a, "export-complete-document", true, document.TrimEnd().EndsWith("</script></body></html>", StringComparison.Ordinal));
        }
        finally { directory.Delete(true); }
    });
}
