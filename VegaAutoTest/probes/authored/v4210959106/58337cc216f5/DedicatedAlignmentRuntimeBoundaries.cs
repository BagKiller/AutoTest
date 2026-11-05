#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

[Collection("WI3716")]
public sealed class DedicatedAlignmentRuntimeBoundaries
{
    private static object Sequence() => C.Bare(C.Type("Vega.BL.WaferRun.WaferRunSequenceBase, Vega.BL.WaferRun"));
    private static void Configure(object sequence, EdgeDetectionConfig edge)
    {
        var f = C.Field(sequence.GetType(), "_configsManager");
        if (f.FieldType.IsInterface)
            C.Stub(sequence, "_configsManager", (m, _) => m.Name == "get_EdgeDetectionConfig" ? edge : C.Default(m.ReturnType));
        else
        {
            var manager = C.Bare(f.FieldType);
            C.Put(manager, "EdgeDetectionConfig", edge);
            f.SetValue(sequence, manager);
        }
        C.Stub(sequence, "_logger", (m, _) => C.Default(m.ReturnType));
    }

    [Fact]
    public async Task CachedModeMissingRecipeAndEdgeReadRoutingExecuteProductBoundaries()
    {
        var a = new AssertionRecorder();
        try
        {
            (string? Value, bool Expected)[] modes = { ("Inline", false), ("dEdIcAtEd", true), ("bad", false), (null, false) };
            for (int i = 0; i < modes.Length; i++)
            {
                var edge = new EdgeDetectionConfig { NotchScanMode = modes[i].Value! };
                var sequence = Sequence();
                Configure(sequence, edge);
                var property = sequence.GetType().GetProperty("IsDedicatedNotchScan", BindingFlags.Instance | BindingFlags.NonPublic)!;
                C.Eq(a, $"cache.{i}.initial", modes[i].Expected, (bool)property.GetValue(sequence)!);
                edge.NotchScanMode = modes[i].Expected ? "Inline" : "Dedicated";
                C.Eq(a, $"cache.{i}.stable", modes[i].Expected, (bool)property.GetValue(sequence)!);
            }
            foreach (bool empty in new[] { false, true })
            {
                var sequence = Sequence();
                Recipe? source = empty ? new Recipe() : null;
                source?.CollectionElement.Collections.Clear();
                int loads = 0, motionCalls = 0, daqCalls = 0;
                long requested = -1;
                C.Stub(sequence, "_recipeStorageProvider", (m, args) =>
                {
                    if (m.Name != "GetFileByIDAsync") return C.Default(m.ReturnType);
                    loads++;
                    requested = Convert.ToInt64(args[0]);
                    return C.Completed(m.ReturnType.GetGenericArguments()[0], source);
                });
                C.Stub(sequence, "_motion", (m, _) => { motionCalls++; return C.Default(m.ReturnType); });
                C.Stub(sequence, "_dataCollector", (m, _) => { daqCalls++; return C.Default(m.ReturnType); });
                var error = await Record.ExceptionAsync(async () =>
                {
                    await (Task)C.Method(sequence, "DoPerformDedicatedAlignmentPrescanAsync")
                        .Invoke(sequence, new object[] { new WaferRunInfo(), 0 })!;
                });
                string p = empty ? "empty202." : "missing202.";
                C.Eq(a, p + "exception", "VegaBaseException", error?.GetType().Name);
                C.Eq(a, p + "diagnosis", true, error?.Message.Contains("202") == true
                    && error.Message.Contains("missing or has no collection") && error.Message.Contains("Inline"));
                C.Eq(a, p + "requested", 202L, requested);
                C.Eq(a, p + "loads", 1, loads);
                C.Eq(a, p + "noMotion", 0, motionCalls);
                C.Eq(a, p + "noDaq", 0, daqCalls);
            }
            var helper = C.Bare(C.Type("Vega.BL.IWaferRun.WaferRunDeviceControlHelper, Vega.BL.IWaferRun"));
            C.Stub(helper, "_logger", (m, _) => C.Default(m.ReturnType));
            object?[] seen = Array.Empty<object?>();
            ushort[] payload = { 7, 1024, 32768, 65535 };
            C.Stub(helper, "_dataCollector", (m, args) =>
            {
                if (m.Name != "GetEdgeDataGroup") return C.Default(m.ReturnType);
                seen = args.ToArray();
                var parameters = m.GetParameters();
                for (int j = 0; j < parameters.Length; j++)
                    if (parameters[j].ParameterType == typeof(ushort[]).MakeByRefType()) args[j] = payload;
                return true;
            });
            var run = new WaferRunInfo();
            using var cts = new CancellationTokenSource();
            string?[] overrides = { null, "regression-main_PreScan", "" };
            for (int i = 0; i < overrides.Length; i++)
            {
                var result = (ushort[])C.Method(helper, "DoPerformEdgeDataCollectionForChannel").Invoke(helper,
                    new object?[] { run, 0, (int)ChannelMode.DN_Normal, 3, 2, cts.Token, "WI3716", overrides[i] })!;
                string p = $"routing.{i}.";
                C.Eq(a, p + "key", overrides[i] ?? run.GetDataCacheKey(0), (string?)seen[0]);
                C.Eq(a, p + "channel", (int)ChannelMode.DN_Normal, Convert.ToInt32(seen[1]));
                C.Eq(a, p + "start", 3, Convert.ToInt32(seen[2]));
                C.Eq(a, p + "tracks", 2, Convert.ToInt32(seen[3]));
                C.Eq(a, p + "cancellation", cts.Token, (CancellationToken)seen[5]!);
                C.Eq(a, p + "samples", true, payload.SequenceEqual(result));
            }
        }
        finally { a.Flush(); }
    }
}
