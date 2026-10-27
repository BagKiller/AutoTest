using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using Vega.Algo.Imaging;
using Vega.AutoTest.Harness;
using Vega.BL.WaferAlignment;
using Vega.BL.WaferRun;
using Vega.Data.Measurements;
using Vega.Data.Recipes;
using Vega.Utility.Common;
using Galaxy.Platform.IAccessControl;
using Galaxy.Platform.ICalibrations;

namespace Vega.AutoTest.IndividualCommits;

public static class Evidence
{
    public static void Check(string id, Action action)
    {
        var recorder = new AssertionRecorder();
        try { action(); recorder.Pass(id, "logic", id, "specified behavior", "matched"); }
        catch (Exception ex) { recorder.Fail(id, "logic", id, "specified behavior", ex.ToString()); throw; }
        finally { recorder.Flush(); }
    }

    public static void FreshBinary(Type type)
    {
        var file = type.Assembly.Location;
        var root = Environment.GetEnvironmentVariable("VEGA_SW_ROOT")!;
        var project = type.Assembly.GetName().Name!;
        var candidates = Directory.GetFiles(Path.Combine(root, project, "bin", "Debug"), Path.GetFileName(file), SearchOption.AllDirectories);
        Assert.NotEmpty(candidates);
        var hash = SHA256.HashData(File.ReadAllBytes(file));
        Assert.Contains(candidates, path => SHA256.HashData(File.ReadAllBytes(path)).SequenceEqual(hash));
    }

    public static void SetField(object obj, string name, object value)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) { field.SetValue(obj, value); return; }
        }
        throw new MissingFieldException(name);
    }
}

public sealed class WorkItem3672Logic
{
    private static WaferAlignmentService Service()
    {
        var service = (WaferAlignmentService)RuntimeHelpers.GetUninitializedObject(typeof(WaferAlignmentService));
        Evidence.SetField(service, "<Logger>k__BackingField", new Vega.AutoTest.Spike.StubLogService());
        Evidence.SetField(service, "<Name>k__BackingField", "WI3672Test");
        Evidence.SetField(service, "<CurrentEdgeScan>k__BackingField", new EdgeScanInfo {
            Pitch = 18, ClampingModeOption = Vega.Data.Commons.ClampingMode.EdgeChuck });
        return service;
    }

    [Fact]
    public void ActualServiceRepeatedCircleFitDoesNotJump() => Evidence.Check("wi3672.actual-service-repeat-10", () => {
        Evidence.FreshBinary(typeof(WaferAlignmentService));
        var points = Circle(720).Select(p => new PointInPolar(Math.Sqrt(p.x*p.x + p.y*p.y), Math.Atan2(p.y,p.x))).ToArray();
        var method = typeof(WaferAlignmentService).GetMethod("FindCOWCircleFitting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var service = Service();
        var expected = ((double x,double y))method.Invoke(service, [points, false])!;
        Assert.InRange(Math.Abs(expected.x - 31), 0, 1);
        Assert.InRange(Math.Abs(expected.y + 47), 0, 1);
        for (int i = 0; i < 10; i++) {
            var actual = ((double x,double y))method.Invoke(service, [points, false])!;
            Assert.Equal(expected, actual);
        }
    });

    [Fact]
    public void ActualServiceTooFewPointsReturnsNaN() => Evidence.Check("wi3672.actual-service-small", () => {
        var method = typeof(WaferAlignmentService).GetMethod("FindCOWCircleFitting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = ((double x,double y))method.Invoke(Service(), [new[] {new PointInPolar(150000,0),new PointInPolar(150000,Math.PI)}, false])!;
        Assert.True(double.IsNaN(result.x)); Assert.True(double.IsNaN(result.y));
    });

    private static List<(double x, double y)> Circle(int count = 120) => Enumerable.Range(0, count)
        .Select(i => { double t = i * 2 * Math.PI / count; return (31 + 150000 * Math.Cos(t), -47 + 150000 * Math.Sin(t)); }).ToList();

    [Fact]
    public void AlgebraicCircleRecoversKnownCenter() => Evidence.Check("wi3672.circle-known-center", () => {
        Evidence.FreshBinary(typeof(CircleModel));
        var result = CircleModel.FitLeastSquares(Circle());
        Assert.NotNull(result);
        Assert.InRange(Math.Abs(result.Cx - 31), 0, 1e-6);
        Assert.InRange(Math.Abs(result.Cy + 47), 0, 1e-6);
        Assert.InRange(Math.Abs(result.Radius - 150000), 0, 1e-6);
    });

    [Fact]
    public void FourShortArcsRecoverKnownCenter() => Evidence.Check("wi3672.four-short-arcs", () => {
        var points = Enumerable.Range(0, 4).SelectMany(q => Enumerable.Range(0, 40).Select(i => {
            double t = q * Math.PI / 2 + (i - 20) * 0.0004;
            return (31 + 150000 * Math.Cos(t), -47 + 150000 * Math.Sin(t)); })).ToList();
        var result = CircleModel.FitLeastSquares(points);
        Assert.NotNull(result);
        Assert.InRange(Math.Abs(result.Cx - 31), 0, 1e-5);
        Assert.InRange(Math.Abs(result.Cy + 47), 0, 1e-5);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void FewerThanThreePointsHasExplicitFallback(int count) => Evidence.Check($"wi3672.small-{count}", () => {
        var data = Circle(count);
        Assert.Null(CircleModel.FitLeastSquares(data));
        var result = new RANSACFilter(3, 9, 1000).Filter(data);
        Assert.True(result.usedFallback); Assert.Equal(data, result.inliers); Assert.Empty(result.outliers);
    });

    [Fact]
    public void CollinearInputDoesNotInventCircle() => Evidence.Check("wi3672.collinear", () => {
        Assert.Null(CircleModel.FitLeastSquares([(0, 0), (1, 1), (2, 2), (3, 3)]));
    });

    [Fact]
    public void RepeatedRansacIsDeterministicWithOutliers() => Evidence.Check("wi3672.ransac-repeat-30", () => {
        var data = Circle(); data.AddRange([(220000, 0), (0, 220000), (-220000, 0)]);
        var filter = new RANSACFilter(24, 9, 1000);
        var expected = filter.Filter(data);
        Assert.False(expected.usedFallback); Assert.Equal(120, expected.inliers.Count); Assert.Equal(3, expected.outliers.Count);
        for (int i = 0; i < 30; i++) {
            var actual = filter.Filter(data);
            Assert.Equal(expected.inliers, actual.inliers); Assert.Equal(expected.outliers, actual.outliers);
            Assert.Equal(expected.usedFallback, actual.usedFallback);
        }
    });

    [Fact]
    public void NoConsensusIsMarkedAsFallback() => Evidence.Check("wi3672.no-consensus", () => {
        var data = Circle(); var result = new RANSACFilter(1000, 9, 20).Filter(data);
        Assert.True(result.usedFallback); Assert.Equal(data, result.inliers); Assert.Empty(result.outliers);
    });
}

public sealed class Commit8d088291Logic
{
    public static IEnumerable<object[]> ProductCases() => typeof(Vega.BL.WaferAlignment.Tests.TestHelpers).Assembly.GetTypes()
        .Where(t => t.Namespace == "Vega.BL.WaferAlignment.Tests")
        .SelectMany(t => t.GetMethods().Where(m => m.GetCustomAttribute<FactAttribute>() != null)
            .Select(m => new object[] { t.FullName!, m.Name }));

    [Theory]
    [MemberData(nameof(ProductCases))]
    public void OriginalRegression(string className, string methodName) => Evidence.Check($"commit8d088291.{className}.{methodName}", () => {
        Evidence.FreshBinary(typeof(WaferAlignmentService));
        var type = typeof(Commit8d088291Logic).Assembly.GetType(className)!;
        try { type.GetMethod(methodName)!.Invoke(Activator.CreateInstance(type), null); }
        catch (TargetInvocationException ex) when (ex.InnerException != null) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); }
    });
}

public sealed class WorkItem3677Logic
{
    [Fact]
    public void ReproPostProcKeepsPersistedPitch() => Evidence.Check("wi3677.repro-preserves-persisted-pitch", () => {
        var sequence = Sequence();
        Evidence.SetField(sequence, "_offlineParams", new OfflineRunParams { Mode = OfflineRunMode.ReproPostProc });
        foreach (var name in new[] { "_scanDataQueues", "_scanLineCount", "_sendTracksDic", "_sendCompletionSemaphores" }) {
            var field = typeof(WaferRunSequenceBase).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
            field.SetValue(sequence, Activator.CreateInstance(field.FieldType));
        }
        var run = new WaferRunInfo { WaferID = "repro-test", LotID = "test" };
        run.AddCollectionRun(new CollectionRunInfo { CollectionName = "persisted", PitchInMicron = 25 });
        var task = (Task)typeof(WaferRunOfflineSequence).GetMethod("SetupForCollectAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sequence, [run, 0, CancellationToken.None])!;
        task.GetAwaiter().GetResult();
        Assert.Equal(25, run[0].PitchInMicron); Assert.Single(run.CollectionRunList);
    });

    static WorkItem3677Logic()
    {
        // Independent headless fixture only. This does not change any Vega session configuration.
        var config = new {
            ToolConfigCollection = new { SelectedToolType = "Vega300", ToolConfigs = new[] {
                new { ToolType = "Vega300", RootCondition = "Vega300,Simulator,GemSim,Gen2", Features = "EFEM" } } },
            Components = new[] { new {
                Name = "LogService", Interface = "Galaxy.Platform.ILogging.ILogService, Galaxy.Platform.ILogging",
                Implementation = "Vega.AutoTest.Spike.StubLogService, Vega.BL.WaferAlignment.Tests", RunOrder = 0, Condition = "" }, new {
                Name = "AccessControlService", Interface = "Galaxy.Platform.IAccessControl.IAccessControlService, Galaxy.Platform.IAccessControl",
                Implementation = "Vega.AutoTest.CommitProbe.ModelMarkersService, Vega.BL.WaferAlignment.Tests", RunOrder = 1, Condition = "" } }
        };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(config);
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Resources"));
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Resources", "ComponentConfig.json"), json);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "Galaxy_ComponentConfig.json"), json);
    }

    [Theory]
    [InlineData(IncidenceType.Normal, 18)]
    [InlineData(IncidenceType.Oblique, 36)]
    public void NewCollectionGetsRecipeThroughputPitch(IncidenceType incidence, double expected) => Evidence.Check("wi3677.populate-recipe." + incidence, () => {
        var recipe = new Recipe();
        var collection = recipe.CollectionElement.Collections[0];
        collection.DarkField.SizingContent.Incidence = incidence;
        collection.BrightField.IsDICEnabled = false;
        foreach (var channel in collection.DarkField.ChannelSettingContent.Collectors) channel.IsEnabled = false;
        var config = (ConfigsManager)RuntimeHelpers.GetUninitializedObject(typeof(ConfigsManager));
        config.WaferRunConfig = new WaferRunConfig { ThroughputParams = new ThroughputParams {
            Throughputs = [collection.WaferScan.Throughput.ToString()], Pitches = [18], ObliquePitches = [36],
            RPMs = [5000], AbondonedTracks = [0], AutoFocusVols = [20], NormalOmegas = [18],
            NormalTangentialOmegas = [5], ObliqueOmegas = [36], ObliqueTangentialOmegas = [5] } };
        config.AnalysisConfig = new AnalysisConfig();
        var sequence = Sequence();
        Evidence.SetField(sequence, "_configsManager", config);
        Evidence.SetField(sequence, "_calibrationService", DispatchProxy.Create<ICalibrationService, DefaultServices>());
        Evidence.SetField(sequence, "_accessControlService", DispatchProxy.Create<IAccessControlService, DefaultServices>());
        var run = new WaferRunInfo { WaferID = "offline-pitch-test", LotID = "test", RecipeSnapshot = recipe };
        typeof(WaferRunOfflineSequence).GetMethod("PopulateCollectionRunFromJson", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(sequence, [run, 0, collection]);
        Assert.Equal(expected, run[0].PitchInMicron);
        Assert.Equal(300000, run[0].WaferSizeInMicron);
    });

    [Theory]
    [InlineData("Wafer_1_202610215854_Oblique_20261021215958.json")]
    [InlineData("Carr003-01_261021194832_Normal_20261021195012.json")]
    public void OriginalIssueJsonKeepsNonzeroFallback(string fileName) => Evidence.Check("wi3677.original-json." + fileName, () => {
        var folder = Environment.GetEnvironmentVariable("VEGA_INDIVIDUAL_EVIDENCE") ?? Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var json = JObject.Parse(File.ReadAllText(Path.Combine(folder, fileName)));
        Assert.Null(json["pitch_size"]); Assert.Null(json["pitchSize"]);
        var collection = new CollectionRunInfo { PitchInMicron = 18 };
        typeof(WaferRunOfflineSequence).GetMethod("ApplyJsonPitch", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(Sequence(), [collection, json]);
        Assert.Equal(18, collection.PitchInMicron);
    });

    private static WaferRunOfflineSequence Sequence()
    {
        var sequence = (WaferRunOfflineSequence)RuntimeHelpers.GetUninitializedObject(typeof(WaferRunOfflineSequence));
        // Only call the isolated JSON-pitch method. No device services or run engine start.
        Evidence.SetField(sequence, "_logger", new Vega.AutoTest.Spike.StubLogService());
        Evidence.SetField(sequence, "_compName", "WI3677Test");
        return sequence;
    }

    [Theory]
    [InlineData("{}", 18)]
    [InlineData("{\"pitch_size\":null}", 18)]
    [InlineData("{\"pitch_size\":20}", 20)]
    [InlineData("{\"pitchSize\":21}", 21)]
    [InlineData("{\"pitch_size\":\"19.5\"}", 19.5)]
    [InlineData("{\"pitch_size\":0}", 18)]
    [InlineData("{\"pitch_size\":-1}", 18)]
    [InlineData("{\"pitch_size\":\"bad\"}", 18)]
    [InlineData("{\"pitch_size\":\"NaN\"}", 18)]
    [InlineData("{\"pitch_size\":\"Infinity\"}", 18)]
    [InlineData("{\"pitch_size\":1.79}", 18)]
    [InlineData("{\"pitch_size\":180.01}", 18)]
    [InlineData("{\"pitch_size\":1.8}", 1.8)]
    [InlineData("{\"pitch_size\":180}", 180)]
    [InlineData("{\"pitch_size\":null,\"pitchSize\":22}", 22)]
    [InlineData("{\"pitch_size\":20,\"pitchSize\":22}", 20)]
    public void JsonPitchPreservesFallbackOrUsesValidValue(string json, double expected) => Evidence.Check("wi3677.json-pitch." + json, () => {
        Evidence.FreshBinary(typeof(WaferRunOfflineSequence));
        var sequence = Sequence(); var collection = new CollectionRunInfo { CollectionName = "test", PitchInMicron = 18 };
        var method = typeof(WaferRunOfflineSequence).GetMethod("ApplyJsonPitch", BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(sequence, [collection, JObject.Parse(json)]);
        Assert.Equal(expected, collection.PitchInMicron);
    });

    [Fact]
    public void ParsingUsesInvariantCulture() => Evidence.Check("wi3677.invariant-culture", () => {
        var old = CultureInfo.CurrentCulture;
        try {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var collection = new CollectionRunInfo { PitchInMicron = 18 };
            typeof(WaferRunOfflineSequence).GetMethod("ApplyJsonPitch", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(Sequence(), [collection, JObject.Parse("{\"pitch_size\":\"19.5\"}")]);
            Assert.Equal(19.5, collection.PitchInMicron);
        } finally { CultureInfo.CurrentCulture = old; }
    });
}

public class DefaultServices : DispatchProxy
{
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        targetMethod!.ReturnType == typeof(void) ? null : targetMethod.ReturnType.IsValueType ? Activator.CreateInstance(targetMethod.ReturnType) : null;
}
