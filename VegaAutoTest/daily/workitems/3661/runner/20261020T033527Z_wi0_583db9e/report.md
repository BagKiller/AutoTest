# 工作项 #0 验证报告

- 结论：验证工具异常（ERROR / CONTRACT_VIOLATION）
- 说明：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 5 个用例异常（非断言失败）
- runId：`20261020T033527Z_wi0_583db9e`
- 用例：wi3661-acceptance
- 断言：12 过 21（失败 9，跳过 0）
- 被测：commit `985a08aa`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效）
- 二进制构建时间：2026-10-20T10:16:19+08:00
- 起止：2026-10-20T11:35:27+08:00 → 2026-10-20T11:35:49+08:00（22.2 秒）

## 失败用例

- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.HardwareParameters_CompiledView_FilterAndSaveBareKeys`（Failed）：System.Windows.Markup.XamlParseException : 'Provide value on 'System.Windows.StaticResourceExtension' threw an exception.' Line number '38' and line position '41'.
- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.ResultSummary_CompiledView_RendersLabelsWithoutChangingKeys`（Failed）：Assert.Equal() Failure: Values differ
- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.BatchEditor_CompiledWindow_FilterSaveAndJsonRoundTrip`（Failed）：Assert.Equal() Failure: Values differ
- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.Calculator_CompiledView_FilterAndNoWriteBack`（Failed）：Assert.Equal() Failure: Values differ
- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.Lpd_PirSir_CompiledView_FilterEditSaveAndHiddenData`（Failed）：Assert.Equal() Failure: Values differ

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| FAIL | wi3661.summary.render.DW2 Mask | summary.render.DW2 Mask | "DW Mask" | "" |
| FAIL | wi3661.summary | summary | scenario completes | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ |
| FAIL | wi3661.hardware | hardware | scenario completes | System.Windows.Markup.XamlParseException: 'Provide value on 'System.Windows.StaticResourceExtension' threw an exception.' Line number '38' and line position '41'. |
| PASS | wi3661.calculator.bare-DW2 | calculator.bare-DW2 | true | true |
| PASS | wi3661.calculator.DW1-filter | calculator.DW1-filter | false | false |
| PASS | wi3661.calculator.DN | calculator.DN | true | true |
| FAIL | wi3661.calculator.render | calculator.render | "DW" | "" |
| FAIL | wi3661.calculator | calculator | scenario completes | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ |
| PASS | wi3661.lpd.pir.bare-DW2 | lpd.pir.bare-DW2 | true | true |
| PASS | wi3661.lpd.pir.DW1-filter | lpd.pir.DW1-filter | false | false |
| PASS | wi3661.lpd.pir.DN | lpd.pir.DN | true | true |
| PASS | wi3661.lpd.sir.bare-DW2 | lpd.sir.bare-DW2 | true | true |
| PASS | wi3661.lpd.sir.DW1-filter | lpd.sir.DW1-filter | false | false |
| PASS | wi3661.lpd.sir.DN | lpd.sir.DN | true | true |
| FAIL | wi3661.lpd.pir.render | lpd.pir.render | "DW" | "" |
| FAIL | wi3661.lpd | lpd | scenario completes | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ |
| PASS | wi3661.batch.hardware.bare-DW2 | batch.hardware.bare-DW2 | true | true |
| PASS | wi3661.batch.hardware.DW1-filter | batch.hardware.DW1-filter | false | false |
| PASS | wi3661.batch.hardware.DN | batch.hardware.DN | true | true |
| FAIL | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | false |
| FAIL | wi3661.batch | batch | scenario completes | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\workitems\3661\runner\20261020T033527Z_wi0_583db9e`

