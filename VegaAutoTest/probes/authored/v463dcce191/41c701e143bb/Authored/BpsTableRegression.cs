using System.Linq;
using Vega.Data.Recipes.Helper;
using Xunit;

namespace Vega.AutoTest.Authored;

public class BpsTableRegression
{
    [Fact]
    public void ExactTargetTableResolvesEveryCombinationAndBoundary()
    {
        BpsContract.Run(a =>
        {
            var table = BpsContract.LoadTable();
            foreach (var incidence in BpsContract.Incidences)
            foreach (var throughput in BpsContract.Throughputs)
            foreach (var polarizer in BpsContract.Polarizers)
            {
                foreach (double power in BpsContract.Probes)
                {
                    string id = $"table/{incidence}/{throughput}/{polarizer}/{power}";
                    var expected = BpsContract.Expected(throughput, polarizer, power);
                    int matches = table.PowerRules.Count(r => r.Incidence == incidence && r.Throughput == throughput &&
                        r.Polarizer == polarizer && !r.BEAFilter.HasValue && power > r.MinLaserPowerPercent && power <= r.MaxLaserPowerPercent);
                    BpsContract.Equal(a, id + "/unique", 1, matches);
                    var (_, od, exposure) = table.FindODAndExpTime(incidence, polarizer, throughput, power, null);
                    BpsContract.Equal(a, id + "/lookup-OD", expected.Od, od);
                    BpsContract.Equal(a, id + "/lookup-exposure", expected.Exposure, exposure);
                    var result = BpsContract.Compute(table, true, incidence, polarizer, throughput, power);
                    BpsContract.Equal(a, id + "/recommendation-status", BpsRecommendationStatus.Matched, result.Status);
                    BpsContract.Equal(a, id + "/recommendation-OD", expected.Od, result.BpsROD);
                    BpsContract.Equal(a, id + "/recommendation-exposure", expected.Exposure, result.ExposureTime);
                }
                foreach (double invalid in new[] { -1.0, 100.1 })
                {
                    var error = Record.Exception(() => { table.FindODAndExpTime(incidence, polarizer, throughput, invalid, null); });
                    BpsContract.Equal(a, $"outside/{incidence}/{throughput}/{polarizer}/{invalid}", true, error != null);
                }
            }
        });
    }
}
