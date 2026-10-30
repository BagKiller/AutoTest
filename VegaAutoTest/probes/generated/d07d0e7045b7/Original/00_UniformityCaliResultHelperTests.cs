// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Galaxy.Data.Calibrations;
using System.Collections.ObjectModel;
using Vega.Data.Calibration;
using Vega.Data.Commons;
using Vega.Data.Recipes;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   Uniformity 校准结果 Helper 纯函数回归：legacy 峰值数据按模式迁移（幂等）、
///   配对 Cal 键构造、配对 Cal 结果存在性判定（空壳行不算已校准）与 umLSE 配方判定。
/// </summary>
public class UniformityCaliResultHelperTests
{
    private static UniformityCaliConfigItem BuildItem(CalibrationMode mode)
    {
        return new UniformityCaliConfigItem
        {
            Key = UniformityCaliResultHelper.BuildCalKey(mode, WaferSizeType.WS300, SpinSpeedType.SpinStd, IncidenceType.Normal, ThroughputType.StandardThroughput),
            CalibrationMode = mode,
            WaferSize = WaferSizeType.WS300,
            SpinSpeed = SpinSpeedType.SpinStd,
            Incidence = IncidenceType.Normal,
            Throughput = ThroughputType.StandardThroughput,
        };
    }

    // —— MigrateLegacyRadiusAndPeakData ——

    [Fact]
    public void Migrate_CalLegacyList_MovesIntoRppmSizeListAndClearsLegacy()
    {
        var result = new UniformityCaliAssociatedResult(BuildItem(CalibrationMode.Cal).Key) { CalibrationMode = CalibrationMode.Cal };
        var measurement = new UniformityMeasurementResult();
        measurement.RadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 10, PeakData = 1.5 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(results);

        Assert.Empty(measurement.RadiusAndPeakDataList);
        Assert.Single(measurement.RppmSizeRadiusAndPeakDataList);
        Assert.Empty(measurement.SizeRadiusAndPeakDataList);
        Assert.Equal(10, measurement.RppmSizeRadiusAndPeakDataList[0].Radius);
        Assert.Equal(1.5, measurement.RppmSizeRadiusAndPeakDataList[0].PeakData);
    }

    [Fact]
    public void Migrate_VerifyLegacyList_MovesIntoSizeList()
    {
        var result = new UniformityCaliAssociatedResult(BuildItem(CalibrationMode.Verify).Key) { CalibrationMode = CalibrationMode.Verify };
        var measurement = new UniformityMeasurementResult();
        measurement.RadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 20, PeakData = 88.8 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(results);

        Assert.Empty(measurement.RadiusAndPeakDataList);
        Assert.Single(measurement.SizeRadiusAndPeakDataList);
        Assert.Empty(measurement.RppmSizeRadiusAndPeakDataList);
        Assert.Equal(88.8, measurement.SizeRadiusAndPeakDataList[0].PeakData);
    }

    [Fact]
    public void Migrate_SecondCall_IsIdempotent()
    {
        var result = new UniformityCaliAssociatedResult(BuildItem(CalibrationMode.Cal).Key) { CalibrationMode = CalibrationMode.Cal };
        var measurement = new UniformityMeasurementResult();
        measurement.RadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 10, PeakData = 1.5 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(results);
        UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(results);

        Assert.Single(measurement.RppmSizeRadiusAndPeakDataList);
    }

    [Fact]
    public void Migrate_NullMeasurementResultsOrEmptyLegacy_DoesNotThrow()
    {
        var nullResults = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput") { UniformityMeasurementResults = null! };
        var emptyLegacy = new UniformityCaliAssociatedResult("Cal.WS200.SpinStd.Oblique.StandardThroughput");
        emptyLegacy.UniformityMeasurementResults.Add(new UniformityMeasurementResult());
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { nullResults, emptyLegacy };

        UniformityCaliResultHelper.MigrateLegacyRadiusAndPeakData(results);

        Assert.Null(nullResults.UniformityMeasurementResults);
        Assert.Empty(emptyLegacy.UniformityMeasurementResults[0].RadiusAndPeakDataList);
        Assert.Empty(emptyLegacy.UniformityMeasurementResults[0].RppmSizeRadiusAndPeakDataList);
    }

    // —— BuildCalKey ——

    [Fact]
    public void BuildCalKey_FromItem_AlwaysProducesCalKeyOfItemDimensions()
    {
        var item = BuildItem(CalibrationMode.Verify);

        var key = UniformityCaliResultHelper.BuildCalKey(item);

        Assert.Equal("Cal.WS300.SpinStd.Normal.StandardThroughput", key);
    }

    // —— HasCalibrationResult ——

    [Fact]
    public void HasCalibrationResult_MissingRow_ReturnsFalse()
    {
        var results = new ObservableCollection<UniformityCaliAssociatedResult>();

        Assert.False(UniformityCaliResultHelper.HasCalibrationResult(results, BuildItem(CalibrationMode.Verify)));
    }

    [Fact]
    public void HasCalibrationResult_EmptyShellRow_ReturnsFalse()
    {
        var row = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput") { CalibrationMode = CalibrationMode.Cal };
        row.UniformityMeasurementResults.Add(new UniformityMeasurementResult());
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { row };

        Assert.False(UniformityCaliResultHelper.HasCalibrationResult(results, BuildItem(CalibrationMode.Verify)));
    }

    [Fact]
    public void HasCalibrationResult_RppmSizeDataPresent_ReturnsTrue()
    {
        var row = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput") { CalibrationMode = CalibrationMode.Cal };
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 10, PeakData = 1.5 });
        row.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { row };

        Assert.True(UniformityCaliResultHelper.HasCalibrationResult(results, BuildItem(CalibrationMode.Verify)));
    }

    [Fact]
    public void HasCalibrationResult_OnlySizeDataPresent_ReturnsTrue()
    {
        var row = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput") { CalibrationMode = CalibrationMode.Cal };
        var measurement = new UniformityMeasurementResult();
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 10, PeakData = 90 });
        row.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { row };

        Assert.True(UniformityCaliResultHelper.HasCalibrationResult(results, BuildItem(CalibrationMode.Verify)));
    }

    // —— AppendUnscannedRegionDefaults ——

    [Fact]
    public void AppendUnscanned_WS300ScannedTo147_Fills148To151WithLastPeakData()
    {
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 146, PeakData = 0.004 });
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 90 });

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);

        Assert.Equal(6, measurement.RppmSizeRadiusAndPeakDataList.Count);
        Assert.Equal(new[] { 148d, 149d, 150d, 151d }, measurement.RppmSizeRadiusAndPeakDataList.Skip(2).Select(x => x.Radius));
        Assert.All(measurement.RppmSizeRadiusAndPeakDataList.Skip(2), x => Assert.Equal(0.005, x.PeakData));
        Assert.Equal(5, measurement.SizeRadiusAndPeakDataList.Count);
        Assert.Equal(new[] { 148d, 149d, 150d, 151d }, measurement.SizeRadiusAndPeakDataList.Skip(1).Select(x => x.Radius));
        Assert.All(measurement.SizeRadiusAndPeakDataList.Skip(1), x => Assert.Equal(90, x.PeakData));
        // 补值前 Size 列表的最大实测半径被记录（兼作已补值标志），供 StdDev/Percent 只按实测段统计
        Assert.Equal(147, measurement.SizeMaxMeasuredRadius);
    }

    [Fact]
    public void AppendUnscanned_SecondCall_IsIdempotent_BoundaryNotOverwritten()
    {
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 90 });

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);
        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);

        // 重复调用不得把实测边界覆写为含尾巴的最大值（151），否则统计会把补入段重新计入；尾巴也不得重复追加
        Assert.Equal(147, measurement.SizeMaxMeasuredRadius);
        Assert.Equal(5, measurement.RppmSizeRadiusAndPeakDataList.Count);
        Assert.Equal(5, measurement.SizeRadiusAndPeakDataList.Count);
    }

    [Fact]
    public void BuildUnscannedTail_PureComputation_DoesNotModifyInputList()
    {
        var peakDataList = new List<RadiusAndPeakData> { new() { Radius = 147, PeakData = 0.005 } };

        var tail = UniformityCaliResultHelper.BuildUnscannedTail(peakDataList, WaferSizeType.WS300);

        Assert.Single(peakDataList);
        Assert.Equal(4, tail.Count);
        Assert.Equal(151, tail.Last().Radius);
    }

    [Fact]
    public void GetUniformityCaliResult_PadsTailForUnenrichedResult()
    {
        var result = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput")
        {
            CalibrationMode = CalibrationMode.Cal,
            WaferSize = WaferSizeType.WS300
        };
        var measurement = new UniformityMeasurementResult { ChannelName = ChannelMode.DN_Normal };
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });
        result.UniformityMeasurementResults.Add(measurement);
        var original = UniformityCaliResultHelper.UniformityCaliAssociatedResults;
        UniformityCaliResultHelper.UniformityCaliAssociatedResults = new ObservableCollection<UniformityCaliAssociatedResult> { result };
        try
        {
            // 算法消费点兜底：结果行未经加载/扫描期补值时，因子曲线仍补齐到 wafer 半径 +1
            var points = UniformityCaliResultHelper.GetUniformityCaliResult(WaferSizeType.WS300, SpinSpeedType.SpinStd, IncidenceType.Normal, ThroughputType.StandardThroughput, ChannelMode.DN_Normal);

            Assert.NotEmpty(points);
            Assert.Equal(151, points.Last().X);
        }
        finally
        {
            UniformityCaliResultHelper.UniformityCaliAssociatedResults = original;
        }
    }

    [Fact]
    public void AppendUnscanned_EmptyList_Unchanged()
    {
        var measurement = new UniformityMeasurementResult();

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);

        Assert.Empty(measurement.RppmSizeRadiusAndPeakDataList);
        Assert.Empty(measurement.SizeRadiusAndPeakDataList);
        Assert.Equal(0, measurement.SizeMaxMeasuredRadius);
    }

    [Fact]
    public void AppendUnscanned_AlreadyCoveringTarget_Unchanged()
    {
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 151, PeakData = 0.006 });

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);

        Assert.Single(measurement.RppmSizeRadiusAndPeakDataList);
    }

    [Fact]
    public void AppendUnscanned_InternalGapPreserved_OnlyTailFilled()
    {
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 1, PeakData = 0.01 });
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 2, PeakData = 0.01 });
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS300);

        Assert.Equal(7, measurement.RppmSizeRadiusAndPeakDataList.Count);
        Assert.Equal(2, measurement.RppmSizeRadiusAndPeakDataList[1].Radius);
        Assert.Equal(147, measurement.RppmSizeRadiusAndPeakDataList[2].Radius);
    }

    [Fact]
    public void AppendUnscanned_WS200_FillsUpTo101()
    {
        var measurement = new UniformityMeasurementResult();
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 98, PeakData = 55 });

        UniformityCaliResultHelper.AppendUnscannedRegionDefaults(measurement, WaferSizeType.WS200);

        Assert.Equal(4, measurement.SizeRadiusAndPeakDataList.Count);
        Assert.Equal(new[] { 99d, 100d, 101d }, measurement.SizeRadiusAndPeakDataList.Skip(1).Select(x => x.Radius));
    }

    // —— EnsureUnscannedRegionDefaults（旧结果加载/导入时补齐） ——

    [Fact]
    public void EnsureUnscanned_LegacyRowWithoutBoundary_AppendsTailAndRecordsBoundary()
    {
        var result = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput")
        {
            CalibrationMode = CalibrationMode.Cal,
            WaferSize = WaferSizeType.WS300
        };
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 90 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(results);

        Assert.Equal(147, measurement.SizeMaxMeasuredRadius);
        Assert.Equal(5, measurement.RppmSizeRadiusAndPeakDataList.Count);
        Assert.Equal(151, measurement.RppmSizeRadiusAndPeakDataList.Last().Radius);
        Assert.Equal(0.005, measurement.RppmSizeRadiusAndPeakDataList.Last().PeakData);
    }

    [Fact]
    public void EnsureUnscanned_SecondCall_IsIdempotent()
    {
        var result = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput")
        {
            CalibrationMode = CalibrationMode.Cal,
            WaferSize = WaferSizeType.WS300
        };
        var measurement = new UniformityMeasurementResult();
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 0.005 });
        measurement.SizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 147, PeakData = 90 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(results);
        UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(results);

        Assert.Equal(147, measurement.SizeMaxMeasuredRadius);
        Assert.Equal(5, measurement.RppmSizeRadiusAndPeakDataList.Count);
        Assert.Equal(5, measurement.SizeRadiusAndPeakDataList.Count);
    }

    [Fact]
    public void EnsureUnscanned_AlreadyEnrichedRow_Skipped()
    {
        var result = new UniformityCaliAssociatedResult("Cal.WS300.SpinStd.Normal.StandardThroughput")
        {
            CalibrationMode = CalibrationMode.Cal,
            WaferSize = WaferSizeType.WS300
        };
        var measurement = new UniformityMeasurementResult
        {
            SizeMaxMeasuredRadius = 147
        };
        measurement.RppmSizeRadiusAndPeakDataList.Add(new RadiusAndPeakData { Radius = 100, PeakData = 0.01 });
        result.UniformityMeasurementResults.Add(measurement);
        var results = new ObservableCollection<UniformityCaliAssociatedResult> { result };

        UniformityCaliResultHelper.EnsureUnscannedRegionDefaults(results);

        // 边界已持久化的行（新执行器产出）不再处理：边界与列表都不动
        Assert.Single(measurement.RppmSizeRadiusAndPeakDataList);
        Assert.Equal(147, measurement.SizeMaxMeasuredRadius);
    }

    // —— UniformityCaliRecipeRule.IsUmLseRecipe ——
    // 单测环境无法构造 Recipe（元素初始化依赖 Galaxy 容器），走 DFSizingUnit 纯判定缝；
    // null Recipe 分支不触发元素构造，可覆盖。

    [Fact]
    public void IsUmLseRecipe_UmLseUnit_ReturnsTrue()
    {
        Assert.True(UniformityCaliRecipeRule.IsUmLseRecipe((DFSizingUnit?)DFSizingUnit.umLSE));
    }

    [Fact]
    public void IsUmLseRecipe_NonUmLseUnit_ReturnsFalse()
    {
        Assert.False(UniformityCaliRecipeRule.IsUmLseRecipe((DFSizingUnit?)DFSizingUnit.um));
        Assert.False(UniformityCaliRecipeRule.IsUmLseRecipe((DFSizingUnit?)DFSizingUnit.nPPM));
        Assert.False(UniformityCaliRecipeRule.IsUmLseRecipe((DFSizingUnit?)DFSizingUnit.rPPM));
    }

    [Fact]
    public void IsUmLseRecipe_MissingUnit_ReturnsFalse()
    {
        Assert.False(UniformityCaliRecipeRule.IsUmLseRecipe((DFSizingUnit?)null));
    }

    [Fact]
    public void IsUmLseRecipe_NullRecipe_ReturnsFalse()
    {
        Assert.False(UniformityCaliRecipeRule.IsUmLseRecipe((Recipe?)null));
    }
}