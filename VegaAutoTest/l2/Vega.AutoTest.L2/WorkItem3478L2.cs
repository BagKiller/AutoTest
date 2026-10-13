// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// #3478 正式 L2 回归用例：登录就绪 → 打开检测区域 → 打开默认配方文档 →
/// 断言核心 DF 控件可读值 → oracle 交叉验证（UI 显示 mm ≈ 配方内部 mm，±0.011 容差）。
/// </summary>
public sealed class WorkItem3478L2 : L2TestBase
{
    public WorkItem3478L2() : base("_wi3478_l2.txt") { }

    [Fact]
    public void NavigateToInspectionRegion()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));

        var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
        page.OpenInspection();

        AutomationElement? recipeNode = null;
        try
        {
            recipeNode = page.FindRecipeNode(TimeSpan.FromSeconds(60));
        }
        finally
        {
            if (page.IsDocumentLoaded)
            {
                Log.Step("文档已自动打开，跳过双击配方节点");
            }
            else
            {
                Assert.NotNull(recipeNode);
                Log.Step($"双击配方节点: '{UiProps.Name(recipeNode!)}'");
                recipeNode!.DoubleClick();
                Log.Step("  双击已发送");
            }
        }

        page.WaitForDocument(TimeSpan.FromSeconds(90));
        var values = page.ReadRegionValues();
        Assert.True(values.Count >= 2, $"核心 DF 控件只命中 {values.Count}/4，未真正进入检测区域");
        page.CrossCheckWithRecipe(values);
    }
}
