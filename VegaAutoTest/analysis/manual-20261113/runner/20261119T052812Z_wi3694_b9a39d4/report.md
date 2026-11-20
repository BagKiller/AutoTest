# 工作项 #3694 验证报告

- 结论：无法验证（BLOCKED / UI_NOT_READY）
- 说明：UI_NOT_READY: Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] 工单没有给出可导入的作业。当前配方：Default Recipe 元素列表没有分档。作业页没有附图中的 25 片 Wafer Summary。作业页文字：浏览 | 操作 | 配方 | 展示管理 | 工具 | 帮助 | 任务 | Default Job | 任务摘要 | 展示 | 导出设置 | 导出 | 使用任务名 | 使用任务名和数字后缀 | 使用文件名 | 使用文件名和数字后缀 | 文件名 | 文件路径 | ... | 循环选项 | 无 | 无限循环 | 循环 | 通过JobQueue执行时循环设置不生效，如需使用循环功能，请直接在当前Job上右键选择"开始" | LotLevel / WaferLevel ID设置 | 修改LotLevel 和 WaferLevel ID | [WaferEdgeTypeConfigService],Successfully loaded WaferEdgeType configuration | [HazeCaliResultHelper],No matching Key was found.Key-P-U-U.DN.Full.None.HighSensitivity | Offline | Local | 设备状态 | 空闲 | 任务状态 | 批次状态 | 检测台晶圆状态
- runId：`20261119T052812Z_wi3694_b9a39d4`
- 用例：wi3694-binning-ui
- 断言：0 过 0（失败 0，跳过 0）
- 被测：commit `c6bedced`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T13:19:07+08:00
- 起止：2026-11-19T13:28:12+08:00 → 2026-11-19T13:29:16+08:00（64.1 秒）

## 失败用例

- `Vega.AutoTest.L2.WorkItem3694L2.OpenBinningEditorAndLookForIllustratedSummary`（Failed）：Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] 工单没有给出可导入的作业。当前配方：Default Recipe 元素列表没有分档。作业页没有附图中的 25 片 Wafer Summary。作业页文字：浏览 | 操作 | 配方 | 展示管理 | 工具 | 帮助 | 任务 | Default Job | 任务摘要 | 展示 | 导出设置 | 导出 | 使用任务名 | 使用任务名和数字后缀 | 使用文件名 | 使用文件名和数字后缀 | 文件名 | 文件路径 | ... | 循环选项 | 无 | 无限循环 | 循环 | 通过JobQueue执行时循环设置不生效，如需使用循环功能，请直接在当前Job上右键选择"开始" | LotLevel / WaferLevel ID设置 | 修改LotLevel 和 WaferLevel ID | [WaferEdgeTypeConfigService],Successfully loaded WaferEdgeType configuration | [HazeCaliResultHelper],No matching Key was found.Key-P-U-U.DN.Full.None.HighSensitivity | Offline | Local | 设备状态 | 空闲 | 任务状态 | 批次状态 | 检测台晶圆状态

## 断言

无结构化断言。

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\manual-20261113\runner\20261119T052812Z_wi3694_b9a39d4`

