using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Vega.BL.DataReporting;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.Platform.Helpers;
using Xunit;

namespace Vega.AutoTest.Authored;

public class ShortSummaryMalformedAnchorTests
{
    [Fact]
    public void MissingTbodyDelimitersWarnAndLeaveTheInputUnchanged()
    {
        SummaryTest.Run(a =>
        {
            var method = typeof(ShortChannelDefectSummaryBuilder).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Single(m => m.Name == "TryAppendWaferToExistingLot");
            Type lotType = method.GetParameters().Single(p => p.ParameterType.Name == "LotRunInfo").ParameterType;
            object lot = RuntimeHelpers.GetUninitializedObject(lotType);
            lotType.GetProperty("ID")!.SetValue(lot, 17L);
            lotType.GetProperty("LotName")!.SetValue(lot, "LOT-17");
            var recipe = new Recipe();
            recipe.CollectionElement.Collections.Clear();
            lotType.GetProperty("RecipeSnapshot")!.SetValue(lot, recipe);
            var wafer = SummaryTest.Bare<WaferRunInfo>();
            wafer.ID = 42;
            const string cells = "<table><thead><tr><td class=\"lotSpecial-cell\">LOT-17</td><td class=\"lot-cell\">LOT-17</td><td>LOT-17</td></tr></thead>";
            var rows = new[]
            {
                (Html: cells + "</table>", Message: "tbody not found after lot cell"),
                (Html: cells + "<tbody><tr><td>previous wafer</td></tr>", Message: "tbody close tag not found")
            };
            foreach (var row in rows)
            {
                var logger = SummaryTest.Logger(out var log);
                var builder = SummaryTest.Bare<ShortChannelDefectSummaryBuilder>();
                SummaryTest.Set(builder, "_logger", logger);
                var sb = new StringBuilder(row.Html);
                object?[] args = method.GetParameters().Select(p =>
                    p.ParameterType == lotType ? lot :
                    p.ParameterType == typeof(WaferRunInfo) ? wafer :
                    p.ParameterType == typeof(StringBuilder) ? sb :
                    p.ParameterType == typeof(WaferStaticsSummary) ? new WaferStaticsSummary() :
                    p.ParameterType == typeof(Recipe) ? recipe :
                    p.HasDefaultValue ? p.DefaultValue :
                    throw new NotSupportedException("Unmapped append parameter: " + p.Name)).ToArray();
                object? result = method.Invoke(builder, args);
                SummaryTest.Check(a, result is false, "Malformed short summary requests section rebuild", false, result ?? "null");
                SummaryTest.Check(a, sb.ToString() == row.Html, "Rejected append preserves original HTML", row.Html, sb.ToString());
                var warnings = log.Entries.Where(e => e.Level == "Warning").ToList();
                SummaryTest.Check(a, warnings.Count == 1, "Malformed-anchor warning occurs once", 1, warnings.Count);
                string expected = "TryAppendWaferToExistingLot " + row.Message
                    + ", fallback to lot section rebuild lot=LOT-17 waferRunId=42";
                SummaryTest.Check(a, warnings[0].Text.Contains(expected, StringComparison.Ordinal),
                    "Warning identifies malformed delimiter, lot and wafer", expected, warnings[0].Text);
            }
        });
    }
}
