# 每日自动校验修复与实测记录

记录时间：2026-10-28T15:38:49.591184+08:00。

已完成用户授权的独立修复复验：10 个提交全部流程收尾，pending=0。9 个 SHA 成功独立构建；1 个 SHA 在自身源码上真实编译失败。
针对修复点的 10 次 Runner 运行全部 PASS，合计 226 条结构化断言。
路径误报修正版在 9 个可构建 SHA 上独立补测全部 PASS，合计 388 条断言；最新 SHA 还通过了完整四机型基线。

**这不是全部产品验收通过。** 实际 UI、远端采集和锁定还原等未验范围仍明确保留。原始冻结批次/原 Runner verdict 不改写。

[本次逐提交原始报告](C:/AutoTest/AutoTest/VegaAutoTest/daily/runs/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/report.md)；[机器可读验证汇总](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/repair-20261028/verification-summary.json)。

## 根因与已落实的修正

1. **规则互相冲突。** 旧 README、VegaAgent 系统提示、AutoProcessor 和 MCP 工具描述要求“未登记转人工”，每日规则却要求 AI 补写。已统一为冻结前自动准备、冻结后禁止修改断言；也修正了把 Runner 的 builtFromCurrentSource=false 一概当作旧 DLL 的错误表述。
2. **Python 只生成候选计划，AI 没有完成准备。** 现已增加 --prepare-all、按 SHA 隔离的产品测试适配器、自动复用锁图测试，以及 --check-cases 的实际编译/发现。保留原产品测试程序集身份、xUnit output helper、Theory 数据及 async 生命周期；缺用例必须有实际尝试证据。
3. **队列推进依赖对话记忆。** 新 --drain 在所有计划冻结后串行执行整批，单项构建/测试失败继续，重复启动不重跑已收尾 SHA，实际 UI 请求单独返回，不把空 UI slot 当作已验。此次原来的“首项完成、9项遗留”变为 10 项全部收尾。
4. **旧回归断言过于依赖字符串写法。** Windows 的反斜杠与正斜杠路径被错误判为不同目录，造成 7 个旧基线 FAIL。新增独立 v3 基线，只统一分隔符，不改变期望目录、其他业务断言或断言数量。原 FAIL 保留。第一次 v2 补测暴露程序集绑定错误，原 ERROR 也保留；v3 使用稳定程序集身份/动态类型绑定并已重测通过。
5. **测试来源可漂移。** 存在写死旧临时 worktree 的源码引用及读取历史 UI 日志的旧用例。新准备门禁拒绝外部写死源码，只选审核明确选择的用例；冻结覆盖共享项目依赖、链接源码、附件及准备证据。旧历史用例/原结果保留，不再拿它们填新计划覆盖。
6. **环境与证据不稳定。** 修正 MachineConfiguration.json 没有恢复、Prepare 失败/无进程时的会话遗漏、无范围限制杀 Vega、超时丢失 host 日志、Windows 原子写临时文件名过长和 CP936/UTF-8 输出不一致。恢复实测确认三个配置文件逐字节一致。
7. **数据与磁盘没有闭环。** 新增同源、限时限量的工单附件下载，支持 ADO 项目 GUID 地址，缩短附件落盘名避免 MAX_PATH。已取回 4 个实际附件。仅清理有路径指纹证明的测试编译副本；完成的本项目 worktree 可回收未跟踪的 obj/中间 bin，保留源码和整个 Vega/bin，空间紧张时使用不改文件字节的 NTFS 压缩。

## 实测与原始结果

| SHA | 原冻结状态 | 针对性回归 | 独立修正补测 |
| --- | --- | --- | --- |
| `00b3004a` | NEEDS_CASE | commit-00b3004a58de-cache-isolation: PASS (20261028T062747Z_wi0_732f6c7)<br>commit-00b3004a58de-product-tests: PASS (20261028T062805Z_wi0_f716c5e) | PASS (20261028T071043Z_wi0_9d69bf5) |
| `6b4cf4af` | NEEDS_CASE | commit-6b4cf4af7928-behavior: PASS (20261028T063007Z_wi0_47125da) | PASS (20261028T071151Z_wi0_d8f5e81) |
| `227ace85` | FAIL | commit-227ace851042-product-tests: PASS (20261028T063234Z_wi0_93428cf) | PASS (20261028T071258Z_wi0_70bf6cb) |
| `bd9ccb55` | ERROR | 产品构建失败，未伪造 Runner 结果 | 构建失败，不能运行 |
| `19c18be5` | FAIL | commit-19c18be54684-product-tests: PASS (20261028T063439Z_wi0_dddb26f) | PASS (20261028T071406Z_wi0_32f50bc) |
| `9fa008a9` | FAIL | commit-9fa008a98a35-product-tests: PASS (20261028T063642Z_wi0_581719d) | PASS (20261028T071514Z_wi0_5463104) |
| `d117a21d` | FAIL | commit-d117a21d8bef-product-tests: PASS (20261028T063901Z_wi0_7febdd6) | PASS (20261028T071623Z_wi0_0ca7448) |
| `eba9945a` | FAIL | commit-eba9945afbca-product-tests: PASS (20261028T064154Z_wi0_8e8d86b) | PASS (20261028T071731Z_wi0_3fde5f4) |
| `b428717a` | FAIL | commit-b428717a56aa-behavior: PASS (20261028T064536Z_wi0_9d642d9) | PASS (20261028T071840Z_wi0_fa3a586) |
| `c30defb8` | FAIL | commit-c30defb85f29-behavior: PASS (20261028T064746Z_wi0_21ae828) | PASS (20261028T070931Z_wi0_5b3656d) |

真实编译错误：`bd9ccb55` 的 `Vega.Data.Calibration/CaliCommonDataDefinition.cs` 第 68、73 行引用不存在的 `FileName`，错误码 CS0103。后续 SHA 使用 `PmtCaliConfigPaths.ConditionsFileName`。没有修改被测产品源码来隐藏该失败。

## 验证边界与交付

- 55 项 Python 编排/门禁/清理回归通过；11 项 Runner 契约回归通过，包含真实子进程超时与日志保留。配置 Prepare/Restore 实测通过。
- 原批次证据以 original-evidence-hashes.json 核对，所有 10 个新冻结计划/测试源码也完成最终指纹检查。每日游标没有因本次专项复验推进。
- 定时任务 vega 已更新并读回一致；名称、每日 23:30、模型/推理设置、绑定项目及目录保持不变。入口详见 daily/AGENTS.md 的 2026-10-28 章节。
- 自动化执行链及针对性测试已跑通；完整软件界面、远端 Linux 采集/算法链及真实设备验收仍未完成，不能根据本报告宣称全部产品验收通过。

[11 个历史批次审计](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/repair-20261028/daily-audit.json)；[项目源码遍历清单](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/repair-20261028/source-inventory.json)；[最终冻结源审计](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/repair-20261028/frozen-source-final-audit.json)；[定时任务前后对照](C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/repair-20261028/automation-change.json)。
