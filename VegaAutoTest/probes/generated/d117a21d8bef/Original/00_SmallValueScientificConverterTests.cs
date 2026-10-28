// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Globalization;
using Vega.UI.Calibration;

namespace Vega.UI.Calibration.Tests;

/// <summary>
/// 锁定 SmallValueScientificConverter 的格式化分支，防止后续修改重新引入显示回归：
/// 1) 带 ConverterParameter（正常值格式串，如 F3）时——真零按该格式如实显示（0.000），
///    非零但会被固定位数截成全零的小值降级 E2 科学计数法（Uniformity StdDev 显示需求，
///    防止 F3 把极小标准差显示成 0.000 误导操作员；.NET 8 E 格式指数固定 3 位，如 4.00E-004），
///    其余按该格式显示。
/// 2) 不带参数保持既有 F2/E2 阈值行为（Haze 结果列与 A/B/C 系数零影响）。
/// 3) NaN（未计算语义）显示占位符 "--"，与真零 0.000 区分。
/// </summary>
public class SmallValueScientificConverterTests
{
    private static readonly CultureInfo s_culture = CultureInfo.InvariantCulture;

    private readonly SmallValueScientificConverter _converter = new();

    #region ConverterParameter=F3 —— Uniformity StdDev 显示分支

    [Theory]
    [InlineData(4e-4, "4.00E-004")]
    [InlineData(-4e-4, "-4.00E-004")]
    public void Convert_WithF3Parameter_ValueRoundedToZeroByFixedFormat_ShowsScientific(double value, string expected)
    {
        Assert.Equal(expected, _converter.Convert(value, typeof(string), "F3", s_culture));
    }

    [Fact]
    public void Convert_WithF3Parameter_Zero_ShowsFixedFormatZero()
    {
        Assert.Equal("0.000", _converter.Convert(0d, typeof(string), "F3", s_culture));
    }

    [Theory]
    [InlineData(1e-3, "0.001")]
    [InlineData(2.5, "2.500")]
    public void Convert_WithF3Parameter_NormalValue_KeepsFixedFormat(double value, string expected)
    {
        Assert.Equal(expected, _converter.Convert(value, typeof(string), "F3", s_culture));
    }

    #endregion

    #region 无参数 —— 既有 Haze 行为兼容

    [Theory]
    [InlineData(0d, "0.00")]
    [InlineData(0.5, "5.00E-001")]
    [InlineData(2.5, "2.50")]
    public void Convert_WithoutParameter_KeepsLegacyThresholdBehavior(double value, string expected)
    {
        // 空串与 WPF 传参缺失（null）走同一 IsNullOrEmpty 兼容分支
        Assert.Equal(expected, _converter.Convert(value, typeof(string), string.Empty, s_culture));
    }

    #endregion

    [Fact]
    public void ConvertBack_ScientificNotationText_ParsesBackToValue()
    {
        Assert.Equal(4e-4, Assert.IsType<double>(_converter.ConvertBack("4.00E-04", typeof(double), string.Empty, s_culture)));
    }

    [Fact]
    public void Convert_NonDoubleValue_ReturnsTextAsIs()
    {
        Assert.Equal("abc", _converter.Convert("abc", typeof(string), "F3", s_culture));
    }

    [Fact]
    public void Convert_NaN_ShowsNotComputedPlaceholder_RegardlessOfParameter()
    {
        Assert.Equal("--", _converter.Convert(double.NaN, typeof(string), "F3", s_culture));
        Assert.Equal("--", _converter.Convert(double.NaN, typeof(string), string.Empty, s_culture));
    }
}