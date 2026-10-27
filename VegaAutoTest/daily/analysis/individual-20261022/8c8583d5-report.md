# #3672：CoW 拟合与 Notch offset 跳变：逐提交复验报告

提交 `8c8583d50d3592ed718b8223bf415a54076bb1c5`。

工作项明确要求：对同一组 Edge 数据反复分析时，CoW 不再在相差 20+ μm 的两点之间跳动。提交修改 Kåsa 初值、RANSAC 固定种子、内点带宽和 LM 径向 RMS 护栏。

本次 Runner 原始 verdict：**PASS / OK**；结构化检查 10/10；runId `20261023T081231Z_wi0_35eec62`。

被测版本为昨天最终 HEAD `10c33df5e69aa932bea8d843a29d8f6530d1fd73`；本次按提交各自设计和执行独立测试，共用此确定版本的重新构建模块。未逐个 checkout 三个历史 SHA 做隔离二分。

## 实际执行的验证

- 已知圆心 (31,-47) μm、半径 150000 μm 的理想圆：代数拟合圆心/半径误差不超过 1e-6 μm。
- 四段短弧输入：圆心误差不超过 1e-5 μm。
- 真实 WaferAlignmentService.FindCOWCircleFitting：720 点合成圆拟合圆心误差小于等于 1 μm，随后重复调用 10 次，输出严格一致。
- RANSAC：120 个圆内点与 3 个明确离群点，连续 30 次返回相同的 120/3 分组，未走 fallback。
- 0/1/2 点、共线输入、无足够共识分别检查退化返回和 fallback 标记；实际 service 两点输入返回 NaN。

## 覆盖边界与未验项

缺少工单截图对应的原始 Edge BIN，未做该现场数据的重复回放、最终 Notch theta 测量或真实 EdgeChuck 设备验收。测试覆盖实际算法模块及服务方法，但没有触发硬件或完整诊断页流程。

本项登记为 `partial-acceptance`，算法/逻辑 PASS 不等于整条工作项或整机验收。

## 来源与证据

- [原始 result.json](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/wi3672-logic/20261023T081231Z_wi0_35eec62/result.json)
- [Runner 原生报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/wi3672-logic/20261023T081231Z_wi0_35eec62/report.md)
- [本次独立构建及 DLL SHA256](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/build-reference.json)
- [提交原始 diff](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/8c8583d5.diff)

Runner 没有调用其自身 --rebuild，故 `builtFromCurrentSource=false` 原样保留；其内置字段不能证明本次模块构建。附加构建证据与测试内 SHA256 比对证明所调用 DLL 来自本次独立目录；未使用旧批次 DLL，未构建/启动完整 Vega.exe。

## 逐用例原始结果

| 用例 | 结果 |
| --- | --- |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.FewerThanThreePointsHasExplicitFallback(count: 2) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.CollinearInputDoesNotInventCircle | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.FourShortArcsRecoverKnownCenter | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.ActualServiceRepeatedCircleFitDoesNotJump | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.NoConsensusIsMarkedAsFallback | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.AlgebraicCircleRecoversKnownCenter | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.FewerThanThreePointsHasExplicitFallback(count: 0) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.RepeatedRansacIsDeterministicWithOutliers | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.ActualServiceTooFewPointsReturnsNaN | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3672Logic.FewerThanThreePointsHasExplicitFallback(count: 1) | Passed |

## 历次执行记录

| runId | 原始 verdict | 检查数 |
| --- | --- | --- |
| `20261023T075917Z_wi0_7109af4` | ERROR/TEST_HOST_FAILED | 0/0 |
| `20261023T080108Z_wi0_4ee01d9` | ERROR/TEST_HOST_FAILED | 0/0 |
| `20261023T080248Z_wi0_2d1c123` | PASS/OK | 8/8 |
| `20261023T080417Z_wi0_60f5005` | PASS/OK | 10/10 |
| `20261023T081231Z_wi0_35eec62` | PASS/OK | 10/10 |

早期锁文件冲突、测试适配代码编译/容器配置异常属于测试准备阶段，原始 ERROR 全部保留；正式结果取本文件列出的最终 runId，未编辑历史 Runner 结果。
