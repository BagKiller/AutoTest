// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using Galaxy.Data.Management;
using Vega.Data.Management;
using Vega.Utility.Helper;

namespace Vega.Architecture.Tests;

/// <summary>
/// EFEM 供应商 token 族注册门禁：EFEM_Support / Wafer_Recovery 的 Marker 为 "EFEM"，
/// 须同时接受存量裸 EFEM 与装机向导写入的 EFEMRorze / EFEMFortrend，否则 EFEM LD 实例不会创建。
/// 注册过滤与 UI gate 同源，token 池为 ToolFeaturesHelper.AllGrants。
/// </summary>
public class VegaFeaturesTests
{
    /// <summary>ToolFeaturesHelper.AllGrants 的私有 Lazy 缓存；测试经反射替换以覆盖各 token 组合。</summary>
    private static readonly FieldInfo s_allGrantsField = typeof(ToolFeaturesHelper)
        .GetField("s_allGrants", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("ToolFeaturesHelper.s_allGrants not found.");

    /// <summary>过滤逻辑本体（私有静态），直接覆盖 marker/grants 全矩阵，含 marker=null 与 grants=null。</summary>
    private static readonly MethodInfo s_isToolSpecificMarkerSupported = typeof(VegaFeatureFactory)
        .GetMethod("IsToolSpecificMarkerSupported", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("VegaFeatureFactory.IsToolSpecificMarkerSupported not found.");

    [Fact]
    public void NullToolFeatures_EfemFeaturesAreNotRegistered() => AssertEfemFeaturesRegistered(false, null);

    [Fact]
    public void EmptyToolFeatures_EfemFeaturesAreNotRegistered() => AssertEfemFeaturesRegistered(false, []);

    [Fact]
    public void LegacyEfemToken_EfemFeaturesAreRegistered() => AssertEfemFeaturesRegistered(true, [ToolFeaturesHelper.EFEMToken]);

    [Fact]
    public void RorzeToken_EfemFeaturesAreRegistered() => AssertEfemFeaturesRegistered(true, [ToolFeaturesHelper.EFEMRorzeToken]);

    [Fact]
    public void FortrendToken_EfemFeaturesAreRegistered() => AssertEfemFeaturesRegistered(true, [ToolFeaturesHelper.EFEMFortrendToken]);

    [Fact]
    public void ClpaToken_RegistersOnlyClpaFeature()
    {
        HashSet<long> ids = GetSupportedFeatureIds(["CLPA"]);

        Assert.Contains(VegaFeatures.Recipe_CLPA, ids);
        Assert.DoesNotContain(VegaFeatures.Bright_Field_Channel, ids);
    }

    [Fact]
    public void DicToken_RegistersOnlyDicFeature()
    {
        HashSet<long> ids = GetSupportedFeatureIds(["DIC"]);

        Assert.Contains(VegaFeatures.Bright_Field_Channel, ids);
        Assert.DoesNotContain(VegaFeatures.Recipe_CLPA, ids);
    }

    /// <summary>
    /// 过滤逻辑全矩阵：toolFeatures=null；marker="EFEM" 与裸 EFEM/EFEMRorze/EFEMFortrend/无关 token；
    /// 其他 marker（CLPA/DIC）命中与未命中；marker=null 边界（永不放行）。
    /// </summary>
    [Theory]
    [InlineData(ToolFeaturesHelper.EFEMToken, null, false)]
    [InlineData(ToolFeaturesHelper.EFEMToken, "", false)]
    [InlineData(ToolFeaturesHelper.EFEMToken, ToolFeaturesHelper.EFEMToken, true)]
    [InlineData(ToolFeaturesHelper.EFEMToken, ToolFeaturesHelper.EFEMRorzeToken, true)]
    [InlineData(ToolFeaturesHelper.EFEMToken, ToolFeaturesHelper.EFEMFortrendToken, true)]
    [InlineData(ToolFeaturesHelper.EFEMToken, "CLPA", false)]
    [InlineData("CLPA", "CLPA", true)]
    [InlineData("CLPA", "DIC", false)]
    [InlineData("DIC", "DIC", true)]
    [InlineData("DIC", "", false)]
    [InlineData(null, null, false)]
    [InlineData(null, "", false)]
    [InlineData(null, ToolFeaturesHelper.EFEMToken, false)]
    public void IsToolSpecificMarkerSupported_HandlesMarkerAndTokenMatrix(
        string? marker,
        string? featuresCsv,
        bool expected)
    {
        List<string>? grants = featuresCsv is null
            ? null
            : [.. featuresCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)];

        bool supported = (bool) s_isToolSpecificMarkerSupported.Invoke(null, [marker, grants])!;

        Assert.Equal(expected, supported);
    }

    /// <summary>
    /// 供应商互斥健康检查：Fortrend 与 Rorze 族（含存量裸 EFEM）并存即为冲突；
    /// EFEM + EFEMRorze 同属 Rorze 族，不报冲突。
    /// </summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(ToolFeaturesHelper.EFEMToken, false)]
    [InlineData(ToolFeaturesHelper.EFEMRorzeToken, false)]
    [InlineData(ToolFeaturesHelper.EFEMFortrendToken, false)]
    [InlineData(ToolFeaturesHelper.EFEMToken + "," + ToolFeaturesHelper.EFEMRorzeToken, false)]
    [InlineData(ToolFeaturesHelper.EFEMFortrendToken + "," + ToolFeaturesHelper.EFEMRorzeToken, true)]
    [InlineData(ToolFeaturesHelper.EFEMFortrendToken + "," + ToolFeaturesHelper.EFEMToken, true)]
    [InlineData(ToolFeaturesHelper.EFEMToken + "," + ToolFeaturesHelper.EFEMRorzeToken + "," + ToolFeaturesHelper.EFEMFortrendToken, true)]
    [InlineData("CLPA", false)]
    public void HasConflictingEFEMVendorTokens_DetectsMixedVendors(string? featuresCsv, bool expected)
    {
        List<string>? features = featuresCsv is null
            ? null
            : [.. featuresCsv.Split(',', StringSplitOptions.RemoveEmptyEntries)];

        Assert.Equal(expected, VegaFeatureFactory.HasConflictingEFEMVendorTokens(features));
    }

    private static void AssertEfemFeaturesRegistered(bool expected, List<string>? grants)
    {
        HashSet<long> ids = GetSupportedFeatureIds(grants);

        Assert.Equal(expected, ids.Contains(Features.EFEM_Support));
        Assert.Equal(expected, ids.Contains(Features.Wafer_Recovery));
    }

    private static HashSet<long> GetSupportedFeatureIds(List<string>? grants)
    {
        var original = (Lazy<List<string>>?) s_allGrantsField.GetValue(null);
        s_allGrantsField.SetValue(null, new Lazy<List<string>>(() => grants!));
        try
        {
            return new VegaFeatureFactory().GetSupportedFeatures()
                .Select(feature => feature.ID)
                .ToHashSet();
        }
        finally
        {
            s_allGrantsField.SetValue(null, original);
        }
    }
}