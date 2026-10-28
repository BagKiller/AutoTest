// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using Galaxy.Data.Wafers;
using Vega.BL.WaferAlignment;

namespace Vega.BL.WaferAlignment.Tests;

/// <summary>
/// SetupWaferAlignment 两个初始化入口（数值/快照）共用的定位边类型校验单测：
/// 未知枚举值必须显式拒绝（ArgumentOutOfRange），不得静默按 Notch 分支处理
/// （实例方法依赖容器服务，按模块约定走构建/集成验证；此处覆盖其纯静态校验内核）。
/// </summary>
public class SetupWaferAlignmentEdgeTypeValidationTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(99)]
    [InlineData(-1)]
    public void ThrowIfUnsupportedNotchType_UndefinedValue_Throws(int undefinedValue)
    {
        var undefined = (NotchTypes) undefinedValue;

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => WaferAlignmentService.ThrowIfUnsupportedNotchType(undefined));
        Assert.Contains("Unsupported wafer edge type", ex.Message);
    }

    [Theory]
    [InlineData(NotchTypes.Notch)]
    [InlineData(NotchTypes.Flat)]
    public void ThrowIfUnsupportedNotchType_DefinedValue_DoesNotThrow(NotchTypes defined)
    {
        WaferAlignmentService.ThrowIfUnsupportedNotchType(defined);
    }
}