using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Data.Recipes.Helper;
using Vega.Utility.Helper;
using Xunit;

namespace Vega.AutoTest.Authored;

internal static class BpsContract
{
    internal static readonly IncidenceType[] Incidences = { IncidenceType.Normal, IncidenceType.Oblique };
    internal static readonly PolarizerType[] Polarizers = { PolarizerType.P, PolarizerType.S, PolarizerType.C };
    internal static readonly ThroughputType[] Throughputs = { ThroughputType.HighSensitivity, ThroughputType.HighThroughput };
    internal static readonly BPSROD[] Ods = { BPSROD.OD1, BPSROD.OD2, BPSROD.OD3, BPSROD.OD4, BPSROD.OD5, BPSROD.OD6 };
    internal static readonly double[] Probes = { 0, 1, 1.0001, 5, 5.0001, 10, 10.0001, 20, 20.0001, 40, 40.0001, 100 };

    internal static void Run(Action<AssertionRecorder> body)
    {
        var recorder = new AssertionRecorder();
        try { body(recorder); }
        catch (Exception ex)
        {
            recorder.Fail("execution", "behavior", "Production execution or assertion failed", "Contract satisfied", ex.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    internal static void Equal<T>(AssertionRecorder recorder, string id, T expected, T actual)
    {
        string wanted = expected is null ? "null" : expected.ToString()!;
        string observed = actual is null ? "null" : actual.ToString()!;
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            recorder.Fail(id, "behavior", id, wanted, observed);
            Assert.Equal(expected, actual);
        }
        else recorder.Pass(id, "behavior", id, wanted, observed);
    }

    internal static Laser_BpsConfig LoadTable()
    {
        string? root = Environment.GetEnvironmentVariable("VEGA_SW_ROOT");
        string path = string.IsNullOrWhiteSpace(root)
            ? Path.Combine(AppContext.BaseDirectory, "Resources", "Configs", "Vega100Configs", "LaserBpsODConfig.json")
            : Path.Combine(root, "Vega", "Resources", "Configs", "Vega100Configs", "LaserBpsODConfig.json");
        return JsonHelper.Deserialize<Laser_BpsConfig>(path)
            ?? throw new InvalidDataException("Cannot deserialize exact-target BPS table: " + path);
    }

    internal static Laser_BpsConfig Table(IncidenceType incidence, PolarizerType polarizer,
        ThroughputType throughput, BPSROD od = BPSROD.OD5, ushort exposure = 7)
        => new Laser_BpsConfig { PowerRules = new List<Laser_BpsRule> { new Laser_BpsRule {
            Incidence = incidence, Polarizer = polarizer, Throughput = throughput,
            MinLaserPowerPercent = 50, MaxLaserPowerPercent = 100,
            BPSIOD = BPSIOD.OD0, BPSROD = od, ExposureTime = exposure
        } } };

    internal static BpsRecommendation Compute(Laser_BpsConfig table, bool v100, IncidenceType incidence,
        PolarizerType polarizer, ThroughputType throughput, double power = 100,
        BPSROD defaultOd = BPSROD.OD2, ICollection<BPSROD>? supported = null)
    {
        var method = typeof(LaserBpsCalculator).GetMethod("ComputeFromTable", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            return (BpsRecommendation)method.Invoke(null, new object?[] {
                table, v100, incidence, polarizer, throughput, power, null, defaultOd, supported ?? Ods
            })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    // Independent oracle: committed placeholder settings, not measured optical calibration.
    internal static (BPSROD Od, ushort Exposure) Expected(ThroughputType throughput, PolarizerType polarizer, double power)
    {
        if (throughput == ThroughputType.HighSensitivity)
        {
            int band = power > 40 ? 0 : power > 20 ? 1 : power > 5 ? 2 : 3;
            ushort[] exposures = polarizer == PolarizerType.P
                ? new ushort[] { 1, 2, 5, 2 } : new ushort[] { 4, 1, 2, 9 };
            return (BPSROD.OD4, exposures[band]);
        }
        if (polarizer == PolarizerType.P)
            return (BPSROD.OD1, (ushort)(power > 10 ? 1 : 9));
        return power > 10 ? (BPSROD.OD3, (ushort)1)
            : power > 1 ? (BPSROD.OD3, (ushort)9) : (BPSROD.OD2, (ushort)5);
    }

    internal static object Proxy(Type contract, Func<MethodInfo, object?[]?, object?> handler)
    {
        object proxy = DispatchProxy.Create(contract, typeof(BpsPort));
        ((BpsPort)proxy).Handler = handler;
        return proxy;
    }

    internal static void InstallLogger(object component)
    {
        for (Type? type = component.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperty("Logger", BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property == null) continue;
            object logger = Proxy(property.PropertyType, (method, _) =>
                method.ReturnType == typeof(void) || !method.ReturnType.IsValueType
                    ? null : Activator.CreateInstance(method.ReturnType));
            if (property.GetSetMethod(true) != null) { property.SetValue(component, logger); return; }
            var field = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .SingleOrDefault(f => f.FieldType == property.PropertyType);
            if (field != null) { field.SetValue(component, logger); return; }
        }
        throw new InvalidOperationException("Cannot inject the component logging dependency.");
    }
}

public class BpsPort : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
}
