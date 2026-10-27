# 2026-10-22 三条提交逐项复验

复验时间：2026-10-23T16:15:10.161521+08:00。远端重新刷新后确认昨天只有以下 3 条新增提交，历史积压未纳入。

本次逐条分析原始 diff、读取 #3672/#3677 的实际工作项要求，补充专属测试，并顺序独立调用 Runner。没有重用昨晚的四个批次回归结果。

| 提交 | 验证对象 | 本次原始 verdict | 检查 | runId | 详细报告 |
| --- | --- | --- | --- | --- | --- |
| `8c8583d5` | #3672：CoW 拟合与 Notch offset 跳变 | PASS/OK | 10/10 | `20261023T081231Z_wi0_35eec62` | [逐条报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/8c8583d5-report.md) |
| `8d088291` | PR4975：Notch 假候选与阈值包夹过滤 | PASS/OK | 27/27 | `20261023T081237Z_wi0_4a4c580` | [逐条报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/8d088291-report.md) |
| `10c33df5` | #3677：离线后处理 pitch 缺失与 DC 不合并 | PASS/OK | 22/22 | `20261023T081242Z_wi0_afd6e2a` | [逐条报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/10c33df5-report.md) |

三组共 59 项结构化检查。PASS 代表上述实际执行范围；三条均保留为部分覆盖，完整桌面流程、现场数据与最终 DC 合并等未验项见各自报告。

独立源码目录：`C:\AutoTest\workspaces\individual-20261022-20261023`；目标为昨天最终 HEAD `10c33df5e69aa932bea8d843a29d8f6530d1fd73`。构建 SDK10，测试宿主 SDK8/.NET8；对 37 个项目 DLL 核对 SHA256。相关模块 WaferAlignment 构建 0 错误/231 警告，WaferRun 构建 0 错误/384 警告。

完整 Vega.exe 未构建，未启动 GUI、Authenticator、Simulator 或真机；未修改产品模型配置，无产品配置恢复动作。测试容器配置仅写在独立测试宿主输出。用户 checkout、每日游标和已停用积压队列均未更改。

Runner `builtFromCurrentSource=false` 未改动；另附独立构建指纹。独立 worktree 中只有 unlocked restore 生成的 packages.lock.json 变化，产品源文件与目标 HEAD 一致。

- [独立构建指纹](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/build-reference.json)
- [机器可读汇总及附件指纹](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/summary.json)

测试库新增 wi3672-logic、commit-8d088291-logic、wi3677-logic 三个部分验收条目。初期测试适配异常保留在各条目的运行历史中；未改产品代码、未改 Runner verdict 语义或历史 result.json。
