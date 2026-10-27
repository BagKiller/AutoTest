# 2026-10-20 每日定时验证执行前检查

## 结论

今晚（2026-10-20）北京时间 **23:30** 的现有 Codex 自动化 `vega` 已启用并更新为最新流程。已实跑远端最新代码的独立构建、四组无界面 Runner 和报告生成；单组异常没有阻塞后续组，也没有等待人工输入。

**调度配置与批次执行链已检查完成，但测试结果不是全绿，也不能提前保证尚未发生的定时触发。** 本次十提交预演最终为 `INCOMPLETE`：9 个提交缺登记用例，1 个提交关联 Runner 异常。缺用例与测试不通过应进入报告，不能视为调度失效或伪造为验收通过。

## 检查与修正

| 项目 | 检查结果/处理 |
| --- | --- |
| 最新自动化工程 | 本地 HEAD `a549a5d`，已读取最新 daily/AGENTS.md、README.md、daily.py、scheduling.json 和 AI 操作手册 |
| 定时时间 | 每天 23:30；ACTIVE；本机 China Standard Time；项目 C:\AutoTest |
| 调度入口 | Python 固定路径运行 daily.py --execute；此命令只执行无界面阶段 |
| 界面续跑 | 已更新现有自动化提示：读取末行 JSON 的 pendingUiSlots，逐个 Prepare、官方认证、--resume --run-slot --session，finally 恢复；没有 slot 就不启动多余机型 |
| 无人值守 | 不询问用户点击；有限超时、最多一次恢复重试；清理已确认属于验证工作区的 Simulator，异常继续其他组并生成报告 |
| 数据库 | 检查时未运行；已用原有 MongoDB 8.0.20 和原数据目录启动，ping=1、isWritablePrimary=true、setName=rs0 |
| 数据库恢复入口 | 新增 Ensure-DailyDatabase.ps1；只复用已有数据库，有 WiredTiger 标记才允许启动；45 秒有界等待；重复执行已验证不会重复启动 |
| 新目录构建 | 修复 Build-DailyVega.ps1 漏构建独立 RecipeBatchEditor 的问题；该 DLL 是 CommitProbe 的必需引用；现在记录两个 DLL 的 SHA256 |
| 睡眠 | 当前电源方案交流/直流自动睡眠均为 0（从不）；未改动电源设置 |
| 原用户工程 | 未重置 C:\Vega\Vega_SW；测试使用新 detached worktree |

## 最新目标实跑证据

- 模式：`--last 10 --plan-only` 远端编排，然后对同一 manifest `--resume ... --execute`。
- 示例模式不推进正式每日收集游标，也不修改正式待验队列。
- 批次：`20261020T163511+0800_98fa0a`。
- 产品目标：`cebf91e922b690c62d03f159a7899bb61344b6f3`，remoteFresh=true。
- 独立工作区：`C:\AutoTest\workspaces\20261020T163511+0800_98fa0a_20261020T163746_dec389`。
- 主程序构建：0 错误，3821 警告；独立批量编辑器：0 错误，143 警告。
- 主程序 SHA256：`A3440379C93B7F78103808C1F30F6FDF476C78191090BFFE42414326CBBED9C4`。
- 批量编辑器 SHA256：`30DD9A48E7120072A7A6FFF5D76F46554B65A9EFCBEFC0F14C642D098A11A77A`。
- 本次 pendingUiSlots 为空，没有启动原生 Vega 界面，也没有修改机型运行配置。

| 用例 | 原始 Runner 结论 | 断言 | runId |
| --- | --- | --- | --- |
| daily-supplement | ERROR / CONTRACT_VIOLATION | 17/18 | 20261020T084606Z_wi0_4f45e5d |
| wi3661-acceptance | PASS / OK | 180/180 | 20261020T084736Z_wi0_6b88c50 |
| daily-recent10 | PASS / OK | 320/320 | 20261020T084757Z_wi0_d250364 |
| wi3644-core | PASS / OK | 99/99 | 20261020T084815Z_wi0_51d4a7a |

共 617 个断言，616 通过，1 失败，0 跳过。Runner 自身 `builtFromCurrentSource=false` 原样保留；本批独立 build-reference.json 证明实际目标提交和 DLL 来源。

异常明细：`DirectSend_EmptyBytes_CountConsumedTracks(tracks:1,cancelled:true,expected:1)` 中 `delivered` 实际为 0。Runner 将该原始断言失败汇总为 HARNESS_EXCEPTION，并进一步给出 CONTRACT_VIOLATION；正式结论保留 ERROR，不手改为 PASS 或 FAIL。没有为了获得通过结果修改测试断言或产品代码。该问题仍需排查，今晚相同目标和用例可能再次报告异常，但本次已证明整批会继续执行并出报告。

`doctor` 对本批正确目标路径验证了 Vega.exe、Git、.NET 以及空闲 API 端口；仍缺旧 L1 插件脚本 `Vega.BL.AgentControlService/scripts/run-vega-with-plugin.ps1`。本次计划不使用旧 L1 路径，四组实际用例已执行完；因此不把 doctor 描述为全通过。未来如果计划增加依赖此脚本的 L1 用例，仍需补齐对应入口。

## 报告与运行条件

- [本次总报告](../../runs/20261020T163511+0800_98fa0a/report.md)
- [机器可读汇总](../../runs/20261020T163511+0800_98fa0a/summary.json)
- [构建来源](../../runs/20261020T163511+0800_98fa0a/build-reference.json)
- [异常原始 Runner 报告](../../runs/20261020T163511+0800_98fa0a/runner/20261020T084606Z_wi0_4f45e5d/report.md)

今晚仍需要电脑与 Codex 桌面应用保持运行、项目目录可访问；有界面会话时需要可交互桌面。该本地执行前提来自 [OpenAI 官方定时任务文档](https://learn.chatgpt.com/docs/automations?surface=app)。本次没有等待人工操作，也没有创建重复定时任务；现有任务保持每天 23:30 执行。
