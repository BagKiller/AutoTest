# 工作项 #0 验证报告

- 结论：验证工具异常（ERROR / CONTRACT_VIOLATION）
- 说明：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 2 个用例异常（非断言失败）
- runId：`20261023T080847Z_wi0_81c817e`
- 用例：wi3677-logic
- 断言：19 过 21（失败 2，跳过 0）
- 被测：commit `10c33df5`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效），工作区有未提交改动
- 起止：2026-10-23T16:08:47+08:00 → 2026-10-23T16:08:57+08:00（9.7 秒）

## 失败用例

- `Vega.AutoTest.IndividualCommits.WorkItem3677Logic.NewCollectionGetsRecipeThroughputPitch(incidence: Normal, expected: 18)`（Failed）：System.InvalidOperationException : Failed to resolve service of type 'Galaxy.Platform.IAccessControl.IAccessControlService' from the Galaxy container. Ensure it is registered in ComponentConfig.json.
- `Vega.AutoTest.IndividualCommits.WorkItem3677Logic.NewCollectionGetsRecipeThroughputPitch(incidence: Oblique, expected: 36)`（Failed）：System.InvalidOperationException : Failed to resolve service of type 'Galaxy.Platform.IAccessControl.IAccessControlService' from the Galaxy container. Ensure it is registered in ComponentConfig.json.

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | wi3677.json-pitch.{"pitch_size":20} | wi3677.json-pitch.{"pitch_size":20} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":"19.5"} | wi3677.json-pitch.{"pitch_size":"19.5"} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":"Infinity"} | wi3677.json-pitch.{"pitch_size":"Infinity"} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":20,"pitchSize":22} | wi3677.json-pitch.{"pitch_size":20,"pitchSize":22} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":1.79} | wi3677.json-pitch.{"pitch_size":1.79} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":180} | wi3677.json-pitch.{"pitch_size":180} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":"bad"} | wi3677.json-pitch.{"pitch_size":"bad"} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":0} | wi3677.json-pitch.{"pitch_size":0} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitchSize":21} | wi3677.json-pitch.{"pitchSize":21} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":180.01} | wi3677.json-pitch.{"pitch_size":180.01} | specified behavior | matched |
| PASS | wi3677.json-pitch.{} | wi3677.json-pitch.{} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":null} | wi3677.json-pitch.{"pitch_size":null} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":null,"pitchSize":22} | wi3677.json-pitch.{"pitch_size":null,"pitchSize":22} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":"NaN"} | wi3677.json-pitch.{"pitch_size":"NaN"} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":-1} | wi3677.json-pitch.{"pitch_size":-1} | specified behavior | matched |
| PASS | wi3677.json-pitch.{"pitch_size":1.8} | wi3677.json-pitch.{"pitch_size":1.8} | specified behavior | matched |
| FAIL | wi3677.populate-recipe.Oblique | wi3677.populate-recipe.Oblique | specified behavior | System.InvalidOperationException: Failed to resolve service of type 'Galaxy.Platform.IAccessControl.IAccessControlService' from the Galaxy container. Ensure it is registered in ComponentConfig.json. |
| FAIL | wi3677.populate-recipe.Normal | wi3677.populate-recipe.Normal | specified behavior | System.InvalidOperationException: Failed to resolve service of type 'Galaxy.Platform.IAccessControl.IAccessControlService' from the Galaxy container. Ensure it is registered in ComponentConfig.json. |
| PASS | wi3677.invariant-culture | wi3677.invariant-culture | specified behavior | matched |
| PASS | wi3677.original-json.Wafer_1_202610215854_Oblique_20261021215958.json | wi3677.original-json.Wafer_1_202610215854_Oblique_20261021215958.json | specified behavior | matched |
| PASS | wi3677.original-json.Carr003-01_261021194832_Normal_20261021195012.json | wi3677.original-json.Carr003-01_261021194832_Normal_20261021195012.json | specified behavior | matched |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\analysis\individual-20261022\wi3677-logic\20261023T080847Z_wi0_81c817e`

