using System;
using System.Collections.Generic;
using System.Threading;
using Vega.AutoTest.Harness;
using Vega.Data.Measurements;
using Xunit;

namespace Vega.AutoTest.Authored;

public class DedicatedCacheReadRegressionTests
{
    [Fact]
    public void EdgeReaderUsesPrescanOverrideWithoutConsumingMainScanData()
    {
        var r = new AssertionRecorder();
        try
        {
            for (int run = 1; run <= 2; run++)
            for (int collection = 0; collection < 2; collection++)
            {
                var wafer = new WaferRunInfo { ID = 7000 + run, WaferID = "REGRESSION_WAFER" };
                string main = wafer.GetDataCacheKey(collection);
                string prescan = main + "_PreScan";
                var buffers = new Dictionary<string, ushort[]>
                {
                    [main] = new ushort[] { 101, (ushort)run, (ushort)collection },
                    [prescan] = new ushort[] { 202, (ushort)run, (ushort)collection },
                    [""] = new ushort[] { 303, (ushort)run, (ushort)collection }
                };
                var helper = DedicatedSupport.Bare(DedicatedSupport.ProductType(
                    "Vega.BL.IWaferRun", "Vega.BL.IWaferRun.WaferRunDeviceControlHelper"));
                DedicatedSupport.Inject(helper, "_logger", (m, a) => DedicatedSupport.Default(m));
                object?[] observed = Array.Empty<object?>();
                DedicatedSupport.Inject(helper, "_dataCollector", (m, a) =>
                {
                    if (m.Name != "GetEdgeDataGroup") throw new InvalidOperationException("Unexpected DAQ call: " + m.Name);
                    observed = (object?[])a.Clone();
                    string key = (string)a[0]!;
                    if (!buffers.Remove(key, out var data)) throw new InvalidOperationException("Cache already consumed: " + key);
                    a[4] = data;
                    return true;
                });
                using var cts = new CancellationTokenSource();
                var rows = new (object? Override, string Key, ushort Marker)[]
                {
                    (prescan, prescan, 202), (Type.Missing, main, 101), ("", "", 303)
                };
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i];
                    var actual = (ushort[])DedicatedSupport.Call(helper, "DoPerformEdgeDataCollectionForChannel",
                        wafer, collection, 123, 4, 2, cts.Token, "DedicatedRegression", row.Override)!;
                    string id = $"run-{run}-collection-{collection}-row-{i}";
                    DedicatedSupport.Equal(r, id + "-key", row.Key, (string)observed[0]!);
                    DedicatedSupport.Equal(r, id + "-channel", 123, (int)observed[1]!);
                    DedicatedSupport.Equal(r, id + "-start", 4, (int)observed[2]!);
                    DedicatedSupport.Equal(r, id + "-tracks", 2, (int)observed[3]!);
                    DedicatedSupport.Equal(r, id + "-token", cts.Token, (CancellationToken)observed[5]!);
                    DedicatedSupport.Equal(r, id + "-payload", $"{row.Marker},{run},{collection}", string.Join(",", actual));
                }
            }
        }
        finally { r.Flush(); }
    }
}
