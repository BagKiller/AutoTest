using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OpenCvSharp;
using Vega.Algo.Imaging;
using Vega.AutoTest.Harness;
using Vega.UI.StorageMaintenance;
using Xunit;
using Bitmap = System.Drawing.Bitmap;
using Color = System.Drawing.Color;
using ImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Vega.AutoTest.Authored;

internal static class DependencyChecks
{
    internal static void Equal<T>(AssertionRecorder recorder, string id, T expected, T actual)
    {
        bool same = object.Equals(expected, actual);
        string expectedText = Convert.ToString(expected) ?? "null";
        string actualText = Convert.ToString(actual) ?? "null";
        if (same)
            recorder.Pass(id, "behavior", id, expectedText, actualText);
        else
            recorder.Fail(id, "behavior", id, expectedText, actualText);
        Assert.True(same, $"{id}: expected {expectedText}; actual {actualText}");
    }

    internal static void Run(Action<AssertionRecorder> action, bool sta = false)
    {
        var recorder = new AssertionRecorder();
        try
        {
            if (!sta)
            {
                action(recorder);
                return;
            }
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try { action(recorder); }
                catch (Exception ex) { failure = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
                ExceptionDispatchInfo.Capture(failure).Throw();
        }
        catch (Exception ex)
        {
            recorder.Fail("execution", "exception", "Dependency behavior completes", "no exception", ex.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    internal static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}

public sealed class ImagingDependencyRegression
{
    [Fact]
    public void VegaSaturationCountUsesNativeOpenCvAtByteBoundary()
    {
        DependencyChecks.Run(recorder =>
        {
            var samples = new[]
            {
                (Rows: 1, Cols: 1, Pixels: new byte[] { 254 }, Expected: 0),
                (Rows: 1, Cols: 1, Pixels: new byte[] { 255 }, Expected: 1),
                (Rows: 2, Cols: 3, Pixels: new byte[] { 0, 1, 127, 253, 254, 254 }, Expected: 0),
                (Rows: 2, Cols: 3, Pixels: new byte[] { 255, 254, 255, 0, 253, 255 }, Expected: 3),
                (Rows: 3, Cols: 2, Pixels: new byte[] { 255, 255, 255, 255, 255, 255 }, Expected: 6)
            };
            for (int index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                using var image = new Mat(sample.Rows, sample.Cols, MatType.CV_8UC1);
                for (int row = 0; row < sample.Rows; row++)
                    for (int col = 0; col < sample.Cols; col++)
                        image.Set<byte>(row, col, sample.Pixels[row * sample.Cols + col]);
                int actual = ImageProcessor.GetSaturatedPixelCount(image);
                DependencyChecks.Equal(recorder, $"saturation-{index}", sample.Expected, actual);
            }
        });
    }

    [Fact]
    public void UpdatedDrawingPackagePreservesOpaquePixelsThroughLosslessCodecs()
    {
        DependencyChecks.Run(recorder =>
        {
            var colors = new[]
            {
                Color.Red, Color.Lime, Color.Blue,
                Color.Black, Color.White, Color.FromArgb(17, 34, 51)
            };
            using var source = new Bitmap(3, 2);
            for (int index = 0; index < colors.Length; index++)
                source.SetPixel(index % 3, index / 3, colors[index]);
            foreach (var format in new[] { ImageFormat.Bmp, ImageFormat.Png })
            {
                using var stream = new MemoryStream();
                source.Save(stream, format);
                stream.Position = 0;
                using var decoded = new Bitmap(stream);
                string id = format.ToString();
                DependencyChecks.Equal(recorder, id + "-width", 3, decoded.Width);
                DependencyChecks.Equal(recorder, id + "-height", 2, decoded.Height);
                for (int index = 0; index < colors.Length; index++)
                    DependencyChecks.Equal(recorder, id + "-pixel-" + index,
                        colors[index].ToArgb(), decoded.GetPixel(index % 3, index / 3).ToArgb());
            }
        });
    }
}

public sealed class XamlBehaviorDependencyRegression
{
    public sealed class SelectionRow : INotifyPropertyChanged
    {
        private bool selected;
        public string Id { get; }
        public int Changes { get; private set; }
        public SelectionRow(string id, bool initial) { Id = id; selected = initial; }
        public bool IsSelected
        {
            get => selected;
            set
            {
                if (selected == value) return;
                selected = value;
                Changes++;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static void Selection(AssertionRecorder recorder, DataGrid grid, string id, string expected)
    {
        DependencyChecks.Drain();
        string actual = string.Join(",", grid.SelectedItems.Cast<SelectionRow>()
            .Select(row => row.Id).OrderBy(value => value, StringComparer.Ordinal));
        DependencyChecks.Equal(recorder, id, expected, actual);
    }

    [Fact]
    public void UpgradedBehaviorRuntimeMaintainsTwoWaySelectionAndDetaches()
    {
        DependencyChecks.Run(recorder =>
        {
            var a = new SelectionRow("a", true);
            var b = new SelectionRow("b", false);
            var c = new SelectionRow("c", true);
            var rows = new ObservableCollection<SelectionRow> { a, b };
            var grid = new DataGrid
            {
                SelectionMode = DataGridSelectionMode.Extended,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                ItemsSource = rows
            };
            var behavior = new SelectedItemsBehavior();
            behavior.Attach(grid);
            try
            {
                Selection(recorder, grid, "initial-flags", "a");
                b.IsSelected = true;
                Selection(recorder, grid, "flag-add", "a,b");
                grid.SelectedItems.Remove(a);
                Selection(recorder, grid, "grid-remove", "b");
                DependencyChecks.Equal(recorder, "removed-row-flag", false, a.IsSelected);
                DependencyChecks.Equal(recorder, "other-row-retained", true, b.IsSelected);
                grid.SelectedItems.Add(a);
                Selection(recorder, grid, "grid-add", "a,b");
                DependencyChecks.Equal(recorder, "added-row-flag", true, a.IsSelected);
                a.IsSelected = false;
                Selection(recorder, grid, "flag-remove", "b");
                rows.Add(c);
                Selection(recorder, grid, "insert-preselected-row", "b,c");
                rows.Remove(a);
                Selection(recorder, grid, "remove-unselected-row", "b,c");
                a.IsSelected = true;
                Selection(recorder, grid, "removed-item-unsubscribed", "b,c");
                grid.Items.Refresh();
                Selection(recorder, grid, "refresh-retains-selection", "b,c");
                DependencyChecks.Equal(recorder, "refresh-retains-b-flag", true, b.IsSelected);
                DependencyChecks.Equal(recorder, "refresh-retains-c-flag", true, c.IsSelected);
                behavior.Detach();
                c.IsSelected = false;
                Selection(recorder, grid, "detach-stops-item-sync", "b,c");
                grid.SelectedItems.Remove(b);
                Selection(recorder, grid, "detached-grid-change", "c");
                DependencyChecks.Equal(recorder, "detach-stops-grid-sync", true, b.IsSelected);
            }
            finally
            {
                if (((Microsoft.Xaml.Behaviors.IAttachedObject)behavior).AssociatedObject != null) behavior.Detach();
                DependencyChecks.Drain();
            }
        }, sta: true);
    }

    [Fact]
    public void RowHeaderBehaviorTogglesExactlyOnceAndStopsAfterDetach()
    {
        DependencyChecks.Run(recorder =>
        {
            var row = new SelectionRow("clicked", false);
            var other = new SelectionRow("other", true);
            var checkbox = new CheckBox { DataContext = row };
            var behavior = new RowHeaderSelectionBehavior();
            behavior.Attach(checkbox);
            try
            {
                for (int click = 1; click <= 2; click++)
                {
                    var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                    { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent };
                    checkbox.RaiseEvent(args);
                    DependencyChecks.Equal(recorder, $"click-{click}-handled", true, args.Handled);
                    DependencyChecks.Equal(recorder, $"click-{click}-flag", click == 1, row.IsSelected);
                    DependencyChecks.Equal(recorder, $"click-{click}-writes", click, row.Changes);
                    DependencyChecks.Equal(recorder, $"click-{click}-other-row", true, other.IsSelected);
                }
                behavior.Detach();
                checkbox.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
                DependencyChecks.Equal(recorder, "detached-row-flag", false, row.IsSelected);
                DependencyChecks.Equal(recorder, "detached-row-writes", 2, row.Changes);
            }
            finally { if (((Microsoft.Xaml.Behaviors.IAttachedObject)behavior).AssociatedObject != null) behavior.Detach(); }
        }, sta: true);
    }
}
