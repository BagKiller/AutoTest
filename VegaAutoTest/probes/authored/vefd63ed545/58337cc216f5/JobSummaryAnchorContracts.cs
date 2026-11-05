using System;
using System.Collections.Generic;
using Vega.BL.DataReporting;
using Xunit;
using static Vega.AutoTest.Authored.JobSummaryProbe;

namespace Vega.AutoTest.Authored;

public class JobSummaryAnchorContracts
{
    [Fact]
    public void ClassificationAndDiagnosticInventoryFollowTheDocumentedContract() => Run(a =>
    {
        const string prefix = "<tbody id=\"T-tbody\">old";
        const string otherRow = "<tr data-wafer-run-id=\"420\"></tr>";
        const string prior = "<tbody id=\"U-tbody\"></tbody>";
        var rows = new[]
        {
            (prefix + "</tbody>", "Inserted", prefix.Length),
            ("<p>no table</p>", "AnchorMissing", -1),
            (prefix, "AnchorMissing", -1),
            (prior, "AnchorMissing", -1),
            ("<tr data-wafer-run-id=\"42\"></tr>", "DedupSkipped", -1),
            (prefix + "<tr data-wafer-run-id=\"42\"></tr>", "DedupSkipped", -1),
            (prefix + otherRow + "</tbody>", "Inserted", prefix.Length + otherRow.Length),
            (prior + prefix + "</tbody>", "Inserted", prior.Length + prefix.Length)
        };
        for (int i = 0; i < rows.Length; i++)
        {
            object?[] args = { rows[i].Item1, 42L, "T", -1 };
            var result = Call(null, typeof(ExtendedChannelDefectSummaryBuilder), "ClassifyTableAnchor", args);
            Equal(a, $"classification-{i}", rows[i].Item2, result!.ToString());
            Equal(a, $"position-{i}", rows[i].Item3, (int)args[3]!);
        }
        var inventories = new[]
        {
            ("<html>none</html>", ""),
            ("<tbody id=\"A-tbody\"></tbody><tbody id=\"B-tbody\"></tbody>", "A-tbody|B-tbody"),
            ("<tbody id=\"A-tbody\"></tbody><tbody id=\"A-tbody\"></tbody>", "A-tbody|A-tbody"),
            ("<tbody id=\"\"></tbody>", ""),
            ("<tbody id=\"A-tbody\"></tbody><tbody id=\"unfinished", "A-tbody")
        };
        for (int i = 0; i < inventories.Length; i++)
        {
            var ids = (List<string>)Call(null, typeof(ExtendedChannelDefectSummaryBuilder), "ExtractAllTbodyIds", inventories[i].Item1)!;
            Equal(a, $"tbody-inventory-{i}", inventories[i].Item2,
                i == 3 ? (ids.Count == 1 && ids[0] == "" ? "" : "incorrect empty-id inventory") : string.Join("|", ids));
        }
    });
}
