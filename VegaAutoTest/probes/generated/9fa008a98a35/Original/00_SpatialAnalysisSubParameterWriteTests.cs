// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;
using Vega.Data.Defects;
using Vega.Data.Recipes;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// SPA 子参数开关与 TargetDefectMap 的回归测试：
/// TargetDefectMap 必须始终等于 SPA 的实际产出（只含已勾选子参数对应的缺陷），
/// 下游所有写通道定义的路径直接使用它。
/// </summary>
public class SpatialAnalysisSubParameterWriteTests
{
    private const string ElementName = "SPA 1";
    private const string ChannelName = "DW1A Oblique";

    [Fact]
    public void Load_WithSubParametersDisabled_TargetDefectsEmpty()
    {
        var (vm, _, _, _) = CreateViewModel(clusterScratch: false, slipline: false);

        Assert.Empty(vm.TargetDefectMap);
    }

    [Fact]
    public void Load_WithOnlyClusterScratch_UsesRecipeSourceDefinitions()
    {
        var (vm, cluster, scratch, slipline) = CreateViewModel(clusterScratch: true, slipline: false);

        Assert.Equal(["Cluster", "Scratch"], vm.TargetDefectMap.Select(d => d.DefectName));
        Assert.Same(cluster, vm.TargetDefectMap[0]);
        Assert.Same(scratch, vm.TargetDefectMap[1]);
        Assert.Equal(201, vm.TargetDefectMap[0].RBC);
        Assert.Equal(211, vm.TargetDefectMap[1].RBC);
        Assert.DoesNotContain(slipline, vm.TargetDefectMap);
    }

    [Fact]
    public void ToggleSliplineOn_WithClusterScratchDisabled_WritesOnlySlipline()
    {
        var (vm, _, _, _) = CreateViewModel(clusterScratch: false, slipline: false);

        var messages = CaptureChannelMessages(() => vm.IsSliplineEnabled = true);

        Assert.Equal(2, messages.Count);
        var addMessage = Assert.Single(messages.Where(m => m.newChannelName == ChannelName));
        Assert.Equal(["Slipline"], addMessage.defects.Select(d => d.DefectName));
        Assert.Equal(["Slipline"], vm.TargetDefectMap.Select(d => d.DefectName));
    }

    [Fact]
    public void ToggleSliplineOff_WithClusterScratchEnabled_RemovesAllThenWritesClusterScratch()
    {
        var (vm, _, _, _) = CreateViewModel(clusterScratch: true, slipline: true);
        Assert.Equal(["Cluster", "Scratch", "Slipline"], vm.TargetDefectMap.Select(d => d.DefectName));

        var messages = CaptureChannelMessages(() => vm.IsSliplineEnabled = false);

        Assert.Equal(2, messages.Count);
        var removeMessage = Assert.Single(messages.Where(m => m.oldChannelName == ChannelName));
        Assert.Equal(["Cluster", "Scratch", "Slipline"], removeMessage.defects.Select(d => d.DefectName));
        var addMessage = Assert.Single(messages.Where(m => m.newChannelName == ChannelName));
        Assert.Equal(["Cluster", "Scratch"], addMessage.defects.Select(d => d.DefectName));
        Assert.Equal(["Cluster", "Scratch"], vm.TargetDefectMap.Select(d => d.DefectName));
    }

    private static (SpatialAnalysisViewModel Vm, DefectSourceDefinition Cluster, DefectSourceDefinition Scratch, DefectSourceDefinition Slipline)
        CreateViewModel(bool clusterScratch, bool slipline)
    {
        var sourceMap = new DefectSourceDefinitionMapCollection();
        var clusterDef = new DefectSourceDefinition(5, "Cluster", 201, 0xFFFFC001);
        var scratchDef = new DefectSourceDefinition(6, "Scratch", 211, 0xFF33FF01);
        var sliplineDef = new DefectSourceDefinition(7, "Slipline", 221, 0xFF00FF01);
        sourceMap.DefinitionMap.Definitions.AddRange([clusterDef, scratchDef, sliplineDef]);

        var recipe = (Recipe)RuntimeHelpers.GetUninitializedObject(typeof(Recipe));
        recipe.RecipeElements = [];
        recipe.DefectSourceDefinitionMapCollection = sourceMap;

        var vm = (SpatialAnalysisViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SpatialAnalysisViewModel));
        SetField(vm, "_content", new SpatialAnalysisContent(ElementName));
        SetField(vm, "_isElementEnabled", true);
        SetField(vm, "_selectedChannelName", ChannelName);
        SetField(vm, "_isClusterScratchEnabled", clusterScratch);
        SetField(vm, "_isSliplineEnabled", slipline);
        vm.TargetDefectMap = new ObservableCollection<DefectSourceDefinition>();

        var loadTargetDefects = typeof(SpatialAnalysisViewModel).GetMethod(
            "LoadTargetDefects", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("LoadTargetDefects not found.");
        loadTargetDefects.Invoke(vm, [recipe]);

        return (vm, clusterDef, scratchDef, sliplineDef);
    }

    private static List<UpdateSelectedChannelMessage> CaptureChannelMessages(Action action)
    {
        var messages = new List<UpdateSelectedChannelMessage>();
        var recipient = new object();
        WeakReferenceMessenger.Default.Register<UpdateSelectedChannelMessage>(
            recipient,
            (_, message) =>
            {
                if (message.elementName == ElementName)
                {
                    // 消息按引用传递集合，后续 RebuildTargetDefects 会原地清空重建；
                    // 这里在收到时快照，等于同步处理方当时看到的内容。
                    messages.Add(message with { defects = new ObservableCollection<DefectSourceDefinition>(message.defects) });
                }
            });
        try
        {
            action();
        }
        finally
        {
            WeakReferenceMessenger.Default.Unregister<UpdateSelectedChannelMessage>(recipient);
        }

        return messages;
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(instance, value);
                return;
            }
        }

        throw new InvalidOperationException($"Field {fieldName} not found.");
    }
}