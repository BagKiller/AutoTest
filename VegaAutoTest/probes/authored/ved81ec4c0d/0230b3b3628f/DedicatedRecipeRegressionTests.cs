using System;
using System.Globalization;
using System.Linq;
using Vega.AutoTest.Harness;
using Vega.Data.Commons;
using Vega.Data.Recipes;
using Vega.Platform.Database;
using Xunit;

namespace Vega.AutoTest.Authored;

public class DedicatedRecipeRegressionTests
{
    [Fact]
    public void GenerateUsableFixedOpticsAndKeep202OutsideDiagnosticSizeWhitelist()
    {
        var r = new AssertionRecorder();
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var row in new[]
            {
                (Culture: "en-US", Name: "Extra Alignment Scan Recipe"),
                (Culture: "zh-Hans", Name: "额外Alignment扫描配方")
            })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(row.Culture);
                var folder = new BaseFolder();
                DedicatedSupport.Write(folder, "Path", "/Regression/Calibration");
                var recipe = AlignmentScanRecipeGenerator.Generate(folder);
                void E<T>(string id, T expected, T actual)
                    => DedicatedSupport.Equal(r, row.Culture + "-" + id, expected, actual);
                E("id", 202L, recipe.ID);
                E("folder", BaseFolder.CalibrationFolderID, recipe.FolderID);
                E("path", "/Regression/Calibration/", recipe.FolderPath);
                E("name", row.Name, recipe.Name);
                E("collection-count", 1, recipe.CollectionElement.Collections.Count);
                E("manual-run-size", WaferSizeType.WS200, recipe.CollectionElement.WaferSize);
                var c = recipe.CollectionElement.Collections[0];
                E("throughput", ThroughputType.HighSensitivity, c.WaferScan.Throughput);
                E("incidence", IncidenceType.Normal, c.DarkField.SizingContent.Incidence);
                E("polarization", PolarizerType.C, c.DarkField.SizingContent.Polarization);
                E("legal-power-gear", true, new[] { 100.0, 10.0, 1.0 }.Contains(c.DarkField.SizingContent.LaserPower));
                E("edge-exclusion", 0.0, c.DarkField.InspectionRegionContent.EdgeExclusion);
                E("secondary-disabled", false, c.DarkField.InspectionRegionContent.IsSecScanEnable);
                E("manual-run-end-mm", 100.0, c.DarkField.InspectionRegionContent.PriInspRegionEndRadius);
                E("dic-disabled", false, c.BrightField.IsDICEnabled);
                foreach (var mode in new[] { ChannelMode.DN_Normal, ChannelMode.DW2_Normal })
                {
                    var block = c.DarkField.ChannelSettingContent[mode];
                    E(mode + "-exact-block", mode, block.ChannelMode);
                    E(mode + "-enabled", true, block.IsEnabled);
                    E(mode + "-usable-od", true, block.OpticalFilter != CollectODFilterType.ODp4);
                    E(mode + "-usable-pmt", true, double.IsFinite(block.PMTVolatge) && block.PMTVolatge > 0);
                    E(mode + "-n-mask", "Full", block.NMask);
                    E(mode + "-w-mask", "Full", block.WMask);
                }
                E("dw1-disabled", false, c.DarkField.ChannelSettingContent.DW1.IsEnabled);
                E("not-diagnostic-family", false, NotchScanRecipeLookup.IsSystemRecipeID(recipe.ID));
                var ex = Record.Exception(() => NotchScanRecipeLookup.GetExpectedWaferSize(recipe.ID));
                E("no-diagnostic-size", typeof(ArgumentOutOfRangeException), ex?.GetType());
                E("rejected-parameter", "recipeID", ((ArgumentOutOfRangeException)ex!).ParamName);
            }
        }
        finally { CultureInfo.CurrentUICulture = previous; r.Flush(); }
    }
}
