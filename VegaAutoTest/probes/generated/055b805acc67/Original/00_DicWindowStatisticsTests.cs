// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.UI.Calibration;

namespace Vega.UI.Calibration.Tests;

public class DicWindowStatisticsTests
{
    [Fact]
    public void ComputeContrast_Normal_ReturnsRatioPercent()
    {
        // (100-50)/(100+50)*100
        Assert.Equal(100.0 / 3, DicWindowStatistics.ComputeContrast(100, 50)!.Value, precision: 6);
    }

    [Fact]
    public void ComputeContrast_MaxEqualsMin_ReturnsZero()
    {
        Assert.Equal(0d, DicWindowStatistics.ComputeContrast(5, 5)!.Value);
    }

    [Fact]
    public void ComputeContrast_NullOrNonPositiveSum_ReturnsNull()
    {
        Assert.Null(DicWindowStatistics.ComputeContrast(null, 5));
        Assert.Null(DicWindowStatistics.ComputeContrast(5, null));
        Assert.Null(DicWindowStatistics.ComputeContrast(0, 0));
        // DAC 非负假定的边界：负值和 ≤ 0 判无数据
        Assert.Null(DicWindowStatistics.ComputeContrast(-1, -1));
    }
}