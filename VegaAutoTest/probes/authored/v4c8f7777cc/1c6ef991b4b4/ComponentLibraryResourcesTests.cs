using System;
using System.Globalization;
using System.Resources;
using Vega.AutoTest.Harness;
using Vega.Utility.Common;
using Xunit;
using Xunit.Sdk;

namespace Vega.AutoTest.Authored;

public sealed class ComponentLibraryResourcesTests
{
    [Fact]
    public void RetiredResourceFallsBackInBothLanguagesWhileExistingResourceStillLocalizes()
    {
        var assertions = new AssertionRecorder();
        var original = CultureInfo.CurrentUICulture;
        try
        {
            var resources = new ResourceManager("Vega.Utility.Common.Properties.Resources", typeof(Localizer).Assembly);
            foreach (var row in new[] {
                (Culture: "", Existing: "Current Component(s)"),
                (Culture: "en-US", Existing: "Current Component(s)"),
                (Culture: "zh-Hans", Existing: "当前的组件") })
            {
                var culture = CultureInfo.GetCultureInfo(row.Culture);
                CultureInfo.CurrentUICulture = culture;
                Verify(row.Culture + "/removed", resources.GetString("ComponentLibrary", culture) == null,
                    "No localized ComponentLibrary resource", resources.GetString("ComponentLibrary", culture) ?? "<null>");
                Verify(row.Culture + "/fallback", Localizer.ToLocal("ComponentLibrary") == "ComponentLibrary",
                    "ComponentLibrary", Localizer.ToLocal("ComponentLibrary"));
                Verify(row.Culture + "/control", Localizer.ToLocal("CurrentComponent") == row.Existing,
                    row.Existing, Localizer.ToLocal("CurrentComponent"));
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
            assertions.Flush();
        }

        void Verify(string id, bool success, string expected, string actual)
        {
            if (!success)
            {
                assertions.Fail(id, "localization", "Compiled resource lookup and production Localizer behavior", expected, actual);
                throw new XunitException(id + ": " + actual);
            }
            assertions.Pass(id, "localization", "Compiled resource lookup and production Localizer behavior", expected, actual);
        }
    }
}
