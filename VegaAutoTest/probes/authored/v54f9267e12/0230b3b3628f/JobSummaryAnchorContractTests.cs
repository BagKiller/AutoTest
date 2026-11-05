using System;
using System.Collections.Generic;
using Vega.BL.DataReporting;
using Xunit;

namespace Vega.AutoTest.Authored;

public class JobSummaryAnchorContractTests
{
    [Fact]
    public void ClassificationAndDiagnosticIdsFollowTheDocumentedContract()
    {
        SummaryTest.Run(a =>
        {
            const string prefix = "<html><tbody id=\"other-tbody\"></tbody><tbody id=\"T-tbody\"><tr><td>old</td></tr>";
            const string marker = "<tr data-wafer-run-id=\"42\"></tr>";
            var rows = new (string Html, string Classification, int Position)[]
            {
                (prefix + "</tbody></html>", "Inserted", prefix.Length),
                (marker, "DedupSkipped", -1),
                (marker + "<tbody id=\"T-tbody\">", "DedupSkipped", -1),
                ("<tbody id=\"other-tbody\"></tbody>", "AnchorMissing", -1),
                ("<tbody id=\"T-tbody\">unfinished", "AnchorMissing", -1),
                ("<tbody id=\"T2-tbody\"></tbody>", "AnchorMissing", -1),
                (prefix + "<tr data-wafer-run-id=\"420\"></tr></tbody>", "Inserted", prefix.Length + "<tr data-wafer-run-id=\"420\"></tr>".Length),
                ("<tbody id=\"other-tbody\">" + marker + "</tbody>" + prefix + "</tbody>", "DedupSkipped", -1)
            };
            foreach (var row in rows)
            {
                object?[] arguments = { row.Html, 42L, "T", -1 };
                string actual = SummaryTest.Call(typeof(ExtendedChannelDefectSummaryBuilder), "ClassifyTableAnchor", arguments)!.ToString()!;
                SummaryTest.Check(a, actual == row.Classification, "Anchor classification", row.Classification, actual);
                SummaryTest.Check(a, (int)arguments[3]! == row.Position, "Insertion position or absent-position sentinel", row.Position, arguments[3]!);
            }

            var documents = new (string Html, string[] Ids)[]
            {
                ("<tbody id=\"lotA-tbody\"></tbody><tbody id=\"lotB-tbody\"></tbody>", new[] { "lotA-tbody", "lotB-tbody" }),
                ("<html><body>no table</body></html>", Array.Empty<string>()),
                ("<tbody id=\"same-tbody\"></tbody><tbody id=\"same-tbody\"></tbody>", new[] { "same-tbody", "same-tbody" }),
                ("<tbody id=\"complete-tbody\"></tbody><tbody id=\"unfinished", new[] { "complete-tbody" }),
                ("<tbody id=\"lot&amp;A-tbody\"></tbody>", new[] { "lot&amp;A-tbody" })
            };
            foreach (var document in documents)
            {
                var ids = (List<string>)SummaryTest.Call(typeof(ExtendedChannelDefectSummaryBuilder), "ExtractAllTbodyIds", document.Html)!;
                string expected = string.Join("|", document.Ids);
                string actual = string.Join("|", ids);
                SummaryTest.Check(a, expected == actual && document.Ids.Length == ids.Count,
                    "Diagnostic IDs preserve order, duplicates, suffixes and raw attribute values", expected, actual);
            }
        });
    }
}
