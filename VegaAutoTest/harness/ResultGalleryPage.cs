// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Newtonsoft.Json.Linq;

namespace Vega.AutoTest.Harness;

/// <summary>
/// Gallery 中的一片 WaferRun 快照。Index 是可选项（非校准组）里从左到右的 0 基序号。
/// Element 只在本次快照内有效：Gallery 重排或新 Job 插入后要重新 ListItems。
/// </summary>
public sealed record GalleryItem(
    int Index,
    int GroupIndex,
    int IndexInGroup,
    string GroupName,
    string Slot,
    string StartTimeText,
    DateTime? StartTime,
    string WaferId,
    string Status,
    bool IsInProcess,
    bool IsCalibrationJob,
    bool IsOffscreen,
    AutomationElement Element)
{
    /// <summary>同一时刻能唯一定位一片的键：槽位 + 开始时间（ItemInfo 原文）。</summary>
    public string Key => $"[{Slot}] {StartTimeText}";

    public override string ToString()
        => $"#{Index} g{GroupIndex}/{IndexInGroup} job='{GroupName}' slot={Slot} start='{StartTimeText}' " +
           $"wafer='{WaferId}' status='{Status}' inProcess={IsInProcess} cali={IsCalibrationJob} offscreen={IsOffscreen}";
}

/// <summary>
/// 页面对象：结果 Gallery（Vega.UI.Inspection/Result/ResultGalleryView.xaml）。
///
/// 源码事实：
/// - Gallery 挂在 MainWindow 的 SharedResultPaneContainer，只有激活的文档是 Inspection 时可见
///   （MainWindow.xaml.cs UpdateSharedResultPaneState）。顶部菜单 GalleryMenuItem 是清结果/Pin，不是入口。
/// - 外层 ItemsControl x:Name=GalleryControl（每个 Job 一组，横向 StackPanel），
///   组内 ItemsControl x:Name=WaferMapControls（每片一个按钮）。
/// - 所有晶圆按钮共用 AutomationId ResultGalleryView_SelectWaferRunCommandButton，
///   每组末尾的 Job Summary 按钮共用 ResultGalleryView_ShowSelectedJobSummaryCommandButton。
///   因此按树顺序切组：一串晶圆按钮 + 一个 Summary 按钮 = 一组。
/// - 按钮子文本：ItemInfo="[槽位]\n开始时间"，其后是 Status（Grade/Passed/Failed，进行中为空）。
///   ToolTip 字符串含 "Wafer: {WaferID} start at:"，UIA 上为 HelpText。
/// - 新 Job Insert(0)，组会重排；校准组（DFCaliJob/HazeCaliJob 前缀）对非 su 用户 Collapsed。
/// - SelectWaferRun 需要 Result_Read 权限，进行中的项点击无效；选中只改边框颜色，UIA 读不到。
/// </summary>
public sealed class ResultGalleryPage
{
    public const string WaferButtonId = "ResultGalleryView_SelectWaferRunCommandButton";
    public const string SummaryButtonId = "ResultGalleryView_ShowSelectedJobSummaryCommandButton";
    public const string GalleryControlId = "GalleryControl";

    private static readonly Regex WaferIdInToolTip = new(@"Wafer:\s*(.*?)\s+start at:", RegexOptions.Compiled);
    private static readonly Regex JobInToolTip = new(@"Job:\s*(.*?)\s+start at:", RegexOptions.Compiled);
    private static readonly Regex ItemInfoPattern = new(@"^\[(?<slot>[^\]]*)\]\s*(?<time>.*)$", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly Window _main;
    private readonly DialogHandler _dialogs;
    private readonly EvidenceLog _log;

    public ResultGalleryPage(Window main, DialogHandler dialogs, EvidenceLog log)
    {
        _main = main;
        _dialogs = dialogs;
        _log = log;
    }

    public static bool IsCalibrationJobName(string? jobName)
        => jobName is not null && (jobName.StartsWith("DFCaliJob", StringComparison.Ordinal)
                                   || jobName.StartsWith("HazeCaliJob", StringComparison.Ordinal));

    /// <summary>
    /// Gallery 根：优先 GalleryControl；WPF 未把 x:Name 暴露成 AutomationId 时，
    /// 退到第一个按钮所在的整片结果区（SharedResultPaneContainer），再退到主窗口。
    /// </summary>
    public AutomationElement? FindRoot()
    {
        try
        {
            return _main.FindFirstDescendant(cf => cf.ByAutomationId(GalleryControlId))
                   ?? _main.FindFirstDescendant(cf => cf.ByAutomationId("SharedResultPaneContainer"));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>等 Gallery 出现（打开配方文档后）。有按钮或空 GalleryControl 都算出现。</summary>
    public bool WaitForGallery(TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            _dialogs.HandleAll(_main);
            var root = FindRoot();
            if (root is not null)
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] Gallery 根 id='{UiProps.Id(root)}' 已出现");
                return true;
            }

            if (SafeFindFirst(_main, WaferButtonId) is not null)
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F0}s] 未找到 GalleryControl，但晶圆按钮已出现");
                return true;
            }

            Thread.Sleep(1000);
        }

        _log.Step($"!! {timeout.TotalSeconds:F0}s 内 Gallery 未出现，dump 主窗口:");
        UiTreeDumper.Dump(_main, maxDepth: 3, _log);
        return false;
    }

    /// <summary>
    /// 读当前 Gallery 全部晶圆项，从左到右。默认去掉校准组（非 su 用户看不到）。
    /// Index 只在去掉之后的序列里编号，即用户眼里的“第几个”。
    /// </summary>
    public IReadOnlyList<GalleryItem> ListItems(bool includeCalibrationJobs = false)
    {
        var root = FindRoot() ?? _main;
        AutomationElement[] buttons;
        try
        {
            buttons = root.FindAllDescendants(cf =>
                cf.ByAutomationId(WaferButtonId).Or(cf.ByAutomationId(SummaryButtonId)));
        }
        catch (Exception ex)
        {
            _log.Step($"!! 枚举 Gallery 按钮失败：{ex.GetBaseException().Message}");
            return Array.Empty<GalleryItem>();
        }

        var raw = new List<(AutomationElement Button, int Group, int InGroup)>();
        var groupNames = new List<string>();
        var groupIndex = 0;
        var inGroup = 0;
        foreach (var button in buttons)
        {
            if (UiProps.Id(button) == SummaryButtonId)
            {
                groupNames.Add(ReadGroupName(button));
                groupIndex++;
                inGroup = 0;
                continue;
            }

            raw.Add((button, groupIndex, inGroup++));
        }

        var items = new List<GalleryItem>();
        var index = 0;
        foreach (var (button, group, pos) in raw)
        {
            var helpText = HelpText(button);
            var groupName = group < groupNames.Count ? groupNames[group] : string.Empty;
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = Match(JobInToolTip, helpText);
            }

            var isCali = IsCalibrationJobName(groupName);
            if (isCali && !includeCalibrationJobs)
            {
                continue;
            }

            var (slot, timeText, status) = ReadButtonTexts(button);
            items.Add(new GalleryItem(
                index++,
                group,
                pos,
                groupName,
                slot,
                timeText,
                ParseTime(timeText),
                Match(WaferIdInToolTip, helpText),
                status,
                string.IsNullOrEmpty(status),
                isCali,
                IsOffscreen(button),
                button));
        }

        return items;
    }

    /// <summary>把 ListItems 结果写进取证日志。</summary>
    public void LogItems(IReadOnlyList<GalleryItem> items)
    {
        _log.Step($"=== Gallery 共 {items.Count} 片（从左到右）===");
        foreach (var item in items)
        {
            _log.Step($"  {item}");
        }
    }

    /// <summary>
    /// 点第 index 个（0 基，不含校准组）。先把下标换成键再点，日志里同时记下标和键。
    /// 进行中的项点了不会生效，默认拒绝。返回被点的那一项；越界或被拒返回 null。
    /// </summary>
    public GalleryItem? SelectByIndex(int index, bool allowInProcess = false)
    {
        var items = ListItems();
        if (index < 0 || index >= items.Count)
        {
            _log.Step($"!! Gallery 只有 {items.Count} 片，第 {index} 个越界");
            return null;
        }

        return Select(items[index], allowInProcess);
    }

    /// <summary>按键（槽位 + 开始时间）或 WaferID 点；适合回归用例，不受重排影响。</summary>
    public GalleryItem? SelectWhere(Func<GalleryItem, bool> match, string what, bool allowInProcess = false)
    {
        var item = ListItems().FirstOrDefault(match);
        if (item is null)
        {
            _log.Step($"!! Gallery 中找不到 {what}");
            return null;
        }

        return Select(item, allowInProcess);
    }

    public GalleryItem? Select(GalleryItem item, bool allowInProcess = false)
    {
        if (item.IsInProcess && !allowInProcess)
        {
            _log.Step($"!! {item.Key} 正在进行中，SelectWaferRun 会忽略，不点");
            return null;
        }

        _dialogs.HandleAll(_main);
        _log.Step($"点击 Gallery 第 {item.Index} 个：{item}");
        if (!InvokeOrClick(item.Element, item.IsOffscreen))
        {
            return null;
        }

        Thread.Sleep(500);
        _dialogs.HandleAll(_main);
        return item;
    }

    /// <summary>
    /// 结果区（ResultView）的文本指纹，用于确认点击后结果区换了内容。
    /// 只读 Text 的 Name，限 400 个，避免缺陷图里大量元素拖慢。
    /// </summary>
    public string ResultPaneFingerprint()
    {
        try
        {
            var pane = _main.FindFirstDescendant(cf => cf.ByClassName("ResultView"));
            if (pane is null)
            {
                return string.Empty;
            }

            var texts = pane.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Take(400)
                .Select(UiProps.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n));
            return string.Join("|", texts);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>结果区是否正在显示 item：标题/字段里能读到它的开始时间，有 Job 名时也要读到。</summary>
    public bool ResultPaneShows(GalleryItem item)
    {
        var texts = ResultPaneFingerprint();
        return !string.IsNullOrEmpty(item.StartTimeText)
               && texts.Contains(item.StartTimeText, StringComparison.Ordinal)
               && (string.IsNullOrEmpty(item.GroupName) || texts.Contains(item.GroupName, StringComparison.Ordinal));
    }

    /// <summary>选中后等结果区切到 item；超时返回 false。</summary>
    public bool WaitResultPaneShows(GalleryItem item, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (ResultPaneShows(item))
            {
                _log.Step($"[{sw.Elapsed.TotalSeconds:F1}s] 结果区已显示 {item.Key} job='{item.GroupName}'");
                return true;
            }

            Thread.Sleep(500);
        }

        _log.Step($"!! {timeout.TotalSeconds:F0}s 内结果区没显示 {item.Key} job='{item.GroupName}'");
        return false;
    }

    /// <summary>
    /// L1 oracle：在最近 limit 片 WaferRun 里找与 item 的 WaferID、槽位、开始时间（秒级）都对上的那片，
    /// 返回 waferRunId；L1 未就绪或没有对上返回 null，原因写日志。
    /// </summary>
    public long? ResolveWaferRunId(GalleryItem item, int limit = 50)
    {
        var (status, body) = L1Client.Get($"query/recent-wafer-runs?limit={limit}");
        if (status != 200)
        {
            _log.Step($"!! recent-wafer-runs HTTP {status}");
            return null;
        }

        JArray runs;
        try
        {
            runs = JObject.Parse(body)["runs"] as JArray ?? new JArray();
        }
        catch (Exception ex)
        {
            _log.Step($"!! recent-wafer-runs 解析失败：{ex.Message}");
            return null;
        }

        foreach (var run in runs)
        {
            var id = run["ID"]?.Value<long?>() ?? run["waferRunId"]?.Value<long?>();
            if (id is null)
            {
                continue;
            }

            var (s, detailBody) = L1Client.Get($"query/wafer-run?id={id}");
            if (s != 200)
            {
                continue;
            }

            JObject detail;
            try
            {
                detail = JObject.Parse(detailBody);
            }
            catch
            {
                continue;
            }

            var waferId = detail["WaferID"]?.ToString() ?? string.Empty;
            var slot = detail["SourceSlotNumber"]?.ToString() ?? string.Empty;
            var start = detail["startTime"]?.Type == JTokenType.Date
                ? detail["startTime"]!.Value<DateTime>()
                : ParseTime(detail["startTime"]?.ToString() ?? string.Empty);

            var sameWafer = string.IsNullOrEmpty(item.WaferId) || waferId == item.WaferId;
            var sameSlot = slot == item.Slot;
            var sameTime = start is not null && item.StartTime is not null
                           && Math.Abs((start.Value - item.StartTime.Value).TotalSeconds) < 1.5;
            if (sameWafer && sameSlot && sameTime)
            {
                _log.Step($"  L1 对上 waferRunId={id} wafer='{waferId}' slot={slot} start={start:O}");
                return id;
            }
        }

        _log.Step($"!! 最近 {runs.Count} 片里没有与 {item.Key} wafer='{item.WaferId}' 对上的 WaferRun");
        return null;
    }

    private (string Slot, string TimeText, string Status) ReadButtonTexts(AutomationElement button)
    {
        string[] texts;
        try
        {
            texts = button.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(UiProps.Name)
                .ToArray();
        }
        catch
        {
            return (string.Empty, string.Empty, string.Empty);
        }

        for (var i = 0; i < texts.Length; i++)
        {
            var m = ItemInfoPattern.Match(texts[i]);
            if (!m.Success)
            {
                continue;
            }

            var timeText = Regex.Replace(m.Groups["time"].Value, @"\s+", " ").Trim();
            var status = i + 1 < texts.Length ? texts[i + 1].Trim() : string.Empty;
            if (status == "In process")
            {
                status = string.Empty;
            }

            return (m.Groups["slot"].Value.Trim(), timeText, status);
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    private static string ReadGroupName(AutomationElement summaryButton)
    {
        try
        {
            return summaryButton.FindAllDescendants(cf => cf.ByControlType(ControlType.Text))
                .Select(UiProps.Name)
                .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n) && n != "Job Summary") ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string HelpText(AutomationElement e)
    {
        try
        {
            return e.Properties.HelpText.ValueOrDefault ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsOffscreen(AutomationElement e)
    {
        try
        {
            return e.Properties.IsOffscreen.ValueOrDefault;
        }
        catch
        {
            return false;
        }
    }

    private static string Match(Regex regex, string text)
    {
        var m = regex.Match(text);
        return m.Success ? m.Groups[1].Value.Trim() : string.Empty;
    }

    private static DateTime? ParseTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var t)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out t))
        {
            return t;
        }

        return null;
    }

    private static AutomationElement? SafeFindFirst(AutomationElement root, string id)
    {
        try
        {
            return root.FindFirstDescendant(cf => cf.ByAutomationId(id));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Invoke 不需要按钮在视野内；只有不支持 Invoke 时才物理点击，屏幕外则放弃。</summary>
    private bool InvokeOrClick(AutomationElement el, bool offscreen)
    {
        try
        {
            if (el.Patterns.Invoke.IsSupported)
            {
                el.Patterns.Invoke.Pattern.Invoke();
                return true;
            }
        }
        catch (Exception ex)
        {
            _log.Step($"  Invoke 失败：{ex.GetBaseException().Message}");
        }

        if (offscreen)
        {
            _log.Step("!! 按钮在屏幕外且不能 Invoke，放弃物理点击");
            return false;
        }

        try
        {
            el.Click();
            return true;
        }
        catch (Exception ex)
        {
            _log.Step($"!! 物理点击失败：{ex.GetBaseException().Message}");
            return false;
        }
    }
}
