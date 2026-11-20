using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.Wi3796Histogram;

/// <summary>
/// WI 3796：导入工单附件 WaferRun，查看 DNA 斜入射 LPD 直方图。
/// 附件是 VEGAENC，界面类别名是「[LPD,Size] DNA Oblique」。存储表的名称列是 WaferID。
/// </summary>
public sealed class WorkItem3796L2 : L2TestBase
{
    private const int WmSetText = 0x000C;
    private const int WmCommand = 0x0111;
    private const int EnChange = 0x0300;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, string? lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetParent(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

    [DllImport("user32.dll")]
    private static extern int GetDlgCtrlID(IntPtr hwnd);
    private const string AttachmentPath =
        @"C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261116T233003+0800_10a53e\commits\ab2ced6a19a731912257401588f48a07d23de55c\preparation\attachments\3796-92016e74f767.untrusted-data";
    private const string AttachmentSha256 = "4d004f82732e818aea176a7bc42c789c9da85311dd91e70203ca7bf3e2d49b29";
    private const string ImportFileName =
        "WorkSpaceOfflineJob_20261109134113242_Cycle1_261109134113_JRH_EPI_Particle_dual_20261106__261109134113.json";
    private const string JobName = "WorkSpaceOfflineJob_20261109134113242_Cycle1";
    private const string WaferId = "Wafer_1_202611094113";
    private const string Expected = "DNA Oblique LPD 直方图显示该缺陷";

    public WorkItem3796L2() : base("wi3796-histogram-ui.txt") { }

    [Fact]
    public void ImportAttachedWaferRunAndInspectDnaHistogram()
    {
        try
        {
            string importPath = StageAttachment();
            LaunchAndPrepare();
            Import(importPath);
            OpenImportedWafer();
            var histogram = FindDnaObliqueLpdHistogram();
            StepEvidence.Capture(MainWindow!, "step-1-import-view-histogram");
            var observation = ObserveBar(histogram);
            string actual = observation.Detail;
            bool shown = observation.VisibleBar && observation.WidthRatio is >= 0.08 and <= 0.20;
            if (shown)
                Assertions.Pass("dna-oblique-lpd-bar", "UI", Expected, "visible bar, width ratio about 1/8", actual);
            else
                Assertions.Fail("dna-oblique-lpd-bar", "UI", Expected, "visible bar, width ratio about 1/8", actual);
            StepEvidence.Capture(MainWindow!, "result");
            Assert.True(shown, Expected + ": expected visible bar, width ratio about 1/8; actual " + actual);
        }
        finally
        {
            Assertions.Flush();
        }
    }

    private string StageAttachment()
    {
        if (!File.Exists(AttachmentPath))
            throw new FileNotFoundException("WI 3796 附件不在已保存的准备目录。", AttachmentPath);
        byte[] bytes = File.ReadAllBytes(AttachmentPath);
        string hash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!hash.Equals(AttachmentSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("WI 3796 附件哈希与准备记录不一致。");
        if (bytes.Length < 7 || System.Text.Encoding.ASCII.GetString(bytes, 0, 7) != "VEGAENC")
            throw new InvalidDataException("WI 3796 附件不是产品加密的 WaferRun。");
        Assertions.Pass("attachment", "UI", "工单附件原字节", AttachmentSha256, hash);
        // The common-file dialog rejects the Runner output path once it exceeds MAX_PATH.
        string importPath = Path.Combine(@"C:\AutoTest\data\wi3796", ImportFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(importPath)!);
        File.WriteAllBytes(importPath, bytes);
        Log.Step("已按短路径暂存加密附件 " + importPath);
        return importPath;
    }

    private void Import(string importPath)
    {
        UiFind.Require(MainWindow!, "Tools menu", automationId: "ToolsMenuItem").Click();
        UiFind.Wait(MainWindow!, "Storage maintenance", TimeSpan.FromSeconds(10),
            automationId: "OptionsMenuItem").Click();
        var importButton = UiFind.Wait(MainWindow!, "Storage import",
            TimeSpan.FromSeconds(30), automationId: "StorageMaintenanceView_PerformImportCommandButton");
        var storage = AncestorContaining(importButton, "StorageMaintenanceView_WaferDataGrid");
        importButton.AsButton().Invoke();
        ChooseImportFile(importPath);

        SetDate(storage, "StorageMaintenanceView_GenericDatePicker", new DateTime(2026, 11, 1));
        SetDate(storage, "StorageMaintenanceView_GenericDatePicker_1", new DateTime(2026, 11, 18));
        SearchBox(storage).AsTextBox().Text = JobName;

        var deadline = DateTime.UtcNow.AddMinutes(3);
        var seenWindows = new HashSet<string>(StringComparer.Ordinal);
        AutomationElement? row = null;
        while (row is null && DateTime.UtcNow < deadline)
        {
            foreach (var child in MainWindow!.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
            {
                string name = UiProps.Name(child);
                if (seenWindows.Add(name))
                    Log.Step("导入期间子窗口 '" + name + "'");
            }
            UiFind.Require(storage, "Search", automationId: "StorageMaintenanceView_SearchCommandButton").Click();
            Thread.Sleep(1000);
            row = UiFind.All(storage, "Storage rows", controlType: ControlType.DataItem)
                .FirstOrDefault(item => Texts(item).Any(text => text.Contains(WaferId, StringComparison.Ordinal)));
        }
        if (row is null)
            throw new UiElementNotFoundException("导入后的晶圆没有出现在存储列表：" + WaferId);
        Assertions.Pass("imported-wafer", "UI", WaferId, WaferId, WaferId);
        UiFind.Require(row, "Select imported wafer", automationId: "StorageMaintenanceView_IsSelectedCheckBox")
            .AsCheckBox().IsChecked = true;
        UiFind.Require(storage, "Add to gallery", automationId: "StorageMaintenanceView_AddSelectedToGalleryCommandButton").Click();
        Log.Step("已把 " + WaferId + " 加入结果 Gallery");
    }

    private void OpenImportedWafer()
    {
        UiFind.Require(MainWindow!, "Explorer menu", automationId: "ExplorerMenuItem").Click();
        UiFind.Wait(MainWindow!, "Inspection", TimeSpan.FromSeconds(10), automationId: "InspectionMenuItem").Click();
        var gallery = new ResultGalleryPage(MainWindow!, Dialogs, Log);
        if (!gallery.WaitForGallery(TimeSpan.FromSeconds(60)))
            throw new UiElementNotFoundException("结果 Gallery 没有出现。");
        var deadline = DateTime.UtcNow.AddMinutes(2);
        GalleryItem? item = null;
        while (item is null && DateTime.UtcNow < deadline)
        {
            var items = gallery.ListItems(includeCalibrationJobs: true);
            gallery.LogItems(items);
            item = items.FirstOrDefault(candidate => candidate.WaferId.Contains(WaferId, StringComparison.Ordinal))
                ?? items.FirstOrDefault(candidate => candidate.GroupName.Contains(JobName, StringComparison.Ordinal));
            if (item is null)
                Thread.Sleep(1000);
        }
        if (item is null)
            throw new UiElementNotFoundException("Gallery 中没有导入的晶圆 " + WaferId);
        if (gallery.Select(item) is null)
            throw new UiElementNotFoundException("无法打开 Gallery 中的 " + item);
        Assertions.Pass("gallery-wafer", "UI", WaferId, WaferId, item.WaferId + " / " + item.GroupName);
        Log.Step("已打开结果 " + item);
    }

    private AutomationElement FindDnaObliqueLpdHistogram()
    {
        var deadline = DateTime.UtcNow.AddSeconds(60);
        do
        {
            var labels = UiFind.All(MainWindow!, "Histogram category labels", controlType: ControlType.Text)
                .Where(text =>
                {
                    string name = UiProps.Name(text);
                    return name.Contains("DNA Oblique", StringComparison.Ordinal)
                           && name.Contains("LPD", StringComparison.Ordinal);
                })
                .ToArray();
            foreach (var label in labels)
            {
                var image = HistogramNear(label);
                if (image is not null)
                {
                    Log.Step("找到 DNA Oblique LPD 直方图，类别 " + UiProps.Name(label));
                    return image;
                }
            }
            if (labels.Length > 0)
            {
                Log.Step("看到 DNA Oblique LPD 类别 " + labels.Length + " 个，直方图图像尚未出现");
            }
            Thread.Sleep(500);
        } while (DateTime.UtcNow < deadline);
        throw new UiElementNotFoundException("结果页没有出现类别名同时包含 DNA Oblique 和 LPD 的直方图。");
    }

    private static AutomationElement? HistogramNear(AutomationElement label)
    {
        var current = label;
        for (var depth = 0; depth < 8 && current is not null; depth++)
        {
            var image = UiFind.Optional(current, "Histogram drawing", automationId: "histogramImage");
            if (image is not null && image.BoundingRectangle.Width >= 20 && image.BoundingRectangle.Height >= 20)
                return image;
            current = current.Parent;
        }
        return null;
    }

    private AutomationElement ChooseImportFile(string importPath)
    {
        var desktop = MainWindow!.Automation.GetDesktop();
        var deadline = DateTime.UtcNow.AddSeconds(20);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        AutomationElement? dialog = null;
        while (dialog is null && DateTime.UtcNow < deadline)
        {
            var windows = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .Concat(MainWindow!.FindAllChildren(cf => cf.ByControlType(ControlType.Window)));
            foreach (var window in windows)
            {
                string name = UiProps.Name(window);
                string key = name + "|" + window.ClassName;
                if (seen.Add(key))
                    Log.Step($"顶层窗口 '{name}' class={window.ClassName}");
                if (name.Contains("打开", StringComparison.Ordinal) || name.Contains("Open", StringComparison.Ordinal)
                    || window.ClassName == "#32770")
                {
                    dialog = window;
                    break;
                }
            }
            if (dialog is null)
                Thread.Sleep(400);
        }
        if (dialog is null)
            throw new UiElementNotFoundException("点击导入后没有出现文件选择窗口。");

        var edit = dialog.FindAllDescendants(cf => cf.ByClassName("Edit"))
            .First(item => UiProps.Id(item) == "1148");
        if (!edit.Properties.NativeWindowHandle.IsSupported)
            throw new UiElementNotFoundException("文件名输入框没有窗口句柄。");
        IntPtr editHandle = edit.Properties.NativeWindowHandle.Value;
        SendMessage(editHandle, WmSetText, IntPtr.Zero, importPath);
        int controlId = GetDlgCtrlID(editHandle);
        SendMessage(GetParent(editHandle), WmCommand, (IntPtr)((EnChange << 16) | (controlId & 0xFFFF)), editHandle);
        SendMessage(GetAncestor(editHandle, 2), WmCommand, (IntPtr)1, IntPtr.Zero);
        Log.Step("已向文件对话框提交附件路径");
        var gone = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < gone)
        {
            bool stillOpen = desktop.FindAllChildren(cf => cf.ByControlType(ControlType.Window))
                .Concat(MainWindow!.FindAllChildren(cf => cf.ByControlType(ControlType.Window)))
                .Any(window => UiProps.Name(window).Contains("打开", StringComparison.Ordinal));
            if (!stillOpen)
            {
                Log.Step("文件选择窗口已关闭");
                return dialog;
            }
            Thread.Sleep(400);
        }
        throw new UiElementNotFoundException("文件选择窗口没有接受附件路径。");
    }

    private void SetDate(AutomationElement storage, string id, DateTime value)
    {
        var date = UiFind.Require(storage, "Storage date", automationId: id);
        var box = UiFind.Require(date, "Date editor", automationId: "PART_TextBox", controlType: ControlType.Edit);
        box.AsTextBox().Text = value.ToString("yyyy/M/d", CultureInfo.InvariantCulture);
        Log.Step(id + " => " + box.AsTextBox().Text);
    }

    private static AutomationElement SearchBox(AutomationElement storage)
    {
        var search = UiFind.All(storage, "Storage editors", controlType: ControlType.Edit)
            .Where(edit => !InsideDatePicker(edit))
            .OrderBy(edit => edit.BoundingRectangle.Top)
            .FirstOrDefault();
        return search ?? throw new UiElementNotFoundException("存储维护里找不到搜索框。");
    }

    private static AutomationElement AncestorContaining(AutomationElement start, string automationId)
    {
        var current = start;
        for (var depth = 0; depth < 12 && current is not null; depth++)
        {
            if (UiFind.Optional(current, automationId, automationId: automationId) is not null)
                return current;
            current = current.Parent;
        }
        throw new UiElementNotFoundException("找不到包含 " + automationId + " 的界面。");
    }

    private static bool InsideDatePicker(AutomationElement element)
    {
        var current = element;
        for (var depth = 0; depth < 6 && current is not null; depth++)
        {
            if (UiProps.Id(current).StartsWith("StorageMaintenanceView_GenericDatePicker", StringComparison.Ordinal))
                return true;
            current = current.Parent;
        }
        return false;
    }

    private static IEnumerable<string> Texts(AutomationElement element)
        => UiFind.All(element, "Row text", controlType: ControlType.Text)
            .Select(UiProps.Name)
            .Where(text => !string.IsNullOrWhiteSpace(text));

    private static (bool VisibleBar, double WidthRatio, string Detail) ObserveBar(AutomationElement histogram)
    {
        string path = Path.Combine(RunContext.Dir, "wi3796-histogram-screen.png");
        using (var capture = FlaUI.Core.Capturing.Capture.Element(histogram))
            capture.ToFile(path);
        var pixels = WpfTestHost.Run(() =>
        {
            using var stream = File.OpenRead(path);
            var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var bytes = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(bytes, bitmap.PixelWidth * 4, 0);
            return (Bytes: bytes, Width: bitmap.PixelWidth, Height: bitmap.PixelHeight);
        });
        int width = pixels.Width;
        int height = pixels.Height;
        var colors = new Dictionary<int, int>();
        for (int i = 0; i < pixels.Bytes.Length; i += 4)
        {
            int color = (pixels.Bytes[i] >> 3) | ((pixels.Bytes[i + 1] >> 3) << 5) | ((pixels.Bytes[i + 2] >> 3) << 10);
            colors[color] = colors.GetValueOrDefault(color) + 1;
        }
        int background = colors.OrderByDescending(pair => pair.Value).First().Key;
        bool Ink(int x, int y)
        {
            int i = (y * width + x) * 4;
            return Math.Abs(pixels.Bytes[i] - ((background & 31) * 8 + 4))
                + Math.Abs(pixels.Bytes[i + 1] - (((background >> 5) & 31) * 8 + 4))
                + Math.Abs(pixels.Bytes[i + 2] - (((background >> 10) & 31) * 8 + 4)) > 100;
        }
        int baseline = -1;
        int left = 0;
        int length = 0;
        for (int y = height / 2; y < height * 19 / 20; y++)
        {
            int run = 0;
            for (int x = 0; x < width; x++)
            {
                run = Ink(x, y) ? run + 1 : 0;
                if (run >= width / 2 && run >= length)
                {
                    length = run;
                    left = x - run + 1;
                    baseline = y;
                }
            }
        }
        if (baseline < 24)
            return (false, 0, "no measurable plot baseline; screenshot=wi3796-histogram-screen.png");
        int top = baseline / 4;
        int bottom = baseline * 3 / 4;
        int consecutive = 0;
        int widest = 0;
        for (int x = left + 3; x < left + length; x++)
        {
            int ink = 0;
            for (int y = top; y < bottom; y++)
                if (Ink(x, y))
                    ink++;
            consecutive = ink >= (bottom - top) * 0.95 ? consecutive + 1 : 0;
            widest = Math.Max(widest, consecutive);
        }
        double ratio = length == 0 ? 0 : (double)widest / length;
        bool visible = widest >= 6;
        return (visible, ratio,
            $"baseline={baseline}px plot={length}px bar={widest}px ratio={ratio:F3}; screenshot=wi3796-histogram-screen.png");
    }
}
