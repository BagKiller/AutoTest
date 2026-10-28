// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Runtime.CompilerServices;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.Data.Recipes;
using Vega.Data.Recipes.Migrations;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   SPA/SAE 管理的动态通道定义（Cluster/Scratch/Slipline）对账器测试。
///   <para>
///   与 Migration0004SaeScratchSliplineTests 同理，完整 <c>Recipe</c> 的构造依赖 Galaxy 框架，
///   单元测试环境不具备，因此直接测试核心原语
///   <see cref="SpaManagedDefinitionReconciler.Reconcile"/> 与
///   <see cref="SpaManagedDefinitionReconciler.ComputeExpectedChannelDefects"/>。
///   </para>
/// </summary>
public class SpaManagedDefinitionReconcilerTests
{
    private static DefectDefinitionMapCollection CreateRecipeMaps()
    {
        var maps = new DefectDefinitionMapCollection();
        // 基础 collector map（非动态），对账不应触碰
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW1A",
            Definitions = [new DefectDefinition(0, "LPD", true, 0, 0xFF00BFFF)]
        });
        // 复现线上 recipe 的 DW1A Oblique：Cluster/Scratch/Slipline 齐备（Slipline 为残留）
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW1A Oblique",
            Definitions =
            [
                new DefectDefinition(5, "Cluster", true, true, 200, 0xFFFFC000, [new DefectAttribute { AttributeName = "Area" }], "Area", null),
                new DefectDefinition(6, "Scratch", true, true, 210, 0xFF33FFFF, [new DefectAttribute { AttributeName = "Area" }], "Area", null),
                new DefectDefinition(7, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Area", null)
            ]
        });
        // DW2A Oblique：无残留
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW2A Oblique",
            Definitions =
            [
                new DefectDefinition(5, "Cluster", true, true, 200, 0xFFFFC000, [new DefectAttribute { AttributeName = "Area" }], "Area", null),
                new DefectDefinition(6, "Scratch", true, true, 210, 0xFF33FFFF, [new DefectAttribute { AttributeName = "Area" }], "Area", null)
            ]
        });
        // DC 复合通道的 own map 同样参与对账（物理通道定义的合并在读取侧 GetMergedMap，不落盘）
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DC Oblique",
            Definitions = [new DefectDefinition(7, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Area", null)]
        });
        return maps;
    }

    [Fact]
    public void Reconcile_RemovesStaleSliplineWhenSpaSliplineDisabled()
    {
        var maps = CreateRecipeMaps();
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DW1A Oblique"] = ["Cluster", "Scratch"],
            ["DW2A Oblique"] = ["Cluster", "Scratch"]
        };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var dw1a = maps.DefinitionMaps.Single(m => m.Mode == "DW1A Oblique");
        Assert.Equal(["Cluster", "Scratch"], dw1a.Definitions.Select(d => d.DefectName));
        // DW2A 本就匹配，不应变更
        var dw2a = maps.DefinitionMaps.Single(m => m.Mode == "DW2A Oblique");
        Assert.Equal(["Cluster", "Scratch"], dw2a.Definitions.Select(d => d.DefectName));
    }

    [Fact]
    public void Reconcile_KeepsSliplineWhenExpected()
    {
        var maps = CreateRecipeMaps();
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DW1A Oblique"] = ["Cluster", "Scratch", "Slipline"],
            ["DW2A Oblique"] = ["Cluster", "Scratch"],
            ["DC Oblique"] = ["Slipline"]
        };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.False(changed);
        var dw1a = maps.DefinitionMaps.Single(m => m.Mode == "DW1A Oblique");
        Assert.Contains(dw1a.Definitions, d => d.DefectName == "Slipline");
    }

    [Fact]
    public void Reconcile_IsIdempotent()
    {
        var maps = CreateRecipeMaps();
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DW1A Oblique"] = ["Cluster", "Scratch"]
        };

        bool first = SpaManagedDefinitionReconciler.Reconcile(maps, expected);
        bool second = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public void Reconcile_RemovesAllManagedDefectsWhenChannelHasNoExpectedEntry()
    {
        var maps = CreateRecipeMaps();

        // 没有任何期望条目（SPA 已禁用/删除）：所有动态通道的管理名全部清除
        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, new Dictionary<string, HashSet<string>>());

        Assert.True(changed);
        Assert.Empty(maps.DefinitionMaps.Single(m => m.Mode == "DW1A Oblique").Definitions);
        Assert.Empty(maps.DefinitionMaps.Single(m => m.Mode == "DW2A Oblique").Definitions);
        // 基础 map 不受影响；复合通道的 own map 参与对账，管理名同样清除
        Assert.NotEmpty(maps.DefinitionMaps.Single(m => m.Mode == "DW1A").Definitions);
        Assert.Empty(maps.DefinitionMaps.Single(m => m.Mode == "DC Oblique").Definitions);
    }

    [Fact]
    public void Reconcile_CleansCompositeChannelResidueAndBackfillsExpected()
    {
        var maps = CreateRecipeMaps();
        // DC 复合通道（Work Item 点名的 DCA 同属 DC 组）：期望 Cluster/Scratch，Slipline 为残留
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DC Oblique"] = ["Cluster", "Scratch"]
        };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var dc = maps.DefinitionMaps.Single(m => m.Mode == "DC Oblique");
        Assert.Equal(["Cluster", "Scratch"], dc.Definitions.Select(d => d.DefectName));
    }

    [Fact]
    public void Reconcile_CleansNoCollectionCompositeChannelWhenExpected()
    {
        var maps = new DefectDefinitionMapCollection();
        // collection 为空时复合通道名就是 "DCA"，Mode 不含空格，不是 IsDynamicMap
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DCA",
            Definitions =
            [
                new DefectDefinition(0, "LPD", true, 100, 0xFF00BFFF),
                new DefectDefinition(7, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Length", null)
            ]
        });
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DCA"] = ["Cluster", "Scratch"]
        };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var dca = maps.DefinitionMaps.Single(m => m.Mode == "DCA");
        Assert.Equal(["LPD", "Cluster", "Scratch"], dca.Definitions.Select(d => d.DefectName));
    }

    [Fact]
    public void Reconcile_DoesNotTouchCompositeBaseMapWithoutExpectation()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DC",
            Definitions = [new DefectDefinition(7, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Length", null)]
        });

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, new Dictionary<string, HashSet<string>>());

        Assert.False(changed);
        Assert.Contains(maps.DefinitionMaps.Single(m => m.Mode == "DC").Definitions, d => d.DefectName == "Slipline");
    }

    [Fact]
    public void Reconcile_DoesNotTouchNonManagedDefinitions()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW1A Oblique",
            Definitions =
            [
                new DefectDefinition(5, "Cluster", true, true, 200, 0xFFFFC000, [new DefectAttribute { AttributeName = "Area" }], "Area", null),
                new DefectDefinition(99, "LineLength", true, true, 300, 0xFF123456, [new DefectAttribute { AttributeName = "Length" }], "Length", null)
            ]
        });
        var expected = new Dictionary<string, HashSet<string>>();

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var map = maps.DefinitionMaps.Single(m => m.Mode == "DW1A Oblique");
        // 只清管理名，Linear/NonLinear 等其他元素写入的定义保留
        Assert.Equal(["LineLength"], map.Definitions.Select(d => d.DefectName));
    }

    [Fact]
    public void Reconcile_BackfillsMissingExpectedDefinitionsWithBuiltinTemplate()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW2A Oblique",
            Definitions = [new DefectDefinition(5, "Cluster", true, true, 200, 0xFFFFC000, [new DefectAttribute { AttributeName = "Area" }], "Area", null)]
        });
        var expected = new Dictionary<string, HashSet<string>>
        {
            ["DW2A Oblique"] = ["Cluster", "Scratch", "Slipline"]
        };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var map = maps.DefinitionMaps.Single(m => m.Mode == "DW2A Oblique");
        Assert.Equal(["Cluster", "Scratch", "Slipline"], map.Definitions.Select(d => d.DefectName));
        var slipline = map.Definitions.Single(d => d.DefectName == "Slipline");
        Assert.True(slipline.IsDynamic);
        Assert.Equal(220, slipline.RBC);
        Assert.Contains("Length", slipline.GetAttributes());
    }

    [Fact]
    public void Reconcile_PrefersProviderTemplateForBackfill()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap { Mode = "DW1A Oblique", Definitions = [] });
        var expected = new Dictionary<string, HashSet<string>> { ["DW1A Oblique"] = ["Slipline"] };

        try
        {
            RecipeMigrationTemplateSource.SetFactory(channelName => channelName == "DW1A Oblique"
                ? new List<DefectDefinition> { new(7, "Slipline", true, true, 222, 0xFF778899, [new DefectAttribute { AttributeName = "Length" }], "Length", null) }
                : null);

            bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected, RecipeMigrationTemplateSource.Resolve);

            Assert.True(changed);
            var slipline = maps.DefinitionMaps.Single(m => m.Mode == "DW1A Oblique").Definitions.Single(d => d.DefectName == "Slipline");
            Assert.Equal(222, slipline.RBC);
            Assert.Equal(0xFF778899, slipline.ColorCode);
        }
        finally
        {
            RecipeMigrationTemplateSource.SetFactory(null);
        }
    }

    [Fact]
    public void ComputeExpected_RespectsSpaTogglesAndElementState()
    {
        var elements = new List<ElementContentBase>
        {
            new SpatialAnalysisContent("SPA 1")
            {
                IsElementEnabled = true,
                SelectedChannelName = "DW1A Oblique",
                IsSliplineEnabled = false,
                IsClusterScratchEnabled = true
            },
            new SpatialAnalysisContent("SPA 2")
            {
                IsElementEnabled = false, // 禁用元素不贡献
                SelectedChannelName = "DW2A Oblique",
                IsSliplineEnabled = true,
                IsClusterScratchEnabled = true
            }
        };

        var expected = SpaManagedDefinitionReconciler.ComputeExpectedChannelDefects(elements, useAdvancedAsOrdinary: false);

        var entry = expected.Single();
        Assert.Equal("DW1A Oblique", entry.Key);
        Assert.Equal(["Cluster", "Scratch"], entry.Value);
    }

    [Fact]
    public void ComputeExpected_SaeContributesScratchAndSlipline()
    {
        var elements = new List<ElementContentBase>
        {
            new SliplineAnalysisContent("SAE 1")
            {
                IsElementEnabled = true,
                SelectedChannelName = "DW2A Oblique"
            }
        };

        var expected = SpaManagedDefinitionReconciler.ComputeExpectedChannelDefects(elements, useAdvancedAsOrdinary: false);

        Assert.Equal(["Scratch", "Slipline"], expected["DW2A Oblique"]);
    }

    [Fact]
    public void ComputeExpected_MirrorsAdvancedToOrdinaryWhenUseAdvancedAsOrdinary()
    {
        var elements = new List<ElementContentBase>
        {
            new SpatialAnalysisContent("SPA 1")
            {
                IsElementEnabled = true,
                SelectedChannelName = "DW1A Oblique",
                IsSliplineEnabled = false,
                IsClusterScratchEnabled = true
            }
        };

        var expected = SpaManagedDefinitionReconciler.ComputeExpectedChannelDefects(elements, useAdvancedAsOrdinary: true);

        Assert.Equal(["Cluster", "Scratch"], expected["DW1A Oblique"]);
        Assert.Equal(["Cluster", "Scratch"], expected["DW1 Oblique"]);
    }

    [Theory]
    [InlineData("DW1A Oblique", "DW1 Oblique")]
    [InlineData("DW1A", "DW1")]
    [InlineData("DNA Oblique", "DN Oblique")]
    [InlineData("DW1 Oblique", null)]
    [InlineData("", null)]
    public void GetOrdinaryChannelName_MapsAdvancedChannels(string channelName, string? expected)
    {
        Assert.Equal(expected, SpaManagedDefinitionReconciler.GetOrdinaryChannelName(channelName));
    }

    [Fact]
    public void Reconcile_LeavesSurfImageScopedDefinitionsUntouched()
    {
        var maps = CreateSingleChannelMapsWithSurfSlipline();
        var expected = new Dictionary<string, HashSet<string>> { ["DW1A Oblique"] = [] };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.False(changed);
        Assert.Single(maps.DefinitionMaps.Single().Definitions);
    }

    [Fact]
    public void Reconcile_BackfillsWhenOnlySurfImageScopedDefinitionExists()
    {
        var maps = CreateSingleChannelMapsWithSurfSlipline();
        var expected = new Dictionary<string, HashSet<string>> { ["DW1A Oblique"] = ["Slipline"] };

        bool changed = SpaManagedDefinitionReconciler.Reconcile(maps, expected);

        Assert.True(changed);
        var definitions = maps.DefinitionMaps.Single().Definitions;
        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, d => d.DefectName == "Slipline" && string.IsNullOrEmpty(d.SurfImageCategoryName));
        Assert.Contains(definitions, d => d.DefectName == "Slipline" && !string.IsNullOrEmpty(d.SurfImageCategoryName));
    }

    [Fact]
    public void Ensure_RemovesInvalidLayoutAndBinningReferences()
    {
        // 构造器会读硬件启用的 collector（触 Galaxy 容器），测试用未初始化对象
        var graphicLayout = (GraphicLayoutContent)RuntimeHelpers.GetUninitializedObject(typeof(GraphicLayoutContent));
        graphicLayout.GraphicItems = [];
        graphicLayout.GraphicItems.Add(new GraphicItemContent(
            new Area(0, 0, 1, 1),
            GraphicTypes.WaferMap,
            new DefectCategoryItemContent("DW1", "Oblique", "Slipline", "Length", 0xFF00FF00, isAdvanced: true)));
        // 缺陷名小写：验证类别项与统计键共用忽略大小写口径
        graphicLayout.GraphicItems.Add(new GraphicItemContent(
            new Area(0, 0, 1, 1),
            GraphicTypes.Histogram,
            new DefectCategoryItemContent("DW1", "Oblique", "slipline", "Length", 0xFF00FF00, isAdvanced: true)));
        // 对账前就为空的图表项：不在清理范围内，应保留给 GraphicLayoutViewModel.LoadData 处理
        var preExistingEmptyItem = new GraphicItemContent();
        graphicLayout.GraphicItems.Add(preExistingEmptyItem);

        var channelSummary = new ChannelDefectSummaryContent([new ChannelInfo("Oblique", ChannelMode.DW1_Oblique, 1000, true)]);
        var stats = channelSummary.ChannelSummaryList[0].ChannelSummaryDic;
        stats["Slipline Count"] = 0;
        stats["Slipline Total Length(μm)"] = 0;
        stats["SliplineExtra Count"] = 0;
        stats["Count(Defects)"] = 3;

        var binSummary = new DefectBinSummaryContent(
            new DefectCategoryItemContent("DW1", "Oblique", "Slipline", "Length", 0xFF00FF00, isAdvanced: true));

        var layout = (ResultsLayoutContent)RuntimeHelpers.GetUninitializedObject(typeof(ResultsLayoutContent));
        layout.GraphicLayout = graphicLayout;
        layout.SummaryList = [binSummary, channelSummary];

        var binning = new BinningContent("Binning 1");
        binning.BinningCollections.Add(new BinningItemContent
        {
            Category = new DefectCategoryItemContent("DW1", "Oblique", "Slipline", "Length", 0xFF00FF00, isAdvanced: true)
        });

        var spa = new SpatialAnalysisContent("SPA 1")
        {
            IsElementEnabled = true,
            SelectedChannelName = "DW1A Oblique",
            IsClusterScratchEnabled = false,
            IsSliplineEnabled = false
        };

        var maps = CreateSingleChannelMapsWithSlipline();
        var recipe = (Recipe)RuntimeHelpers.GetUninitializedObject(typeof(Recipe));
        recipe.RecipeElements = [spa, layout, binning];
        recipe.DefectDefinitionMapCollection = maps;

        bool changed = SpaManagedDefinitionReconciler.Ensure(recipe, useAdvancedAsOrdinary: false);

        Assert.True(changed);
        Assert.Empty(maps.DefinitionMaps.Single().Definitions);
        // 只清类别项，不删图表项（空图表由 GraphicLayoutViewModel.LoadData 移除并 AutoTile）
        Assert.Equal(3, graphicLayout.GraphicItems.Count);
        Assert.Empty(graphicLayout.GraphicItems[0].CategoryItems);
        Assert.Empty(graphicLayout.GraphicItems[1].CategoryItems);
        Assert.Contains(preExistingEmptyItem, graphicLayout.GraphicItems);
        Assert.DoesNotContain(layout.SummaryList, summary => summary is DefectBinSummaryContent);
        Assert.DoesNotContain("Slipline Count", stats.Keys);
        Assert.DoesNotContain("Slipline Total Length(μm)", stats.Keys);
        Assert.Contains("SliplineExtra Count", stats.Keys);
        Assert.Contains("Count(Defects)", stats.Keys);
        Assert.Empty(binning.BinningCollections);
    }

    private static DefectDefinitionMapCollection CreateSingleChannelMapsWithSlipline()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW1A Oblique",
            Definitions =
            [
                new DefectDefinition(7, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Length", null)
            ]
        });
        return maps;
    }

    private static DefectDefinitionMapCollection CreateSingleChannelMapsWithSurfSlipline()
    {
        var maps = new DefectDefinitionMapCollection();
        maps.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW1A Oblique",
            Definitions =
            [
                new DefectDefinition(99, "Slipline", true, true, 220, 0xFF00FF00, [new DefectAttribute { AttributeName = "Length" }], "Length", "[Image]")
            ]
        });
        return maps;
    }
}