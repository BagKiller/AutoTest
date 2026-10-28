// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Reflection;
using Newtonsoft.Json.Linq;
using Vega.Data.Calibration;

namespace Vega.Utility.Common.Tests;

/// <summary>
///   回归：PMT 校准条件/公式表的内置兜底必须与发布 JSON 逐条一致（"内置 ≡ 发布"不变量）。
///   <para>
///   公式表 PMTAttenuationFormulas 的 Numerator/Denominator 按条件 Index 位置引用条件阶梯，
///   两侧分叉时兜底路径衰减系数静默算错（PR 5012 前共享条件表兜底与发布 JSON 14 条中 11 条不同，
///   同类缺陷第二次发生）——以机器校验钉死，改任一侧 JSON/兜底不同步即测试红。
///   经反射取 private 兜底方法，不为测试改生产代码可见性；逐行投影为字符串比较，失败输出即两侧全量阶梯。
///   </para>
/// </summary>
public class PMTCaliBuiltInDefaultsConsistencyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vega.sln")))
        {
            dir = dir.Parent;
        }
        Assert.True(dir is not null, $"Vega.sln not found upward from {AppContext.BaseDirectory}");
        return dir.FullName;
    }

    // ── 条件表：内置兜底 ≡ 发布 JSON ──

    [Theory]
    [InlineData("SharedDefaults", "Configs")]
    [InlineData("Vega100Defaults", @"Configs\Vega100Configs")]
    public void Conditions_BuiltInDefaults_MatchPublishedJson(string methodName, string configSubPath)
    {
        var published = ParsePublishedConditions(Path.Combine(RepoRoot(), "Vega", "Resources", configSubPath, "PMTCaliConditions.json"))
            .Select(p => $"#{p.Index}: {p.Incidence}/{p.Collect}/{p.Ff}/{p.Power}");
        var builtIn = InvokeConditionsDefaults(methodName)
            .Select(c => $"#{c.Index}: {c.IncidenceFilter}/{c.CollectODFilter}/{c.FFilter}/{c.LaserPower}");

        Assert.Equal(published, builtIn);
    }

    // ── 公式表：内置兜底 ≡ 发布 JSON ──

    [Theory]
    [InlineData("SharedDefaultFormulas", "Configs")]
    [InlineData("Vega100DefaultFormulas", @"Configs\Vega100Configs")]
    public void Formulas_BuiltInDefaults_MatchPublishedJson(string methodName, string configSubPath)
    {
        var published = ParsePublishedFormulas(Path.Combine(RepoRoot(), "Vega", "Resources", configSubPath, "PMTAttenuationFormulas.json"))
            .Select(FormatFormula);
        var builtIn = InvokeFormulasDefaults(methodName)
            .Select(f => FormatFormula((f.Index, f.Constant, f.Factors)));

        Assert.Equal(published, builtIn);
    }

    private static string FormatFormula((int Index, double Constant, List<(int Numerator, int Denominator)>? Factors) formula)
    {
        var factors = formula.Factors is null ? "[]" : $"[{string.Join(", ", formula.Factors.Select(f => $"{f.Numerator}/{f.Denominator}"))}]";
        return $"#{formula.Index}: Constant={formula.Constant}, Factors={factors}";
    }

    /// <summary>发布 JSON 的条件行（枚举以字符串名书写，与内置兜底的枚举 ToString 对齐比较）。</summary>
    private static List<(int Index, string Incidence, string Collect, string Ff, string Power)> ParsePublishedConditions(string path)
    {
        return [.. JArray.Parse(File.ReadAllText(path)).Select(row => (
            Index: (int)row["Index"]!,
            Incidence: (string)row["IncidenceFilter"]!,
            Collect: (string)row["CollectODFilter"]!,
            Ff: (string)row["FFilter"]!,
            Power: (string)row["LaserPower"]!))];
    }

    private static List<(int Index, double Constant, List<(int Numerator, int Denominator)>? Factors)> ParsePublishedFormulas(string path)
    {
        return [.. JArray.Parse(File.ReadAllText(path)).Select(row =>
        {
            List<(int, int)>? factors = null;
            if (row["Factors"] is { } jsonFactors)
            {
                factors = [.. jsonFactors.Select(f => ((int)f["Numerator"]!, (int)f["Denominator"]!))];
            }
            return ((int)row["Index"]!, (double?)row["Constant"] ?? 1.0, factors);
        })];
    }

    private static List<PMTCaliCondition> InvokeConditionsDefaults(string methodName)
    {
        var method = typeof(PMTCaliConditions).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{nameof(PMTCaliConditions)}.{methodName} not found");
        return (List<PMTCaliCondition>)method.Invoke(null, null)!;
    }

    // PMTGainCalibrationExecutor 是 internal，公式表兜底经反射按属性取值
    private static List<(int Index, double Constant, List<(int Numerator, int Denominator)>? Factors)> InvokeFormulasDefaults(string methodName)
    {
        var executorType = typeof(Vega.Calibration.Executor.PMTCaliBaseExecutor).Assembly
            .GetType("Vega.Calibration.Executor.PMTGainCalibrationExecutor")
            ?? throw new InvalidOperationException("PMTGainCalibrationExecutor type not found");
        var method = executorType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{executorType.Name}.{methodName} not found");
        var rows = (System.Collections.IEnumerable)method.Invoke(null, null)!;

        var result = new List<(int, double, List<(int, int)>?)>();
        foreach (var row in rows)
        {
            var type = row!.GetType();
            int index = (int)type.GetProperty("Index")!.GetValue(row)!;
            double constant = (double)type.GetProperty("Constant")!.GetValue(row)!;
            List<(int, int)>? factors = null;
            if (type.GetProperty("Factors")!.GetValue(row) is System.Collections.IEnumerable formulaFactors)
            {
                var list = new List<(int, int)>();
                foreach (var f in formulaFactors)
                {
                    var fType = f!.GetType();
                    list.Add(((int)fType.GetProperty("Numerator")!.GetValue(f)!, (int)fType.GetProperty("Denominator")!.GetValue(f)!));
                }
                factors = list;
            }
            result.Add((index, constant, factors));
        }
        return result;
    }
}