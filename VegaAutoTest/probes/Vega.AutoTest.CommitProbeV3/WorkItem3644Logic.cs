using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Vega.AutoTest.Harness;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.UI.RecipeBatchEditor.Converters;
using Vega.Utility.Helper;
using Vega.Utility.Common;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Vega.AutoTest.CommitProbe;

/// <summary>
/// Focused logic coverage, not complete UI acceptance of #3644.
/// Runner launches a fresh process for each model. Only the test-host config is written;
/// production configuration, persisted calibration data and hardware are not touched.
/// </summary>
public sealed class WorkItem3644Logic
{
    private static readonly string Model = Path.GetFileName(RunContext.Dir).ToLowerInvariant() switch {
        "logic500" => "Vega500", "logic300" or "logic300-hwv2" => "Vega300", "logic-none" => "Standalone", _ => "Vega100"
    };
    private static WaferSizeType Expected => Model == "Vega100" ? WaferSizeType.WS200 : WaferSizeType.WS300;

    static WorkItem3644Logic()
    {
        var config = new {
            ToolConfigCollection = new { SelectedToolType = Model, ToolConfigs = new[] {
                new { ToolType = Model, RootCondition = $"{Model},Simulator,GemSim,Gen2", Features = "EFEM" +
                    (Model == "Vega100" ? ",OuterToInner,LaserPIC" : "") +
                    (Model == "Vega500" || Path.GetFileName(RunContext.Dir) == "logic300-hwv2" ? ",HasSlitCylinder,HasW1FFCylinder" : "") }
            } },
            Components = new[] { new {
                Name = "LogService", Interface = "Galaxy.Platform.ILogging.ILogService, Galaxy.Platform.ILogging",
                Implementation = typeof(Vega.AutoTest.Spike.StubLogService).AssemblyQualifiedName, RunOrder = 0, Condition = ""
            }, new {
                Name = "AccessControlService", Interface = "Galaxy.Platform.IAccessControl.IAccessControlService, Galaxy.Platform.IAccessControl",
                Implementation = typeof(ModelMarkersService).AssemblyQualifiedName, RunOrder = 1, Condition = ""
            } }
        };
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "ComponentConfig.json"), json);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Galaxy_ComponentConfig.json"), json);
        File.AppendAllText(Path.Combine(RunContext.Dir, "_wi3644_logic.txt"),
            $"Model={Model}; binary directory={Environment.GetEnvironmentVariable("VEGA_SW_ROOT")}\\Vega\\bin; batch converter=linked current source.\n" +
            "Scope excludes full XY/XYAuto/Ecam UI and real hardware; KTStyle helper and BeamStability validation are invoked in isolation.\n");
        var binaryDir = Path.Combine(Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!, "Vega", "bin", "Debug", "net8.0-windows10.0.19041.0", "win-x64");
        foreach (var file in new[] { "Vega.Data.Calibration.dll", "Vega.Data.Recipes.dll", "Vega.UI.Calibration.dll", "Vega.Utility.Helper.dll" })
        {
            var original = Path.Combine(binaryDir, file);
            var loadedCopy = Path.Combine(AppContext.BaseDirectory, file);
            var expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(original)));
            var actualHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(loadedCopy)));
            if (expectedHash != actualHash) throw new InvalidOperationException($"Binary copy mismatch: {file}");
            File.AppendAllText(Path.Combine(RunContext.Dir, "_wi3644_logic.txt"), $"SHA256 {file}={actualHash}\n");
        }
    }

    private static void Equal<T>(string id, T expected, T actual)
    {
        var recorder = new AssertionRecorder();
        var passed = EqualityComparer<T>.Default.Equals(expected, actual);
        recorder.Record($"wi3644.{Model}.{id}", "logic", id, expected?.ToString() ?? "null", actual?.ToString() ?? "null", null, null, passed ? "PASS" : "FAIL");
        recorder.Flush();
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ModelContextAndDefaultAreCorrect()
    {
        Equal("tool-context", Model, ToolFeaturesHelper.ToolType);
        Equal("model-condition-resolves", true, VegaValidHelper.IsValid(Model));
        Equal("other-model-excluded", false, VegaValidHelper.IsValid(Model == "Vega100" ? "Vega500" : "Vega100"));
        Equal("default-size", Expected, WaferSizeConfigService.Instance.GetDefaultWaferSize());
        Equal("default-supported", true, WaferSizeConfigService.Instance.GetSupportedWaferSizes().Contains(Expected));
        Equal("300-supported", Model != "Vega100", WaferSizeConfigService.Instance.IsWaferSizeSupported(WaferSizeType.WS300));
    }

    [Fact]
    public void XYInitialConfigurationUsesModelDefault()
        => Equal("xy-initial-size", Expected, XYCalConfig.InitCalConfig().WaferSize);

    [Fact]
    public void CrossToolFallbackRespectsPresenceOfModelContext()
    {
        Equal("try-default-context", Model == "Standalone" ? (WaferSizeType?)null : Expected,
            WaferSizeConfigService.Instance.TryGetDefaultWaferSize());
        Equal("cross-tool-default", Expected, WaferSizeConfigService.GetDefaultOrFallback());
    }

    [Fact]
    public void Persisted300IsClampedOnlyWhenUnsupported()
    {
        var resolved = WaferSizeType.WS300.ResolveSupportedOrDefault();
        Equal("restored-300-size", Expected, resolved.Effective);
        Equal("restored-300-fallback", Model == "Vega100", resolved.FellBack);
        Equal("supported-200-preserved", (WaferSizeType.WS200, false), WaferSizeType.WS200.ResolveSupportedOrDefault());
        Equal("unknown-size-fallback", (Expected, true), ((WaferSizeType)999).ResolveSupportedOrDefault());
    }

    [Fact]
    public void BatchConverterNullAndInvalidValuesUseModelDefault()
    {
        var converter = new WaferSizeConverter();
        Equal("batch-null-display", Expected.ToString(), converter.Convert(null!, typeof(string), null!, CultureInfo.InvariantCulture));
        Equal("batch-invalid-display", Expected.ToString(), converter.Convert(new object(), typeof(string), null!, CultureInfo.InvariantCulture));
        Equal("batch-null-save", (object)Expected, converter.ConvertBack(null!, typeof(WaferSizeType), null!, CultureInfo.InvariantCulture));
        Equal("batch-empty-save", (object)Expected, converter.ConvertBack("", typeof(WaferSizeType), null!, CultureInfo.InvariantCulture));
        Equal("batch-invalid-save", (object)Expected, converter.ConvertBack("invalid", typeof(WaferSizeType), null!, CultureInfo.InvariantCulture));
        Equal("batch-valid-preserved", (object)WaferSizeType.WS200, converter.ConvertBack("WS200", typeof(WaferSizeType), null!, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void KTStyleLookupRequiresExplicitWaferSize()
    {
        var type = Assembly.Load("Vega.UI.Calibration").GetType("Vega.UI.Calibration.KTStyleViewModel", throwOnError: true)!;
        var method = type.GetMethod("GetBFPRValue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var size = method.GetParameters().Single(p => p.Name == "waferSize");
        Equal("bfp-explicit-size", false, size.IsOptional);
    }

    [Fact]
    public void KTStyleUsesMeasuredBfpWidthsForModelSize()
    {
        var type = Assembly.Load("Vega.UI.Calibration").GetType("Vega.UI.Calibration.KTStyleViewModel", true)!;
        var vm = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        var settings = new WaferRunConfig { ThroughputParams = new ThroughputParams {
            Throughputs = [ThroughputType.StandardThroughput.ToString()], NormalOmegas = [170], NormalTangentialOmegas = [180]
        } };
        SetDependency(vm, "ConfigsManager", (method, _) => method.Name == "get_WaferRunConfig" ? settings : throw new NotSupportedException(method.Name));
        object? calibration = new BFPCalibrationResult { MeasuredResults = new() {
            [BFPCalibrationResult.GenerateKey(Expected, IncidenceType.Normal, ThroughputType.StandardThroughput, WaferType.Standard)]
                = new BFPMeasurementResult { IsAvailable = true, RMeasWidth = 12, TMeasWidth = 14 }
        } };
        SetDependency(vm, "CalibrationService", (method, _) => method.Name == "GetLatestResult" ? calibration : throw new NotSupportedException(method.Name));
        var lookup = type.GetMethod("GetBFPRValue", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object[] args = [ThroughputType.StandardThroughput, IncidenceType.Normal, Expected, WaferType.Standard];
        var measured = ((double R, double T))lookup.Invoke(vm, args)!;
        Equal("bfp-measured-r", 6.0, measured.R);
        Equal("bfp-measured-t", 7.0, measured.T);
        calibration = null;
        var nominal = ((double R, double T))lookup.Invoke(vm, args)!;
        Equal("bfp-missing-nominal-r", 170.0, nominal.R);
        Equal("bfp-missing-nominal-t", 180.0, nominal.T);
    }

    [Theory]
    [InlineData("empty", 0, 0)]
    [InlineData("unselected", 1, 0)]
    [InlineData("mismatch", 1, 1)]
    public void BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(string scenario, int total, int selected)
    {
        var config = new BeamStabilityCaliConfig { WaferSize = Expected, CaliMode = CalibrationMode.Cal };
        if (total > 0) config.BeamStabilityCaliSequnce.Add(new BeamStabilityCaliConfigItem {
            IsSelected = selected > 0, WaferSize = Expected == WaferSizeType.WS200 ? WaferSizeType.WS300 : WaferSizeType.WS200,
            CalibrationMode = CalibrationMode.Cal
        });
        var type = Assembly.Load("Vega.Calibration.Executor").GetType("Vega.Calibration.Executor.BeamStabilityCalibrationExecutor", true)!;
        var executor = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
        var validate = type.GetMethod("ValidateAndPrepare", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var exception = Record.Exception(() => validate.Invoke(executor, [config]));
        var message = exception is TargetInvocationException invocation ? invocation.InnerException?.Message ?? "" : exception?.Message ?? "";
        Equal($"beam-{scenario}-reject", true, message.Contains("No calibration sequence matches current settings"));
        Equal($"beam-{scenario}-total", true, message.Contains($"queue total={total}"));
        Equal($"beam-{scenario}-selected", true, message.Contains($"selected={selected}"));
    }

    private static void SetDependency(object target, string propertyName, Func<MethodInfo, object?[]?, object?> invoke)
    {
        var property = target.GetType().GetProperty(propertyName)!;
        var proxy = DispatchProxy.Create(property.PropertyType, typeof(DependencyProxy));
        ((DependencyProxy)proxy).InvokeHandler = invoke;
        property.SetValue(target, proxy);
    }
}

public class DependencyProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeHandler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeHandler(targetMethod!, args);
}
