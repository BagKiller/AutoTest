// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Vega.BL.WaferAlignment;
using Vega.Data.Measurements;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// Flat 几何核心合成测试（flat-wafer-alignment-design-v4 §8.1）：
/// 驱动区域识别 → 粗定位选优 → 精拟合 → 法向定向主链，圆心来自现有 FitCircleCore。
/// 合成几何用例的数值容差不等于机台精度指标；真实 bin 对照见移植验收记录。
/// </summary>
public class FlatGeometryTests
{
    private const double Radius = 100000.0;   // 8" 满径（µm）
    private const double FlatDepth = 1500.0;  // 与 XYCal FlatDepth 初值同量级
    private const int SampleCount = 3600;
    private const double Pitch = 25.0;
    private const double CenterX = 300.0;
    private const double CenterY = -200.0;

    /// <summary>构造“圆弧 + 平边（+ 可选第二平边）”极径序列（通道原点坐标，取外缘解）。</summary>
    private static PointInPolar[] BuildPoints(
        double flatTheta = Math.PI / 6.0,
        double depth = FlatDepth,
        double cx = CenterX,
        double cy = CenterY,
        int n = SampleCount,
        double radius = Radius,
        (double Theta, double Depth, double HalfAngle)? secondFlat = null,
        Func<double, double>? radiusTransform = null)
    {
        (double fnx, double fny) = (Math.Cos(flatTheta), Math.Sin(flatTheta));
        var points = new PointInPolar[n];
        for (int i = 0; i < n; i++)
        {
            double theta = 2.0 * Math.PI * i / n;
            double r = SolveRadius(theta, radius, cx, cy, flatTheta, fnx, fny, depth);

            if (secondFlat is { } chip)
            {
                double delta2 = WrapAngle(theta - chip.Theta);
                if (Math.Abs(delta2) < chip.HalfAngle)
                {
                    (double cnx, double cny) = (Math.Cos(chip.Theta), Math.Sin(chip.Theta));
                    r = (radius - chip.Depth + cnx * cx + cny * cy) / Math.Cos(delta2);
                }
            }

            if (radiusTransform != null)
            {
                r = radiusTransform(r);
            }
            points[i] = new PointInPolar(r, theta);
        }
        return points;
    }

    /// <summary>θ 处外缘极径：平边扇区内取射线与平边线交点，否则取 |P − C| = R 外缘解。</summary>
    private static double SolveRadius(double theta, double radius, double cx, double cy, double flatTheta, double fnx, double fny, double depth)
    {
        double delta = WrapAngle(theta - flatTheta);
        double flatHalfAngle = Math.Acos((radius - depth) / radius);
        if (Math.Abs(delta) < flatHalfAngle)
        {
            return (radius - depth + fnx * cx + fny * cy) / Math.Cos(delta);
        }
        double proj = Math.Cos(theta) * cx + Math.Sin(theta) * cy;
        return proj + Math.Sqrt(proj * proj - (cx * cx + cy * cy) + radius * radius);
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

    private static double WrapTo2Pi(double angle)
    {
        double a = angle % (2.0 * Math.PI);
        return a < 0.0 ? a + 2.0 * Math.PI : a;
    }

    private static double ThetaError(double actual, double expected)
    {
        return Math.Abs(WrapAngle(actual - expected));
    }

    private static Func<double, double> GaussianNoise(Random random, double sigma)
    {
        return r => r + sigma * Math.Sqrt(-2.0 * Math.Log(random.NextDouble() + 1e-12)) * Math.Cos(2.0 * Math.PI * random.NextDouble());
    }

    /// <summary>按生产同构路径求圆心（中值滤波 + RANSAC + LM），再执行 Flat 全流程。</summary>
    private static bool RunFlat(PointInPolar[] points, double pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason)
    {
        WaferAlignmentService.CircleFitCoreResult fit = WaferAlignmentService.FitCircleCore(points, pitch, ransacBandUm: pitch / 10.0);
        return WaferAlignmentService.TryFindFlat(points, fit.Cx, fit.Cy, pitch, out line, out reason);
    }

    // ---------- T3：已知圆心/已知直线的合成 Flat ----------

    [Theory]
    [InlineData(0.0)]                    // 竖直平边（法向沿 +x）
    [InlineData(Math.PI / 6.0)]          // 任意角
    [InlineData(Math.PI / 2.0)]          // 水平平边（法向沿 +y）
    [InlineData(Math.PI)]                // 反向
    [InlineData(3.0 * Math.PI / 2.0)]    // 法向沿 −y
    [InlineData(-Math.PI / 12.0)]        // 跨 0°/360°
    [InlineData(2.0 * Math.PI - 0.05)]   // 紧贴 0° 另一侧
    public void T3_KnownFlat_VariousOrientations_FindsNormal(double flatTheta)
    {
        PointInPolar[] points = BuildPoints(flatTheta: flatTheta);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.Equal(WaferAlignmentService.FlatFailureReason.None, reason);

        Assert.True(ThetaError(line.Theta, flatTheta) < 2e-3,
            $"theta={line.Theta:F6}, expected={flatTheta:F6}, err={ThetaError(line.Theta, flatTheta):F6}");

        // 线位：定向后 b − n·C ≈ R − depth（法向自圆心指向平边侧）。
        double d = line.B - (line.Nx * CenterX + line.Ny * CenterY);
        Assert.True(Math.Abs(d - (Radius - FlatDepth)) < 30.0, $"line distance {d:F1}, expected {Radius - FlatDepth:F1}");
    }

    [Fact]
    public void T3_QuantizedFlat_StillFindsNormal()
    {
        PointInPolar[] points = BuildPoints(radiusTransform: r => Math.Round(r));
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 2e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T3_NoisyFlat_Sigma10Um_FindsNormal()
    {
        PointInPolar[] points = BuildPoints(radiusTransform: GaussianNoise(new Random(20261021), 10.0));
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Theory]
    [InlineData(300.0)]      // 浅平边
    [InlineData(20000.0)]    // 平边点占比 ~20%（中值稳健性边界）
    public void T3_DepthVariants_FindsNormal(double depth)
    {
        PointInPolar[] points = BuildPoints(depth: depth);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"depth={depth}, reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"depth={depth}, theta={line.Theta:F6}");
    }

    [Fact]
    public void T3_SignificantButShortFlat_FailsSupportGate()
    {
        // 深度 50µm 足够显著，但约 6mm 的弦不足批准的 10mm 支撑下限。
        PointInPolar[] points = BuildPoints(depth: 50.0);
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    [Theory]
    [InlineData(3.0)]        // 深度低于显著性门槛：区域点数不足，不强行输出
    public void T3_IndistinguishableShallowFlat_FailsNoReliableSeed(double depth)
    {
        PointInPolar[] points = BuildPoints(depth: depth);
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    [Fact]
    public void T3_DownsampledLikePoints_N900_FindsNormal()
    {
        // DUV 降采样量级（SamplePerTrack/Step = 900）下的角度效果。
        PointInPolar[] points = BuildPoints(n: 900);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 4e-3, $"theta={line.Theta:F6}");
    }

    [Theory]
    [InlineData(500.0, -300.0)]
    [InlineData(-800.0, 600.0)]
    [InlineData(0.0, 0.0)]
    public void T3_EccentricCenters_FindsNormal(double cx, double cy)
    {
        PointInPolar[] points = BuildPoints(cx: cx, cy: cy);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"cx={cx}, cy={cy}, reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"cx={cx}, cy={cy}, theta={line.Theta:F6}");
    }

    // ---------- T4：纯圆不得误报 ----------

    [Fact]
    public void T4_PureCircle_Exact_NoReliableSeed()
    {
        PointInPolar[] points = BuildPoints(depth: 0.0);
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    [Fact]
    public void T4_PureCircle_Quantized1Um_NoReliableSeed()
    {
        PointInPolar[] points = BuildPoints(depth: 0.0, radiusTransform: r => Math.Round(r));
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    [Fact]
    public void T4_PureCircle_NoisySigma10Um_NoReliableSeed()
    {
        PointInPolar[] points = BuildPoints(depth: 0.0, radiusTransform: GaussianNoise(new Random(20261022), 10.0));
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    // ---------- T5：区域分离 / 两近平边选优 ----------

    [Fact]
    public void T5_SameFlatWithLiftedDip_KeepsSeparateRegions()
    {
        // 平边中部 10 个连续点抬回圆弧半径：有效非内缩点必须切断区域，剩余较长一侧仍可独立完成精定位。
        PointInPolar[] points = BuildPoints();
        double flatHalfAngle = Math.Acos((Radius - FlatDepth) / Radius);
        int dipped = 0;
        for (int i = 0; i < points.Length && dipped < 10; i++)
        {
            double delta = WrapAngle(points[i].Theta - Math.PI / 6.0);
            if (delta > flatHalfAngle * 0.4 && delta < flatHalfAngle * 0.6)
            {
                double proj = Math.Cos(points[i].Theta) * CenterX + Math.Sin(points[i].Theta) * CenterY;
                points[i].Radius = proj + Math.Sqrt(proj * proj - (CenterX * CenterX + CenterY * CenterY) + Radius * Radius);
                dipped++;
            }
        }
        Assert.True(dipped == 10);

        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Theory]
    [InlineData(Math.PI / 6.0, Math.PI / 6.0 + Math.PI / 2.0, Math.PI / 6.0 + Math.PI / 2.0)]
    [InlineData(Math.PI / 6.0 + Math.PI / 2.0, Math.PI / 6.0, Math.PI / 6.0)]
    public void T5_TwoComparableFlats_SelectsHigherScore(double mainTheta, double secondTheta, double expected)
    {
        // 两个独立平边都可定位；不判歧义，按评分选择支撑更长的第二平边（两种角向顺序一致）。
        PointInPolar[] points = BuildPoints(
            flatTheta: mainTheta,
            secondFlat: (Theta: secondTheta, Depth: 1700.0, HalfAngle: Math.Acos((Radius - 1700.0) / Radius)));
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, expected) < 3e-3, $"theta={line.Theta:F6}, expected={expected:F6}");
    }

    [Fact]
    public void T5_SmallChip_NotAdmitted_MainFlatWins()
    {
        // 小崩边（5 点、浅）：不满足区域/支撑准入 → 不构成候选；主平边正常输出。
        PointInPolar[] points = BuildPoints(
            secondFlat: (Theta: Math.PI / 6.0 + Math.PI / 2.0, Depth: 800.0, HalfAngle: 5.0 * 2.0 * Math.PI / SampleCount));
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T5_WeakChip_ClearlyDistinguishable_MainFlatWins()
    {
        // 深度 800/1500 ≈ 0.53：支撑更短、评分更低 → 主平边胜出。
        PointInPolar[] points = BuildPoints(
            secondFlat: (Theta: Math.PI / 6.0 + Math.PI / 2.0, Depth: 800.0, HalfAngle: Math.Acos((Radius - 800.0) / Radius)));
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    // ---------- T6：缺失/空洞与支撑判断 ----------

    [Fact]
    public void T6_ContiguousHole12Points_RecoversInFineFit()
    {
        // 平边中部 12 点连续缺失（≈2.1mm 空洞 <14mm 上限）：区域桥接，最终支撑连续。
        PointInPolar[] points = BuildPoints();
        MakeHole(points, 12);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T6_LongHole20Points_UsesSingleRegionFit()
    {
        // 20 点连续空洞（≈3.5mm）保留在同一区域；全区拟合仍满足支撑空洞上限。
        PointInPolar[] points = BuildPoints();
        MakeHole(points, 20);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T6_Hole40Points_SingleRegionFitSucceeds()
    {
        PointInPolar[] points = BuildPoints();
        MakeHole(points, 40);
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 2e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T6_CrossZeroHole60Points_SingleRegionFitSucceeds()
    {
        const double flatTheta = 0.03;
        PointInPolar[] points = BuildPoints(flatTheta: flatTheta);
        MakeHole(points, 60, flatTheta);

        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, flatTheta) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T6_Hole90Points_ExceedsRegionSupportGapLimit()
    {
        PointInPolar[] points = BuildPoints();
        MakeHole(points, 90);

        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.InsufficientFlatSupport, reason);
    }

    [Fact]
    public void T6_IsolatedOutliers_DoNotDominateDirection()
    {
        // 平边内 5 个孤立点法向内推 200µm：精拟合支撑带排除，方向不受主导。
        PointInPolar[] points = BuildPoints();
        double flatHalfAngle = Math.Acos((Radius - FlatDepth) / Radius);
        var random = new Random(20261023);
        int pushed = 0;
        while (pushed < 5)
        {
            int i = random.Next(points.Length);
            double delta = WrapAngle(points[i].Theta - Math.PI / 6.0);
            if (Math.Abs(delta) < flatHalfAngle * 0.8)
            {
                points[i].Radius -= 200.0;
                pushed++;
            }
        }
        Assert.True(RunFlat(points, Pitch, out WaferAlignmentService.FlatLineResult line, out WaferAlignmentService.FlatFailureReason reason), $"reason={reason}");
        Assert.True(ThetaError(line.Theta, Math.PI / 6.0) < 5e-3, $"theta={line.Theta:F6}");
    }

    [Fact]
    public void T6_ShortFlat_FailsMinimumSupport()
    {
        // 弦半角约 24 点（≈8.4mm）低于 10mm 支撑下限：不强行输出。
        double halfAngle = 24.0 * 2.0 * Math.PI / SampleCount;
        double depth = halfAngle * halfAngle * Radius / 2.0; // 弦深 ≈ R·θ²/2
        PointInPolar[] points = BuildPoints(depth: depth);
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.NoReliableSeed, reason);
    }

    // ---------- 区域识别边界（算法自有 FlatRegion，无 trace DTO） ----------

    [Theory]
    [InlineData(360, 1, 100)]
    [InlineData(361, 2, 100)]
    [InlineData(360, 1, 3400)]
    [InlineData(361, 2, 3400)]
    public void RegionGap_UsesInclusive36DegreeLimit(int gap, int regionCount, int start)
    {
        PointInPolar[] points = Enumerable.Range(0, SampleCount).Select(i => new PointInPolar(Radius, 2.0 * Math.PI * i / SampleCount)).ToArray();
        for (int k = 0; k < 24; k++)
        {
            points[(start + k) % SampleCount].Radius -= 1000.0;
            points[(start + 24 + gap + k) % SampleCount].Radius -= 1000.0;
        }
        for (int k = 0; k < gap; k++)
        {
            points[(start + 24 + k) % SampleCount].Radius = double.NaN;
        }

        List<WaferAlignmentService.FlatRegion> regions = BuildRegions(points);
        Assert.Equal(regionCount, regions.Count);
    }

    [Fact]
    public void RegionChain_StopsBeforeHalfCircle()
    {
        PointInPolar[] points = Enumerable.Range(0, SampleCount).Select(i => new PointInPolar(Radius, 2.0 * Math.PI * i / SampleCount)).ToArray();
        for (int segment = 0; segment < 9; segment++)
        {
            for (int k = 0; k < 120; k++)
            {
                points[100 + segment * 240 + k].Radius -= 1000.0;
                if (segment < 8)
                {
                    points[220 + segment * 240 + k].Radius = double.NaN;
                }
            }
        }

        List<WaferAlignmentService.FlatRegion> regions = BuildRegions(points);
        Assert.True(regions.Count >= 2);
        Assert.All(regions, r => Assert.True(r.PointCount < SampleCount / 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RegionBoundary_ValidNonInwardPointNeverBridges(bool mixedMissing)
    {
        PointInPolar[] points = BuildPoints(cx: 0, cy: 0, flatTheta: 0);
        points[0].Radius = Radius + 100.0;
        if (mixedMissing)
        {
            points[1].Radius = double.NaN;
            points[^1].Radius = double.NaN;
        }

        List<WaferAlignmentService.FlatRegion> regions = BuildRegions(points);
        Assert.NotEqual(RegionIdAt(regions, 10), RegionIdAt(regions, SampleCount - 10));
        Assert.Equal(-1, RegionIdAt(regions, 0));
    }

    [Fact]
    public void RegionBoundary_ShallowInwardPointRemainsContinuous()
    {
        PointInPolar[] points = BuildPoints(cx: 0, cy: 0, flatTheta: 0);
        points[0].Radius = Radius - 1.0;

        List<WaferAlignmentService.FlatRegion> regions = BuildRegions(points);
        Assert.NotEqual(-1, RegionIdAt(regions, 0));
        Assert.Equal(RegionIdAt(regions, 10), RegionIdAt(regions, 0));
        Assert.Equal(RegionIdAt(regions, SampleCount - 10), RegionIdAt(regions, 0));
    }

    [Fact]
    public void RegionBoundary_NumericalEqualityIsNotInward()
    {
        PointInPolar[] points = BuildPoints(cx: 0, cy: 0, flatTheta: 0);
        points[0].Radius = Radius - 0.00005;

        List<WaferAlignmentService.FlatRegion> regions = BuildRegions(points);
        Assert.Equal(-1, RegionIdAt(regions, 0));
        Assert.NotEqual(RegionIdAt(regions, 10), RegionIdAt(regions, SampleCount - 10));
    }

    [Fact]
    public void FineFailure_DoesNotSwitchToOtherQualifiedRegion()
    {
        // 主平边被 90 点空洞打断（支撑空洞 >14mm）精拟合失败；另一侧较短平边可拟合，但不得切换。
        PointInPolar[] points = BuildPoints(cx: 0, cy: 0,
            secondFlat: (Theta: Math.PI, Depth: 1500.0, HalfAngle: 0.08));
        MakeHole(points, 90);
        Assert.False(RunFlat(points, Pitch, out _, out WaferAlignmentService.FlatFailureReason reason));
        Assert.Equal(WaferAlignmentService.FlatFailureReason.InsufficientFlatSupport, reason);
    }

    [Fact]
    public void FineResidualLimit_IsCappedAt500Um()
    {
        Assert.Equal(1000.0, WaferAlignmentService.FlatSupportBandMaximumUm);
        Assert.Equal(500.0, WaferAlignmentService.FlatResidualLimit(double.PositiveInfinity, WaferAlignmentService.FlatSupportBandMaximumUm));
        Assert.True(WaferAlignmentService.FlatResidualLimit(10.0, 900.0) <= 450.0);
    }

    // ---------- Flat 接入结果语义 ----------

    [Fact]
    public void FlatCircleFitResult_UpdatesCenterAndClearsOrientation()
    {
        AlignmentInfo alignment = new()
        {
            CenterOfWaferX = -1.0,
            CenterOfWaferY = -2.0,
            WaferOrientation = 1.5
        };

        WaferAlignmentService.ApplyFlatCircleFitResult(alignment, CenterX, CenterY);

        Assert.Equal(CenterX, alignment.CenterOfWaferX);
        Assert.Equal(CenterY, alignment.CenterOfWaferY);
        Assert.Equal(0.0, alignment.WaferOrientation);
    }

    [Fact]
    public void ComputeFlatAlignment_GeometryFailure_RetainsCircleCenter()
    {
        PointInPolar[] points = BuildPoints(depth: 0.0);

        WaferAlignmentService.FlatAlignmentResult outcome = WaferAlignmentService.ComputeFlatAlignment(points, Pitch, Data.Commons.ClampingMode.VacuumChuck);

        Assert.True(outcome.CircleFitSucceeded);
        Assert.False(outcome.Success);
        Assert.NotEqual(WaferAlignmentService.FlatFailureReason.None, outcome.Reason);
        Assert.InRange(Math.Abs(outcome.CenterX - CenterX), 0.0, 1.0);
        Assert.InRange(Math.Abs(outcome.CenterY - CenterY), 0.0, 1.0);
        Assert.Equal(0.0, outcome.Orientation);
    }

    [Fact]
    public void ComputeFlatAlignment_EdgeChuck_RejectedWithoutCircleFit()
    {
        PointInPolar[] points = BuildPoints();

        WaferAlignmentService.FlatAlignmentResult outcome = WaferAlignmentService.ComputeFlatAlignment(points, Pitch, Data.Commons.ClampingMode.EdgeChuck);

        Assert.False(outcome.CircleFitSucceeded);
        Assert.False(outcome.Success);
        Assert.Equal(WaferAlignmentService.FlatFailureReason.UnsupportedFlatPath, outcome.Reason);
        Assert.Equal(0.0, outcome.CenterX);
        Assert.Equal(0.0, outcome.CenterY);
    }

    [Fact]
    public void ComputeFlatAlignment_Success_ReturnsCenterAndNormal()
    {
        PointInPolar[] points = BuildPoints();

        WaferAlignmentService.FlatAlignmentResult outcome = WaferAlignmentService.ComputeFlatAlignment(points, Pitch, Data.Commons.ClampingMode.VacuumChuck);

        Assert.True(outcome.CircleFitSucceeded);
        Assert.True(outcome.Success);
        Assert.Equal(WaferAlignmentService.FlatFailureReason.None, outcome.Reason);
        Assert.InRange(Math.Abs(outcome.CenterX - CenterX), 0.0, 1.0);
        Assert.InRange(Math.Abs(outcome.CenterY - CenterY), 0.0, 1.0);
        Assert.True(ThetaError(outcome.Orientation, Math.PI / 6.0) < 2e-3, $"theta={outcome.Orientation:F6}");
    }

    private static List<WaferAlignmentService.FlatRegion> BuildRegions(PointInPolar[] points)
    {
        Assert.True(WaferAlignmentService.TryComputeFlatReference(points, 0.0, 0.0, Pitch / 10.0, out _, out double[] deltas));
        return WaferAlignmentService.BuildFlatCandidateRegions(points, deltas);
    }

    private static int RegionIdAt(List<WaferAlignmentService.FlatRegion> regions, int index)
    {
        int n = SampleCount;
        foreach (WaferAlignmentService.FlatRegion region in regions)
        {
            if (ModIndex(index - region.StartIndex, n) < region.PointCount)
            {
                return region.Id;
            }
        }
        return -1;
    }

    private static int ModIndex(int index, int length)
    {
        int m = index % length;
        return m < 0 ? m + length : m;
    }

    private static void MakeHole(PointInPolar[] points, int holeCount, double flatTheta = Math.PI / 6.0)
    {
        // 在平边中心挖指定长度的连续空洞；按环形索引生成，因此也能覆盖 0°/360°。
        double flatHalfAngle = Math.Acos((Radius - FlatDepth) / Radius);
        int centerIndex = (int) Math.Round(WrapTo2Pi(flatTheta) * points.Length / (2.0 * Math.PI)) % points.Length;
        int startIndex = (centerIndex - holeCount / 2 + points.Length) % points.Length;
        var holeIndexes = new List<int>(holeCount);
        for (int k = 0; k < holeCount; k++)
        {
            int i = (startIndex + k) % points.Length;
            double delta = WrapAngle(points[i].Theta - flatTheta);
            if (Math.Abs(delta) < flatHalfAngle * 0.9)
            {
                holeIndexes.Add(i);
            }
        }
        Assert.True(holeIndexes.Count == holeCount);
        foreach (int i in holeIndexes)
        {
            points[i] = new PointInPolar(double.NaN, points[i].Theta);
        }
    }
}