// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.Data.Commons;
using Vega.Utility.Helper;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   PMT 条件表/公式表序号空间一致性回归：公式表 Factors 的 Numerator/Denominator 引用的是条件表条目序号
///   （1 基，PMTGainCalibrationExecutor 按 PMTCaliConditions.Conditions 顺序构建 ItemList 后按序号取数），
///   两表序号空间强耦合、必须成套更新——单侧增删条目会使另一侧静默错位（数值算错，不报错）。
///   运行时的因子索引是 FilterByMachineSubset 过滤后的列表位置：本文件用两条守卫把"发布态下原表空间 ==
///   运行时过滤后空间"钉死（当前各机型档全量覆盖、零余量）；运行时用户目录副本（OpticsGearConfig.json
///   被裁剪挡位 → 条件被剔除 → 位置空间前移，越界因子被 Executor 静默 continue）不在此守卫范围。
///   shared 路径的内置条件兜底（SharedDefaults）与发布条件阶梯已分叉、条件/公式两路兜底不同源，
///   由 Bug WI 3702 跟踪；本测试只守护随版本发布的表与挡位配置（源码树，不含用户目录副本，decision 0007）。
/// </summary>
public class PmtCaliTablesConsistencyTests
{
    // 最小只读 DTO（仅取断言所需字段；公式表完整类型 AttenuationFormula 在 Galaxy.Data.Calibrations，测试不引重型工程）
    private sealed class ConditionDto
    {
        public int Index { get; set; }
        public string? IncidenceFilter { get; set; }
        public string? CollectODFilter { get; set; }
        public string? FFilter { get; set; }
    }

    private sealed class FactorDto
    {
        public int Numerator { get; set; }
        public int Denominator { get; set; }
    }

    private sealed class FormulaDto
    {
        public int Index { get; set; }
        public List<FactorDto>? Factors { get; set; }
    }

    private sealed class GearProfileDto
    {
        public string? Condition { get; set; }
        public List<string>? IncidenceFilters { get; set; }
        public List<string>? CollectODFilters { get; set; }
        public List<string>? FluorescenceFilters { get; set; }
    }

    private sealed class GearConfigDto
    {
        public List<GearProfileDto>? DeviceMappings { get; set; }
    }

    private static string ResourceDir(string subPath) => Path.Combine(TestPaths.RepoRoot, "Vega", "Resources", subPath);

    private static T LoadJson<T>(string path) where T : class =>
        File.Exists(path)
            ? JsonHelper.Deserialize<T>(path) ?? throw new InvalidOperationException(path)
            : throw new FileNotFoundException($"missing {path}");

    [Theory]
    [InlineData(MachineConfigSubPaths.Default)]
    [InlineData(MachineConfigSubPaths.Vega100)]
    public void FormulaConditionIndices_WithinConditionTableRange(string subPath)
    {
        var dir = ResourceDir(subPath);
        var conditionsPath = Path.Combine(dir, PmtCaliConfigPaths.ConditionsFileName);
        var formulasPath = Path.Combine(dir, PmtCaliConfigPaths.AttenuationFormulasFileName);
        var conditions = LoadJson<List<ConditionDto>>(conditionsPath);
        var formulas = LoadJson<List<FormulaDto>>(formulasPath);

        // 条件序号须为 1..N 连续（公式按 Conditions 顺序取数，依赖连续 1 基序号空间）
        Assert.Equal(Enumerable.Range(1, conditions.Count), conditions.Select(c => c.Index).OrderBy(i => i));

        // 公式引用的条件序号越界 = 序号空间错位形态（两表条数失配/单侧改表），越界即红
        var count = conditions.Count;
        foreach (var formula in formulas)
        {
            foreach (var factor in formula.Factors ?? [])
            {
                Assert.True(factor.Numerator >= 1 && factor.Numerator <= count,
                    $"formula #{formula.Index} numerator {factor.Numerator} out of range [1,{count}] in {formulasPath}");
                Assert.True(factor.Denominator >= 1 && factor.Denominator <= count,
                    $"formula #{formula.Index} denominator {factor.Denominator} out of range [1,{count}] in {formulasPath}");
            }
        }
    }

    [Fact]
    public void MachineGearProfiles_CoverConditionTablesFully()
    {
        // 发布态等价性守卫：每个机型档的挡位子集须覆盖对应条件阶梯全量（FilterByMachineSubset 三维度：
        // Incidence/CollectOD/FF 任一不在档内即被剔除）——覆盖不满 ⇒ 运行时过滤后位置空间前移，
        // 原表序号空间守卫（上一用例）即与运行时脱节，越界因子被 Executor 静默 continue。
        var configPath = Path.Combine(ResourceDir(MachineConfigSubPaths.Default), OpticsGearConfigPaths.FileName);
        var config = LoadJson<GearConfigDto>(configPath);
        Assert.NotNull(config.DeviceMappings);
        Assert.NotEmpty(config.DeviceMappings);

        // 配对关系前提（勿从被测数据静默消失）：V100 档映射必须存在且唯一——否则下方循环无 profile 命中
        // V100 表，覆盖数为 0 仍绿；且已知五档齐全（新增机型档允许超集，删/改任一已知档即红）
        Assert.Single(config.DeviceMappings.Where(p => p.Condition?.Contains("Vega100") == true));
        var profileConditions = config.DeviceMappings.Select(p => p.Condition).ToHashSet();
        foreach (var expected in new[] { "Vega100", "Vega200,Gen1", "Vega200,Gen2|Gen3", "Vega300", "Vega500" })
        {
            Assert.Contains(expected, profileConditions);
        }

        var shared = LoadJson<List<ConditionDto>>(Path.Combine(ResourceDir(MachineConfigSubPaths.Default), PmtCaliConfigPaths.ConditionsFileName));
        var vega100 = LoadJson<List<ConditionDto>>(Path.Combine(ResourceDir(MachineConfigSubPaths.Vega100), PmtCaliConfigPaths.ConditionsFileName));

        foreach (var profile in config.DeviceMappings)
        {
            var conditions = profile.Condition?.Contains("Vega100") == true ? vega100 : shared;
            var dropped = conditions
                .Where(c => profile.IncidenceFilters?.Contains(c.IncidenceFilter) != true
                         || profile.CollectODFilters?.Contains(c.CollectODFilter) != true
                         || profile.FluorescenceFilters?.Contains(c.FFilter) != true)
                .Select(c => $"#{c.Index}({c.IncidenceFilter}/{c.CollectODFilter}/{c.FFilter})")
                .ToList();
            Assert.True(dropped.Count == 0,
                $"gear profile '{profile.Condition}' does not cover {dropped.Count} condition(s) of its table " +
                $"(filtered index space would shift): {string.Join(", ", dropped)}; config {configPath}");
        }
    }
}