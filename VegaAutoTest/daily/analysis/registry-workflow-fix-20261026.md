# 每日 registry / 编排闭环修正

时间：2026-10-26 17:53 +08:00。

## 本次变更

- 新 individual 根计划 schemaVersion=4，收集后只有候选计划，不提前冻结。输出 pendingPreparations、workflowState、nextAction，明确首次 --execute 还没有构建/执行。
- 新增 --prepare-commit（读取需求、刷新候选计划）、--freeze-commit --review-file（逐场景审核后冻结）。需求理解、复用/补写真实业务用例由定时任务中的 AI 执行，不是 Python 自动生成空测试。
- 按场景保存 steps、expected、workItems、caseIds、uiRequired、profiles、gap；工作项读取失败、缺真实用例或必需 UI/机型时保持 NEEDS_CASE，不能被逻辑 PASS 掩盖。
- 记录 diff、需求快照、可执行用例定义/本地测试源码指纹；冻结后拒绝重新编排和测试变化。修复 scope.json 原来指向不存在 diffs 子目录的问题。
- 单提交执行异常可记录 ERROR、收尾后推进后续提交。已明确收尾但未验证的 UI 保留在子报告和 unverifiedUiSlots，不再作为 pending 自动重放。
- 更新 AGENTS.md、README.md 和 AI 操作手册的每日流程说明。更新既有 vega 定时任务提示并读回核对；id、名称、ACTIVE 状态、每天 23:30、模型/推理、执行环境及绑定项目/目录保持不变。

## 验证

命令：固定 Python 加 -B 执行 daily/test_individual.py。

结果：19 项隔离测试全部通过，包含完整命令行队列推进模拟、失败后继续、每 SHA 独立计划、收集后新增用例进入冻结计划、逻辑通过但缺 UI 不验收、未准备不能执行/收尾、冻结后拒绝重算/断言变动、需求读取失败脱敏、旧计划保留及跨提交不共享结果。

另对实际 registry 的 wi3688-disk-full、wi3689-exporter-partial 做只读工程/限定 filter/本地源码指纹检查，成功。git diff --check 无空白错误（原有 Build-DailyVega.ps1 有 LF/CRLF 提示，未改该文件）。

这些是编排工具测试，不是 Vega 产品验收，不产生产品 Runner runId 或 UI 通过结论。本次未执行真实每日批次、未启动 Vega/Authenticator、未推进游标、未修改历史批次、未写 PR/工作项、未改被测产品源码。原有工作区变更保留。

## 保留的边界

无法保证任意新业务需求都能在准备预算内自动生成可执行测试。缺原始数据、预期、合法授权或环境时仍应真实报告未完整验收；不能通过自动追加编号消除 NEEDS_CASE。真实 Simulator UI 与官方授权/清理/恢复纪律保持不变。旧冻结批次不自动迁移或重放。
