// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.WaferAlignment;
using Vega.Data.Measurements;
using Xunit.Abstractions;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// T3a 圆拟合兼容性闸门（flat-wafer-alignment-design-v4 §8.1 / Step 0.4）：
/// 现有圆拟合核心（中值滤波 + RANSAC + Kåsa 初值 + LM，经 FitCircleCore 驱动，与
/// FindCOWCircleFitting 非 EdgeChuck 生产路径一致）在“圆弧点 + 平边点”输入下的
/// 行为表征——异常数、圆心偏差、重复波动。RANSAC 固定种子（同数据同结果），
/// 重复运行用于确认确定性；判定按误差容差/分布比较，不要求逐位一致。
/// 通过标准（宽松，只拦灾难性失败）：无异常、圆心误差与重复波动有界；
/// 实测数值记录于 ve-docs/work/flat-implementation-step0.md。
/// </summary>
public class CircleFitFlatCompatibilityGateTests
{
    private const double Radius = 100000.0;    // 8" 满径（µm）
    private const double FlatDepth = 1500.0;   // 平边深度（µm），与 XYCal FlatDepth 初值同量级
    private const int SampleCount = 3600;      // 角向采样数（与 SamplePerTrack 同量级）
    private const int Repeats = 30;            // RANSAC 重复次数（表征重复波动）
    private const double Pitch = 25.0;         // 与真实 Edge 扫描同量级（RANSAC 距离阈值 = Pitch/2，见 PR 4989 圆拟合）

    private const double CenterX = 300.0;      // 晶圆圆心（通道坐标，µm）
    private const double CenterY = -200.0;
    private const double FlatTheta = Math.PI / 6.0; // 平边法向角（任意角，避开坐标轴特例）

    // 闸门容差：按 2026-10-20 表征实验实测收紧（实测最差 mean=0.86µm / max=1.44µm / std=0.24µm，
    // 见 ve-docs/work/flat-implementation-step0.md），保留一个数量级余量防回归：
    // 平边点占比约 5.5% 时 RANSAC 将弦点判为离群、LM 收敛到圆弧，不允许出现异常或灾难性漂移。
    private const double MeanErrorToleranceUm = 25.0;
    private const double MaxErrorToleranceUm = 50.0;
    private const double StdToleranceUm = 10.0;

    private readonly ITestOutputHelper output;

    public CircleFitFlatCompatibilityGateTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    /// <summary>构造“圆弧 + 平边”轮廓的极径序列（通道原点坐标，取外缘解）。</summary>
    /// <param name="radiusTransform">作用于极径的后处理（量化/加噪）；null 表示精确几何（平边点精确共线）。</param>
    private static PointInPolar[] BuildFlatWaferPoints(Func<double, double>? radiusTransform = null)
    {
        double flatDistance = Radius - FlatDepth; // 平边线到晶圆圆心的距离
        double flatHalfAngle = Math.Acos(flatDistance / Radius);
        (double nx, double ny) = (Math.Cos(FlatTheta), Math.Sin(FlatTheta));
        var points = new PointInPolar[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            double theta = 2.0 * Math.PI * i / SampleCount;
            double delta = WrapAngle(theta - FlatTheta);
            double r;
            if (Math.Abs(delta) < flatHalfAngle)
            {
                // 平边：从通道原点沿 θ 的射线与平边线的交点（精确共线点）。
                r = (flatDistance + nx * CenterX + ny * CenterY) / Math.Cos(delta);
            }
            else
            {
                // 圆弧：|r·u(θ) − C| = R 的外缘解。
                double proj = Math.Cos(theta) * CenterX + Math.Sin(theta) * CenterY;
                r = proj + Math.Sqrt(proj * proj - (CenterX * CenterX + CenterY * CenterY) + Radius * Radius);
            }

            if (radiusTransform != null)
            {
                r = radiusTransform(r);
            }

            points[i] = new PointInPolar(r, theta);
        }
        return points;
    }

    private static double WrapAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= 2.0 * Math.PI;
        }
        while (angle < -Math.PI)
        {
            angle += 2.0 * Math.PI;
        }
        return angle;
    }

    /// <summary>Box-Muller 高斯噪声（µm），种子固定保证用例可复现。</summary>
    private static Func<double, double> GaussianNoise(Random random, double sigma)
    {
        return r => r + sigma * Math.Sqrt(-2.0 * Math.Log(random.NextDouble() + 1e-12)) * Math.Cos(2.0 * Math.PI * random.NextDouble());
    }

    /// <summary>重复运行现有圆拟合核心，返回每次圆心误差；异常计入 failures 并输出。</summary>
    private static (List<double> Errors, List<string> Failures) RunGate(PointInPolar[] points, ITestOutputHelper output, string label)
    {
        var errors = new List<double>(Repeats);
        var failures = new List<string>();
        for (int i = 0; i < Repeats; i++)
        {
            try
            {
                var core = WaferAlignmentService.FitCircleCore(points, Pitch);
                if (!double.IsFinite(core.Cx) || !double.IsFinite(core.Cy))
                {
                    failures.Add($"run {i}: non-finite center ({core.Cx}, {core.Cy})");
                    continue;
                }
                errors.Add(Math.Sqrt(Math.Pow(core.Cx - CenterX, 2) + Math.Pow(core.Cy - CenterY, 2)));
            }
            catch (Exception ex)
            {
                failures.Add($"run {i}: {ex.GetType().Name}: {ex.Message} @ {ex.StackTrace?.Split(Environment.NewLine).FirstOrDefault(l => l.Contains(".cs:line"))}");
            }
        }

        double mean = errors.Count > 0 ? errors.Average() : double.NaN;
        double max = errors.Count > 0 ? errors.Max() : double.NaN;
        double std = errors.Count > 1 ? Math.Sqrt(errors.Select(e => (e - mean) * (e - mean)).Average()) : 0.0;
        output.WriteLine($"[{label}] runs={Repeats}, ok={errors.Count}, failures={failures.Count}, meanErr={mean:F2}um, maxErr={max:F2}um, std={std:F2}um");
        foreach (string failure in failures.Take(5))
        {
            output.WriteLine($"  {failure}");
        }

        return (errors, failures);
    }

    private static void AssertGate(string label, in (List<double> Errors, List<string> Failures) result)
    {
        Assert.True(result.Failures.Count == 0, $"[{label}] unexpected exceptions/non-finite results: {result.Failures.Count}/{Repeats}");
        double mean = result.Errors.Average();
        double max = result.Errors.Max();
        double std = Math.Sqrt(result.Errors.Select(e => (e - mean) * (e - mean)).Average());
        Assert.True(mean < MeanErrorToleranceUm, $"[{label}] mean center error {mean:F2}um exceeds {MeanErrorToleranceUm}um");
        Assert.True(max < MaxErrorToleranceUm, $"[{label}] max center error {max:F2}um exceeds {MaxErrorToleranceUm}um");
        Assert.True(std < StdToleranceUm, $"[{label}] center error std {std:F2}um exceeds {StdToleranceUm}um");
    }

    /// <summary>对照基线：纯圆弧（无平边）下拟合器自身的误差底噪。</summary>
    [Fact]
    public void PureArc_Control_CharacterizesFitterBaseline()
    {
        var points = BuildFlatWaferPoints(radiusTransform: null);
        // 剔除平边段：把平边扇区替换为圆弧解（等价纯圆）。
        double flatHalfAngle = Math.Acos((Radius - FlatDepth) / Radius);
        for (int i = 0; i < points.Length; i++)
        {
            double delta = WrapAngle(points[i].Theta - FlatTheta);
            if (Math.Abs(delta) < flatHalfAngle)
            {
                double proj = Math.Cos(points[i].Theta) * CenterX + Math.Sin(points[i].Theta) * CenterY;
                points[i].Radius = proj + Math.Sqrt(proj * proj - (CenterX * CenterX + CenterY * CenterY) + Radius * Radius);
            }
        }

        var result = RunGate(points, output, "arc-only-control");
        AssertGate("arc-only-control", result);
    }

    /// <summary>T3a 主用例：圆弧点 + 精确共线平边点（含中值滤波对弧/弦过渡的平滑）。</summary>
    [Fact]
    public void ExactCollinearFlat_ArcPlusFlat_PassesGate()
    {
        var points = BuildFlatWaferPoints(radiusTransform: null);
        AssertGate("exact-collinear-flat", RunGate(points, output, "exact-collinear-flat"));
    }

    /// <summary>T3a：极径量化到 1 µm 的平边点。</summary>
    [Fact]
    public void QuantizedFlat_RoundTo1Um_PassesGate()
    {
        var points = BuildFlatWaferPoints(r => Math.Round(r));
        AssertGate("quantized-1um-flat", RunGate(points, output, "quantized-1um-flat"));
    }

    /// <summary>T3a：极径高斯噪声 σ=10 µm 的平边点（噪声接近 RANSAC 距离阈值 Pitch/2=12.5 µm，
    /// 表征 RANSAC 共识收缩与 LM 精修行为）。</summary>
    [Fact]
    public void NoisyFlat_Sigma10Um_PassesGate()
    {
        var points = BuildFlatWaferPoints(GaussianNoise(new Random(20261020), 10.0));
        AssertGate("noisy-sigma10um-flat", RunGate(points, output, "noisy-sigma10um-flat"));
    }
}