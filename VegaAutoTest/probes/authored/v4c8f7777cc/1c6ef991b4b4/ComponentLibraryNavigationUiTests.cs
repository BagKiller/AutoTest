using System;
using System.Linq;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Authored;

public sealed class ComponentLibraryNavigationUiTests : CommitUiBase
{
    public ComponentLibraryNavigationUiTests() : base("_wi3734_navigation.txt") { }

    [Fact]
    public void RetiredEntryIsAbsentAndNeighboringNavigationStillOpensDocuments()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).Watch(Vega).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            Check("startup-retired-label", !IsRetiredLabel(TextOf(MainWindow!)),
                "No retired Component Library label", TextOf(MainWindow!));

            foreach (var section in new[] { "DiagnosticsMenuItem", "ConfigurationMenuItem", "InstrumentStatusMenuItem" })
            {
                Menu("ExplorerMenuItem", section);
                Until(section + " tree", () => MainWindow!.FindAllDescendants(
                    cf => cf.ByControlType(ControlType.TreeItem)).Length > 0);
                ExpandNavigation();
                var items = MainWindow!.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
                var labels = string.Join(" | ", items.Select(TextOf));
                Check(section + "/retired-entry", !IsRetiredLabel(labels),
                    "No ComponentLibrary, Component Library, or 组件库 entry", labels);
                var leaf = items.FirstOrDefault(item => item.IsEnabled && !string.IsNullOrWhiteSpace(NameOf(item))
                    && item.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem)).Length == 0);
                Require(section + " leaf", leaf != null, "An enabled neighboring navigation leaf");
                var expectedTitle = NameOf(leaf!);
                leaf!.DoubleClick();
                bool Opened() => MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                    .Any(tab => NameOf(tab) == expectedTitle || tab.FindAllDescendants()
                        .Any(child => NameOf(child) == expectedTitle));
                Until(section + " document", Opened);
                Check(section + "/document", Opened(), "Opened document tab: " + expectedTitle,
                    string.Join(" | ", MainWindow.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem)).Select(TextOf)));
            }
        }
        finally { Assertions.Flush(); }
    }
}
