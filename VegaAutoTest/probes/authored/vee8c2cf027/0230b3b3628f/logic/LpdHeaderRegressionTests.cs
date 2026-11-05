using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Vega.AutoTest.Harness;
using Vega.Data.Recipes;
using Xunit;
using Resources = Vega.Utility.Common.Properties.Resources;

namespace Vega.AutoTest.Authored;

[Collection("LPD contracts")]
public class LpdHeaderRegressionTests
{
    private static Type ProductType(string assembly, string name) =>
        Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, assembly + ".dll")).GetTypes().Single(t => t.Name == name);
    private static object? Read(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o);
    private static void Write(object o, string name, object value)
    {
        var property = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null) property.SetValue(o, value);
        else o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(o, value);
    }
    private static string Header(string culture, string unit) => culture == "zh-Hans"
        ? "尺寸阈值（" + unit + "）" : "Sizing Thresholds (" + unit + ")";

    [Fact]
    public void MainMessageHandlerAndBatchSelectionProduceLocalizedUnitHeaders()
    {
        var a = new AssertionRecorder();
        var oldCulture = CultureInfo.CurrentUICulture;
        var oldResourceCulture = Resources.Culture;
        try
        {
            foreach (var row in new[] { (DFSizingUnit.umLSE, "µmLSE"), (DFSizingUnit.nPPM, "nPPM"),
                (DFSizingUnit.rPPM, "rPPM"), (DFSizingUnit.um, "um"), ((DFSizingUnit)12345, "12345") })
                LpdTest.Equal(a, "unit display label", row.Item2, row.Item1.ToUnitLabel());
            var mainType = ProductType("Vega.UI.Inspection", "DFChannelSettingViewModel");
            var batchType = ProductType("Vega.UI.RecipeBatchEditor", "CollectionViewViewModel");
            var handler = mainType.GetMethod("OnLPDSizingUnitChanged", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (string culture in new[] { "en-US", "zh-Hans" })
            {
                CultureInfo.CurrentUICulture = Resources.Culture = CultureInfo.GetCultureInfo(culture);
                // Exercise the handler without constructing the unrelated machine-dependent base services.
                var main = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(mainType);
                mainType.GetField("_collectionName", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(main, "A");
                int changes = 0;
                ((INotifyPropertyChanged)main).PropertyChanged += (_, e) => { if (e.PropertyName == "SizingThresholdsHeader") changes++; };
                foreach (var row in new[] { (DFSizingUnit.umLSE, "µmLSE"), (DFSizingUnit.nPPM, "nPPM"), (DFSizingUnit.rPPM, "rPPM") })
                {
                    var message = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(handler.GetParameters()[1].ParameterType);
                    Write(message, "collectionName", "A");
                    Write(message, "newUnit", row.Item1);
                    changes = 0;
                    handler.Invoke(main, new[] { main, message });
                    string expected = Header(culture, row.Item2);
                    LpdTest.Equal(a, "main matching collection header", expected, (string)Read(main, "SizingThresholdsHeader")!);
                    LpdTest.Equal(a, "main binding notification", 1, changes);
                    Write(message, "collectionName", "Other");
                    Write(message, "newUnit", (DFSizingUnit)12345);
                    changes = 0;
                    handler.Invoke(main, new[] { main, message });
                    LpdTest.Equal(a, "other collection preserves header", expected, (string)Read(main, "SizingThresholdsHeader")!);
                    LpdTest.Equal(a, "other collection emits no header notification", 0, changes);
                }
                var batch = batchType.GetConstructors().Single(c => c.GetParameters().Length == 1).Invoke(new object?[] { null });
                LpdTest.Equal(a, "batch initial Greek-mu header", Header(culture, "μmLSE"), (string)Read(batch, "SizingThresholdsHeader")!);
                foreach (var row in new[] { ("rPPM rPPM", "rPPM"), ("nPPM nPPM", "nPPM"),
                    ("μmLSE nPPM", "μmLSE"), ("", "μmLSE"), (" nPPM", "μmLSE") })
                {
                    Write(batch, "SelectedUnitCombination", row.Item1);
                    LpdTest.Equal(a, "batch selected-unit header", Header(culture, row.Item2), (string)Read(batch, "SizingThresholdsHeader")!);
                }
            }
        }
        finally
        {
            Resources.Culture = oldResourceCulture;
            CultureInfo.CurrentUICulture = oldCulture;
            a.Flush();
        }
    }
}
