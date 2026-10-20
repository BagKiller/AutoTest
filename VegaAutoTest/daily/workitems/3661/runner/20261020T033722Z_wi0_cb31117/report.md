# 工作项 #0 验证报告

- 结论：验证工具异常（ERROR / CONTRACT_VIOLATION）
- 说明：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 2 个用例异常（非断言失败）
- runId：`20261020T033722Z_wi0_cb31117`
- 用例：wi3661-acceptance
- 断言：33 过 36（失败 3，跳过 0）
- 被测：commit `985a08aa`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效）
- 二进制构建时间：2026-10-20T10:16:19+08:00
- 起止：2026-10-20T11:37:22+08:00 → 2026-10-20T11:37:40+08:00（18.2 秒）

## 失败用例

- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.Calculator_CompiledView_FilterAndNoWriteBack`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- `Vega.AutoTest.CommitProbe.WorkItem3661Logic.BatchEditor_CompiledWindow_FilterSaveAndJsonRoundTrip`（Failed）：Assert.Equal() Failure: Values differ

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | wi3661.summary.render.DW2 Mask | summary.render.DW2 Mask | "DW Mask" | "DW Mask" |
| PASS | wi3661.summary.render.DW2 Polarizer | summary.render.DW2 Polarizer | "DW Polarizer" | "DW Polarizer" |
| PASS | wi3661.summary.render.DW2 Ideal Gain | summary.render.DW2 Ideal Gain | "DW Ideal Gain" | "DW Ideal Gain" |
| PASS | wi3661.summary.render.DW2-0 | summary.render.DW2-0 | "DW-0" | "DW-0" |
| PASS | wi3661.summary.render.DN Mask | summary.render.DN Mask | "DN Mask" | "DN Mask" |
| PASS | wi3661.summary.bare-keys-unchanged | summary.bare-keys-unchanged | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" |
| PASS | wi3661.hardware.bare-DW2 | hardware.bare-DW2 | true | true |
| PASS | wi3661.hardware.DW1-filter | hardware.DW1-filter | false | false |
| PASS | wi3661.hardware.DN | hardware.DN | true | true |
| PASS | wi3661.hardware.render | hardware.render | "DW" | "DW" |
| PASS | wi3661.hardware.save-DW2 | hardware.save-DW2 | 0.123 | 0.123 |
| PASS | wi3661.hardware.hidden-preserved | hardware.hidden-preserved | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" |
| PASS | wi3661.hardware.no-display-key | hardware.no-display-key | false | false |
| PASS | wi3661.calculator.bare-DW2 | calculator.bare-DW2 | true | true |
| PASS | wi3661.calculator.DW1-filter | calculator.DW1-filter | false | false |
| PASS | wi3661.calculator.DN | calculator.DN | true | true |
| PASS | wi3661.calculator.render | calculator.render | "DW" | "DW" |
| FAIL | wi3661.calculator | calculator | scenario completes | System.NullReferenceException: Object reference not set to an instance of an object. |
| PASS | wi3661.lpd.pir.bare-DW2 | lpd.pir.bare-DW2 | true | true |
| PASS | wi3661.lpd.pir.DW1-filter | lpd.pir.DW1-filter | false | false |
| PASS | wi3661.lpd.pir.DN | lpd.pir.DN | true | true |
| PASS | wi3661.lpd.sir.bare-DW2 | lpd.sir.bare-DW2 | true | true |
| PASS | wi3661.lpd.sir.DW1-filter | lpd.sir.DW1-filter | false | false |
| PASS | wi3661.lpd.sir.DN | lpd.sir.DN | true | true |
| PASS | wi3661.lpd.pir.render | lpd.pir.render | "DW" | "DW" |
| PASS | wi3661.lpd.sir.render | lpd.sir.render | "DW" | "DW" |
| PASS | wi3661.lpd.save.pir-DW2 | lpd.save.pir-DW2 | 7.0 | 7.0 |
| PASS | wi3661.lpd.save.sir-DW2 | lpd.save.sir-DW2 | 8.0 | 8.0 |
| PASS | wi3661.lpd.hidden.pir-DW1 | lpd.hidden.pir-DW1 | 11.0 | 11.0 |
| PASS | wi3661.lpd.hidden.sir-DW1 | lpd.hidden.sir-DW1 | 12.0 | 12.0 |
| PASS | wi3661.lpd.no-display-key | lpd.no-display-key | false | false |
| PASS | wi3661.batch.hardware.bare-DW2 | batch.hardware.bare-DW2 | true | true |
| PASS | wi3661.batch.hardware.DW1-filter | batch.hardware.DW1-filter | false | false |
| PASS | wi3661.batch.hardware.DN | batch.hardware.DN | true | true |
| FAIL | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | false |
| FAIL | wi3661.batch | batch | scenario completes | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\workitems\3661\runner\20261020T033722Z_wi0_cb31117`

