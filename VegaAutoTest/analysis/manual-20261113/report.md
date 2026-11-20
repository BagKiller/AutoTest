# Vega 提交验证 — 2026-11-13（按「有复现步骤就必须复现」重验）

当天 master 上 3 条提交，固定在日终提交 `c6bedced2d0773dc612ace3df66ba98edb7f2b80` 上验证。这是历史回放，不是今天的 latest-head 批次。没有推进 `daily/state.json`，没有占用 `daily-latest`。正式界面结论只取本目录 Runner 的 `result.json`。先前 `daily/runs/manual-20261113T1725` 的记录没有改写。

## 构建

独立工作树 `C:\AutoTest\workspaces\manual-20261113`，HEAD `c6bedced`。Vega DLL SHA-256 `F5C915487AFA865CAD58CA15F2B691FCD626D33DE7E8D14B1D8BFCAC99484CBA`，构建时间 2026-11-19 13:19。`packages.lock.json` 未被改写。界面使用 Vega500 / Simulator / 官方认证器。

## 9c7f9d3a + #3811（PASS）

工单复现步骤字段是空的，正文里也没有【复现步骤】或【复现流程】。验收第 1、5 条写明了要核对的界面：打开存量配方后，KLARF 树里 BF 的 PPD 下出现 `PPD_Polarity`，并且可以勾选。这台机器做得到。

用例 `wi3811-klarf-ui`。runId `20261119T052034Z_wi3811_1e93d0d`，verdict PASS / OK，3/3。实际操作：登录后打开 Default Recipe，选中明场 DIC，打开发布后的 KLARF「选择数据」。树上出现 `BF Default / Defects / PPD / PPD_Polarity`，勾选可以切换并恢复。

截图在 `runner/20261119T052034Z_wi3811_1e93d0d/uia/`。

验收第 2 到第 4 条要算法 JSON 回传后的 KLARF 行内容（极性整数、-1/0/1 或默认 0、Extended 缺项回退、XSize/YSize/HazeAverage 自动成列）。工单没有附这份算法输出或晶圆，这三项没有界面结论。

会话 `daily/sessions/20261119T132016_Vega500_f0b921` 已 RESTORED，ComponentConfig、HardwareProfiles、MachineConfiguration 哈希与准备前一致。

## 1ba13204 + #3694（BLOCKED）

工单没有复现步骤，也没有验收标准字段。描述要求三件事：manual 分档不限制最小值；layout 从通道 minSize 所在档开始显示；Job Summary 里低于 minSize 的档为 NA。三张附图分别是通道最小值表、16 档 manual 分档编辑器，以及 25 片晶圆的 Wafer Summary（Bin1 到 Bin6 为 NA）。

已经启动本构建的 Vega。用例 `wi3694-binning-ui`，runId `20261119T052812Z_wi3694_b9a39d4`，verdict BLOCKED / UI_NOT_READY。实际看到的是：

- Default Recipe 的元素列表里没有分档，只有配方信息、采集、结果布局、结果输出。
- 作业页打开的是 Default Job 的任务摘要和导出设置，没有附图里的「Wafer Summary」，也没有「25 Wafer」和那些 NA 列。

工单没有给出可导入的配方或这 25 片作业，所以色柱起点和 Job Summary 的 NA 列对不上附图。这不是产品判定失败。

同一提交的视图模型测试在这个工作树上重跑：`product-tests/common.trx` 18 通过，`product-tests/display.trx` 4 通过，合计 22 通过、0 失败。这组测试不是 Runner 界面结论。

截图：`runner/20261119T052812Z_wi3694_b9a39d4/uia/step-1-recipe.png`、`step-3-job.png`。

会话 `daily/sessions/20261119T132513_Vega500_bc0855` 已 RESTORED，三份配置哈希一致。

## c6bedced（NOT_APPLICABLE）

只自动更新 `packages.lock.json`。没有工作项，按锁文件规则跳过。

## 未验范围

- #3811 的 KLARF 文件内容：极性按整数输出、未回传时为 0、简化 KLARF 未勾选时 Extended 的 SIZE/nmSHE/ADC 回退、XSize/YSize/HazeAverage 自动成列。
- #3694 附图中的 16 档色柱起点，以及 25 片 Job Summary 里 Bin1–Bin6 为 NA。本机没有这份作业。
