// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.
using Vega.Data.Commons;
using Vega.Data.Recipes;

namespace Vega.Utility.Common.Tests;

/// <summary>
/// 内置 NotchScan 系统配方 ID 与晶圆尺寸的白名单映射。
/// </summary>
public class NotchScanRecipeLookupTests
{
    [Fact]
    public void GetExpectedWaferSize_SystemRecipe_ReturnsMappedSize()
    {
        Assert.Equal(WaferSizeType.WS300,
            NotchScanRecipeLookup.GetExpectedWaferSize(BaseFile.NotchScanRecipeID));
        Assert.Equal(WaferSizeType.WS200,
            NotchScanRecipeLookup.GetExpectedWaferSize(BaseFile.NotchScan8InchRecipeID));
    }

    [Theory]
    [InlineData(-1L)]
    [InlineData(0L)]
    [InlineData(199L)]
    [InlineData(202L)]
    [InlineData(long.MaxValue)]
    public void GetExpectedWaferSize_NonSystemRecipe_Throws(long recipeID)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => NotchScanRecipeLookup.GetExpectedWaferSize(recipeID));

        Assert.Equal(nameof(recipeID), exception.ParamName);
    }

    [Fact]
    public void IsSystemRecipeID_RecognizesOnlyBuiltInIDs()
    {
        Assert.True(NotchScanRecipeLookup.IsSystemRecipeID(BaseFile.NotchScanRecipeID));
        Assert.True(NotchScanRecipeLookup.IsSystemRecipeID(BaseFile.NotchScan8InchRecipeID));
        Assert.False(NotchScanRecipeLookup.IsSystemRecipeID(199));
        Assert.False(NotchScanRecipeLookup.IsSystemRecipeID(202));
    }

    [Fact]
    public void TryGetRecipeID_SupportedSizes_SelectBuiltInRecipe()
    {
        Assert.True(NotchScanRecipeLookup.TryGetRecipeID(WaferSizeType.WS300, out var twelveInchId));
        Assert.Equal(BaseFile.NotchScanRecipeID, twelveInchId);

        Assert.True(NotchScanRecipeLookup.TryGetRecipeID(WaferSizeType.WS200, out var eightInchId));
        Assert.Equal(BaseFile.NotchScan8InchRecipeID, eightInchId);
    }

    [Fact]
    public void TryGetRecipeID_SizeWithoutDedicatedRecipe_ReturnsFalse()
    {
        // 6 寸没有专属系统配方：ID 200 按 12 寸扫描范围生成，静默回退会以错误范围扫描
        Assert.False(NotchScanRecipeLookup.TryGetRecipeID(WaferSizeType.WS150, out var recipeId));
        Assert.Equal(0, recipeId);

        Assert.False(NotchScanRecipeLookup.TryGetRecipeID((WaferSizeType) 0, out var undefinedId));
        Assert.Equal(0, undefinedId);
    }

    [Fact]
    public void TryGetRecipeID_RoundTripsWithExpectedWaferSize()
    {
        // 映射与尺寸契约必须互为逆关系：TryGetRecipeID 取到的 ID，其期望尺寸即输入尺寸
        Assert.True(NotchScanRecipeLookup.TryGetRecipeID(WaferSizeType.WS300, out var twelveInchId));
        Assert.Equal(WaferSizeType.WS300, NotchScanRecipeLookup.GetExpectedWaferSize(twelveInchId));

        Assert.True(NotchScanRecipeLookup.TryGetRecipeID(WaferSizeType.WS200, out var eightInchId));
        Assert.Equal(WaferSizeType.WS200, NotchScanRecipeLookup.GetExpectedWaferSize(eightInchId));
    }
}