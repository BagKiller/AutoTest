using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Galaxy.Platform.IComponent;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.LD.Optics;
using Vega.PD.IAutoFocusDevice;
using Vega.PD.IMotorsDevice;
using Vega.Utility.Common;
using Xunit;

namespace Vega.AutoTest.Authored;

public class BpsRuntimeRegression
{
    [Fact]
    public void RuntimeIssuesCommandsForAllV100CombinationsAndPreservesFailureSemantics()
    {
        BpsContract.Run(a =>
        {
            var table = BpsContract.LoadTable();
            foreach (var incidence in BpsContract.Incidences)
            foreach (var throughput in BpsContract.Throughputs)
            foreach (var polarizer in BpsContract.Polarizers)
            foreach (double power in new[] { 1.0, 10.0, 100.0 })
            {
                var fixture = new RuntimeFixture(table);
                fixture.Device.SetBpsBySettings(incidence, throughput, polarizer, power);
                var expected = BpsContract.Expected(throughput, polarizer, power);
                BpsContract.Equal(a, $"commands/{incidence}/{throughput}/{polarizer}/{power}",
                    RuntimeFixture.Commands(expected.Od, expected.Exposure), fixture.CommandsObserved);
            }

            var overrides = new[] {
                (Od: (BPSROD?)null, Exp: (ushort?)null, ExpectedOd: BPSROD.OD4, ExpectedExp: (ushort)4),
                (Od: (BPSROD?)BPSROD.OD6, Exp: (ushort?)null, ExpectedOd: BPSROD.OD6, ExpectedExp: (ushort)4),
                (Od: (BPSROD?)null, Exp: (ushort?)0, ExpectedOd: BPSROD.OD4, ExpectedExp: (ushort)1),
                (Od: (BPSROD?)null, Exp: (ushort?)10, ExpectedOd: BPSROD.OD4, ExpectedExp: (ushort)9),
                (Od: (BPSROD?)BPSROD.OD2, Exp: (ushort?)ushort.MaxValue, ExpectedOd: BPSROD.OD2, ExpectedExp: (ushort)9),
                (Od: (BPSROD?)BPSROD.OD3, Exp: (ushort?)5, ExpectedOd: BPSROD.OD3, ExpectedExp: (ushort)5)
            };
            foreach (var row in overrides)
            {
                var fixture = new RuntimeFixture(table);
                fixture.Device.SetBpsBySettings(IncidenceType.Normal, ThroughputType.HighSensitivity, PolarizerType.C, 100, row.Od, row.Exp);
                BpsContract.Equal(a, $"override/{row.Od}/{row.Exp}", RuntimeFixture.Commands(row.ExpectedOd, row.ExpectedExp), fixture.CommandsObserved);
            }

            var cached = new RuntimeFixture(table);
            for (int i = 0; i < 2; i++)
                cached.Device.SetBpsBySettings(IncidenceType.Normal, ThroughputType.HighSensitivity, PolarizerType.C, 100);
            BpsContract.Equal(a, "cache/repeated", "motor:44|exposure:4|exposure:4", cached.CommandsObserved);
            cached.Events.Clear();
            cached.Device.SetBpsBySettings(IncidenceType.Normal, ThroughputType.HighThroughput, PolarizerType.P, 100);
            BpsContract.Equal(a, "cache/changed", "motor:11|exposure:1", cached.CommandsObserved);

            foreach (var incidence in BpsContract.Incidences)
            {
                var missing = new RuntimeFixture(new Laser_BpsConfig());
                var error = Record.Exception(() => missing.Device.SetBpsBySettings(incidence, ThroughputType.HighSensitivity, PolarizerType.C, 100));
                BpsContract.Equal(a, "missing/" + incidence, "VegaBaseException", error?.GetType().Name);
                BpsContract.Equal(a, "missing/no-commands/" + incidence, "", missing.CommandsObserved);
            }
            var failure = Enum.GetValues<DeviceCmdResultState>().First(s => s != DeviceCmdResultState.Succeed);
            var afFailure = new RuntimeFixture(table) { ExposureState = failure };
            var afError = Record.Exception(() => afFailure.Device.SetBpsBySettings(IncidenceType.Normal, ThroughputType.HighSensitivity, PolarizerType.C, 100));
            BpsContract.Equal(a, "AF/failure-type", "VegaDeviceRecoveryException", afError?.GetType().Name);
            BpsContract.Equal(a, "AF/attempted-commands", "motor:44|exposure:4", afFailure.CommandsObserved);
            var motorFailure = new RuntimeFixture(table) { MotorState = failure };
            var motorError = Record.Exception(() => motorFailure.Device.SetBpsBySettings(IncidenceType.Normal, ThroughputType.HighSensitivity, PolarizerType.S, 100));
            BpsContract.Equal(a, "motor/failure-type", "VegaDeviceException", motorError?.GetType().Name);
            BpsContract.Equal(a, "motor/stops-before-AF", "motor:44", motorFailure.CommandsObserved);

            var constructor = typeof(IlluminationOptics).GetConstructors().Single();
            var legacy = (IlluminationOpticsBase)constructor.Invoke(constructor.GetParameters()
                .Select(p => p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray());
            BpsContract.InstallLogger(legacy);
            foreach (var incidence in BpsContract.Incidences)
            foreach (var throughput in BpsContract.Throughputs)
            foreach (var polarizer in BpsContract.Polarizers)
            {
                if (incidence == IncidenceType.Oblique && polarizer != PolarizerType.C) continue;
                // Null table and device dependencies are sentinels: the legacy no-op gate must not touch them.
                var error = Record.Exception(() => legacy.SetBpsBySettings(incidence, throughput, polarizer, 100));
                BpsContract.Equal<string?>(a, $"legacy/no-op/{incidence}/{throughput}/{polarizer}", null, error?.GetType().FullName);
            }
        });
    }

    private sealed class RuntimeFixture
    {
        internal readonly List<string> Events = new();
        internal DeviceCmdResultState MotorState = DeviceCmdResultState.Succeed;
        internal DeviceCmdResultState ExposureState = DeviceCmdResultState.Succeed;
        internal IlluminationOpticsLiteVega100 Device { get; }
        internal string CommandsObserved => string.Join("|", Events);

        internal RuntimeFixture(Laser_BpsConfig table)
        {
            var optics = new OpticsConfig { BPSRODPosition = new BPSRODPositionConfig {
                OD1 = 11, OD2 = 22, OD3 = 33, OD4 = 44, OD5 = 55, OD6 = 66
            } };
            var motor = (IMotorGeneral)BpsContract.Proxy(typeof(IMotorGeneral), (method, args) => {
                if (method.Name != "MovePosition") throw new InvalidOperationException("Unexpected motor call: " + method.Name);
                Events.Add("motor:" + Convert.ToDouble(args![0]).ToString(CultureInfo.InvariantCulture));
                return new DeviceCmdResult(MotorState, "BPSR-test");
            });
            var af = (IAutoFocusDevice)BpsContract.Proxy(typeof(IAutoFocusDevice), (method, args) => {
                if (method.Name != "SetExposureTime") throw new InvalidOperationException("Unexpected AF call: " + method.Name);
                Events.Add("exposure:" + Convert.ToUInt32(args![0]).ToString(CultureInfo.InvariantCulture));
                return new DeviceCmdResult(ExposureState, "AF-test");
            });
            var configs = (IConfigsManager)BpsContract.Proxy(typeof(IConfigsManager), (method, _) =>
                method.Name == "get_OpticsConfig" ? optics : throw new InvalidOperationException("Unexpected configuration call: " + method.Name));
            Device = new IlluminationOpticsLiteVega100(null!, null!, null!, null!, null!, null!,
                null!, null!, null!, null!, motor, af, null!, null!, configs);
            BpsContract.InstallLogger(Device);
            typeof(IlluminationOpticsLiteVega100).GetField("_laser_BpsConfig", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Device, table);
        }

        internal static string Commands(BPSROD od, ushort exposure)
            => "motor:" + ((Array.IndexOf(BpsContract.Ods, od) + 1) * 11).ToString(CultureInfo.InvariantCulture)
                + "|exposure:" + exposure.ToString(CultureInfo.InvariantCulture);
    }
}
