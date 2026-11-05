using System;
using System.Threading.Tasks;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.Platform.Database;
using Xunit;

namespace Vega.AutoTest.Authored;

public class DedicatedMissingRecipeRegressionTests
{
    [Fact]
    public async Task MissingOrEmpty202FailsBeforeHardwareAndDoesNotFallBackInline()
    {
        var r = new AssertionRecorder();
        try
        {
            foreach (bool empty in new[] { false, true })
            {
                Recipe? recipe = null;
                if (empty)
                {
                    var folder = new BaseFolder();
                    DedicatedSupport.Write(folder, "Path", "/Regression");
                    recipe = AlignmentScanRecipeGenerator.Generate(folder);
                    recipe.CollectionElement.Collections.Clear();
                }
                var sequence = DedicatedSupport.Sequence();
                int reads = 0, hardwareCalls = 0;
                long requested = -1;
                DedicatedSupport.Inject(sequence, "_recipeStorageProvider", (m, a) =>
                {
                    if (m.Name != "GetFileByIDAsync") throw new InvalidOperationException("Unexpected recipe operation: " + m.Name);
                    reads++;
                    requested = Convert.ToInt64(a[0]);
                    return DedicatedSupport.Completed(m.ReturnType, recipe);
                });
                DedicatedSupport.Inject(sequence, "_dataCollector", (m, a) =>
                {
                    hardwareCalls++;
                    return DedicatedSupport.Default(m);
                });
                var ex = await Record.ExceptionAsync(async () =>
                    await (Task)DedicatedSupport.Call(sequence, "DoPerformDedicatedAlignmentPrescanAsync",
                        new WaferRunInfo { WaferID = "MISSING_202" }, 0)!);
                string id = empty ? "empty" : "missing";
                DedicatedSupport.Equal(r, id + "-failure-type", "VegaBaseException", ex?.GetType().Name);
                DedicatedSupport.Equal(r, id + "-identifies-recipe", true, ex!.Message.Contains("202", StringComparison.Ordinal));
                DedicatedSupport.Equal(r, id + "-explicit-recovery", true, ex.Message.Contains("Inline", StringComparison.Ordinal));
                DedicatedSupport.Equal(r, id + "-requested-id", 202L, requested);
                DedicatedSupport.Equal(r, id + "-single-load", 1, reads);
                DedicatedSupport.Equal(r, id + "-no-acquisition-fallback", 0, hardwareCalls);
                DedicatedSupport.Equal(r, id + "-not-completed", false,
                    (bool)DedicatedSupport.Field(sequence.GetType(), "_dedicatedAlignmentPrescanDone").GetValue(sequence)!);
            }
        }
        finally { r.Flush(); }
    }
}
