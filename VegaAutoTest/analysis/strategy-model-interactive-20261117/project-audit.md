# 每日验证链路跨项目审查

时间：2026-11-17T16:37:34.096132+08:00

范围：遍历C:/AutoTest/AutoTest的活动源码/配置/文档索引（309文件），交叉检查采集、绑定、构建、Runner、UI会话、调度及收尾入口。生成产物bin/obj、历史runs/analysis、Git缓存及第三方依赖不作为本轮修改目标。未声称逐行审计所有产品源码。

## 已修复

1. 旧Install/Start/autopilot/daily入口仍能重新启动脚本夜间队列：新增execution-policy.json并在各入口启动前拒绝。显式历史复验及只读status保留；model-interactive批次不能交旧冻结/执行链路处理。
2. 残留VegaAutoTest-Redo1030-CapacityRetry已过期但仍启用：导出XML后禁用。原每日Windows任务继续禁用，Codex vega仍ACTIVE23:30。
3. model_daily收集中断可能缺失清单、跨午夜无法恢复已有窗口：在collection cursor前持久化模式/单位，通过幂等initialize补缺不改原结果；已存在T窗口可跨午夜恢复，新窗口不跨日误建。
4. 新模型入口遗漏当前Runner构建：ensure_runner构建并校验Runner DLL/源码指纹，防旧Runner缺--registry等功能。只构建一次的约束针对产品目标，不能把Runner旧版本当可用。
5. 复用产品只核对Vega.dll：增加实际GitHEAD/clean检查、RecipeBatchEditor和ComponentConfigEditor精确输出路径哈希核对。
6. 工单/附件获取只有叙述约定：新增--requirements机械入口，仅获取本次匹配单位的工单和附件；模型仍须完整阅读。
7. 收尾只靠模型手写文件：新增--finish检查完整SHA+工单配对集合、PENDING/错误跳过、真实Runner的目标HEAD/工单/verdict对应、真实缺口文件、会话RESTORED与配置/机器配置哈希。成功后复制原始verdict/verdictCode/runId并输出summary/completion；缺项拒绝完成。所有Prepare会话由模型填sessionPaths以便检查。
8. 顶层README、操作手册、daily旧标题及历史memory仍可引向旧入口：增加当前规则指向，旧说明标为历史；Codex自动化提示同步requirements/finish入口。
9. daily.load未关闭JSON文件：使用with open，消除测试资源泄漏告警。

## 验证

- Python unittest daily全套184/184通过（包含新增配对、漏项、无Runner伪PASS拒绝、全跳过、恢复、防重复和构建中断合同）。日志C:/AutoTest/test-artifacts/daily-audit-final-20261117.log。
- .NET Runner测试55/55通过，无skip；同时编译当前Harness和Runner。日志C:/AutoTest/test-artifacts/runner-audit-20261117.log。
- 活动Python47文件AST解析无错误；PowerShell6脚本解析无错误。
- cases/registry.json共227条，无重复id，所有layer项目路径存在。
- 审查时 `autopilot.py --daily` 和 `daily.py --execute` 会在创建队列前拒绝。随后 Windows 任务以及 `Install-DailyValidationTask.ps1`、`Start-DailyValidation.ps1` 已删除，不要把它们加回来。
- 本轮未收集真实提交、未构建/运行Vega产品、未改产品源码和历史原始结果。模型可用工具/桌面登录状态/ADO及构建依赖的真实端到端效果，须首轮实际夜间执行确认，单元测试不能证明它们始终可用。

C:可用17045610496bytes（15.87GiB），低于30GB；未清理。
