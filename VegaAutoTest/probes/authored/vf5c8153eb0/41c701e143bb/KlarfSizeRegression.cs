using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vega.AutoTest.Harness;
using Vega.BL.DataReporting.Helper;
using Vega.Data.Defects;
using Vega.Data.Measurements;
using Xunit;

namespace Vega.AutoTest.Authored;

public class KlarfNormalSizeRegression
{
    [Fact]
    public void SaturatedLpdSizesAreExactIntegerTokens()
    {
        KlarfFixture.Run(r =>
        {
            var helper = KlarfFixture.CreateHelper();
            foreach (var culture in new[] { "en-US", "fr-FR" })
            {
                using var scope = new KlarfFixture.CultureScope(culture);
                var samples = new (double Value, bool Missing)[]
                {
                    (0, false), (12.345678, false), (-12.345678, false),
                    (200000.9999, false), (0, true)
                };
                for (int i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    var defect = KlarfFixture.Defect(SizeType.LPD_SATURATED, sample.Value, sample.Missing);
                    var row = KlarfFixture.NormalRow(helper, defect, true, out int index);
                    string id = $"saturated/{culture}/{i}";
                    KlarfFixture.Equal(r, id + "/row",
                        " 8 200000 200000 200000 200000 200000 200000 200000 3.125", row);
                    KlarfFixture.Equal(r, id + "/index", "8", index.ToString(CultureInfo.InvariantCulture));
                }
            }
        });
    }

    [Fact]
    public void OtherSizeTypesPreserveTheirPrecisionAndValues()
    {
        KlarfFixture.Run(r =>
        {
            var helper = KlarfFixture.CreateHelper();
            var types = new[]
            {
                SizeType.UNKNOWN, SizeType.LPD_SIZED, SizeType.LPD_EXTENDED,
                SizeType.PPD_SIZED, SizeType.PPD_SATURATED
            };
            var samples = new (double Value, string Three, string Four)[]
            {
                (12.345678, "12.345", "12.3456"),
                (-12.345678, "-12.345", "-12.3456"),
                (0, "0.000", "0.0000"),
                (200000.9999, "200000.999", "200000.9999")
            };
            foreach (var culture in new[] { "en-US", "fr-FR" })
            {
                using var scope = new KlarfFixture.CultureScope(culture);
                foreach (var type in types)
                {
                    for (int i = 0; i < samples.Length; i++)
                    {
                        var sample = samples[i];
                        var row = KlarfFixture.NormalRow(helper,
                            KlarfFixture.Defect(type, sample.Value, false), true, out int index);
                        string expected = " " + string.Join(" ", new[]
                        {
                            "8", sample.Three, sample.Four, sample.Three,
                            sample.Three, sample.Three, sample.Three, sample.Three, "3.125"
                        });
                        string id = $"ordinary/{culture}/{type}/{i}";
                        KlarfFixture.Equal(r, id + "/row", expected, row);
                        KlarfFixture.Equal(r, id + "/index", "8", index.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        });
    }

    [Fact]
    public void SaturationDoesNotEnableUnselectedAttributes()
    {
        KlarfFixture.Run(r =>
        {
            var row = KlarfFixture.NormalRow(KlarfFixture.CreateHelper(),
                KlarfFixture.Defect(SizeType.LPD_SATURATED, 12.345678, false), false, out int index);
            KlarfFixture.Equal(r, "disabled/row", " 8 0 0 0 0 0 0 0 0", row);
            KlarfFixture.Equal(r, "disabled/index", "8", index.ToString(CultureInfo.InvariantCulture));
        });
    }
}

public class KlarfClusterSizeRegression
{
    [Fact]
    public void SharedSentinelPreservesClusterSizeColumns()
    {
        KlarfFixture.Run(r =>
        {
            var helper = KlarfFixture.CreateHelper();
            var map = new Dictionary<string, string> { { "ID", "ID" } };
            foreach (var attribute in KlarfFixture.SizeAttributes)
                if (attribute != "DSize") map.Add(attribute, attribute);
            var samples = new (double Value, bool Missing)[]
            {
                (0, false), (12.345678, false), (0, true)
            };
            foreach (var culture in new[] { "en-US", "fr-FR" })
            {
                using var scope = new KlarfFixture.CultureScope(culture);
                for (int i = 0; i < samples.Length; i++)
                {
                    var sample = samples[i];
                    var attributes = KlarfFixture.Defect(SizeType.LPD_SATURATED,
                        sample.Value, sample.Missing).Attributes;
                    var row = (string) KlarfFixture.Invoke(helper, "SingleClusterInfoRow",
                        new object[] { attributes, 7, 2, attributes, 3, 0, map, false, 150d, 1d });
                    KlarfFixture.Equal(r, $"cluster/{culture}/{i}",
                        " 7 200000 200000 200000 200000 200000 200000", row);
                }
            }
        });
    }
}

internal static class KlarfFixture
{
    internal static readonly string[] SizeAttributes =
    {
        "DSize", "Size", "Size_DN_Normal", "Size_DW2_Normal",
        "Size_DN_Oblique", "Size_DW1_Oblique", "Size_DW2_Oblique"
    };

    // These formatter paths use no injected services. Avoid starting the application's
    // dependency container; execute the production row builders on deterministic data.
    internal static KlarfSaveHelper CreateHelper() =>
        (KlarfSaveHelper) RuntimeHelpers.GetUninitializedObject(typeof(KlarfSaveHelper));

    internal static DefectInfo Defect(SizeType type, double value, bool missing)
    {
        var defect = new DefectInfo
        {
            DefectName = "LPD", SizeType = type,
            Attributes = new Dictionary<string, double> { { "XSize", 3.1259 } }
        };
        if (!missing)
            foreach (var attribute in SizeAttributes) defect.Attributes.Add(attribute, value);
        return defect;
    }

    internal static string NormalRow(KlarfSaveHelper helper, DefectInfo defect,
        bool enabled, out int index)
    {
        var setting = new ChannelSetting("test", "test", default, 0, false);
        var selected = new HashSet<string>();
        var map = new Dictionary<string, string> { { "ID", "ID" } };
        foreach (var attribute in SizeAttributes)
        {
            string column = attribute == "DSize" ? "DSIZE" :
                attribute == "Size" ? "DEFECTSIZE" : attribute;
            map.Add(column, attribute);
            if (enabled) selected.Add(attribute);
        }
        map.Add("XSIZE", "XSize");
        if (enabled) selected.Add("XSize");
        setting.EnabledDefects.Add(defect.DefectName, selected);
        object[] args = { defect, new AnalysisResult(), setting, map, false, 150d, 7 };
        string row = (string) Invoke(helper, "BuildNormalDefectRow", args);
        index = (int) args[6];
        return row;
    }

    internal static object Invoke(object target, string method, object[] args)
    {
        var member = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(target.GetType().FullName, method);
        return member.Invoke(target, args)!;
    }

    internal static void Equal(AssertionRecorder recorder, string id, string expected, string actual)
    {
        if (string.Equals(expected, actual, StringComparison.Ordinal))
            recorder.Pass(id, "behavior", "KLARF row formatting contract", expected, actual);
        else
            recorder.Fail(id, "behavior", "KLARF row formatting contract", expected, actual);
        Assert.Equal(expected, actual);
    }

    internal static void Run(Action<AssertionRecorder> body)
    {
        var recorder = new AssertionRecorder();
        try { body(recorder); }
        catch (Exception exception)
        {
            recorder.Fail("execution", "behavior", "Production formatter execution",
                "All row expectations satisfied", exception.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    internal sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentCulture;
        internal CultureScope(string name) => CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        public void Dispose() => CultureInfo.CurrentCulture = previous;
    }
}
