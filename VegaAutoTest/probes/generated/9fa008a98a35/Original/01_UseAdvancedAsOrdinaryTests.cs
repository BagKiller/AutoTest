// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vega.Data.Recipes;
using Vega.Data.Defects;
using Vega.Platform.IDatabase;
using Vega.Utility.Common;

namespace Vega.UI.Inspection.Tests;

public class UseAdvancedAsOrdinaryTests
{
    [Theory]
    [InlineData("DW2A Default", "DW2 Default")]
    [InlineData("DCA Default", "DC Default")]
    [InlineData("GCA Default", "GC Default")]
    public void GetOrdinaryChannelName_AdvancedChannel_ReturnsMatchingOrdinaryChannel(string advancedChannel, string ordinaryChannel)
    {
        var result = InvokePrivateStatic("GetOrdinaryChannelName", advancedChannel);

        Assert.Equal(ordinaryChannel, result);
    }

    [Theory]
    [InlineData("DW2 Default")]
    [InlineData("DC Default")]
    [InlineData("")]
    public void GetOrdinaryChannelName_OrdinaryOrEmptyChannel_ReturnsNull(string channelName)
    {
        var result = InvokePrivateStatic("GetOrdinaryChannelName", channelName);

        Assert.Null(result);
    }

    [Fact]
    public void Clone_PreservesAppliedUseAdvancedAsOrdinary()
    {
        var source = new DefectDefinitionMapCollection
        {
            AppliedUseAdvancedAsOrdinary = true
        };

        var clone = source.Clone();

        Assert.True(clone.AppliedUseAdvancedAsOrdinary);
    }

    [Fact]
    public void Reconcile_DisabledAnalysis_RemovesItsStaleDefectDefinition()
    {
        var collections = (CollectionsContent)RuntimeHelpers.GetUninitializedObject(typeof(CollectionsContent));
        collections.Collections = [];

        var recipe = (Recipe)RuntimeHelpers.GetUninitializedObject(typeof(Recipe));
        recipe.IsWaferRunRecipe = true;
        recipe.RecipeElements = [collections, collections];
        recipe.DefectDefinitionMapCollection = new DefectDefinitionMapCollection();
        recipe.DefectDefinitionMapCollection.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = "DW2A Default",
            Definitions = [new DefectDefinition { DefectName = "Slipline", Attributes = [] }]
        });

        var analysis = (TestChannelDefectAnalysis)RuntimeHelpers.GetUninitializedObject(typeof(TestChannelDefectAnalysis));
        analysis.IsElementEnabled = false;
        analysis.SelectedChannelName = "DW2A Default";
        analysis.TargetDefectMap = new ObservableCollection<DefectSourceDefinition>
        {
            new() { DefectName = "Slipline" }
        };

        var viewModel = (RecipeViewModel)RuntimeHelpers.GetUninitializedObject(typeof(RecipeViewModel));
        SetInstanceField(viewModel, "_currentRecipe", recipe);
        SetInstanceField(viewModel, "_isRecipeElementsFullyInitialized", true);
        SetInstanceField(viewModel, "_recipeElementViewModels", new SmartObservableCollection<RecipeElementViewModelBase> { analysis });

        viewModel.OnActivated();

        Assert.Empty(recipe.DefectDefinitionMapCollection.DefinitionMaps.Single().Definitions);
        Assert.False(recipe.DefectDefinitionMapCollection.AppliedUseAdvancedAsOrdinary);
    }

    [Fact]
    public void Reconcile_WhenEnablingUseAdvancedAsOrdinary_MirrorsAdvancedDefinitionsToOrdinaryChannel()
    {
        var advancedAnalysis = CreateAnalysis("DW2A Default", "Slipline");
        var (recipe, viewModel) = CreateReconciliationFixture(false, true, advancedAnalysis);
        AddRecipeDefinition(recipe, "DW2A Default", "Slipline");

        viewModel.OnActivated();

        AssertDefinitionExists(recipe, "DW2A Default", "Slipline");
        AssertDefinitionExists(recipe, "DW2 Default", "Slipline");
        Assert.True(recipe.DefectDefinitionMapCollection.AppliedUseAdvancedAsOrdinary);
    }

    [Fact]
    public void Reconcile_WhenDisablingUseAdvancedAsOrdinary_RemovesOnlyOrdinaryMirror()
    {
        var advancedAnalysis = CreateAnalysis("DW2A Default", "Slipline");
        var (recipe, viewModel) = CreateReconciliationFixture(true, false, advancedAnalysis);
        AddRecipeDefinition(recipe, "DW2A Default", "Slipline");
        AddRecipeDefinition(recipe, "DW2 Default", "Slipline");

        viewModel.OnActivated();

        AssertDefinitionExists(recipe, "DW2A Default", "Slipline");
        AssertDefinitionDoesNotExist(recipe, "DW2 Default", "Slipline");
        Assert.False(recipe.DefectDefinitionMapCollection.AppliedUseAdvancedAsOrdinary);
    }

    [Fact]
    public void Reconcile_WhenDisablingUseAdvancedAsOrdinary_KeepsDefinitionsUsedByOrdinaryAnalysis()
    {
        var advancedAnalysis = CreateAnalysis("DW2A Default", "Slipline");
        var ordinaryAnalysis = CreateAnalysis("DW2 Default", "Slipline");
        var (recipe, viewModel) = CreateReconciliationFixture(true, false, advancedAnalysis, ordinaryAnalysis);
        AddRecipeDefinition(recipe, "DW2A Default", "Slipline");
        AddRecipeDefinition(recipe, "DW2 Default", "Slipline");

        viewModel.OnActivated();

        AssertDefinitionExists(recipe, "DW2A Default", "Slipline");
        AssertDefinitionExists(recipe, "DW2 Default", "Slipline");
    }

    [Fact]
    public void Reconcile_WhenSpatialSubParametersAreEnabled_RestoresTheirMirroredDefinitions()
    {
        var spatialAnalysis = (SpatialAnalysisViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SpatialAnalysisViewModel));
        SetInstanceField(spatialAnalysis, "_isElementEnabled", true);
        SetInstanceField(spatialAnalysis, "_selectedChannelName", "DW2A Default");
        SetInstanceField(spatialAnalysis, "_targetDefectMap", SourceDefinitions("LPD", "Slipline", "Cluster", "Scratch"));
        SetInstanceField(spatialAnalysis, "_isSliplineEnabled", true);
        SetInstanceField(spatialAnalysis, "_isClusterScratchEnabled", true);

        var (recipe, viewModel) = CreateReconciliationFixture(false, true, spatialAnalysis);

        viewModel.OnActivated();

        AssertDefinitionExists(recipe, "DW2 Default", "LPD");
        AssertDefinitionExists(recipe, "DW2 Default", "Slipline");
        AssertDefinitionExists(recipe, "DW2 Default", "Cluster");
        AssertDefinitionExists(recipe, "DW2 Default", "Scratch");
    }

    [Fact]
    public void Reconcile_WhenAppliedStateChanges_SavesRecipe()
    {
        var analysis = CreateAnalysis("DW2A Default", "Slipline");
        var (recipe, viewModel) = CreateReconciliationFixture(false, true, analysis);
        recipe.IsWaferRunRecipe = false;
        var recipeProvider = RecipeStorageProviderProxy.Create();
        viewModel.RecipeProvider = recipeProvider;

        viewModel.OnActivated();

        Assert.Equal(1, RecipeStorageProviderProxy.GetSaveFileCallCount(recipeProvider));
    }

    [Fact]
    public void Reconcile_WhenForcedAndAppliedStateIsUnchanged_DoesNotSaveRecipeAgain()
    {
        var analysis = CreateAnalysis("DW2A Default", "Slipline");
        var (recipe, viewModel) = CreateReconciliationFixture(true, true, analysis);
        recipe.IsWaferRunRecipe = false;
        AddRecipeDefinition(recipe, "DW2A Default", "Slipline");
        AddRecipeDefinition(recipe, "DW2 Default", "Slipline");
        var recipeProvider = RecipeStorageProviderProxy.Create();
        viewModel.RecipeProvider = recipeProvider;

        InvokePrivateInstance(viewModel, "ReconcileUseAdvancedAsOrdinary", true);

        Assert.Equal(0, RecipeStorageProviderProxy.GetSaveFileCallCount(recipeProvider));
    }

    private static (Recipe Recipe, RecipeViewModel ViewModel) CreateReconciliationFixture(
        bool appliedUseAdvancedAsOrdinary,
        bool useAdvancedAsOrdinary,
        params RecipeElementViewModelBase[] analyses)
    {
        var collections = (CollectionsContent)RuntimeHelpers.GetUninitializedObject(typeof(CollectionsContent));
        collections.Collections = [];

        var recipe = (Recipe)RuntimeHelpers.GetUninitializedObject(typeof(Recipe));
        recipe.IsWaferRunRecipe = true;
        recipe.RecipeElements = [collections, collections];
        recipe.DefectDefinitionMapCollection = new DefectDefinitionMapCollection
        {
            AppliedUseAdvancedAsOrdinary = appliedUseAdvancedAsOrdinary
        };

        var definitionProvider = new TestDefinitionProvider(CreateSourceDefinitions());
        var viewModel = (RecipeViewModel)RuntimeHelpers.GetUninitializedObject(typeof(RecipeViewModel));
        SetInstanceField(viewModel, "_currentRecipe", recipe);
        SetInstanceField(viewModel, "_isRecipeElementsFullyInitialized", true);
        var viewModels = new SmartObservableCollection<RecipeElementViewModelBase>();
        foreach (var analysis in analyses)
        {
            viewModels.Add(analysis);
        }
        SetInstanceField(viewModel, "_recipeElementViewModels", viewModels);
        viewModel.ConfigsManager = TestConfigsManager.Create(useAdvancedAsOrdinary);
        viewModel.DefinitionProvider = definitionProvider;

        return (recipe, viewModel);
    }

    private static TestChannelDefectAnalysis CreateAnalysis(string channelName, params string[] defectNames)
    {
        var analysis = (TestChannelDefectAnalysis)RuntimeHelpers.GetUninitializedObject(typeof(TestChannelDefectAnalysis));
        analysis.IsElementEnabled = true;
        analysis.SelectedChannelName = channelName;
        analysis.TargetDefectMap = SourceDefinitions(defectNames);
        return analysis;
    }

    private static DefectDefinitionMapCollection CreateSourceDefinitions()
    {
        var definitions = new DefectDefinitionMapCollection();
        definitions.DefinitionMaps.Add(CreateDefinitionMap("DW2A"));
        definitions.DefinitionMaps.Add(CreateDefinitionMap("DW2"));
        return definitions;
    }

    private static DefectDefinitionMap CreateDefinitionMap(string mode) => new()
    {
        Mode = mode,
        Definitions = new[] { "LPD", "Slipline", "Cluster", "Scratch" }.Select(CreateDefinition).ToList()
    };

    private static DefectDefinition CreateDefinition(string defectName) => new()
    {
        DefectName = defectName,
        Attributes = []
    };

    private static ObservableCollection<DefectSourceDefinition> SourceDefinitions(params string[] defectNames) =>
        new(defectNames.Select(defectName => new DefectSourceDefinition { DefectName = defectName }));

    private static void AddRecipeDefinition(Recipe recipe, string mode, string defectName)
    {
        recipe.DefectDefinitionMapCollection.DefinitionMaps.Add(new DefectDefinitionMap
        {
            Mode = mode,
            Definitions = [CreateDefinition(defectName)]
        });
    }

    private static void AssertDefinitionExists(Recipe recipe, string mode, string defectName) =>
        Assert.Contains(recipe.DefectDefinitionMapCollection.DefinitionMaps.Single(map => map.Mode == mode).Definitions,
            definition => definition.DefectName == defectName);

    private static void AssertDefinitionDoesNotExist(Recipe recipe, string mode, string defectName) =>
        Assert.DoesNotContain(recipe.DefectDefinitionMapCollection.DefinitionMaps.Single(map => map.Mode == mode).Definitions,
            definition => definition.DefectName == defectName);

    private static void SetInstanceField(object instance, string fieldName, object value)
    {
        var field = GetInstanceField(instance.GetType(), fieldName);
        Assert.NotNull(field);

        field.SetValue(instance, value);
    }

    private static FieldInfo? GetInstanceField(Type? type, string fieldName)
    {
        while (type != null)
        {
            var field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                return field;
            }
            type = type.BaseType;
        }

        return null;
    }

    private static object? InvokePrivateStatic(string methodName, params object[] arguments)
    {
        var method = typeof(RecipeViewModel).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        return method.Invoke(null, arguments);
    }

    private static object? InvokePrivateInstance(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        return method.Invoke(instance, arguments);
    }

    private sealed class TestDefinitionProvider(DefectDefinitionMapCollection definitions) : IDefectDefinitionMapCollectionProvider
    {
        public DefectDefinitionMapCollection DefectDefinitionMapCollection { get; } = definitions;
        public void Load() { }
        public void Save(string? filePath = null) { }
        public void Update(string filePath, params string[] mapNames) { }
    }

    private class TestConfigsManager : DispatchProxy
    {
        private readonly AnalysisConfig _analysisConfig = new();

        public static IConfigsManager Create(bool useAdvancedAsOrdinary)
        {
            var proxy = DispatchProxy.Create<IConfigsManager, TestConfigsManager>();
            ((TestConfigsManager)(object)proxy)._analysisConfig.UseAdvancedAsOrdinary = useAdvancedAsOrdinary;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_AnalysisConfig" ? _analysisConfig : null;
    }

    private class RecipeStorageProviderProxy : DispatchProxy
    {
        private int _saveFileCallCount;

        public static IRecipeStorageProvider Create() => DispatchProxy.Create<IRecipeStorageProvider, RecipeStorageProviderProxy>();

        public static int GetSaveFileCallCount(IRecipeStorageProvider provider) =>
            ((RecipeStorageProviderProxy)(object)provider)._saveFileCallCount;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == "SaveFile")
            {
                _saveFileCallCount++;
            }

            return null;
        }
    }

    public sealed class TestChannelDefectAnalysis : RecipeElementViewModelBase, IChannelDefectAnalysis
    {
        public string AnalysisType => "Test";
        public ObservableCollection<string> AvailableChannels { get; } = [];
        public string SelectedChannelName { get; set; } = string.Empty;
        public ObservableCollection<DefectSourceDefinition> TargetDefectMap { get; set; } = [];

        public void ReorderAvailableChannels(Recipe recipe)
        {
        }
    }
}