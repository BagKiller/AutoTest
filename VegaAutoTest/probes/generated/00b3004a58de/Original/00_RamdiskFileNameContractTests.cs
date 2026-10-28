// Copyright (c) Shanghai Vega Technology Co., Ltd. All rights reserved.

using System.Runtime.CompilerServices;
using Vega.Data.Analysis;
using Vega.Data.Commons;
using Vega.Data.Measurements;
namespace Vega.Utility.Common.Tests;

/// <summary>
/// ramdisk 分析 bin 文件名契约（PR #5015 / WI #3706，build 11119 事故防回归）：
/// AnalysisDataStorage.DataFileName 的基底段必须与 WaferRunInfo.GetRamdiskFileBase 逐字一致
/// （算法服务器按 post 的 data_file_path 全名开文件），且不得混入采集缓存键的 run 种子。
/// Linux 侧落盘名 = fileBase + "_{channelName}_{start}_{end}.bin"，channelName 由
/// XilinxCardDeviceProxy 经 SetChannelNames 下发（ChannelMode.ToString()），与 Windows 侧同源。
/// </summary>
public class RamdiskFileNameContractTests
{
    [Fact]
    public void DataFileName_UsesRamdiskFileBaseWithoutRunSeed()
    {
        // WaferRunInfo 构造函数链会实例化 Recipe 并触发 ToolFeaturesHelper→XActivator 容器初始化，
        // 单测环境无 Galaxy 运行时；本契约只测 GetRamdiskFileBase/GetDataCacheKey 的纯格式逻辑
        // （仅依赖 WaferID 与 ID/_dataCacheSeed），故两个对象都用 GetUninitializedObject 绕过构造副作用。
        var waferRun = (WaferRunInfo) RuntimeHelpers.GetUninitializedObject(typeof(WaferRunInfo));
        waferRun.WaferID = "WAFER_14";
        waferRun.LotID = "LOT_1";
        waferRun.ID = 123456789012; // run 雪花号，即 GetDataCacheKey 的种子来源

        var storage = (AnalysisDataStorage) RuntimeHelpers.GetUninitializedObject(typeof(AnalysisDataStorage));
        storage.WaferID = waferRun.WaferID;
        storage.CollectionName = "Collection1";
        storage.CollectionIndex = 2;
        storage.ChannelMode = ChannelMode.DW1_Oblique;
        storage.TrackStartIndex = 30;
        storage.TrackEndIndex = 40;

        string expected = $"{waferRun.GetRamdiskFileBase(2)}_{ChannelMode.DW1_Oblique}_30_40.bin";
        Assert.Equal(expected, storage.DataFileName);
        Assert.StartsWith(waferRun.GetRamdiskFileBase(2) + "_", storage.DataFileName);

        // 采集缓存键（含 run 种子）不得进入文件名——种子进名即与算法报名断裂（build 11119）。
        Assert.DoesNotContain(waferRun.GetDataCacheKey(2), storage.DataFileName);
        Assert.DoesNotContain("_123456789012_", storage.DataFileName);
    }
}