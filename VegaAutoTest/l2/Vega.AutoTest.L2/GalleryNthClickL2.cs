// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using FlaUI.Core.AutomationElements;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.L2;

/// <summary>
/// 点 Gallery 第 N 个 WaferRun（N 由 VEGA_AUTOTEST_GALLERY_INDEX 给，0 基，默认 1）。
/// 打开配方文档让 Gallery 出现 → 列出全部晶圆项 → 先点另一片再点目标，
/// 确认结果区内容跟着变 → 重新列出确认目标仍在原位 → L1 用 WaferID/槽位/开始时间对上 waferRunId。
/// 只读：不删、不清、不导出，不发 /cmd。
/// </summary>
public sealed class GalleryNthClickL2 : L2TestBase
{
    private const string Kind = "ui-value";
    private readonly List<string> _failures = new();

    public GalleryNthClickL2() : base("_gallery_nth_l2.txt") { }

    [Fact]
    public void ClickNthWaferRun()
    {
        LaunchAndPrepare();
        new LoginFlow(Dialogs, States, Log).WaitReady(MainWindow!, TimeSpan.FromMinutes(6));
        OpenRecipeDocument();

        var gallery = new ResultGalleryPage(MainWindow!, Dialogs, Log);
        if (!gallery.WaitForGallery(TimeSpan.FromSeconds(60)))
        {
            Assertions.Fail("gallery.visible", Kind, "打开配方后 Gallery 出现", "出现", "未出现");
            Assert.Fail("Gallery 未出现");
        }

        Assertions.Pass("gallery.visible", Kind, "打开配方后 Gallery 出现", "出现", "出现");

        var items = WaitForItems(gallery, TimeSpan.FromSeconds(60));
        gallery.LogItems(items);
        var selectable = items.Where(i => !i.IsInProcess).ToList();
        if (selectable.Count == 0)
        {
            Assertions.Skip("gallery.list", Kind, "Gallery 至少有一片已完成的 WaferRun",
                $"共 {items.Count} 片，可点 0 片");
            return;
        }

        var parsed = items.Count(i => !string.IsNullOrEmpty(i.Slot) && i.StartTime is not null);
        Check(parsed == items.Count, "gallery.list", "每片都能读出槽位和开始时间",
            $"{items.Count}/{items.Count}", $"{parsed}/{items.Count}");

        var index = TargetIndex(items.Count);
        var target = items[index];
        if (target.IsInProcess)
        {
            Assertions.Skip("gallery.nth.select", Kind, $"点第 {index} 个", $"{target.Key} 正在进行中");
            return;
        }

        var other = selectable.FirstOrDefault(i => i.StartTimeText != target.StartTimeText);
        if (other is null)
        {
            Assertions.Skip("gallery.nth.baseline", Kind, "先点另一片，结果区显示那片而不是目标",
                "没有开始时间不同的另一片，无法区分点没点中");
        }
        else
        {
            gallery.Select(other);
            var showsOther = gallery.WaitResultPaneShows(other, TimeSpan.FromSeconds(20));
            var showsTarget = gallery.ResultPaneShows(target);
            Check(showsOther && !showsTarget, "gallery.nth.baseline",
                $"先点第 {other.Index} 个，结果区显示那片而不是目标",
                $"显示 {other.Key}，不显示 {target.Key}",
                $"显示那片={showsOther}，显示目标={showsTarget}");
        }

        var clicked = gallery.SelectByIndex(index);
        Check(clicked is not null && clicked.Key == target.Key, "gallery.nth.select",
            $"按下标点第 {index} 个", target.Key, clicked?.Key ?? "未点到");

        var shown = clicked is not null && gallery.WaitResultPaneShows(target, TimeSpan.FromSeconds(20));
        Check(shown, "gallery.nth.result", $"点第 {index} 个后结果区显示这片",
            $"{target.Key} job='{target.GroupName}'", shown ? "已显示" : "未显示");

        var again = gallery.ListItems();
        var stillThere = index < again.Count && again[index].Key == target.Key;
        Check(stillThere, "gallery.nth.stable", $"点击后重新列出，目标仍是第 {index} 个",
            target.Key, index < again.Count ? again[index].Key : "越界");

        CrossCheckWithL1(gallery, target);
        Assert.True(_failures.Count == 0, string.Join(" | ", _failures));
    }

    private void OpenRecipeDocument()
    {
        var page = new InspectionRegionPage(MainWindow!, Dialogs, Log, Assertions);
        page.OpenInspection();
        if (page.IsDocumentLoaded)
        {
            Log.Step("文档已自动打开");
            return;
        }

        AutomationElement? node = page.FindRecipeNode(TimeSpan.FromSeconds(60));
        Assert.NotNull(node);
        Log.Step($"双击配方节点 '{UiProps.Id(node!)}'");
        node!.DoubleClick();
        page.WaitForDocument(TimeSpan.FromSeconds(90));
    }

    private IReadOnlyList<GalleryItem> WaitForItems(ResultGalleryPage gallery, TimeSpan timeout)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        IReadOnlyList<GalleryItem> items = Array.Empty<GalleryItem>();
        while (sw.Elapsed < timeout)
        {
            items = gallery.ListItems();
            if (items.Count > 0)
            {
                return items;
            }

            Thread.Sleep(2000);
        }

        return items;
    }

    private int TargetIndex(int count)
    {
        var raw = Environment.GetEnvironmentVariable("VEGA_AUTOTEST_GALLERY_INDEX");
        var wanted = int.TryParse(raw, out var n) ? n : 1;
        var index = Math.Clamp(wanted, 0, count - 1);
        Log.Step($"目标下标：要求 {wanted}（{(string.IsNullOrEmpty(raw) ? "默认" : "环境变量")}），实际 {index}");
        return index;
    }

    private void CrossCheckWithL1(ResultGalleryPage gallery, GalleryItem target)
    {
        if (!L1Client.WaitForReady(90))
        {
            Assertions.Skip("gallery.nth.l1", "l1-query", "L1 用 WaferID/槽位/开始时间对上 waferRunId", "L1 未就绪");
            return;
        }

        var id = gallery.ResolveWaferRunId(target);
        Check(id is not null, "gallery.nth.l1", "L1 用 WaferID/槽位/开始时间对上 waferRunId",
            target.Key, id?.ToString() ?? "未对上", "l1-query");
    }

    private void Check(bool ok, string id, string desc, string expected, string actual, string kind = Kind)
    {
        if (ok)
        {
            Assertions.Pass(id, kind, desc, expected, actual);
        }
        else
        {
            Assertions.Fail(id, kind, desc, expected, actual);
            _failures.Add($"{id}: 期望 {expected}，实际 {actual}");
        }

        Log.Step($"{(ok ? "PASS" : "FAIL")} {id}: expected {expected}, actual {actual}");
    }
}
