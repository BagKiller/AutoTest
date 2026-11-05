using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;
using OpenCvSharp;
using Vega.AutoTest.Harness;
using Xunit;
using Drawing = System.Drawing;

namespace Vega.AutoTest.Authored;

internal static class DependencyEvidence
{
    public static void Run(Action<AssertionRecorder> body)
    {
        var recorder = new AssertionRecorder();
        try { body(recorder); }
        catch (Exception error)
        {
            recorder.Fail("execution", "dependency", "Dependency behavior completed",
                "No unexpected exception", error.ToString());
            throw;
        }
        finally { recorder.Flush(); }
    }

    public static void Verify(AssertionRecorder recorder, string id,
        string expected, string actual, Action assertion)
    {
        var error = Record.Exception(assertion);
        if (error == null)
        {
            recorder.Pass(id, "behavior", id, expected, actual);
            return;
        }
        recorder.Fail(id, "behavior", id, expected, actual + "; " + error.Message);
        ExceptionDispatchInfo.Capture(error).Throw();
    }
}

public sealed class DependencyRuntimeRegressionTests
{
    [Fact]
    public void DrawingLosslessCodecsPreservePixels()
    {
        DependencyEvidence.Run(r =>
        {
            var colors = new[]
            {
                Drawing.Color.Black, Drawing.Color.White, Drawing.Color.Red,
                Drawing.Color.Green, Drawing.Color.Blue, Drawing.Color.FromArgb(255, 17, 93, 201)
            };
            using var source = new Drawing.Bitmap(3, 2);
            for (int i = 0; i < colors.Length; i++)
                source.SetPixel(i % 3, i / 3, colors[i]);
            foreach (var format in new[]
            {
                Drawing.Imaging.ImageFormat.Png, Drawing.Imaging.ImageFormat.Bmp
            })
            {
                using var buffer = new MemoryStream();
                source.Save(buffer, format);
                buffer.Position = 0;
                using var decoded = new Drawing.Bitmap(buffer);
                string prefix = format.ToString();
                DependencyEvidence.Verify(r, prefix + ".width", "3", decoded.Width.ToString(),
                    () => Assert.Equal(3, decoded.Width));
                DependencyEvidence.Verify(r, prefix + ".height", "2", decoded.Height.ToString(),
                    () => Assert.Equal(2, decoded.Height));
                for (int i = 0; i < colors.Length; i++)
                {
                    int expected = colors[i].ToArgb();
                    int actual = decoded.GetPixel(i % 3, i / 3).ToArgb();
                    DependencyEvidence.Verify(r, prefix + ".pixel." + i,
                        expected.ToString(), actual.ToString(), () => Assert.Equal(expected, actual));
                }
            }
        });
    }

    [Fact]
    public void CentralTransitiveOpenCvExecutesNativeThreshold()
    {
        DependencyEvidence.Run(r =>
        {
            byte[] input = { 0, 126, 127, 128, 254, 255 };
            byte[] expected = { 0, 0, 0, 255, 255, 255 };
            using var source = new Mat(2, 3, MatType.CV_8UC1);
            using var result = new Mat();
            for (int i = 0; i < input.Length; i++)
                source.Set<byte>(i / 3, i % 3, input[i]);
            double threshold = Cv2.Threshold(source, result, 127, 255, ThresholdTypes.Binary);
            DependencyEvidence.Verify(r, "opencv.threshold", "127", threshold.ToString(),
                () => Assert.Equal(127d, threshold));
            for (int i = 0; i < expected.Length; i++)
            {
                byte wanted = expected[i];
                byte actual = result.At<byte>(i / 3, i % 3);
                DependencyEvidence.Verify(r, "opencv.pixel." + i,
                    wanted.ToString(), actual.ToString(), () => Assert.Equal(wanted, actual));
            }
        });
    }

    [Fact]
    public void WpfEventTriggerInvokesCommandAndDetaches()
    {
        DependencyEvidence.Run(r => RunSta(() =>
        {
            var source = new Border();
            var command = new CountingCommand();
            var trigger = new Microsoft.Xaml.Behaviors.EventTrigger("Loaded")
            {
                SourceObject = source
            };
            trigger.Actions.Add(new InvokeCommandAction
            {
                Command = command,
                CommandParameter = "dependency-regression"
            });
            var triggers = Interaction.GetTriggers(source);
            triggers.Add(trigger);
            DependencyEvidence.Verify(r, "behaviors.attached", "Source border",
                ReferenceEquals(source, trigger.AssociatedObject).ToString(),
                () => Assert.Same(source, trigger.AssociatedObject));
            source.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            DependencyEvidence.Verify(r, "behaviors.invoke", "1", command.Count.ToString(),
                () => Assert.Equal(1, command.Count));
            DependencyEvidence.Verify(r, "behaviors.parameter", "dependency-regression",
                command.Parameter?.ToString() ?? "null",
                () => Assert.Equal("dependency-regression", command.Parameter));
            triggers.Remove(trigger);
            DependencyEvidence.Verify(r, "behaviors.detached", "null",
                trigger.AssociatedObject?.ToString() ?? "null",
                () => Assert.Null(trigger.AssociatedObject));
            source.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            DependencyEvidence.Verify(r, "behaviors.unsubscribed", "1", command.Count.ToString(),
                () => Assert.Equal(1, command.Count));
        }));
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { error = caught; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }

    private sealed class CountingCommand : ICommand
    {
        public int Count { get; private set; }
        public object? Parameter { get; private set; }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) { Count++; Parameter = parameter; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
