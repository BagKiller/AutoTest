# Cursor CLI 入口试跑 — 2026-11-11 提交窗口

这是 **CLI 入口试跑**，不是北京时间 23:30 的正式夜间批次。今天是 2026-11-19，日期窗口不能新建 2026-11-11 的 daily 批次，因此未调用 `model_daily.py --begin/--finish`，未推进 `daily/state.json`，未占用 `workspaces/daily-latest`，也未改写任何已有 nightly/manual 批次的 `result.json`。

正式界面结论只取本目录 Runner 的 `result.json`。`testingPolicy=latest-head`，`perCommitAcceptance=false`。

## 输入和构建

- 固定窗口：北京时间 2026-11-11 当天 master 上全部 3 条提交；日终 HEAD `58606ec9c84080feb715145c8100262097b60ef0`。
- 绑定单位：`58606ec9`+#3779，`23a12f31`+#3802，`fe6dcc62`+#3803，`fe6dcc62`+#3804。无锁文件-only、无无工单跳过。
- 工单只读拉取：`preparation/workitems.json`（retrievedAt 2026-11-19T15:56:16+08:00）。四份工单 ReproSteps 字段均为空；材料来自标题/描述/验收文字与完整 diff。
- 独立工作树：`C:\AutoTest\workspaces\manual-20261111`，检出上述 HEAD。
- 构建一次：`attempts/build-20261119/build-provenance.json`。Vega.dll SHA256 `24D3BA3B85B2E013704842D2FC7B1763888FD51B2CC576B62956A8B79AE79800`。Runner 未自行重编（`builtFromCurrentSource=false`），指纹与本轮构建一致。

## 逐项与复现步骤 UI 验证

### 58606ec9c84080feb715145c8100262097b60ef0 ＋ #3779（修订9）— PASSED_AT_HEAD（仅基线项）

需求：Vega100 加装 Edge Detector 后边缘对位数据源可切 DarkField/EdgeDetector。验收含 V100 基线无 DAQDevice_2、ED Inline/Dedicated、回退暗场、V200 回归、装机向导、单测。

本轮只在 **Vega100-PIC Simulator** 核对验收第 1 条基线：诊断 ComponentManager 无 `DAQDevice_2`。用例 `wi3779-v100-no-daq2`。runId `20261119T081609Z_wi3779_4ae21ad`，verdict **PASS / OK**，1/1。实际机型行 `Vega100 (EFEM, OuterToInner, LaserPIC)`，组件列表未见 DAQDevice_2。

未验：V100+ED Inline/Dedicated/回退暗场（需 -ED profile；真实对位成功依赖 Edge Detector 信号，属 EXTERNAL_HARDWARE）、装机向导（需安装环境）、V200 回归。单测未在本轮 Runner 重跑。

会话 `daily/sessions/20261119T161531_Vega100-PIC_9effb2` 已 RESTORED，三份配置哈希一致。

### 23a12f319c94a6e223fa6d0fffb5ca2222c99a9d ＋ #3802（修订7）— PASSED_AT_HEAD

需求：按膜系独立 BPS OD 表（LaserBpsODFilmConfig），诊断侧可管理膜系表。

在 Vega500 Simulator 打开诊断→设备诊断→BPS。用例 `wi3802-bps-film-ui`。正式结论 runId `20261119T081803Z_wi3802_1e89fbf`，verdict **PASS / OK**，1/1。页面可见「膜系表」「58 条规则」及新建/重命名/删除。

先前尝试：展开失败、一次误点到 ComponentManager 的假阳性已作废，以本 runId 为准。未做完整膜系标定/扫片读数闭环。

### fe6dcc62d7c88d1e95866b1693db0006609592ed ＋ #3803（修订20）— PASSED_AT_HEAD（仅慢门控件可见性）

需求：每个 collection 扫描前并行检查慢门，未开则重开。

在 Vega500 诊断「光学」页确认 Simulator 暴露「打开/关闭激光慢快门」。用例 `wi3803-optics-shutter`。runId `20261119T081849Z_wi3803_65c449a`，verdict **PASS / OK**，1/1。

未验验收全文：手动关慢门后跑 wafer、核对「未开→重开」日志、重开失败 GEM 报警、多 collection 逐次检查。这些需要完整 wafer 运行与日志断言，本试跑未执行。

### fe6dcc62d7c88d1e95866b1693db0006609592ed ＋ #3804（修订19）— FAIL（过滤断言）/ 配额用尽

需求：Lot 页 Recipe 下拉支持输入过滤；清空恢复全量；选中保存与失焦回显。

实际操作与 Runner：

1. `20261119T080441Z_wi3804_5e311b0` BLOCKED：Job 子节点不叫 “Lot”，定位失败。
2. `20261119T080704Z_wi3804_1db2bf4` PASS：打开 `WI3689_UI_20261026`，定位到可编辑 `LotView_AvailableRecipesComboBox`。
3. `20261119T080852Z_wi3804_3e71be7` **FAIL**：输入 `Default Recipe` 后列表变为 `Default Recipe | JRH_EPI_Particle_dual`（已选中项按产品设计恒保留）；清空后恢复 5 项（filter-clear PASS）。filter-match 因当时断言要求“全部含关键词”而失败。
4. `20261119T081022Z_wi3804_fca7bc2` BLOCKED：`LOGIN_NOT_READY`（认证器无 Gen2 选项）。配额用尽，未再跑。

正式过滤结论取 FAIL runId `20261119T080852Z_wi3804_3e71be7`。视觉观察：选中项钉住与提交说明一致，清空恢复已在同次 Runner 中 PASS；保存回显与键盘导航未验。

## 会话恢复与停止

| SessionPath | 机型 | 状态 |
| --- | --- | --- |
| `daily/sessions/20261119T144753_Vega500_40e7fe` | 历史卡住 PREPARING（他批） | 本试跑开始前已 Restore 成功 |
| `daily/sessions/20261119T160334_Vega500_427db7` | Vega500 | RESTORED |
| `daily/sessions/20261119T161148_Vega500_b4f15c` | Vega500 | RESTORED |
| `daily/sessions/20261119T161531_Vega100-PIC_9effb2` | Vega100-PIC | RESTORED |
| `daily/sessions/20261119T161716_Vega500_3fb124` | Vega500 | RESTORED |

各次 Restore exit=0，`session.json` 为 RESTORED；ComponentConfig / HardwareProfiles / MachineConfiguration SHA256 与准备前一致（见 `restore-hashes-*.json`）。

## 统计与未验范围

- 提交 3；绑定单位 4；正式 Runner 结果已落盘；跳过 0。
- 单位结论：#3779 基线 PASS；#3802 PASS；#3803 控件可见 PASS（wafer 闭环未验）；#3804 过滤正式 FAIL。
- C: 剩余约 32.2 GB（高于 30 GB 阈值）。
- 未 push、未建 PR、未改工作项、未改产品源码、未改 `nightly-prompt.txt`、未删除计划任务 `VegaAutoTest-NightlyAgent`。
