// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Windows.Media;
using Vega.Data.Commons;
using Vega.Data.Defects;
using Vega.UI.Common;

namespace Vega.UI.Inspection.Tests;

/// <summary>
/// 缩放跨 10 倍阈值（位图烘焙 → 矢量圆）的缺陷点绘制回归：
/// 正常数据矢量圆齐全且 bounds 有界；越界/异常坐标缺陷被跳过，不得撑爆 DrawingGroup.Bounds
/// （否则 Stretch=Uniform 会把全部内容压缩到不可见，表现为高缩放下点全部消失）。
/// </summary>
[Collection("Result summary binning")]
public sealed class WaferMapImageViewZoomVectorTests
{
    private readonly SummaryBinningDispatcher _dispatcher;

    public WaferMapImageViewZoomVectorTests(SummaryBinningDispatcher dispatcher) => _dispatcher = dispatcher;

    private static DefectInfo CreatePointDefect(double x, double y)
    {
        return new DefectInfo
        {
            CollectionName = "Default",
            ChannelMode = ChannelMode.DW2_Normal,
            DefectName = "LPD",
            SizeType = SizeType.LPD_SIZED,
            Attributes = new Dictionary<string, double>
            {
                [nameof(AttributeName.Size)] = 0.3,
                [nameof(AttributeName.X)] = x,
                [nameof(AttributeName.Y)] = y
            }
        };
    }

    [Fact]
    public void HighZoom_CrossingThreshold_StillDrawsPointDots()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var vm = new WaferMapImageViewModel();
            vm.SetWaferInfos(150000, 3000);
            var defects = Enumerable.Range(0, 20)
                .Select(i => CreatePointDefect(i * 10000 - 95000, 95000 - i * 10000))
                .ToList();
            var group = new DefectGroup(System.Drawing.Color.FromArgb(255, 0, 0, 255), defects);

            vm.DrawDefects([group]);
            vm.ScaleTransform = 10.5;
            vm.DrawDefects([group], pointsInBitmap: false);

            var dotDrawings = vm.WaferMapDrawingGroup.Children
                .OfType<GeometryDrawing>()
                .Where(d => d.Geometry is GeometryGroup)
                .ToList();
            var dotCount = dotDrawings.Sum(d => ((GeometryGroup) d.Geometry).Children.Count);
            Assert.Equal(20, dotCount);
            Assert.True(vm.WaferMapDrawingGroup.Bounds.Width <= 4300);
            Assert.False(vm.IsEmpty);
            services.AssertNoErrors();
        });
    }

    [Fact]
    public void HighZoom_OutlierDefect_DoesNotExplodeDrawingBounds()
    {
        _dispatcher.Run(() =>
        {
            using var services = new SummaryBinningEnabledTests.TestServices();
            var vm = new WaferMapImageViewModel();
            vm.SetWaferInfos(150000, 3000);
            var defects = Enumerable.Range(0, 19)
                .Select(i => CreatePointDefect(i * 10000 - 95000, 95000 - i * 10000))
                .ToList();
            defects.Add(CreatePointDefect(5e8, -5e8));
            var group = new DefectGroup(System.Drawing.Color.FromArgb(255, 0, 0, 255), defects);

            vm.DrawDefects([group]);
            var bitmapBounds = vm.WaferMapDrawingGroup.Bounds;
            vm.ScaleTransform = 10.5;
            vm.DrawDefects([group], pointsInBitmap: false);
            var vectorBounds = vm.WaferMapDrawingGroup.Bounds;

            Assert.True(bitmapBounds.Width <= 4300, $"位图模式 bounds 应被位图裁剪: {bitmapBounds.Width}");
            Assert.True(vectorBounds.Width <= 4300, $"矢量模式越界缺陷撑爆 bounds: {vectorBounds.Width}");

            var dotCount = vm.WaferMapDrawingGroup.Children
                .OfType<GeometryDrawing>()
                .Where(d => d.Geometry is GeometryGroup)
                .Sum(d => ((GeometryGroup) d.Geometry).Children.Count);
            Assert.Equal(19, dotCount);
            Assert.False(vm.IsEmpty);
            services.AssertNoErrors();
        });
    }
}