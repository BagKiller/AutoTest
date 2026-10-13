// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3575：Haze 校准配置界面应为入射偏振不匹配的配方拦截继续。
/// 预期（工作项复现步骤）：Calibration → Haze Calibration Setup，为配置项指定
/// 入射偏振不一致的配方后点 Continue，弹出 Error recipe / IncPolarizer，且不继续。
/// 修复已合入本地历史 10dab910（PR 4840）。本用例启动当前 Vega.exe 走这条界面。
/// </summary>
public sealed class WorkItem3575L2 : L2TestBase
{
    public WorkItem3575L2() : base("_wi3575_l2.txt") { }

    [Fact]
    public void MismatchedIncidentPolarizationBlocksContinue()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        OpenHazeCalibration();
        OpenSetupWindow();

        var setup = FindWindow("XHazeCalibSetupWindow");
        Assert.True(setup is not null, "未打开 Haze Calibration Setup（XHazeCalibSetupWindow）");
        Assertions.Pass("haze.setup.window", "ui-expect",
            "打开 Haze Calibration Setup", "XHazeCalibSetupWindow", "已打开");

        SelectConfigAndAssignMismatchedRecipe(setup!);

        var (title, message) = WaitMessageDialog(TimeSpan.FromSeconds(12));
        if (!IsPolarizerBlock(title, message))
        {
            var continueButton = FindByName(setup!, "继续") ?? FindByName(setup!, "Continue");
            Assert.True(continueButton is not null, "设置窗里没有继续按钮");
            Log.Step("指定配方后未立即拦截，点击继续");
            InvokeSafely(continueButton!);
            (title, message) = WaitMessageDialog(TimeSpan.FromSeconds(12));
        }

        Log.Step($"观察结果 title='{title}' message='{message}'");
        if (IsPolarizerBlock(title, message))
        {
            Assertions.Pass("haze.inc-polarizer", "ui-expect",
                "入射偏振不匹配时阻止继续并提示 IncPolarizer",
                "IncPolarizer / not compatible", $"{title} | {message}");
            return;
        }

        Assertions.Fail("haze.inc-polarizer", "ui-expect",
            "入射偏振不匹配时阻止继续并提示 IncPolarizer",
            "IncPolarizer / not compatible",
            string.IsNullOrEmpty(title) && string.IsNullOrEmpty(message)
                ? "未出现配方错误提示"
                : $"{title} | {message}");
        Assert.Fail($"未观察到入射偏振拦截。实际弹窗 title='{title}' message='{message}'");
    }

    private void OpenHazeCalibration()
    {
        var explorer = Waits.ForElement(MainWindow!, "ExplorerMenuItem", TimeSpan.FromSeconds(30), Log);
        Assert.True(explorer is not null, "未找到 Explorer 菜单");
        TryExpand(explorer!);

        var calibration = Waits.ForElement(explorer!, "CalibrationMenuItem", TimeSpan.FromSeconds(15), Log)
            ?? Waits.ForElement(MainWindow!, "CalibrationMenuItem", TimeSpan.FromSeconds(10), Log);
        Assert.True(calibration is not null, "未找到校准菜单");
        InvokeSafely(calibration!);
        Log.Step("已打开校准");
        AcceptSectionSwitch(TimeSpan.FromSeconds(8));

        // CalibrationTreeNode.json 的 Header 是 HazeNormAndDriftCalibration，
        // 中文界面 AutomationId 为「Haze Norm and Drift校准」，ViewModel 仍是 HazeCalibrationViewModel。
        var haze = WaitById(MainWindow!, TimeSpan.FromSeconds(40),
            "Haze Norm and Drift校准", "HazeNormAndDriftCalibration",
            "Haze Calibration", "Haze校准");
        if (haze is null)
        {
            LogTreeItems(MainWindow!);
        }

        Assert.True(haze is not null, "校准树里没有 Haze 节点");
        try
        {
            haze!.Patterns.ScrollItem.Pattern.ScrollIntoView();
        }
        catch
        {
            // 不支持滚动则直接点
        }

        Log.Step($"打开 Haze 节点 name='{UiProps.Name(haze)}' id='{UiProps.Id(haze)}'");
        haze.DoubleClick();
        Thread.Sleep(2000);
        Dialogs.HandleAll(MainWindow!);
    }

    private void OpenSetupWindow()
    {
        var setupButton = WaitNamed(MainWindow!, TimeSpan.FromSeconds(30), "设置校准队列", "Setup for Cal Queue");
        Assert.True(setupButton is not null, "Haze 页没有「设置校准队列」按钮");
        Log.Step("点击设置校准队列");
        InvokeSafely(setupButton!);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < TimeSpan.FromSeconds(20))
        {
            if (FindWindow("XHazeCalibSetupWindow") is not null)
            {
                Log.Step("Haze Calibration Setup 已打开");
                return;
            }

            Thread.Sleep(500);
        }
    }

    /// <summary>
    /// 选中一条光学配置和晶圆，打开配方选择，指定一条配方。
    /// CheckRecipe 在配方确认时就会跑；入射偏振不一致应立刻弹出 IncPolarizer。
    /// </summary>
    private void SelectConfigAndAssignMismatchedRecipe(AutomationElement setup)
    {
        var selectAllConfig = FindByName(setup, "选择所有");
        if (selectAllConfig is not null)
        {
            Log.Step("勾选全部光学配置");
            InvokeSafely(selectAllConfig);
            Thread.Sleep(800);
        }

        var grids = setup.FindAllDescendants(cf => cf.ByControlType(ControlType.DataGrid));
        Assert.True(grids.Length >= 1, "设置窗里没有光学配置表");
        var optics = grids[0];
        var row = WaitGridRow(optics, TimeSpan.FromSeconds(8));
        if (row is null)
        {
            LogGridShape(optics);
        }

        Assert.True(row is not null, "光学配置表没有行，无法指定配方");
        Log.Step($"选中光学配置行: {RowText(row!)}");
        SelectElement(row!);
        Thread.Sleep(800);

        var selectWafers = FindByName(setup, "全选");
        Assert.True(selectWafers is not null, "配方选择区没有「全选」");
        Log.Step("勾选该配置下的晶圆");
        InvokeSafely(selectWafers);
        Thread.Sleep(800);

        var pick = WaitNamed(setup, TimeSpan.FromSeconds(8), "...");
        Assert.True(pick is not null, "晶圆行上没有配方选择按钮（...），晶圆可能未勾选");
        Log.Step("打开配方选择");
        InvokeSafely(pick!);

        var recipeWindow = WaitWindowById("RecipeSelecting", TimeSpan.FromSeconds(20));
        Assert.True(recipeWindow is not null, "未打开配方选择窗 RecipeSelecting");
        AssignFirstRecipe(recipeWindow!);
    }

    private void AssignFirstRecipe(AutomationElement recipeWindow)
    {
        var combo = recipeWindow.FindFirstDescendant(cf => cf.ByAutomationId("SelectRecipeView_SelectedRecipeComboBox"));
        if (combo is not null)
        {
            try
            {
                var box = combo.AsComboBox();
                box.Expand();
                Thread.Sleep(600);
                if (box.Items.Length > 0)
                {
                    var label = box.Items[0].Text;
                    Log.Step($"配方下拉共 {box.Items.Length} 项，选择第一项 '{label}'");
                    box.Items[0].Select();
                    Thread.Sleep(400);
                }
                else
                {
                    Log.Step("配方下拉为空，改为点浏览表第一行");
                    ClickFirstRecipeRow(recipeWindow);
                }
            }
            catch (Exception ex)
            {
                Log.Step($"配方下拉选择失败: {ex.Message}，改为点浏览表");
                ClickFirstRecipeRow(recipeWindow);
            }
        }
        else
        {
            ClickFirstRecipeRow(recipeWindow);
        }

        var ok = recipeWindow.FindFirstDescendant(cf => cf.ByName("确认"))
            ?? recipeWindow.FindFirstDescendant(cf => cf.ByName("OK"))
            ?? recipeWindow.FindFirstDescendant(cf => cf.ByName("确定"));
        Assert.True(ok is not null, "配方选择窗没有确认按钮");
        Log.Step("确认配方");
        InvokeSafely(ok!);
        Thread.Sleep(1500);
    }

    private void ClickFirstRecipeRow(AutomationElement recipeWindow)
    {
        var grid = recipeWindow.FindFirstDescendant(cf => cf.ByAutomationId("SelectRecipeView_SelectedRecipeAndfolderDataGrid"))
            ?? recipeWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataGrid));
        var row = grid?.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
        if (row is null)
        {
            Log.Step("配方浏览表没有行");
            return;
        }

        Log.Step($"点选配方行: {RowText(row)}");
        SelectElement(row);
        row.DoubleClick();
        Thread.Sleep(800);
    }

    private AutomationElement? WaitGridRow(AutomationElement grid, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var row = grid.FindFirstDescendant(cf => cf.ByControlType(ControlType.DataItem));
            if (row is not null)
            {
                return row;
            }

            try
            {
                if (grid.Patterns.Grid.IsSupported && grid.Patterns.Grid.Pattern.RowCount > 0)
                {
                    var cell = grid.Patterns.Grid.Pattern.GetItem(0, 0);
                    return cell.Parent ?? cell;
                }
            }
            catch (Exception ex)
            {
                Log.Step($"读取表格行失败: {ex.Message}");
            }

            Thread.Sleep(500);
        }

        return null;
    }

    private void LogGridShape(AutomationElement grid)
    {
        try
        {
            var rowCount = grid.Patterns.Grid.IsSupported ? grid.Patterns.Grid.Pattern.RowCount : -1;
            var colCount = grid.Patterns.Grid.IsSupported ? grid.Patterns.Grid.Pattern.ColumnCount : -1;
            Log.Step($"光学表 GridPattern {rowCount} 行 {colCount} 列");
            var children = grid.FindAllChildren();
            Log.Step($"光学表直接子节点 {children.Length}");
            foreach (var child in children.Take(12))
            {
                Log.Step($"  child type={child.ControlType} class={child.ClassName} name='{UiProps.Name(child)}'");
            }
        }
        catch (Exception ex)
        {
            Log.Step($"光学表枚举失败: {ex.Message}");
        }
    }

    private static string RowText(AutomationElement row)
    {
        try
        {
            var texts = row.FindAllDescendants(cf => cf.ByControlType(ControlType.Text));
            var parts = texts.Select(t => UiProps.Name(t)).Where(s => !string.IsNullOrWhiteSpace(s)).Take(8);
            var joined = string.Join(" | ", parts);
            return string.IsNullOrWhiteSpace(joined) ? UiProps.Name(row) : joined;
        }
        catch
        {
            return UiProps.Name(row);
        }
    }

    private static void SelectElement(AutomationElement el)
    {
        try
        {
            if (el.Patterns.SelectionItem.IsSupported)
            {
                el.Patterns.SelectionItem.Pattern.Select();
                return;
            }
        }
        catch
        {
            // 落到点击
        }

        el.Click();
    }

    private AutomationElement? WaitWindowById(string automationId, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            var hit = MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            if (hit is not null)
            {
                return hit;
            }

            Thread.Sleep(500);
        }

        return null;
    }

    private (string Title, string Message) WaitMessageDialog(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            AutomationElement[] windows;
            try
            {
                windows = MainWindow!.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            }
            catch
            {
                Thread.Sleep(400);
                continue;
            }

            foreach (var w in windows)
            {
                var title = UiProps.TextOf(w, "TitleTextBlock");
                var message = UiProps.TextOf(w, "MessageTextBlock");
                if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                Log.Step($"发现提示 title='{title}' message='{message}'");
                return (title, message);
            }

            Thread.Sleep(500);
        }

        return ("", "");
    }

    private static bool IsPolarizerBlock(string title, string message)
    {
        var text = title + " " + message;
        return text.Contains("IncPolarizer", StringComparison.Ordinal);
    }

    private AutomationElement? FindWindow(string automationId)
        => MainWindow!.FindFirstDescendant(cf => cf.ByAutomationId(automationId));

    private static AutomationElement? FindByName(AutomationElement root, string name)
        => root.FindFirstDescendant(cf => cf.ByName(name));

    /// <summary>切到校准区时若弹出离开确认，点肯定项。HandleAll 会点「否」，那会留在原界面。</summary>
    private void AcceptSectionSwitch(TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            AutomationElement[] windows;
            try
            {
                windows = MainWindow!.FindAllChildren(cf => cf.ByControlType(ControlType.Window));
            }
            catch
            {
                Thread.Sleep(500);
                continue;
            }

            if (windows.Length == 0)
            {
                Thread.Sleep(500);
                continue;
            }

            foreach (var w in windows)
            {
                if (DialogClassifier.Classify(w) != DialogDecision.Question)
                {
                    Dialogs.HandleAll(MainWindow!);
                    continue;
                }

                var title = UiProps.TextOf(w, "TitleTextBlock");
                var message = UiProps.TextOf(w, "MessageTextBlock");
                var no = w.FindFirstDescendant(cf => cf.ByName("否"))
                    ?? w.FindFirstDescendant(cf => cf.ByName("No"));
                var yes = w.FindFirstDescendant(cf => cf.ByName("确认"))
                    ?? w.FindFirstDescendant(cf => cf.ByName("确定"))
                    ?? w.FindFirstDescendant(cf => cf.ByName("OK"));
                // 是/否 询问默认点否，避免把导航确认掉。OK/Cancel（确认）才表示继续进入校准。
                var choice = no ?? yes;
                Log.Step($"切区确认窗 title='{title}' message='{message}'，点击 '{UiProps.Name(choice)}'");
                if (choice is not null)
                {
                    InvokeSafely(choice);
                }

                Thread.Sleep(1500);
            }

            return;
        }
    }

    private AutomationElement? WaitById(AutomationElement root, TimeSpan timeout, params string[] ids)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            foreach (var id in ids)
            {
                var hit = root.FindFirstDescendant(cf => cf.ByAutomationId(id));
                if (hit is not null)
                {
                    return hit;
                }
            }

            Thread.Sleep(1000);
        }

        return null;
    }

    private void LogTreeItems(AutomationElement root)
    {
        try
        {
            var items = root.FindAllDescendants(cf => cf.ByControlType(ControlType.TreeItem));
            Log.Step($"校准树 TreeItem 数量 {items.Length}");
            foreach (var item in items.Take(40))
            {
                Log.Step($"  tree id='{UiProps.Id(item)}' name='{UiProps.Name(item)}'");
            }
        }
        catch (Exception ex)
        {
            Log.Step($"枚举树失败: {ex.Message}");
        }
    }

    private AutomationElement? WaitNamed(AutomationElement root, TimeSpan timeout, params string[] names)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            foreach (var name in names)
            {
                var hit = root.FindFirstDescendant(cf => cf.ByName(name).And(cf.ByControlType(ControlType.Button)))
                    ?? root.FindFirstDescendant(cf => cf.ByAutomationId(name))
                    ?? root.FindFirstDescendant(cf => cf.ByName(name));
                if (hit is not null)
                {
                    return hit;
                }
            }

            Thread.Sleep(1000);
        }

        return null;
    }

    private void TryExpand(AutomationElement menu)
    {
        try
        {
            if (menu.Patterns.ExpandCollapse.IsSupported)
            {
                menu.Patterns.ExpandCollapse.Pattern.Expand();
                Thread.Sleep(400);
            }
        }
        catch
        {
            Log.Step("菜单展开失败，改为直接查找子项");
        }
    }

    private static void InvokeSafely(AutomationElement el)
    {
        if (el.Patterns.Invoke.IsSupported)
        {
            el.Patterns.Invoke.Pattern.Invoke();
            return;
        }

        el.Click();
    }
}
