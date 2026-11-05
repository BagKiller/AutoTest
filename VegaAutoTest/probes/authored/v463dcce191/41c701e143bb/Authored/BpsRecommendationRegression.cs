using System;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Data.Recipes.Helper;
using Xunit;

namespace Vega.AutoTest.Authored;

public class BpsRecommendationRegression
{
    [Fact]
    public void ModelGateAndExtractedValidationPreserveContract()
    {
        BpsContract.Run(a =>
        {
            foreach (bool v100 in new[] { true, false })
            foreach (var incidence in BpsContract.Incidences)
            foreach (var throughput in BpsContract.Throughputs)
            foreach (var polarizer in BpsContract.Polarizers)
            {
                string id = $"gate/{v100}/{incidence}/{throughput}/{polarizer}";
                bool applicable = v100 || (incidence == IncidenceType.Oblique && polarizer != PolarizerType.C);
                var table = BpsContract.Table(incidence, polarizer, throughput);
                var result = BpsContract.Compute(table, v100, incidence, polarizer, throughput);
                BpsContract.Equal(a, id + "/status", applicable ? BpsRecommendationStatus.Matched : BpsRecommendationStatus.NotApplicable, result.Status);
                BpsContract.Equal(a, id + "/OD", applicable ? BPSROD.OD5 : BPSROD.OD2, result.BpsROD);
                BpsContract.Equal(a, id + "/exposure", (ushort)(applicable ? 7 : 1), result.ExposureTime);
                if (!applicable)
                {
                    var empty = BpsContract.Compute(new Laser_BpsConfig(), false, incidence, polarizer, throughput);
                    BpsContract.Equal(a, id + "/empty/status", BpsRecommendationStatus.NotApplicable, empty.Status);
                    BpsContract.Equal(a, id + "/empty/OD", BPSROD.OD2, empty.BpsROD);
                    BpsContract.Equal(a, id + "/empty/exposure", (ushort)1, empty.ExposureTime);
                }
                if (v100)
                {
                    var error = Record.Exception(() => { BpsContract.Compute(new Laser_BpsConfig(), true, incidence, polarizer, throughput); });
                    BpsContract.Equal(a, id + "/missing-rule-throws", true, error != null);
                }
            }

            var invalid = new[] {
                (Od: BPSROD.OD6, Exp: (ushort)7, Status: BpsRecommendationStatus.OdUnsupported, AppliedOd: BPSROD.OD1, AppliedExp: (ushort)7),
                (Od: BPSROD.OD5, Exp: (ushort)0, Status: BpsRecommendationStatus.ExposureUnsupported, AppliedOd: BPSROD.OD5, AppliedExp: (ushort)1),
                (Od: BPSROD.OD5, Exp: (ushort)10, Status: BpsRecommendationStatus.ExposureUnsupported, AppliedOd: BPSROD.OD5, AppliedExp: (ushort)1),
                (Od: BPSROD.OD6, Exp: (ushort)10, Status: BpsRecommendationStatus.OdUnsupported | BpsRecommendationStatus.ExposureUnsupported, AppliedOd: BPSROD.OD1, AppliedExp: (ushort)1)
            };
            foreach (bool v100 in new[] { true, false })
            foreach (var row in invalid)
            {
                var incidence = v100 ? IncidenceType.Normal : IncidenceType.Oblique;
                var polarizer = v100 ? PolarizerType.C : PolarizerType.P;
                var table = BpsContract.Table(incidence, polarizer, ThroughputType.HighSensitivity, row.Od, row.Exp);
                var result = BpsContract.Compute(table, v100, incidence, polarizer, ThroughputType.HighSensitivity,
                    defaultOd: BPSROD.OD1, supported: new[] { BPSROD.OD1, BPSROD.OD5 });
                string id = $"validation/{v100}/{row.Od}/{row.Exp}";
                BpsContract.Equal(a, id + "/status", row.Status, result.Status);
                BpsContract.Equal(a, id + "/OD", row.AppliedOd, result.BpsROD);
                BpsContract.Equal(a, id + "/exposure", row.AppliedExp, result.ExposureTime);
                BpsContract.Equal<BPSROD?>(a, id + "/raw-OD", row.Od, result.RecommendedBpsROD);
                BpsContract.Equal<ushort?>(a, id + "/raw-exposure", row.Exp, result.RecommendedExposureTime);
            }
        });
    }
}
