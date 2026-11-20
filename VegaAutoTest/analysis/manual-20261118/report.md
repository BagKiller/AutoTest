# 2026-11-18 提交检验

今天是 2026-11-19。按北京时间取出 11 月 18 日 master 上的提交，固定 HEAD `64e92b2921f8c92f02b1722a88098cf5e1bae7d8`。没有调用 `model_daily.py --begin`，没有推进 `daily/state.json`（仍停在 11 月 17 日的 `bcbc53a5`），也没有占用 `daily-latest`。代码在独立工作区 `C:\AutoTest\workspaces\manual-20261118`。

当天 2 条提交。不是每条都做界面验收。

## 822cc2b5 + #3819

`822cc2b564262ac0728b2dba0a8e48755c800df5`，10:23，结果摘要通道行追加荧光滤光片、光学滤波器和 PMT 电压简写。

工单标题「Collection info优化-Part2」，没有复现步骤和验收标准。描述写明三项显示和简写：荧光滤光片 F1/F2/N，光学滤波器 ODp0→OD0、ODp0_5→OD0.5。提交里的 `CollectionSummaryOpticalRowsTests` 把这些字符串锁进 `BuildOpticalSummaryRows`。工单没有给出要打开的配方或结果文件，提交只把结果页渲染写成建议，所以没有为了看 DataGrid 去启动 Vega、也没有编造一次扫描。

在该 HEAD 上用 SDK 10 编译、用 .NET 8 运行这组测试：28 通过，0 失败，0 跳过。TRX：`product-tests/optical-rows.trx`。这是产品单测，不是 Runner `result.json`，不能写成界面通过。

## 64e92b29 + #3828 / #3779

`64e92b2921f8c92f02b1722a88098cf5e1bae7d8`，11:08，Vega100 加装 Edge Detector 后，对位算法入口按数据源分岔。

#3828 有复现步骤：前置是 Vega100-PIC-ED 或 Vega100-Coherent-ED，NotchDataSource=EdgeDetector；流程是实机 Wafer Run，核对 notch 角度。提交自己也写了「真机待验」，并且说明这段编排不写单测。本机只有 Simulator，不能代替加装边缘检测器后的信号和角度。没有启动 Vega 去编一套模拟器对位。

#3779 是父需求，描述硬件改造、配置下拉、第二张采集卡和装机向导。这次 diff 只改了 `WaferRunSequenceBase.DoPerformEdgeAlignmentAsync` 的算法分支：EdgeDetector 源走 `PerformAlignment`，DarkField 仍走 `PerformAlignmentDUVLaser`。父需求里的配置界面和采集卡不在这次提交里，没有单独做界面。
