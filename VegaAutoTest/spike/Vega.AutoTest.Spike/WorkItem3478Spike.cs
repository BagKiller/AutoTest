// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

#if VEGA_SOURCE_AVAILABLE
// ViewModel 探针引用被测主项目源码（Vega.UI.Inspection）。
// 本仓库独立后该引用是可选的：设 VEGA_SW_ROOT 环境变量并重编译时才生效，
// 见 Vega.AutoTest.Spike.csproj 的 HasVegaSource 说明。
using Vega.UI.Inspection;
#endif

namespace Vega.AutoTest.Spike;

/// <summary>
/// 可行性验证：工作项 #3478「Inspection 检测区域参数联动异常」能否在不启动 Vega GUI
/// 的前提下，仅通过 ViewModel 复现并断言。
///
/// ViewModel 内部单位为 micron，界面经 Factor=0.001 的转换器显示为 mm。
/// 下面步骤中的注释按工作项复现步骤给出界面值（mm）。
///
/// 跨仓库说明：本类依赖被测主项目源码，仅在 VEGA_SW_ROOT 已设置（HasVegaSource=true）
/// 时编译进测试程序集；未设置时 logic 层用例不可用，registry 中 logic 层的 dotnet test
/// 会因过滤器零匹配而非零退出（Runner 如实 BLOCKED），不影响 uia 层。
/// </summary>
#if VEGA_SOURCE_AVAILABLE
public class WorkItem3478Spike
{
    private const double WaferRadius300Mm = 150_000;

    /// <summary>
    /// 问题一：起始半径修正为合法值后，结束半径的校验错误未清除，导致仍然无法保存。
    /// 预期（修复后）：所有参数合法时 HasErrors 为 false。
    /// </summary>
    [Fact]
    public void Issue1_StartRadiusCorrected_ShouldClearEndRadiusValidationError()
    {
        var vm = NewViewModel();

        vm.IsSecScanEnable = true;
        vm.SecInspRegionWidth = 6_000;          // 宽度 6
        vm.EdgeExclusion = 1_000;               // 边缘切除 1
        vm.PriInspRegionStartRadius = 100_000;  // 起始半径 100

        vm.EdgeExclusion = 100_000;             // 边缘切除改为 100，结束半径应联动为 44

        Assert.Equal(44_000, vm.PriInspRegionEndRadius);

        vm.PriInspRegionStartRadius = 40_000;   // 起始半径改为 40，此时 40 <= 44，参数已合法

        Assert.False(
            HasError(vm, nameof(vm.PriInspRegionEndRadius)),
            "起始半径修正为合法值后，结束半径的校验错误应被清除");
        Assert.False(vm.HasErrors, "所有参数合法时应允许保存");
    }

    /// <summary>
    /// 问题二：边缘切除等于晶圆半径时，结束半径停留在中间输入值对应的旧值。
    /// 界面 UpdateSourceTrigger=PropertyChanged，逐位输入会逐字符触发联动，这里如实模拟。
    /// </summary>
    [Fact]
    public void Issue2_EdgeExclusionEqualsWaferRadius_ShouldUpdateEndRadius()
    {
        var vm = NewViewModel();

        vm.EdgeExclusion = 1_000;           // 边缘切除 1
        vm.IsSecScanEnable = false;         // 关闭次扫描区域
        vm.PriInspRegionStartRadius = 0;    // 起始半径 0

        // 全选后逐位输入 150：1 -> 15 -> 150
        vm.EdgeExclusion = 1_000;
        vm.EdgeExclusion = 15_000;
        Assert.Equal(135_000, vm.PriInspRegionEndRadius);   // 工作项描述：输入到 15 时结束半径变 135

        vm.EdgeExclusion = 150_000;         // 边缘切除 150，等于晶圆半径

        Assert.Equal(0, vm.PriInspRegionEndRadius);
    }

    private static DFInspectionRegionViewModel NewViewModel()
        => new() { WaferRadius = WaferRadius300Mm };

    private static bool HasError(DFInspectionRegionViewModel vm, string propertyName)
    {
        foreach (var _ in vm.GetErrors(propertyName))
        {
            return true;
        }

        return false;
    }
}
#endif
