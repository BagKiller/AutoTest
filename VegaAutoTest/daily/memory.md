# 当前记忆（2026-11-22 夜间更新）

- 夜间入口是本机计划任务 `VegaAutoTest-NightlyAgent`（每天 23:30，仅在用户已登录时运行）。它调用 `daily/Start-NightlyAgent.ps1`，由 Cursor CLI 读取 `daily/nightly-prompt.txt` 后按 `model-interactive-runbook.md` 执行。不要删除这条任务。
- Windows 任务 `VegaAutoTest-DailyValidation` 已注销。`Install-DailyValidationTask.ps1` 和 `Start-DailyValidation.ps1` 已删除。不要重建那条旧任务。
- 2026-11-17 之前的每日批次、会话、分析记录和监督启动记录已删除。不要从归档把它们恢复成夜间输入。
- 仍保留的记录只有本轮 11 月 13 日手动回放：`daily/runs/manual-20261113T1725`。

## 2026-11-22 批次（T=2026-11-22）

- 根目录：`daily/runs/20261122T233113+0800_445116`；记录 HEAD 仍为 `ef5a6ca06ae37b7d8d00ff7d13c21ebcb484b5f7`（与 previousCollectedHead 相同）。
- testingPolicy=latest-head，perCommitAcceptance=false；acceptanceState=**NO_COMMITS**；completion FINISHED。
- 当日窗口内远端 master 0 提交；绑定单位 0；Runner 0；未构建、未开 Vega。
- C: 剩余约 25.2 GiB，低于 30GB。

## 2026-11-21 批次（摘要）

- 根目录：`daily/runs/20261121T233141+0800_3d17f3`；HEAD `ef5a6ca0…`；acceptanceState=NO_COMMITS；completion FINISHED。

## 2026-11-20 批次（摘要）

- 根目录：`daily/runs/20261120T233123+0800_5ead44`；HEAD `ef5a6ca0…`；#3807/#3827 PASSED_AT_HEAD；#3823 BLOCKED（ADO 404）；acceptanceState=INCOMPLETE。

## 可复用的界面事实（2026-11-20 确认，近两晚无新实测）

- SPA 滑移线参数网格：标签「包含（%）」「旋转角度（°）」；编辑时 TextBox AutomationId 分别为 `InclusionPercentage` / `RotationAngle`。
- 非法值就近红字形如「有效范围为 [0, 90)」或「有效范围为 (0, 100]」；保存时弹「请在保存之前确保没有错误。首条错误：…」。
- 右击「采集设置」标题行空白插入 SPA：文字右缘 +140（与 ui-004 一致），click≈(542,452)。
- `Dialogs.HandleAll` 会把「关闭应用程序 Vega?」当 Prompt 点确认；打开配方前应用 `HandleAllExcept` 并点否定按钮。
