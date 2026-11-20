# Vega 提交验证 — 2026-11-16（按「有复现步骤就必须复现」重验）

当天 master 上 5 条提交，固定在日终提交 `ab2ced6a19a731912257401588f48a07d23de55c` 上验证。这是历史回放，不是今天的 latest-head 批次。没有推进 `daily/state.json`（仍停在 `bcbc53a5`，2026-11-17 23:30），没有占用 `daily-latest`。正式界面结论只取本目录 Runner 的 `result.json`。

## 构建

独立工作树 `C:\AutoTest\workspaces\manual-20261116`，HEAD `ab2ced6a`。Vega DLL SHA-256 `45E41E4B88C1AA0B5003A3A73471C82D1B79F0573A4F712F2DF6602CCFC86DDA`，构建时间 2026-11-19 11:10。`packages.lock.json` 未被改写。界面使用 Vega500 / Simulator / 官方认证器。

## f9d5e582 + #3774（PASS）

工单有【复现流程】。前置是打开已启用通道和 KLARF 的配方，Simulator 上做得到。

在本构建上实跑用例 `wi3774-spa-klarf-ui`。runId `20261119T031122Z_wi3774_eb50262`，verdict PASS / OK，5/5。实际操作：登录后打开 Default Recipe，KLARF「选择数据」里先有 3/35 项手工勾选；在同一通道插入已启用的空间分析（通道 DW2 Default）；再打开 KLARF。新增的 Cluster / Scratch / Slipline 及其 Area、X、Y、Length 全部未勾选，原有勾选保持（DW2 Default 的 LPD Size、X、Y 仍为勾选）。

截图：`runner/20261119T031122Z_wi3774_eb50262/uia/ui-snapshot.png`。画面停在配方编辑器的 KLARF「选择数据」，Defects 下 Cluster、Scratch、Slipline 已展开。

会话 `daily/sessions/20261119T111057_Vega500_647a6e` 已 RESTORED，ComponentConfig、HardwareProfiles、MachineConfiguration 哈希与准备前一致。

## 36d77af9 + #3796（PASS）

工单有【复现流程】：登录后导入附件 WaferRun，查看直方图。附件已重新下载，SHA-256 `4d004f82732e818aea176a7bc42c789c9da85311dd91e70203ca7bf3e2d49b29`，与用例记录一致。

用例 `wi3796-histogram-ui`。runId `20261119T031451Z_wi3796_b7a9ef8`，verdict PASS / OK，4/4。实际操作：导入该加密 WaferRun，打开晶圆 `Wafer_1_202611094113` / `WorkSpaceOfflineJob_20261109134113242_Cycle1`，查看 `[LPD,Size] DNA Oblique`。直方图有可见柱，柱宽 9px、绘图区 91px、比例 0.099，落在约 1/8 宽。

截图：`runner/20261119T031451Z_wi3796_b7a9ef8/uia/wi3796-histogram-screen.png`。画面是该直方图，最左侧一个 bin 有柱，其余 bin 为空。

同一次提交的直方图单测此前已在本目录记过：`product-tests/histogram.trx` 20 通过、0 失败。那组结果不能代替上面的界面结论。

会话 `daily/sessions/20261119T111436_Vega500_3e838f` 已 RESTORED，三份配置哈希一致。

## 4a3df516（NOT_APPLICABLE）

富创德 EFEM REV 0.3 位表、STATE 分项和 GEM 报警。提交说明里没有工作项号，也没有复现步骤。按无工单规则跳过，没有编一套 EFEM 界面。

## aa92638d + #3815

表面质量按膜系分枝。工单复现步骤字段为空，正文里没有【复现步骤】或【复现流程】，新规则不要求启动界面。验收标准里的膜查表 OD/曝光和正入射停靠时序写明要真机，这台机器没有这项前置。

先前记在本目录的产品单测没有重跑：`product-tests/sq-data.trx` 104 通过，`product-tests/sq-bl.trx` 61 通过，与提交说明里的数目一致。诊断页整套界面和那两项真机没有做。

## ab2ced6a + #3820（跳过）

工单有【复现流程】，同时有【前置条件】：机台已经有 12 寸 XY 校准结果，校准页切到 12 寸时各项有校准日期。

查了本机 Simulator 数据 `ResourcesDaily_Vega500\CalibrationDataResult\XYCalibrationData.json`。12 寸（WS300）有 16 行，校准日期全部是 `0001-01-01`，没有 2000 年以后的日期。这份前置不满足，因此没有做 8 寸 Setup → Start（Cal+Verify），也没有做第二次校准、放弃、导出、导入。

## 未验范围

- #3815 的诊断页界面，以及膜查表 OD/曝光、正入射停靠时序这两项真机。
- #3820 的两次校准、放弃后导出与界面是否一致。缺的是已有 12 寸校准日期，不是登录或打开校准页。
- `4a3df516` 的 EFEM 界面。
