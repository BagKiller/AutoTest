// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Measurements;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// 校准类 JobRun 命名约定判定：DF/Haze/Uniformity 三个前缀完整命中才判定为校准 Job，
/// 非超管据此过滤结果栏卡片（前缀必须完整命中，防 "Uniformity_x" 这类普通 Job 误判）。
/// </summary>
public class CaliJobNamingTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("DFCaliJob_20261029112233456_1", true)]
    [InlineData("HazeCaliJob_20261029112233456_1", true)]
    [InlineData("UniformityCaliJob_20261029112233456_1", true)]
    [InlineData("Uniformity_x", false)]
    [InlineData("CaliJob_x", false)]
    [InlineData("Job_xxx", false)]
    [InlineData("DefaultJob_20261029112233233", false)]
    public void IsCaliJob_MatchesCompletePrefixesOnly(string? jobName, bool expected)
    {
        Assert.Equal(expected, CaliJobNaming.IsCaliJob(jobName));
    }
}