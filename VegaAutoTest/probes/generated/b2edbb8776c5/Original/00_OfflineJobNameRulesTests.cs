// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Shared;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// 离线 JobName 命名规则：净化（Trim/剔非法字符/截断 MaxLength）、非法字符校验、
/// 三个入口自动命名格式（各自前缀 + 17 位毫秒时间戳 yyyyMMddHHmmssfff）。
/// </summary>
public class OfflineJobNameRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Normalize_EmptyOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(OfflineJobNameRules.Normalize(input));
    }

    [Fact]
    public void Normalize_TrimsSurroundingWhitespace()
    {
        Assert.Equal("JobA", OfflineJobNameRules.Normalize("  JobA  "));
    }

    [Fact]
    public void Normalize_StripsInvalidFileNameChars()
    {
        Assert.Equal("abcdefghij", OfflineJobNameRules.Normalize("a/b\\c:d*e?f\"g<h>i|j"));
    }

    [Fact]
    public void Normalize_AllInvalid_ReturnsNull()
    {
        Assert.Null(OfflineJobNameRules.Normalize("///"));
    }

    [Fact]
    public void Normalize_TruncatesToMaxLength()
    {
        string longName = new('A', OfflineJobNameRules.MaxLength + 50);

        Assert.Equal(OfflineJobNameRules.MaxLength, OfflineJobNameRules.Normalize(longName)!.Length);
    }

    [Fact]
    public void Normalize_ExactMaxLength_KeptAsIs()
    {
        string name = new('B', OfflineJobNameRules.MaxLength);

        Assert.Equal(name, OfflineJobNameRules.Normalize(name));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a:b")]
    [InlineData("a*b")]
    [InlineData("a?b")]
    [InlineData("a\"b")]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a|b")]
    [InlineData("a\tb")]
    public void ContainsInvalidChars_DetectsEachInvalidChar(string jobName)
    {
        Assert.True(OfflineJobNameRules.ContainsInvalidChars(jobName));
    }

    [Theory]
    [InlineData("RunCurRecipeOfflineJob_20261030143000123")]
    [InlineData("Job-001")]
    [InlineData("")]
    public void ContainsInvalidChars_ValidName_ReturnsFalse(string jobName)
    {
        Assert.False(OfflineJobNameRules.ContainsInvalidChars(jobName));
    }

    [Fact]
    public void AutoRunCurrentRecipeJobName_HasPrefixAndMillisecondTimestamp()
    {
        Assert.Matches(@"^RunCurRecipeOfflineJob_\d{17}$", OfflineJobNameRules.AutoRunCurrentRecipeJobName());
    }

    [Fact]
    public void AutoWorkSpaceJobName_HasPrefixAndMillisecondTimestamp()
    {
        Assert.Matches(@"^WorkSpaceOfflineJob_\d{17}$", OfflineJobNameRules.AutoWorkSpaceJobName());
    }

    [Fact]
    public void AutoJobQueueJobName_UsesJobModelNamePrefix()
    {
        Assert.Matches(@"^MyJob_Offline_\d{17}$", OfflineJobNameRules.AutoJobQueueJobName("MyJob"));
    }

    [Fact]
    public void Compose_NoCustomName_FallsBackToAutoName()
    {
        Assert.Equal("Auto_20261030143000123", OfflineJobNameRules.Compose(null, "Auto_20261030143000123"));
        Assert.Equal("Auto_20261030143000123", OfflineJobNameRules.Compose("   ", "Auto_20261030143000123"));
    }

    [Fact]
    public void Compose_CustomName_AppendsMillisecondTimestamp()
    {
        Assert.Matches(@"^MyName_\d{17}$", OfflineJobNameRules.Compose("MyName", "Auto_1"));
    }

    [Fact]
    public void Compose_CustomNameIsSanitizedBeforeComposition()
    {
        Assert.Matches(@"^MyName_\d{17}$", OfflineJobNameRules.Compose(" My/Name ", "Auto_1"));
    }
}