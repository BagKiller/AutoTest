# 当前记忆（2026-11-19/20 夜间更新）

- 夜间入口是本机计划任务 `VegaAutoTest-NightlyAgent`（每天 23:30，仅在用户已登录时运行）。它调用 `daily/Start-NightlyAgent.ps1`，由 Cursor CLI 读取 `daily/nightly-prompt.txt` 后按 `model-interactive-runbook.md` 执行。不要删除这条任务。
- Windows 任务 `VegaAutoTest-DailyValidation` 已注销。`Install-DailyValidationTask.ps1` 和 `Start-DailyValidation.ps1` 已删除。不要重建那条旧任务。
- 2026-11-17 之前的每日批次、会话、分析记录和监督启动记录已删除。不要从归档把它们恢复成夜间输入。
- 仍保留的记录只有本轮 11 月 13 日手动回放：`daily/runs/manual-20261113T1725`。

## 2026-11-19 批次（T=2026-11-19；跨午夜至 2026-11-20 00:44 收尾）

- 根目录：`daily/runs/20261119T233127+0800_8da069`；HEAD `1ef7faeb9238572fa27daa4955310d14ab02b52c`；testingPolicy=latest-head，perCommitAcceptance=false。
- Vega DLL SHA-256 `41889D656508CD9C3DA0C9164A5926277AB7C04263DA2E83EE25D56B4AEB3FFB`。
- 3 绑定单位全部 **PASSED_AT_HEAD**；acceptanceState=PASSED_AT_HEAD；completion FINISHED。
  - #3829 / `1ef7faeb`：runId `20261119T164317Z_wi3829_f3c326b`（保存重开后 Category 仍展开）
  - #3764 / `875e4722`：runId `20261119T164001Z_wi3764_60a06a6`（DF M53 页 Unif 勾选 + 再分析按钮）
  - #3659 / `4e6baa74`：runId `20261119T163823Z_wi3659_e165c1e`（BFP 模式序列 + BFP+AC(Z=0) 下拉；4/4 配额）
- 7 个 Prepare 会话全部 RESTORED，配置 SHA256 核对一致。
- C: 剩余约 32.2 GiB（收尾时），高于 30GB。

## 可复用的界面事实（本晚确认）

- 校准树叶子（如「光束聚焦轮廓」、DF「M53」）必须 **DoubleClick** 才会 NavigateTo 文档页；单选/单击停在校准队列。
- BFP「设置校准队列」必须找 **ControlType=Button** 再 Invoke；同名 Text 点了不会开 `XBFPCalibSetupWindow`。
- 等待设置窗期间不要对主窗子窗盲目 `HandleAll` 误伤；设置窗可用主窗子窗或桌面顶层 + AutomationId `XBFPCalibSetupWindow` 查找。
- Vega500 暗场归一化子节点是 **M53**（不是 KTStyle 显示名），页面仍是 KTStyleViewModel。
- Category 展开：`CategoryItemListView_ExpandCollapseCategoryItemsCommandToggleButton`；Default Recipe 上可出现多个。

## 2026-11-17 批次（摘要，详见该批 report）

- 根目录：`daily/runs/20261117T233023+0800_2b9698`；#3822 最终 BLOCKED（AutoOptics 按钮需先切检测区）；4/4 配额用尽。
- 脚本用的 PowerShell 7：`C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe`。
