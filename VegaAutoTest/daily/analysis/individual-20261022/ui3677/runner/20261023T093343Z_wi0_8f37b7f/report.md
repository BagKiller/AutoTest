# 工作项 #0 验证报告

- 结论：无法验证（BLOCKED / ZERO_ASSERTIONS_MATCHED）
- 说明：ZERO_ASSERTIONS_MATCHED: 仅命中 0/6 条断言，未真正到达目标界面
- runId：`20261023T093343Z_wi0_8f37b7f`
- 用例：wi3677-ui-evidence
- 断言：0 过 6（失败 0，跳过 6）
- 被测：commit `10c33df5`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效），工作区有未提交改动
- 二进制构建时间：2026-10-23T17:27:03+08:00
- 起止：2026-10-23T17:33:43+08:00 → 2026-10-23T17:34:02+08:00（18.5 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| SKIP | wi3677.ui.loggedIn | loggedIn | Computer Use launch_app timed out; list_windows also timed out; after resetting the JavaScript kernel, reimporting @oai/sky and retrying list_windows, the same timeout persisted. Required desktop operations cannot be performed with the current helper connection. No successful login/import/job/postprocess evidence is available. | (skipped) |
| SKIP | wi3677.ui.recipeImported | recipeImported | Computer Use launch_app timed out; list_windows also timed out; after resetting the JavaScript kernel, reimporting @oai/sky and retrying list_windows, the same timeout persisted. Required desktop operations cannot be performed with the current helper connection. No successful login/import/job/postprocess evidence is available. | (skipped) |
| SKIP | wi3677.ui.jobCreated | jobCreated | Computer Use launch_app timed out; list_windows also timed out; after resetting the JavaScript kernel, reimporting @oai/sky and retrying list_windows, the same timeout persisted. Required desktop operations cannot be performed with the current helper connection. No successful login/import/job/postprocess evidence is available. | (skipped) |
| SKIP | wi3677.ui.bothJsonConfigured | bothJsonConfigured | Computer Use launch_app timed out; list_windows also timed out; after resetting the JavaScript kernel, reimporting @oai/sky and retrying list_windows, the same timeout persisted. Required desktop operations cannot be performed with the current helper connection. No successful login/import/job/postprocess evidence is available. | (skipped) |
| SKIP | wi3677.ui.postprocessCompleted | postprocessCompleted | Computer Use launch_app timed out; list_windows also timed out; after resetting the JavaScript kernel, reimporting @oai/sky and retrying list_windows, the same timeout persisted. Required desktop operations cannot be performed with the current helper connection. No successful login/import/job/postprocess evidence is available. | (skipped) |
| SKIP | wi3677.ui.produced-pitch | Actual produced waferRun collection pitches | No completed UI-produced waferRun metadata | (skipped) |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\analysis\individual-20261022\ui3677\runner\20261023T093343Z_wi0_8f37b7f`

