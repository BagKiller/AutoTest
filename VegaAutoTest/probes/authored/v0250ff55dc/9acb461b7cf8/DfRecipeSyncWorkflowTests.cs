using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using Vega.AutoTest.Harness;
using Xunit;
using CT = FlaUI.Core.Definitions.ControlType;
using R = Vega.Utility.Common.Properties.Resources;

namespace Vega.AutoTest.Authored;

[Collection("DF recipe synchronization")]
public sealed class DfRecipeSyncWorkflowTests : L2TestBase
{
    public DfRecipeSyncWorkflowTests() : base("_wi3670_recipe_sync.txt") { }
    private AutomationElement Desktop => MainWindow!.Automation.GetDesktop();

    [Fact]
    public void QueueSynchronizationValidatesSelectionCancelsAndCopiesTheChosenSize()
    {
        try
        {
            LaunchAndPrepare();
            new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(3));
            Named(MainWindow!, "Calibration").Click();
            var node = Wait(() => MainWindow!.FindAllDescendants(cf => cf.ByControlType(CT.TreeItem)).FirstOrDefault(x => new[] { "DF Calibration", "DF校准", "DF 校准" }.Contains(x.Name)));
            node.DoubleClick();
            Named(MainWindow!, "SetupforCalQueue").AsButton().Invoke();
            var setup = Window("DFCaliSetupWindow");
            var grids = Grids(setup);
            Check("setup-tables", grids.Length == 2, "configuration and size tables", grids.Length.ToString());
            var queue = grids[0];
            foreach (var selected in queue.Patterns.Selection.Pattern.GetSelection()) selected.Patterns.SelectionItem.Pattern.RemoveFromSelection();
            Named(setup, "SyncRecipe").AsButton().Invoke();
            DismissPrompt("SyncRecipeNoConfigItemSelected");
            int count = Rows(queue);
            var keys = Enumerable.Range(0, count).Select(i => Key(queue, i)).ToArray();
            var triples = Enumerable.Range(0, count).Select(i => string.Join("|", Enumerable.Range(1, 3).Select(c => Text(Cell(queue, i, c))))).ToArray();
            var pairs = (from i in Enumerable.Range(0, count) from j in Enumerable.Range(0, count) where i != j && triples[i] == triples[j] select (Source: i, Target: j)).ToArray();
            Check("compatible-fixture", pairs.Length > 0, "two queue items sharing throughput/incidence/incident polarization", pairs.Length.ToString());
            var pair = pairs[0];
            Cell(queue, pair.Target, 1).Click();
            var before = Snapshot(Grids(setup)[1]);
            Check("target-sizes", before.Length > 0, "at least one target size", before.Length.ToString());
            Cell(queue, pair.Source, 1).Click();
            var sourceGrid = Grids(setup)[1];
            var common = Enumerable.Range(0, Rows(sourceGrid)).FirstOrDefault(i => before.Any(x => x.Size == Text(Cell(sourceGrid, i, 1))), -1);
            Check("common-size", common >= 0, "a shared source and target size", common.ToString());
            string chosenSize = Text(Cell(sourceGrid, common, 1));
            Cell(sourceGrid, common, 2).FindFirstDescendant(cf => cf.ByControlType(CT.Button)).AsButton().Invoke();
            var picker = Window("RecipeSelecting");
            var recipes = Wait(() => picker.FindFirstDescendant(cf => cf.ByAutomationId("SelectRecipeView_SelectedRecipeComboBox")));
            Wait(() => recipes.AsComboBox().Items.Length > 0 ? recipes : null);
            recipes.AsComboBox().Select(0);
            Accept(picker);
            sourceGrid = Grids(setup)[1];
            Wait(() => Text(Cell(Grids(setup)[1], common, 2)).Length > 0 ? setup : null);
            Cell(sourceGrid, common, 3).FindFirstDescendant(cf => cf.ByControlType(CT.ComboBox)).AsComboBox().Select("Full Deposit");
            string expectedPath = Text(Cell(Grids(setup)[1], common, 2));
            var original = before.Single(x => x.Size == chosenSize);
            Check("overwrite-fixture", original.Path != expectedPath || original.Region != "Full Deposit", "target business values differ before synchronization", original.Path + ";" + original.Region);
            foreach (string outcome in new[] { "empty", "cancel", "ok" })
            {
                Cell(queue, pair.Source, 1).Click();
                Named(setup, "SyncRecipe").AsButton().Invoke();
                var sync = Window("DFCaliSyncRecipeSelectWindow");
                var targets = Grids(sync)[0];
                var expectedKeys = keys.Where((_, i) => i != pair.Source && triples[i] == triples[pair.Source]).OrderBy(x => x).ToArray();
                var actualKeys = Enumerable.Range(0, Rows(targets)).Select(i => Key(targets, i)).OrderBy(x => x).ToArray();
                Check(outcome + "-filter", expectedKeys.SequenceEqual(actualKeys), string.Join(";", expectedKeys), string.Join(";", actualKeys));
                if (outcome != "empty")
                {
                    int targetIndex = Enumerable.Range(0, Rows(targets)).Single(i => Key(targets, i) == keys[pair.Target]);
                    Cell(targets, targetIndex, 1).Click();
                    var sizes = Grids(sync)[1];
                    Check(outcome + "-details", Rows(sizes) == before.Length, before.Length.ToString(), Rows(sizes).ToString());
                    Named(sync, "UnselectAll", 1).AsButton().Invoke();
                    int sizeIndex = Enumerable.Range(0, Rows(sizes)).Single(i => Text(Cell(sizes, i, 1)) == chosenSize);
                    var sizeCheck = Cell(sizes, sizeIndex, 0).FindFirstDescendant(cf => cf.ByControlType(CT.CheckBox)).AsCheckBox();
                    Check(outcome + "-enabled", sizeCheck.IsEnabled, "shared configured size is enabled", sizeCheck.IsEnabled.ToString());
                    sizeCheck.Toggle();
                    Cell(targets, targetIndex, 0).FindFirstDescendant(cf => cf.ByControlType(CT.CheckBox)).AsCheckBox().Toggle();
                }
                if (outcome == "cancel") Cancel(sync); else Accept(sync);
                if (outcome == "empty") DismissPrompt("SyncRecipeNoTargetSelected");
                Cell(queue, pair.Target, 1).Click();
                var after = Snapshot(Grids(setup)[1]);
                Check(outcome + "-size-count", before.Length == after.Length, before.Length.ToString(), after.Length.ToString());
                foreach (var prior in before)
                {
                    var actual = after.Single(x => x.Size == prior.Size);
                    string path = outcome == "ok" && prior.Size == chosenSize ? expectedPath : prior.Path;
                    string region = outcome == "ok" && prior.Size == chosenSize ? "Full Deposit" : prior.Region;
                    Check(outcome + "-value-" + prior.Size, actual.Path == path && actual.Region == region, path + ";" + region, actual.Path + ";" + actual.Region);
                }
            }
            Named(setup, "Cancel").AsButton().Invoke();
        }
        catch (Exception e)
        {
            Assertions.Fail("workflow", "UIA", "DF queue synchronization workflow", "completed business checks", e.ToString());
            throw;
        }
        finally { Assertions.Flush(); }
    }

    private void Check(string id, bool success, string expected, string actual)
    {
        if (success) Assertions.Pass(id, "UIA", id, expected, actual);
        else Assertions.Fail(id, "UIA", id, expected, actual);
        Assert.True(success, id + ": expected " + expected + "; actual " + actual);
    }
    private static string[] Names(string key) => new[] { "en", "zh-Hans" }.Select(c => R.ResourceManager.GetString(key, CultureInfo.GetCultureInfo(c)) ?? key).Distinct().ToArray();
    private static AutomationElement Named(AutomationElement root, string key, int index = 0) => Wait(() => root.FindAllDescendants(cf => cf.ByControlType(CT.Button).Or(cf.ByControlType(CT.RadioButton))).Where(x => Names(key).Contains(x.Name)).Skip(index).FirstOrDefault());
    private AutomationElement Window(string id) => Wait(() => Desktop.FindFirstDescendant(cf => cf.ByAutomationId(id)));
    private static AutomationElement[] Grids(AutomationElement root) => root.FindAllDescendants(cf => cf.ByControlType(CT.DataGrid));
    private static int Rows(AutomationElement grid) => grid.Patterns.Grid.Pattern.RowCount.Value;
    private static AutomationElement Cell(AutomationElement grid, int row, int column)
    {
        var cell = grid.Patterns.Grid.Pattern.GetItem(row, column);
        if (cell.Patterns.VirtualizedItem.IsSupported) cell.Patterns.VirtualizedItem.Pattern.Realize();
        if (cell.Patterns.ScrollItem.IsSupported) cell.Patterns.ScrollItem.Pattern.ScrollIntoView();
        return cell;
    }
    private static string Text(AutomationElement element)
    {
        var combo = element.FindFirstDescendant(cf => cf.ByControlType(CT.ComboBox));
        if (combo != null) return combo.AsComboBox().SelectedItem?.Name ?? "";
        return string.Join(" ", element.FindAllDescendants(cf => cf.ByControlType(CT.Text)).Select(x => x.Name).Where(x => x.Length > 0 && x != "...").Distinct());
    }
    private static string Key(AutomationElement grid, int row) => string.Join("|", Enumerable.Range(1, 7).Select(c => Text(Cell(grid, row, c))));
    private static (string Size, string Path, string Region)[] Snapshot(AutomationElement grid) => Enumerable.Range(0, Rows(grid)).Select(i => (Text(Cell(grid, i, 1)), Text(Cell(grid, i, 2)), Text(Cell(grid, i, 3)))).ToArray();
    private static void Accept(AutomationElement window) => ActionButton(window, new[] { "OK", "Ok", "确定", "确认" }).AsButton().Invoke();
    private static void Cancel(AutomationElement window) => ActionButton(window, Names("Cancel")).AsButton().Invoke();
    private static AutomationElement ActionButton(AutomationElement root, string[] names) => Wait(() => root.FindAllDescendants(cf => cf.ByControlType(CT.Button)).FirstOrDefault(x => names.Contains(x.Name)));
    private void DismissPrompt(string key)
    {
        var message = Wait(() => Desktop.FindAllDescendants(cf => cf.ByControlType(CT.Text)).FirstOrDefault(x => Names(key).Contains(x.Name)));
        Check(key, Names(key).Contains(message.Name), string.Join(" / ", Names(key)), message.Name);
        var window = message;
        while (window.Properties.ControlType.Value != CT.Window) window = window.Parent;
        Accept(window);
    }
    private static AutomationElement Wait(Func<AutomationElement?> find)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        do { var result = find(); if (result != null) return result; Thread.Sleep(100); } while (DateTime.UtcNow < deadline);
        throw new TimeoutException("Required DF synchronization control or data did not become available.");
    }
}
