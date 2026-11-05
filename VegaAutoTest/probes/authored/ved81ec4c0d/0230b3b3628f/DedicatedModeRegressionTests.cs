using System;
using Vega.AutoTest.Harness;
using Vega.BL.IWaferRun;
using Xunit;

namespace Vega.AutoTest.Authored;

public class DedicatedModeRegressionTests
{
    [Fact]
    public void DefaultsParsingAndSequenceInstanceCaching()
    {
        var r = new AssertionRecorder();
        try
        {
            var edge = DedicatedSupport.EdgeConfig();
            DedicatedSupport.Equal(r, "default-mode", "Inline",
                (string)edge.GetType().GetProperty("NotchScanMode")!.GetValue(edge)!);
            var rows = new (string? Input, NotchScanMode Expected)[]
            {
                ("Dedicated", NotchScanMode.Dedicated), ("dedicated", NotchScanMode.Dedicated),
                ("DeDiCaTeD", NotchScanMode.Dedicated), ("Inline", NotchScanMode.Inline),
                ("inline", NotchScanMode.Inline), (null, NotchScanMode.Inline),
                ("", NotchScanMode.Inline), (" ", NotchScanMode.Inline),
                ("XX", NotchScanMode.Inline), (" Dedicated", NotchScanMode.Inline),
                ("Dedicated ", NotchScanMode.Inline), ("0", NotchScanMode.Inline)
            };
            for (int i = 0; i < rows.Length; i++)
                DedicatedSupport.Equal(r, $"parse-{i}", rows[i].Expected,
                    NotchScanModeResolver.ParseMode(rows[i].Input));
            foreach (var initial in new string?[] { "Dedicated", "Inline", null })
            {
                bool expected = initial == "Dedicated";
                DedicatedSupport.Write(edge, "NotchScanMode", initial);
                var sequence = DedicatedSupport.Sequence();
                DedicatedSupport.Config(sequence, edge);
                DedicatedSupport.Equal(r, $"initial-{initial}", expected,
                    (bool)DedicatedSupport.Call(sequence, "get_IsDedicatedNotchScan")!);
                DedicatedSupport.Write(edge, "NotchScanMode", expected ? "Inline" : "Dedicated");
                DedicatedSupport.Equal(r, $"cached-{initial}", expected,
                    (bool)DedicatedSupport.Call(sequence, "get_IsDedicatedNotchScan")!);
                var fresh = DedicatedSupport.Sequence();
                DedicatedSupport.Config(fresh, edge);
                DedicatedSupport.Equal(r, $"fresh-instance-{initial}", !expected,
                    (bool)DedicatedSupport.Call(fresh, "get_IsDedicatedNotchScan")!);
            }
        }
        finally { r.Flush(); }
    }
}
