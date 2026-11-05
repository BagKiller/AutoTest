using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Vega.AutoTest.Harness;
using Vega.UI.Calibration;
using Xunit;
using static Vega.AutoTest.Authored.UniformitySupport;

namespace Vega.AutoTest.Authored;

public class UniformityStatisticsRegression
{
    [Fact]
    public void SampleDeviationRangePercentageErrorsAndDisplayedPrecision()
    {
        var r = new AssertionRecorder();
        try
        {
            var vm = (UniformityCalibrationViewModel)RuntimeHelpers.GetUninitializedObject(typeof(UniformityCalibrationViewModel));
            double[][] values = { new[] {2d,4,6},new[] {.002,.004,.006},new[] {9d,9,9},new[] {-6d,-4,-2} };
            double[] deviations = { 2,.002,0,2 }, percentages = { 100,100,0,-100 };
            for (int i = 0; i < values.Length; i++)
            {
                var actual = vm.CalculateStatistics(values[i].Select((y,x) => new Point(x * 17,y)).ToList());
                Check(r,$"statistics-{i}-std",Math.Abs(actual.standardDeviation-deviations[i]) <= 1e-12,deviations[i],actual.standardDeviation);
                Check(r,$"statistics-{i}-percent",Math.Abs(actual.maxMinOverMean-percentages[i]) <= 1e-10,percentages[i],actual.maxMinOverMean);
            }
            List<Point>?[] invalid = { null,new(),new() { new Point(1,2) },new() { new Point(1,-2),new Point(2,2) } };
            for (int i = 0; i < invalid.Length; i++)
            {
                Exception? error = Record.Exception(() => vm.CalculateStatistics(invalid[i]!));
                Type expected = i == 3 ? typeof(DivideByZeroException) : typeof(ArgumentException);
                Check(r,$"statistics-error-{i}",error?.GetType() == expected,expected.Name,error?.GetType().Name ?? "no exception");
            }
            var converter = new SmallValueScientificConverter();
            double[] inputs = { double.NaN,0,.00002,.002,20 };
            string[] text = { "--","0.000","2.00E-05","0.002","20.000" };
            for (int i = 0; i < inputs.Length; i++)
            {
                object actual = converter.Convert(inputs[i],typeof(string),"F3",CultureInfo.InvariantCulture);
                Check(r,$"format-{i}",Equals(text[i],actual),text[i],actual);
            }
        }
        finally { r.Flush(); }
    }
}
