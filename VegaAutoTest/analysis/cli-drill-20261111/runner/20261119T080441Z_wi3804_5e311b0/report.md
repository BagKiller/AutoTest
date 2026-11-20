# 工作项 #3804 验证报告

- 结论：无法验证（BLOCKED / UI_NOT_READY）
- 说明：UI_NOT_READY: Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] Job 树里没有 Lot 节点。
- runId：`20261119T080441Z_wi3804_5e311b0`
- 用例：wi3804-lot-recipe-combo
- 断言：0 过 0（失败 0，跳过 0）
- 被测：commit `58606ec9`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T16:00:47+08:00
- 起止：2026-11-19T16:04:39+08:00 → 2026-11-19T16:06:12+08:00（92.8 秒）

## 失败用例

- `Vega.AutoTest.L2.WorkItem3804L2.OpenLotAndLocateRecipeCombo`（Failed）：Vega.AutoTest.Harness.UiPrerequisiteException : [VEGA_UI_PRECONDITION:UI_NOT_READY] Job 树里没有 Lot 节点。

## 断言

无结构化断言。

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\cli-drill-20261111\runner\20261119T080441Z_wi3804_5e311b0`

