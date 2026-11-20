目标日 2026-11-16：流程 FINISHED，completion-check exitCode=0；验收 HAS_FAIL。
testingPolicy=latest-head；perCommitAcceptance=false。原始提交 5；唯一执行目标 1。
正式 runnerRunCount=13；以下逐提交状态来自 summary.json，正式 verdict/verdictCode 来自 Runner。

|SHA|状态|用例与正式 runId|
|---|---|---|
|f9d5e5822d791560d02641fbb1df57bb46c37ad3|ERROR|authored-v7daec72f71-ab2ced6a19a7-wi3774-spa-klarf-selection / 20261117T041901Z_wi0_cccbbd2 / ERROR/HARNESS_EXCEPTION|
|36d77af9b71625370434bf9a56f824a61d03a871|ERROR|authored-va278267459-ab2ced6a19a7-wi3796-import-wafer-histogram / 20261117T042107Z_wi0_6a21e82 / ERROR/HARNESS_EXCEPTION|
|4a3df51621700e20fab37e1cbba09f8368d181ce|FAIL|authored-vc88bedbd33-ab2ced6a19a7-efem-alignment-input-routing / 20261117T041444Z_wi0_20baef8 / PASS/OK<br>authored-vc88bedbd33-ab2ced6a19a7-efem-configured-io-layout / 20261117T041501Z_wi0_44952fd / FAIL/ASSERTION_FAILED<br>authored-vc88bedbd33-ab2ced6a19a7-efem-state-display / 20261117T041520Z_wi0_9fb71c0 / FAIL/ASSERTION_FAILED<br>authored-vc88bedbd33-ab2ced6a19a7-efem-state-refresh-coordinator / 20261117T041537Z_wi0_b43cec3 / PASS/OK|
|aa92638d99937cedb6c3ee18dad42a454f79a542|NEEDS_CASE|authored-vaad53b0c0b-ab2ced6a19a7-sq-film-consumers / 20261117T041339Z_wi0_6b5e909 / PASS/OK<br>authored-vaad53b0c0b-ab2ced6a19a7-sq-film-execution / 20261117T041410Z_wi0_ff6c14f / PASS/OK<br>authored-vaad53b0c0b-ab2ced6a19a7-sq-film-service / 20261117T041427Z_wi0_6fba56d / PASS/OK|
|ab2ced6a19a731912257401588f48a07d23de55c|ERROR|authored-v978e81295b-ab2ced6a19a7-wi3820-xy-save-discard-export-import / 20261117T042002Z_wi0_24ce1f7 / ERROR/HARNESS_EXCEPTION|

全部正式 Runner（含基线；预检不重复统计）：
- 20261117T041339Z_wi0_6b5e909: PASS/OK；OK: 33/33 断言通过
- 20261117T041410Z_wi0_ff6c14f: PASS/OK；OK: 44/44 断言通过
- 20261117T041427Z_wi0_6fba56d: PASS/OK；OK: 32/32 断言通过
- 20261117T041444Z_wi0_20baef8: PASS/OK；OK: 107/107 断言通过
- 20261117T041501Z_wi0_44952fd: FAIL/ASSERTION_FAILED；ASSERTION_FAILED: 1 条断言失败
- 20261117T041520Z_wi0_9fb71c0: FAIL/ASSERTION_FAILED；ASSERTION_FAILED: 1 条断言失败
- 20261117T041537Z_wi0_b43cec3: PASS/OK；OK: 15/15 断言通过
- 20261117T041554Z_wi0_6dc632b: PASS/OK；OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过
- 20261117T041714Z_wi0_ee0bc62: PASS/OK；OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- 20261117T041809Z_wi0_654adfb: PASS/OK；OK: 17/17 断言通过 | OK: 1/1 断言通过
- 20261117T041901Z_wi0_cccbbd2: ERROR/HARNESS_EXCEPTION；HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- 20261117T042002Z_wi0_24ce1f7: ERROR/HARNESS_EXCEPTION；HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- 20261117T042107Z_wi0_6a21e82: ERROR/HARNESS_EXCEPTION；HARNESS_EXCEPTION: 1 个用例异常（非断言失败）

最终 case-check 预检（不是正式验收）：
- authored-v7daec72f71-ab2ced6a19a7-wi3774-spa-klarf-selection: discovery exit=0, runtimeState=REQUIRES_PREPARED_UI_SESSION, usable=None, trial=None, None/None
- authored-v978e81295b-ab2ced6a19a7-wi3820-xy-save-discard-export-import: discovery exit=0, runtimeState=REQUIRES_PREPARED_UI_SESSION, usable=None, trial=None, None/None
- authored-va278267459-ab2ced6a19a7-wi3796-import-wafer-histogram: discovery exit=0, runtimeState=REQUIRES_PREPARED_UI_SESSION, usable=None, trial=None, None/None
- authored-vaad53b0c0b-ab2ced6a19a7-sq-film-consumers: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041058Z_wi0_59dd631, PASS/OK
- authored-vaad53b0c0b-ab2ced6a19a7-sq-film-execution: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041132Z_wi0_f2b0c70, PASS/OK
- authored-vaad53b0c0b-ab2ced6a19a7-sq-film-migration: discovery exit=0, runtimeState=PREPARATION_FAILED, usable=False, trial=20261117T041148Z_wi0_bec2001, ERROR/HARNESS_EXCEPTION
- authored-vaad53b0c0b-ab2ced6a19a7-sq-film-service: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041205Z_wi0_2c064c2, PASS/OK
- authored-vc88bedbd33-ab2ced6a19a7-efem-alignment-input-routing: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041222Z_wi0_ce5b434, PASS/OK
- authored-vc88bedbd33-ab2ced6a19a7-efem-configured-io-layout: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041242Z_wi0_fa1d7e9, FAIL/ASSERTION_FAILED
- authored-vc88bedbd33-ab2ced6a19a7-efem-state-display: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041301Z_wi0_7e1ae69, FAIL/ASSERTION_FAILED
- authored-vc88bedbd33-ab2ced6a19a7-efem-state-refresh-coordinator: discovery exit=0, runtimeState=EXECUTED, usable=True, trial=20261117T041318Z_wi0_a4eb05d, PASS/OK

UI slot Vega500：3 个正式 UI 均 ERROR/HARNESS_EXCEPTION，未完成业务验收；登录/空闲就绪不等于业务通过。
会话 slots/ui-Vega500_20261117T121900/restore.json 输出明确 Original configuration restored and SHA256 verified，exitCode=0。
workspace-reclaim.json: previous-owner-superseded。
未验范围：summary.unverifiedUiSlots 全部保留；其他缺口及外部数据/真机不以基线 PASS 替代。
本报告不引用工单标题/详情；专项 3774 已另用 PAT 只读读取 revision 8。
证据根：C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261116T233003+0800_10a53e