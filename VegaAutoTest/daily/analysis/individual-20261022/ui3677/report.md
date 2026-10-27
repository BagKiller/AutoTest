# #3677 扩容后 UI 复验记录

时间：2026-10-23T17:35:32.028022+08:00。本次实际 UI 验收未完成。

Runner 原始 verdict：**BLOCKED / ZERO_ASSERTIONS_MATCHED**；runId `20261023T093343Z_wi0_8f37b7f`。

## 已完成的操作

- 确认 C 盘已扩容至约 119.90 GB，开始检查时可用约 44.05 GB；构建后仍约 40.85 GB。原磁盘空间不足问题已解除。
- 启动原有 MongoDB 数据目录，127.0.0.1:27017 检查通过；未新建空库。
- 在独立目录重新完整构建昨天目标 `10c33df5e69aa932bea8d843a29d8f6530d1fd73`：主程序 0 错误/3821 警告，配套编辑器 0 错误/143 警告，DLL SHA256 已核对。使用 SDK10 构建，未使用旧批次输出。
- 准备 Vega300-HWv1 / Simulator / Gem=false / Gen2 隔离会话。真实启动 `C:\AutoTest\workspaces\individual-20261022-20261023\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.exe`，PID `8388`，启动时间 `2026-10-23T17:31:39.063162+08:00`。
- 20 秒启动检查中进程仍存在，窗口标题为 ``，Responding=False; 未取得主窗口就绪证据。该短时进程检查不能判定产品启动故障或 UI 通过。
- finally 停止本轮 Vega，恢复 ComponentConfig、HardwareProfiles 及 MachineConfiguration。下表 SHA256 与备份一致。

## 当前阻塞及未执行步骤

Computer Use 的 Authenticator launch_app 超时；后续 list_windows 超时；重置 JavaScript 会话、重新导入 @oai/sky 并重试 list_windows 后仍超时。按工具技能恢复指引停止继续发送桌面输入。本次没有取得可用的界面操作或截图证据。

| #3677 必需步骤 | 本次状态 |
| --- | --- |
| 授权并完成 su 登录 | 未完成 |
| 导入原始 dual.recipe | 未执行 |
| 创建 Job 并选择配方 | 未执行 |
| 离线 postprocess 添加原始 Normal/Oblique JSON | 未执行 |
| 执行并查看结果 | 未执行 |
| 核对本轮 UI 产出的双 collection pitch、DC 合并结果 | 未执行，无本轮产出 |

桌面控制连接恢复前，不能完成上述实际 UI 验收。此前 22 项逻辑检查仍只代表此前逻辑覆盖，不用于替代本次 UI 结果。

## 配置恢复证据

| 文件 | 原始 SHA256 | 恢复 SHA256 |
| --- | --- | --- |
| ComponentConfig | `C673B1F02ADCE40F5E853EDE4424948AF3B8F93EF5A1CC20438E55A69DCC0F6E` | `C673B1F02ADCE40F5E853EDE4424948AF3B8F93EF5A1CC20438E55A69DCC0F6E` |
| HardwareProfiles | `12B209402071FA6297F00A8FDCB2A67A244DF0B87C3FA71FF8F3C57FB5B510AA` | `12B209402071FA6297F00A8FDCB2A67A244DF0B87C3FA71FF8F3C57FB5B510AA` |
| MachineConfiguration | `EADE615A9C6712B6540DD132837F9A993A9FC5037362AC83D8A639879514B6C6` | `EADE615A9C6712B6540DD132837F9A993A9FC5037362AC83D8A639879514B6C6` |

会话：`C:\AutoTest\AutoTest\VegaAutoTest\daily\sessions\20261023T173130_Vega300-HWv1_6476e7`；恢复状态 `RESTORED`。未执行真机动作，未修改每日收集游标或历史 Runner 结果。

## 原始证据

- [Runner 原始 result.json](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/runner/20261023T093343Z_wi0_8f37b7f/result.json)
- [Runner 原生报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/runner/20261023T093343Z_wi0_8f37b7f/report.md)
- [启动与恢复审计](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/startup-status.json)
- [本次完整构建指纹](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/build-after-expand/build-provenance.json)
- [桌面工具故障及未完成步骤](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/ui-evidence.json)
- [机器可读汇总](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/ui3677/ui-summary.json)

Runner 的 builtFromCurrentSource=false 原样保留；独立构建指纹另附。UI 专项检查只审核实际操作证据，缺证据不会算 PASS。初次聚合 L2 工程编译遇到与 #3677 无关的 IncidenceFilterType/IncidenceOD3 引用错误，原始 ERROR 结果保留；随后仅隔离测试工程依赖，未改动六项验收要求或降低 minAssertions。

## Runner 执行历史

| runId | 原始 verdict |
| --- | --- |
| `20261023T093231Z_wi0_9cb92e6` | ERROR/TEST_HOST_FAILED |
| `20261023T093343Z_wi0_8f37b7f` | BLOCKED/ZERO_ASSERTIONS_MATCHED |
