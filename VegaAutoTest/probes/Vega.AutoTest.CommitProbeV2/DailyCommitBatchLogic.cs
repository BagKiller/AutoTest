using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Utility.Helper;

namespace Vega.AutoTest.CommitProbe;

/// <summary>Headless probes of real product binaries; no desktop or hardware interaction.</summary>
public sealed class DailyCommitBatchLogic
{
    static DailyCommitBatchLogic()
    {
        RuntimeHelpers.RunClassConstructor(typeof(WorkItem3644Logic).TypeHandle);
        File.AppendAllText(Path.Combine(RunContext.Dir, "_daily_batch.txt"),
            $"Model={ToolFeaturesHelper.ToolType}; product binary probes plus linked existing product tests. No UI/hardware acceptance.\n");
        var productBin = Path.Combine(Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!, "Vega", "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64");
        foreach (var name in new[] { "Vega.dll", "Vega.BL.IWaferRun.dll", "Vega.Data.Measurements.dll", "Vega.Facade.GEMBase.dll", "Vega.Facade.GEMSim.dll", "Vega.Utility.Common.dll", "Vega.UI.Diagnostics.dll", "Vega.PD.MotorsDevice.dll" })
        {
            var sourceHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(productBin, name)));
            var loadedHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, name)));
            Assert.Equal(Convert.ToHexString(sourceHash), Convert.ToHexString(loadedHash));
            File.AppendAllText(Path.Combine(RunContext.Dir, "_daily_batch.txt"), $"SHA256 {name}={Convert.ToHexString(loadedHash)}\n");
        }
    }

    private static void Equal<T>(string id, T expected, T actual)
    {
        var recorder = new AssertionRecorder();
        recorder.Record("daily." + ToolFeaturesHelper.ToolType + "." + id, "logic", id,
            expected?.ToString() ?? "null", actual?.ToString() ?? "null", null, null,
            EqualityComparer<T>.Default.Equals(expected, actual) ? "PASS" : "FAIL");
        recorder.Flush();
        Assert.Equal(expected, actual);
    }

    // Invoke the repository's existing behavioral test against the copied product DLLs.
    // Both xUnit failures and unexpected exceptions become structured failing assertions.
    [Theory]
    [InlineData("Vega.Utility.Common.Tests.NotchChannelResolverTests")]
    [InlineData("Vega.Facade.GEMBase.Tests.GEMServiceBaseChangeLoadPortAccessModeTests")]
    [InlineData("Vega.Facade.GEMBase.Tests.GEMServiceSimChangeLoadPortAccessModeTests")]
    public void ExistingProductBehaviorTests(string typeName)
    {
        var type = typeof(DailyCommitBatchLogic).Assembly.GetType(typeName, true)!;
        var instance = Activator.CreateInstance(type);
        foreach (var method in type.GetMethods().Where(m => m.IsDefined(typeof(FactAttribute))))
        {
            var rows = method.GetCustomAttributes<InlineDataAttribute>().ToArray();
            var inputs = rows.Length == 0 ? new[] { Array.Empty<object>() } : rows.Select(r => r.GetData(method).Single()).ToArray();
            for (var i = 0; i < inputs.Length; i++)
            {
                var id = type.Name + "." + method.Name + "." + i;
                try { method.Invoke(instance, inputs[i]); Equal(id, true, true); }
                catch (Exception ex)
                {
                    var recorder = new AssertionRecorder();
                    recorder.Fail("daily." + id, "logic", id, "existing behavioral assertions hold", (ex.InnerException ?? ex).ToString());
                    recorder.Flush();
                    throw;
                }
            }
        }
    }

    [Fact]
    public void BfpInitialSizeMatchesModel()
        => Equal("bfp-initial-size", ToolFeaturesHelper.IsVega100 ? WaferSizeType.WS200 : WaferSizeType.WS300,
            BFPCaliConfig.InitCaliConfig().WaferSize);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoOpticsVisibilityHonorsModelAndRole(bool isSuperUser)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Vega.dll");
        var type = Assembly.LoadFrom(path).GetType("Vega.HorizontalToolBarViewModel", true)!;
        var instance = RuntimeHelpers.GetUninitializedObject(type);
        type.GetField("_isSuperUser", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, isSuperUser);
        Equal("auto-optics-superuser-" + isSuperUser, isSuperUser && !ToolFeaturesHelper.IsVega100,
            (bool)type.GetProperty("IsAutoOpticsVisible")!.GetValue(instance)!);
    }

    [Fact]
    public void PmtModelConfigurationPathAndLaserDefault()
    {
        Equal("pmt-config-subpath", ToolFeaturesHelper.IsVega100 ? "Configs/Vega100Configs" : "Configs",
            PMTCaliConditions.ConfigsSubPath.Replace('\\', '/'));
        Equal("pmt-laser-output-default", 100.0, new Vega.Utility.Common.PMTCalConfig().LaserOutputPowerPercent);
    }

    [Fact]
    public void Vega100PmtSourceTablesHaveExpectedConditionsAndValidRatios()
    {
        var root = Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!;
        var dir = Path.Combine(root, "Vega", "Resources", "Configs", "Vega100Configs");
        var conditions = JArray.Parse(File.ReadAllText(Path.Combine(dir, "PMTCaliConditions.json")));
        var formulas = JArray.Parse(File.ReadAllText(Path.Combine(dir, "PMTAttenuationFormulas.json")));
        Equal("pmt-source-condition-count", 14, conditions.Count);
        Equal("pmt-source-full-power-placeholders", true, conditions.All(c => (string?)c["LaserPower"] == "Full"));
        Equal("pmt-source-half-od-present", true, conditions.Any(c => (string?)c["CollectODFilter"] == "ODp0_5"));
        Equal("pmt-source-condition-indices", true, conditions.Select(c => (int)c["Index"]!).SequenceEqual(Enumerable.Range(1, 14)));
        Equal("pmt-source-ratio-references-valid", true, formulas.SelectMany(f => f["Factors"] ?? new JArray())
            .All(f => (int)f["Numerator"]! is >= 1 and <= 14 && (int)f["Denominator"]! is >= 1 and <= 14));
    }

    [Fact]
    public void RunCacheKeysSeparateSameWaferRunsAndStayStableUnderParallelAccess()
    {
        var type = Assembly.Load("Vega.Data.Measurements").GetType("Vega.Data.Measurements.WaferRunInfo", true)!;
        var method = type.GetMethod("GetDataCacheKey")!;
        object Create(long id)
        {
            var item = RuntimeHelpers.GetUninitializedObject(type);
            type.GetProperty("ID")!.SetValue(item, id);
            type.GetProperty("WaferID")!.SetValue(item, "DAILY_WAFER");
            return item;
        }
        string Key(object item, int index) => (string)method.Invoke(item, new object[] { index })!;
        var one = Create(101);
        var two = Create(102);
        Equal("run-key-explicit-id", "DAILY_WAFER_101_3", Key(one, 3));
        Equal("run-key-separated", false, Key(one, 3) == Key(two, 3));
        Equal("run-key-collection-separated", false, Key(one, 3) == Key(one, 4));
        var zero = Create(0);
        var keys = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 256, _ => keys.Add(Key(zero, 5)));
        Equal("run-key-zero-id-first-access-atomic", 1, keys.Distinct().Count());
    }

    [Fact]
    public void XYNotchVisibilityReflectsScanDirection()
    {
        var type = Assembly.Load("Vega.UI.Calibration").GetType("Vega.UI.Calibration.XYCalibrationViewModel", true)!;
        var instance = RuntimeHelpers.GetUninitializedObject(type);
        Equal("outer-to-inner-test-context", ToolFeaturesHelper.IsVega100, ToolFeaturesHelper.IsOuterToInnerScan);
        Equal("xy-notch-region-visible", !ToolFeaturesHelper.IsVega100,
            (bool)type.GetProperty("IsNotchScanRegionVisible")!.GetValue(instance)!);
    }

    [Fact]
    public void Vega300DefaultAndBothJsonMappingsMatchHardwareContract()
    {
        var type = typeof(Vega.Utility.Common.IOBitMappingService);
        // #3655 changes Vega300's fallback. Vega500's established contract is its
        // JSON section, not the unrelated legacy private fallback switch.
        foreach (var model in new[] { "Vega300" })
        {
            var instance = RuntimeHelpers.GetUninitializedObject(type);
            type.GetField("_toolType", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(instance, model);
            var valve = (Dictionary<ValveOutputBit, int>)type.GetMethod("CreateDefaultValveOutputMapping", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null)!;
            var adam = (Dictionary<AdamIOBit, int>)type.GetMethod("CreateDefaultAdamIOMapping", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(instance, null)!;
            Equal(model + ".fallback.slit-output", 21, valve.GetValueOrDefault(ValveOutputBit.Slit, int.MinValue));
            Equal(model + ".fallback.slit-sensor-1", 106, adam.GetValueOrDefault(AdamIOBit.SlitSensor1, int.MinValue));
            Equal(model + ".fallback.slit-sensor-2", 107, adam.GetValueOrDefault(AdamIOBit.SlitSensor2, int.MinValue));
            Equal(model + ".fallback.w1ff-sensor-1", 300, adam.GetValueOrDefault(AdamIOBit.W1FFSensor1, int.MinValue));
            Equal(model + ".fallback.w1ff-sensor-2", 301, adam.GetValueOrDefault(AdamIOBit.W1FFSensor2, int.MinValue));
        }
        var json = JObject.Parse(File.ReadAllText(Path.Combine(Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!,
            "Vega", "Resources", "Configs", "IOBitMappingConfig.json")));
        foreach (var model in new[] { "Vega300", "Vega500" })
        {
            Equal(model + ".json.slit-output", 21, (int?)json[model]?["ValveOutputMapping"]?["Slit"] ?? int.MinValue);
            Equal(model + ".json.slit-sensor-1", 106, (int?)json[model]?["AdamIOMapping"]?["SlitSensor1"] ?? int.MinValue);
            Equal(model + ".json.slit-sensor-2", 107, (int?)json[model]?["AdamIOMapping"]?["SlitSensor2"] ?? int.MinValue);
            Equal(model + ".json.w1ff-sensor-1", 300, (int?)json[model]?["AdamIOMapping"]?["W1FFSensor1"] ?? int.MinValue);
            Equal(model + ".json.w1ff-sensor-2", 301, (int?)json[model]?["AdamIOMapping"]?["W1FFSensor2"] ?? int.MinValue);
        }
    }

    [Fact]
    public void CylinderMappingVisibilityHonorsExplicitCapabilityTokens()
    {
        var type = Assembly.Load("Vega.UI.Diagnostics").GetType("Vega.UI.Diagnostics.DeviceDiagnostics.CylindersDiagnostics.CylinderConfigViewModel", true)!;
        var visible = type.GetMethod("IsValveBitVisible", BindingFlags.Static | BindingFlags.NonPublic)!;
        var expected = ToolFeaturesHelper.IsVega500 || Path.GetFileName(RunContext.Dir) == "logic300-hwv2";
        Equal("slit-capability-context", expected, ToolFeaturesHelper.HasSlitCylinder);
        Equal("w1ff-capability-context", expected, ToolFeaturesHelper.HasW1FFCylinder);
        Equal("slit-map-visible", expected, (bool)visible.Invoke(null, new object[] { ValveOutputBit.Slit })!);
        Equal("w1ff-map-visible", expected, (bool)visible.Invoke(null, new object[] { ValveOutputBit.W1FF1 })!);
    }

    [Fact]
    public void MotorTimeoutMarginIsThirtySeconds_NotPhysicalMotorAcceptance()
    {
        var type = Assembly.Load("Vega.PD.MotorsDevice").GetType("Vega.PD.MotorsDevice.MSRotatingMotorDevice", true)!;
        Equal("ms-timeout-margin-seconds", 30, (int)type.GetField("OscillationTime", BindingFlags.Static | BindingFlags.NonPublic)!.GetRawConstantValue()!);
    }
}
