# 工作项 #0 验证报告

- 结论：无法验证（BLOCKED / ZERO_ASSERTIONS_MATCHED）
- 说明：ZERO_ASSERTIONS_MATCHED: 仅命中 2/6 条断言，未真正到达目标界面
- runId：`20261023T101032Z_wi0_81db622`
- 用例：wi3677-ui-evidence
- 断言：2 过 6（失败 0，跳过 4）
- 被测：commit `10c33df5`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效），工作区有未提交改动
- 二进制构建时间：2026-10-23T17:27:03+08:00
- 起止：2026-10-23T18:10:31+08:00 → 2026-10-23T18:11:21+08:00（50.5 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | wi3677.ui.loggedIn | loggedIn | Recorded actual desktop screenshot with matching hash | True |
| PASS | wi3677.ui.recipeImported | recipeImported | Recorded actual desktop screenshot with matching hash | True |
| SKIP | wi3677.ui.jobCreated | jobCreated | After successful launch, login and original recipe import, switching to Job caused get_window_state timeout. list_windows timed out, repeated after 2 seconds, then reset JavaScript kernel and reimported @oai/sky; final list_windows still timed out. Stopped UI input per Computer Use recovery guidance. Job creation, original JSON configuration and postprocess/output verification not completed. | (skipped) |
| SKIP | wi3677.ui.bothJsonConfigured | bothJsonConfigured | After successful launch, login and original recipe import, switching to Job caused get_window_state timeout. list_windows timed out, repeated after 2 seconds, then reset JavaScript kernel and reimported @oai/sky; final list_windows still timed out. Stopped UI input per Computer Use recovery guidance. Job creation, original JSON configuration and postprocess/output verification not completed. | (skipped) |
| SKIP | wi3677.ui.postprocessCompleted | postprocessCompleted | After successful launch, login and original recipe import, switching to Job caused get_window_state timeout. list_windows timed out, repeated after 2 seconds, then reset JavaScript kernel and reimported @oai/sky; final list_windows still timed out. Stopped UI input per Computer Use recovery guidance. Job creation, original JSON configuration and postprocess/output verification not completed. | (skipped) |
| SKIP | wi3677.ui.produced-pitch | Actual produced waferRun collection pitches | No completed UI-produced waferRun metadata | (skipped) |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\analysis\individual-20261022\ui3677\retry-20261023T175709\runner\20261023T101032Z_wi0_81db622`

