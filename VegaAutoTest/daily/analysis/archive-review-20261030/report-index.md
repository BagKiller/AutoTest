# 139份原报告索引

按原批次日期归属，含次日专项复验。父子报告不能重复计为独立测试。摘要为原文提取，分析解释见 analysis.md。

**001. [20261019T154712+0800_546a27/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T154712+0800_546a27/report.md)**

- 类型：原批次汇总
- 原报告状态：收集 10 个提交；远端刷新：True。总体验收：**INCOMPLETE**。
- 摘要：收集 10 个提交；远端刷新：True。总体验收：**INCOMPLETE**。
- SHA256：`24b302b4c50828b5c189100063ca5e96a12cc5571be915ecf87059346ead4d88`

**002. [20261019T154712+0800_546a27/runner/20261019T080054Z_wi0_f24ddec/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T154712+0800_546a27/runner/20261019T080054Z_wi0_f24ddec/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`642a85e611a220a5873b2f8b1607e6fdfe8735d6be9ea49256828f8852cb3a9c`
- 用例：daily-recent10
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.Vega300And500DefaultSlitMappingsMatchHardwareContract`（Failed）：System.Collections.Generic.KeyNotFoundException : The given key 'Slit' was not present in the dictionary.

**003. [20261019T154712+0800_546a27/runner/20261019T080326Z_wi0_58e3961/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T154712+0800_546a27/runner/20261019T080326Z_wi0_58e3961/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`63233031ff2a1a89b4a2390a8212b4bb6602aa85ba7b7df84b5fb81ce284808b`
- 用例：daily-recent10

**004. [20261019T233014+0800_ddc509/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/report.md)**

- 类型：原批次汇总
- 原报告状态：见报告正文
- 摘要：**结论：已补跑核心与补充逻辑测试，并实际启动三机型/四配置完成下表中的 Simulator 界面检查。总体验收仍为 INCOMPLETE；尚未覆盖的完整检测链、校准前置和真机项目不计为通过。**
- SHA256：`45bbc85f161c95608c8019ef5fba5d2fd53363af5ec2e0009797038a378998b0`

**005. [20261019T233014+0800_ddc509/runner/20261020T021703Z_wi0_206afe8/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/runner/20261020T021703Z_wi0_206afe8/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`9b0e5dadd7aff3be5fafd66e1981b3dce587c2d7741cbbe934f040b146499541`
- 用例：daily-recent10

**006. [20261019T233014+0800_ddc509/supplement-runner/20261020T023140Z_wi0_050c909/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T023140Z_wi0_050c909/report.md)**

- 类型：Runner
- 原报告状态：ERROR / TEST_HOST_FAILED
- 摘要：TEST_HOST_FAILED: 测试工程未能启动（编译失败或宿主退出且没有 trx）。
- SHA256：`bd5b63325f5e0afb7006127f255ae7047de012682f26695e7c6a2848c2249747`
- 用例：daily-supplement

**007. [20261019T233014+0800_ddc509/supplement-runner/20261020T023306Z_wi0_42bb2d6/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T023306Z_wi0_42bb2d6/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`ef59937aafbbb7025127c6d009dd46eb181d4fbf33a756809c1971b16b836949`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.Binning_OriginalProductBehaviorSuite`（Failed）：System.Reflection.TargetParameterCountException : Parameter count mismatch.
- 失败证据：FAIL | binning.LoadDataAndSelectStatistic_MultipleCategories_ShowsEachUnbinnedSummary.1 | binning.LoadDataAndSelectStatistic_MultipleCategories_ShowsEachUnbinnedSummary.1 | Original behavior assertions hold | System.Reflection.TargetParameterCountException: Parameter count mismatch.

**008. [20261019T233014+0800_ddc509/supplement-runner/20261020T023421Z_wi0_f5670ca/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T023421Z_wi0_f5670ca/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`e1cc948b85d6f8bdaea7365dc3e20af65bb9a82653bbd948fbe791bce845c167`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.Binning_OriginalProductBehaviorSuite`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：FAIL | binning.LoadDataAndSelectStatistic_MultipleCategories_ShowsEachUnbinnedSummary.1 | binning.LoadDataAndSelectStatistic_MultipleCategories_ShowsEachUnbinnedSummary.1 | Original behavior assertions hold | System.NullReferenceException: Object reference not set to an instance of an object.

**009. [20261019T233014+0800_ddc509/supplement-runner/20261020T023517Z_wi0_a8011a1/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T023517Z_wi0_a8011a1/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 9/9 断言通过
- SHA256：`f820a77a7d5991706accfc7cd110503482ab38aaf8a18e297c788ceca6d98ec2`
- 用例：daily-supplement

**010. [20261019T233014+0800_ddc509/supplement-runner/20261020T024127Z_wi0_7b65017/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T024127Z_wi0_7b65017/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 5 个用例异常（非断言失败）
- SHA256：`cb8b1b8ea22a2e533e02a08a99f41cb5d31cea47d950f45f5f6a970b4a90153d`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 3, cancelled: False, expected: 3)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 0, cancelled: True, expected: 0)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.ReturnedBytes_CountAndConcatenation`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.StopAcquisition_FencesWaitingReader_WithoutHardware`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.0.True | delivery.empty.0.True | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.3.False | delivery.empty.3.False | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.bytes | delivery.bytes | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | run-fence.reader-exits | run-fence.reader-exits | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.

**011. [20261019T233014+0800_ddc509/supplement-runner/20261020T024253Z_wi0_c93fd29/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T024253Z_wi0_c93fd29/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 5 个用例异常（非断言失败）
- SHA256：`9fd74f81c3e3dc09ce6abe064d38734951d4e880b3d0e8ed348d74d40647461e`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 0, cancelled: True, expected: 0)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.ReturnedBytes_CountAndConcatenation`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.StopAcquisition_FencesWaitingReader_WithoutHardware`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 3, cancelled: False, expected: 3)`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.0.True | delivery.empty.0.True | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.empty.3.False | delivery.empty.3.False | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | delivery.bytes | delivery.bytes | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.
- 失败证据：FAIL | run-fence.reader-exits | run-fence.reader-exits | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.

**012. [20261019T233014+0800_ddc509/supplement-runner/20261020T024415Z_wi0_3267113/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T024415Z_wi0_3267113/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 15/15 断言通过
- SHA256：`075fc912700eed7871eb0c0045fb79283d6ef1642916d8466ee3f9ec23cf7f2f`
- 用例：daily-supplement

**013. [20261019T233014+0800_ddc509/supplement-runner/20261020T024641Z_wi0_a47568a/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T024641Z_wi0_a47568a/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过
- SHA256：`47c66ae394850080c1ff7dc16784f55c451632dd234082f9fa6fdf2e3594123c`
- 用例：daily-supplement

**014. [20261019T233014+0800_ddc509/supplement-runner/20261020T025607Z_wi0_59445a0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T025607Z_wi0_59445a0/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`35aad1edcc7542a8236d7249e2e7abee50481840cdc7cdabf85f944608e183a0`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.Vega100_LpdFilteringAndSave_PreservesBareAndHiddenKeys`（Failed）：System.InvalidOperationException : Failed to resolve service of type 'Galaxy.Platform.INavigations.INavigationService' from the Galaxy container. Ensure it is registered in ComponentConfig.json.
- 失败证据：FAIL | channels100.lpd-filter-save | channels100.lpd-filter-save | behavior holds | System.InvalidOperationException: Failed to resolve service of type 'Galaxy.Platform.INavigations.INavigationService' from the Galaxy container. Ensure it is registered in ComponentConfig.json.

**015. [20261019T233014+0800_ddc509/supplement-runner/20261020T025723Z_wi0_c4d92e2/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T025723Z_wi0_c4d92e2/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`0918c7bafe52b3379b09026d7a624bdcab662d9fcebc16b67c24b74e481ca887`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.Vega100_LpdFilteringAndSave_PreservesBareAndHiddenKeys`（Failed）：System.NullReferenceException : Object reference not set to an instance of an object.
- 失败证据：FAIL | channels100.lpd-filter-save | channels100.lpd-filter-save | behavior holds | System.NullReferenceException: Object reference not set to an instance of an object.

**016. [20261019T233014+0800_ddc509/supplement-runner/20261020T025803Z_wi0_5262d16/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T025803Z_wi0_5262d16/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`f6fc1b65267cb72a681f275b46e18bcad96804d2a336afbb3a27fdf0c1cbdeb5`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.Vega100_LpdFilteringAndSave_PreservesBareAndHiddenKeys`（Failed）：Assert.DoesNotContain() Failure: Filter matched in collection
- 失败证据：FAIL | channels100.lpd-filter-save | channels100.lpd-filter-save | behavior holds | Xunit.Sdk.DoesNotContainException: Assert.DoesNotContain() Failure: Filter matched in collection

**017. [20261019T233014+0800_ddc509/supplement-runner/20261020T025855Z_wi0_9ef2b0d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261019T233014+0800_ddc509/supplement-runner/20261020T025855Z_wi0_9ef2b0d/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过 | OK: 1/1 断言通过
- SHA256：`aafd03fbcdba4de1c2f6342218b626749a2691317b30b064ed157a0ed9a1f753`
- 用例：daily-supplement

**018. [20261020T163511+0800_98fa0a/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T163511+0800_98fa0a/report.md)**

- 类型：原批次汇总
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 9、工具异常 1。
- 摘要：**缺用例**：提交说明无工作项号，且无可映射用例；需人工/Agent 分析 diff 并补用例
- SHA256：`d1e3b95ad17386c78ef87982a479291925049af6c8f8cf5dce3a7dbc614ab932`

**019. [20261020T163511+0800_98fa0a/runner/20261020T084606Z_wi0_4f45e5d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T163511+0800_98fa0a/runner/20261020T084606Z_wi0_4f45e5d/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`8d7c60210e7f32acb5b2dc07878cfb61eea15e3156e0ec5dc4d4ac7f66df0de8`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ

**020. [20261020T163511+0800_98fa0a/runner/20261020T084736Z_wi0_6b88c50/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T163511+0800_98fa0a/runner/20261020T084736Z_wi0_6b88c50/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过
- SHA256：`581bc953706aebecc622bd89a9589ee7d793c578162cc4ca732c83689035d8b4`
- 用例：wi3661-acceptance

**021. [20261020T163511+0800_98fa0a/runner/20261020T084757Z_wi0_d250364/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T163511+0800_98fa0a/runner/20261020T084757Z_wi0_d250364/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`7a638b1c80a47ada61bc94899ac12eecc2318ef2b8a4ad891678e2f0f0475828`
- 用例：daily-recent10

**022. [20261020T163511+0800_98fa0a/runner/20261020T084815Z_wi0_51d4a7a/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T163511+0800_98fa0a/runner/20261020T084815Z_wi0_51d4a7a/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- SHA256：`f3657147db335939ee8a7f6f736a4a705b8c80ae1d4ad443dd0c41fcc2ba3885`
- 用例：wi3644-core

**023. [20261020T232633+0800_50ec37/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T232633+0800_50ec37/report.md)**

- 类型：原批次汇总
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 11、工具异常 1。
- 摘要：**缺用例**：工作项 #3667 未在 registry.json 登记用例
- SHA256：`229c84a888142043bd3b864c7591d9bb02f0df55e8ae16bd9b0e9c6f70308f33`

**024. [20261020T232633+0800_50ec37/runner/20261020T153245Z_wi0_9d2e43e/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T232633+0800_50ec37/runner/20261020T153245Z_wi0_9d2e43e/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`4bdcc8912382e6c0395ff0db87e3a4ecb83da67041730046317a346dae2ccabd`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ

**025. [20261020T232633+0800_50ec37/runner/20261020T153406Z_wi0_2b3ddc8/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T232633+0800_50ec37/runner/20261020T153406Z_wi0_2b3ddc8/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过
- SHA256：`e6bf604cfed3c2ab570b21eb0c87a55e285a71464c96fea07d82242ca010a2ad`
- 用例：wi3661-acceptance

**026. [20261020T232633+0800_50ec37/runner/20261020T153427Z_wi0_4a83222/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T232633+0800_50ec37/runner/20261020T153427Z_wi0_4a83222/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`10178c9d5da3a89a7c68096e3dfeb1ede6bf71aa0f9d0f396198e16f31b31e37`
- 用例：daily-recent10

**027. [20261020T232633+0800_50ec37/runner/20261020T153447Z_wi0_7c1cfd6/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261020T232633+0800_50ec37/runner/20261020T153447Z_wi0_7c1cfd6/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- SHA256：`19bc4bc066cd558dede567e7dbd27d8b27b3a518e3e77ec499f917d6d2155cce`
- 用例：wi3644-core

**028. [20261021T232851+0800_10e3a0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261021T232851+0800_10e3a0/report.md)**

- 类型：原批次汇总
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 12、工具异常 1。
- 摘要：**缺用例**：工作项 #3651 未在 registry.json 登记用例
- SHA256：`115769b5425b5ed40178d981bc4969a5fcf5beaa27137d7e0002f6c19b1be1f9`

**029. [20261021T232851+0800_10e3a0/runner/20261021T153514Z_wi0_d39201b/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261021T232851+0800_10e3a0/runner/20261021T153514Z_wi0_d39201b/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`ddfe3cb4fe488df5461c98e2e0ab8208499b75cba43cbc9b8bf67cb1e594eb4e`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ

**030. [20261021T232851+0800_10e3a0/runner/20261021T153628Z_wi0_3e65162/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261021T232851+0800_10e3a0/runner/20261021T153628Z_wi0_3e65162/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过
- SHA256：`d2fc82c81b7878f0d4c25c4123c32ea7abab8c9cb1cec9817c0d9aecec03f5c7`
- 用例：wi3661-acceptance

**031. [20261021T232851+0800_10e3a0/runner/20261021T153649Z_wi0_65673e0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261021T232851+0800_10e3a0/runner/20261021T153649Z_wi0_65673e0/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`4a49957c4bad8ebf53f8427438e26d77152a65a464cbac6dd2bf5ee4ce23a683`
- 用例：daily-recent10

**032. [20261021T232851+0800_10e3a0/runner/20261021T153708Z_wi0_ac09d11/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261021T232851+0800_10e3a0/runner/20261021T153708Z_wi0_ac09d11/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- SHA256：`bca125bed5a79e843069558df6836064228c19403fd34c1ac6bb4d272a1d37b8`
- 用例：wi3644-core

**033. [20261022T232956+0800_f04cbb/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261022T232956+0800_f04cbb/report.md)**

- 类型：原批次汇总
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 15、工具异常 1。
- 摘要：**缺用例**：工作项 #3677 未在 registry.json 登记用例
- SHA256：`aff05d3028e77eb2b7eefa5b4a12db462bc323dbce9adb56e2aae430f5b33f93`

**034. [20261022T232956+0800_f04cbb/runner/20261022T153703Z_wi0_51b5116/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261022T232956+0800_f04cbb/runner/20261022T153703Z_wi0_51b5116/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`18d3369aecf0aa71efb4b65daa84a571a94d421cf586d37f4247544e69547598`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ

**035. [20261022T232956+0800_f04cbb/runner/20261022T153833Z_wi0_ce44a51/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261022T232956+0800_f04cbb/runner/20261022T153833Z_wi0_ce44a51/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过
- SHA256：`51db353ee39cb700f15f2f7c550905e793fcda21b5646f5901ed413a88a69cc6`
- 用例：wi3661-acceptance

**036. [20261022T232956+0800_f04cbb/runner/20261022T153854Z_wi0_0406f0b/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261022T232956+0800_f04cbb/runner/20261022T153854Z_wi0_0406f0b/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`49876b381aa7ee01a405ca181eec258aa7a71217d4a4925b4b5f6f36a5c79b3a`
- 用例：daily-recent10

**037. [20261022T232956+0800_f04cbb/runner/20261022T153913Z_wi0_7282ec0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261022T232956+0800_f04cbb/runner/20261022T153913Z_wi0_7282ec0/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- SHA256：`cf1d16fbe76a10967cce35f67a7a401cbc70636a09d6d39dfb728903868cbb07`
- 用例：wi3644-core

**038. [20261023T233045+0800_adf235/commits/186102f2032bc08cb43665d500dee0d2822e688b/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/186102f2032bc08cb43665d500dee0d2822e688b/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3684, 工作项 #3685 未在 registry.json 登记用例
- SHA256：`56a640998317fa480a3db687029ece57dfc34a41a1609647f202e7f9e93fb825`

**039. [20261023T233045+0800_adf235/commits/7aede4ff3d226a710e52f035f95a5cd4193e7709/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/7aede4ff3d226a710e52f035f95a5cd4193e7709/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3676 未在 registry.json 登记用例
- SHA256：`73c9b42fbc6e6377000028f3c584837ede4688dbd4e90e517e0c8ff2239f5a77`

**040. [20261023T233045+0800_adf235/commits/8168840ca5f41028099d30a8db36a88066976f61/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/8168840ca5f41028099d30a8db36a88066976f61/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3687 未在 registry.json 登记用例
- SHA256：`1b443974463a7e11f11d4c0ae12056fb2187a2b794b6ee4923f108de42bff6d6`

**041. [20261023T233045+0800_adf235/commits/831307cab6447a492cc7596594fdf83999e4d5d3/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/831307cab6447a492cc7596594fdf83999e4d5d3/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3680 未在 registry.json 登记用例
- SHA256：`de771779bc46a68387d3a8c1051117c8b996a3c2f0eb32db15bd182895292122`

**042. [20261023T233045+0800_adf235/commits/861ad654e3008177477cdd07b335cb255ee56be9/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/861ad654e3008177477cdd07b335cb255ee56be9/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3686 未在 registry.json 登记用例
- SHA256：`af19b4fece2f7a20a54d8ae94937d516dbde7c61dd15de3ca970b072e1f3b39d`

**043. [20261023T233045+0800_adf235/commits/b1dbb2c56e968e98e0c9cddc73dbef75b662f97d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/b1dbb2c56e968e98e0c9cddc73dbef75b662f97d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3682 未在 registry.json 登记用例
- SHA256：`1c50673f9618022f041df6330e76f68ec5ed6564d3fabc7d1b3bed2773984be0`

**044. [20261023T233045+0800_adf235/commits/b5108c3e7badb75b248b56fe45b6f36f2dc4bf5f/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/b5108c3e7badb75b248b56fe45b6f36f2dc4bf5f/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3681 未在 registry.json 登记用例
- SHA256：`1e4618f64615689287392fc6fc62a6b69696c18147e6cb81e57026b406fca620`

**045. [20261023T233045+0800_adf235/commits/bb0b7535925fe3c5561ba28d4c945a06453a4fa5/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/bb0b7535925fe3c5561ba28d4c945a06453a4fa5/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3653 未在 registry.json 登记用例
- SHA256：`6e149a24d17c8efa686784080239dad6d31b9a89a03066c62c54b7f9cec935fd`

**046. [20261023T233045+0800_adf235/commits/e1d149b7ab39e58bce8d499f2d331df9e3337b08/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/e1d149b7ab39e58bce8d499f2d331df9e3337b08/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：工作项 #3669 未在 registry.json 登记用例
- SHA256：`624e754d6e96e9254e0677ab6ad789bc801006bc50e1f5f98bf8372ccf848ac6`

**047. [20261023T233045+0800_adf235/commits/ed31e0ef5cb166568354868d5ec0cdc493e92308/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/commits/ed31e0ef5cb166568354868d5ec0cdc493e92308/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：提交说明无工作项号，且无可映射用例；需人工/Agent 分析 diff 并补用例
- SHA256：`39998b8824cba5327ad57819260b238ad319c58a6f447678196c0225c3105c61`

**048. [20261023T233045+0800_adf235/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261023T233045+0800_adf235/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：INCOMPLETE；尚未收尾的提交数：10。
- 摘要：验收完整性：INCOMPLETE；尚未收尾的提交数：10。
- SHA256：`363ed4c6992491a383cfcfd04387361a032618d1c9f844eb1fd860de7d049c48`

**049. [20261024T232858+0800_97ce23/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261024T232858+0800_97ce23/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：NO_COMMITS；尚未收尾的提交数：0。
- 摘要：验收完整性：NO_COMMITS；尚未收尾的提交数：0。
- SHA256：`ea5939871898c80d2f26068c8cbe3107e39a6c346aaf6c433e5052bcf4b44238`

**050. [20261025T233151+0800_7f69db/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261025T233151+0800_7f69db/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：NO_COMMITS；尚未收尾的提交数：0。
- 摘要：验收完整性：NO_COMMITS；尚未收尾的提交数：0。
- SHA256：`ea5939871898c80d2f26068c8cbe3107e39a6c346aaf6c433e5052bcf4b44238`

**051. [20261026T233206+0800_ed766f/commits/38b7b39789212751b3f725f5c321dbc01614a5c0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/38b7b39789212751b3f725f5c321dbc01614a5c0/report.md)**

- 类型：提交报告
- 原报告状态：当前流程已收尾；验收状态：ERROR。；总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`fbb796d0a1a05e32c0d788ac3ca937621048511a4ef2105d363eb677985faff3`

**052. [20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/report.md)**

- 类型：提交报告
- 原报告状态：当前流程已收尾；验收状态：FAIL。；总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Reviewed registry and candidate plan: no WI3690 executable case or configured UI profile exists. Required real editor interaction and tooltip assertions cannot be represented by the baseline probes; no fabricated expected values or historical screenshots were used. UI remains unverified.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega300-HWv1; No L2/UI fixture creates the required controlled component conditions or drives Filter/Show All. Existing daily-recent10 and daily-supplement do not operate ComponentConfigEditor, so they are not accepted as coverage. UI remains unverified.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega300-HWv1; No registered executable save/reopen fixture or approved baseline data exists for this editor. A logic-only serialization assertion would not cover the mandated save/reopen UI workflow. UI remains unverified.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega300-HWv1
- SHA256：`d9d5e1eaf879938014a0bc5640c839369b000354ea2975fffc86443075af527d`

**053. [20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153742Z_wi0_a47bbad/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153742Z_wi0_a47bbad/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: OK: 80/80 断言通过 | HARNESS_EXCEPTION: 23 个用例异常（非断言失败） | HARNESS_EXCEPTION: 23 个用例异常（非断言失败） | HARNESS_EXCEPTION: 13 个用例异常（非断言失败）
- SHA256：`025e83e8dbf8f718d5b2f143715a306bbbbbb25f5ac7f19c1e0928cd0ae61eed`
- 用例：daily-recent10
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Utility.Common.Tests.NotchChannelResolverTest"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.XYInitialConfigurationUsesModelDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.PmtModelConfigurationPathAndLaserDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Facade.GEMBase.Tests.GEMServiceBaseChangeLoad"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BatchConverterNullAndInvalidValuesUseModelDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.CrossToolFallbackRespectsPresenceOfModelContext`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Facade.GEMBase.Tests.GEMServiceSimChangeLoadP"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.ModelContextAndDefaultAreCorrect`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.Vega300DefaultAndBothJsonMappingsMatchHardwareContract`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.CylinderMappingVisibilityHonorsExplicitCapabilityTokens`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.XYNotchVisibilityReflectsScanDirection`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.AutoOpticsVisibilityHonorsModelAndRole(isSuperUser: False)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "mismatch", total: 1, selected: 1)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "unselected", total: 1, selected: 0)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.RunCacheKeysSeparateSameWaferRunsAndStayStableUnderParallelAccess`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.KTStyleLookupRequiresExplicitWaferSize`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.MotorTimeoutMarginIsThirtySeconds_NotPhysicalMotorAcceptance`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.AutoOpticsVisibilityHonorsModelAndRole(isSuperUser: True)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.KTStyleUsesMeasuredBfpWidthsForModelSize`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.Persisted300IsClampedOnlyWhenUnsupported`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.Vega100PmtSourceTablesHaveExpectedConditionsAndValidRatios`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.BfpInitialSizeMatchesModel`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "empty", total: 0, selected: 0)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.

**054. [20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153808Z_wi0_d8b9328/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153808Z_wi0_d8b9328/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 13 个用例异常（非断言失败） | HARNESS_EXCEPTION: 23 个用例异常（非断言失败） | HARNESS_EXCEPTION: 23 个用例异常（非断言失败） | OK: 80/80 断言通过
- SHA256：`4613243205f938176f7f86e3829bb530b58fe2b0f90c3fd7131796f86fdefa82`
- 用例：daily-recent10
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.BfpInitialSizeMatchesModel`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.AutoOpticsVisibilityHonorsModelAndRole(isSuperUser: False)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.CylinderMappingVisibilityHonorsExplicitCapabilityTokens`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.Vega100PmtSourceTablesHaveExpectedConditionsAndValidRatios`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Facade.GEMBase.Tests.GEMServiceSimChangeLoadP"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Facade.GEMBase.Tests.GEMServiceBaseChangeLoad"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.Vega300DefaultAndBothJsonMappingsMatchHardwareContract`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.PmtModelConfigurationPathAndLaserDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.ExistingProductBehaviorTests(typeName: "Vega.Utility.Common.Tests.NotchChannelResolverTest"···)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.RunCacheKeysSeparateSameWaferRunsAndStayStableUnderParallelAccess`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.XYNotchVisibilityReflectsScanDirection`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.MotorTimeoutMarginIsThirtySeconds_NotPhysicalMotorAcceptance`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailyCommitBatchLogic.AutoOpticsVisibilityHonorsModelAndRole(isSuperUser: True)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.DailyCommitBatchLogic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.XYInitialConfigurationUsesModelDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.CrossToolFallbackRespectsPresenceOfModelContext`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "unselected", total: 1, selected: 0)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.ModelContextAndDefaultAreCorrect`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "mismatch", total: 1, selected: 1)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.KTStyleUsesMeasuredBfpWidthsForModelSize`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BeamStabilityRejectsNoMatchingSequenceBeforeDeviceAccess(scenario: "empty", total: 0, selected: 0)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.KTStyleLookupRequiresExplicitWaferSize`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.BatchConverterNullAndInvalidValuesUseModelDefault`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.WorkItem3644Logic.Persisted300IsClampedOnlyWhenUnsupported`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.

**055. [20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153900Z_wi0_fffa873/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153900Z_wi0_fffa873/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- SHA256：`38a33c50d7456df0ca3eeb01c7314982a39c24b636d8ab4f85db4680e482f790`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：`Vega.AutoTest.CommitProbe.DailyChannelSaveLogic.Vega100_LpdFilteringAndSave_PreservesBareAndHiddenKeys`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ
- 失败证据：FAIL | channels100.lpd-filter-save | channels100.lpd-filter-save | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.

**056. [20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153904Z_wi0_90d3322/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/runner/20261026T153904Z_wi0_90d3322/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 5 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`35e51b80a2d2a5397cc54c0ec92adf2a3e9ea4a9d20783856bfd642cff7de3cf`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 3, cancelled: False, expected: 3)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.PartialRead_IdleTimeoutIsNotSwallowed`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.StopAcquisition_FencesWaitingReader_WithoutHardware`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.ReturnedBytes_CountAndConcatenation`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 0, cancelled: True, expected: 0)`（Failed）：System.TypeInitializationException : The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.empty.0.True | delivery.empty.0.True | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.empty.3.False | delivery.empty.3.False | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.partial-idle-timeout | delivery.partial-idle-timeout | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.
- 失败证据：FAIL | delivery.bytes | delivery.bytes | behavior holds | System.TypeInitializationException: The type initializer for 'Vega.AutoTest.CommitProbe.WorkItem3644Logic' threw an exception.

**057. [20261026T233206+0800_ed766f/commits/cfe9594bbf96336d051ba08cc5e9199f2b591461/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/cfe9594bbf96336d051ba08cc5e9199f2b591461/report.md)**

- 类型：提交报告
- 原报告状态：当前流程已收尾；验收状态：ERROR。；总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`2bd2807733d10d4d2ae4f809eceb1cae977b3e2e706516f2e712b3d19a1cccd5`

**058. [20261026T233206+0800_ed766f/commits/e99f34a1e840226397222121ee80bffffa41ab0f/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/e99f34a1e840226397222121ee80bffffa41ab0f/report.md)**

- 类型：提交报告
- 原报告状态：当前流程已收尾；验收状态：BLOCKED。；总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`e92c25bbe476b679f1a4857f929f65100534d478e41a8bb8e424e7fc8aa8815d`

**059. [20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/report.md)**

- 类型：提交报告
- 原报告状态：当前流程已收尾；验收状态：BLOCKED。；总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：No registered executable case or supplied reproducible wafer/defect baseline covers the changed nearest-neighbor and bounding-box semantics. Existing daily probes are unrelated.; No executable case prepared for this acceptance scenario; Work item has only a title and no reproduction data; registry has no SPA case, input baseline, or UI slot. Actual postprocess/result display is therefore required but unverified; no synthetic expected result was invented.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega300-HWv1
- SHA256：`72634175f3b0954819cce11da3105be9b97f83a858a6980cedc422de0c0e34db`

**060. [20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/runner/20261026T154311Z_wi0_3f1ad45/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/runner/20261026T154311Z_wi0_3f1ad45/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`62be5d5693ab602942d7b3dc28de0361ee2fe5548eb0306a0ab184f67fb9f7f5`
- 用例：daily-recent10

**061. [20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/runner/20261026T154354Z_wi0_4a51878/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/runner/20261026T154354Z_wi0_4a51878/report.md)**

- 类型：Runner
- 原报告状态：ERROR / CONTRACT_VIOLATION
- 摘要：契约自检违例：channels.l2Uia.available=false 但存在通过的断言（检查字段是否被复用） ｜ 原判定 ERROR/HARNESS_EXCEPTION: HARNESS_EXCEPTION: 1 个用例异常（非断言失败） | OK: 1/1 断言通过
- SHA256：`2f1e6d3a024519e28926aa4912c77a42ba6e87f72d8ef789d450c470db158931`
- 用例：daily-supplement
- 失败证据：`Vega.AutoTest.CommitProbe.DailySupplementLogic.DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)`（Failed）：Assert.Equal() Failure: Values differ
- 失败证据：FAIL | delivery.empty.1.True | delivery.empty.1.True | behavior holds | Xunit.Sdk.EqualException: Assert.Equal() Failure: Values differ

**062. [20261026T233206+0800_ed766f/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/report.md)**

- 类型：原批次汇总
- 原报告状态：当前流程：FINISHED；验收：HAS_FAIL；待收尾：0。；验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- 摘要：当前流程：FINISHED；验收：HAS_FAIL；待收尾：0。；验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- SHA256：`3f14969eb4dcc754d62703937884995ae2a28ae966aa945ab3a65a0fc37e86ce`

**063. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/38b7b39789212751b3f725f5c321dbc01614a5c0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/38b7b39789212751b3f725f5c321dbc01614a5c0/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：工具异常 1。
- 摘要：**工具异常**：验证工具异常
- SHA256：`45f72b4081fcfe286195acc737feed468b12e2704104ab2520e63f825a1fa2f5`

**064. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/5b82273357cffd80265e6ec5c4dc5af2793179e2/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: wi3690-recovery；同簇提交见计划，需隔离/二分
- SHA256：`0054eba3e68c6ccf22437e06056f0b343e08fcd51e8abc4a13b77dcf39460f6f`
- 失败证据：`5b822733`：用例失败: wi3690-recovery；同簇提交见计划，需隔离/二分

**065. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/cfe9594bbf96336d051ba08cc5e9199f2b591461/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/cfe9594bbf96336d051ba08cc5e9199f2b591461/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：工具异常 1。
- 摘要：**工具异常**：验证工具异常
- SHA256：`57966a8303a4d5780a049faa61f3559cf8a1c814d60a075a9ef61403a7feda74`

**066. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/e99f34a1e840226397222121ee80bffffa41ab0f/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/e99f34a1e840226397222121ee80bffffa41ab0f/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`1fbf5634df95588b76d58e1c1927f01d99df47096713c594d6e45c884d3a11b0`

**067. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`125a5949fb6dca9b4d79b06ecdfa3d54e75cd5e8ef83f031a65743c2c91d87a2`

**068. [20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe/report.md)**

- 类型：复验汇总
- 原报告状态：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- 摘要：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- SHA256：`619b47c21fb4fcceee0b91c90fbf647196de689ffbc59154b6d5e6df8a1c599b`

**069. [20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Reviewed registry and candidate plan: no executable case models the failed remote collection followed by a same-wafer rerun, and no safe Simulator data/oracle reproduces remote Xilinx collector contention. This batch cannot use real hardware. Existing daily-recent10/daily-supplement are unrelated partial probes and are not claimed as coverage. Required UI and remote-collector verification remain unverified.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega500; The product commit adds a focused unit test, but it is not a registered Runner case and only validates managed filename formatting. No registered L2/UI case, Linux ramdisk fixture, remote collector deployment, or trusted expected defect baseline is available. A real remote-collector run is required and cannot be automated against Simulator; do not infer coverage from the formatting test or headless baseline probes.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega500
- SHA256：`df5115ea7c485f9310177cef9f38e451949b13fbcd54830a92ff8134eb5cc486`

**070. [20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/runner/20261027T153809Z_wi0_3488be6/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/runner/20261027T153809Z_wi0_3488be6/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过 | OK: 80/80 断言通过
- SHA256：`0f004c8b899088f8ee4f303234ea6a3e05aee351f0b85f5ae7b23a70922da9df`
- 用例：daily-recent10

**071. [20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/runner/20261027T153946Z_wi0_49db6cb/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/runner/20261027T153946Z_wi0_49db6cb/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过 | OK: 1/1 断言通过
- SHA256：`3d1590ea0f9097e1bf7ab16012974523dabcf90d287baddf1d61d7ea25d44653`
- 用例：daily-supplement

**072. [20261027T233318+0800_9ad645/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`5fcfd55ceabff0212162bc950ae56c8c289a614e81c12c46edee224b88df4d73`

**073. [20261027T233318+0800_9ad645/commits/227ace85104287d877d8b6869210e573e06bd817/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/227ace85104287d877d8b6869210e573e06bd817/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`48516f6d02625e36f8c365a2ef29ff0e4167598bae468e3a2341f59c89c61082`

**074. [20261027T233318+0800_9ad645/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`306f718609049fec1d2fef3d6c72e4fd3bf1e3462f933793ebe59a66bd590383`

**075. [20261027T233318+0800_9ad645/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`c900a95cd94348e6ff1b33feb409f841a57e4accd3b45e4c32d694e5ea5943f6`

**076. [20261027T233318+0800_9ad645/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`a107deeb1e7926847fd13c34b210138508f6685f0aaa31f05b32919a58a22d49`

**077. [20261027T233318+0800_9ad645/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`c71b96c24de67eaefda1d2b6ff49596d07edfc4c5521323993f2aee19ba1e3f5`

**078. [20261027T233318+0800_9ad645/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`f296474448565d661d63bf64c2aea42e0657e84e8cad269c0cfbb21dfe90d2f5`

**079. [20261027T233318+0800_9ad645/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`410296df9004f65360d9a3345043fb3205295dbb9a7212f99ea7055d85972e8b`

**080. [20261027T233318+0800_9ad645/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`72ab95dbe5da61c3c1df3ff1d0761cf6bd2fd93f3557e4b852e72994f4cde8b8`

**081. [20261027T233318+0800_9ad645/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：INCOMPLETE；尚未收尾的提交数：9。
- 摘要：验收完整性：INCOMPLETE；尚未收尾的提交数：9。
- SHA256：`e41c4f495d1298b525f5e73397ced270f1669b185df6de42b1a5f34f9ce82d1d`

**082. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Managed filename/cache cases were generated/reused and executed. Full remote Linux collector/algorithm deployment and trusted capture/result fixture are not available in this Simulator-only campaign; actual remote UI rerun/analysis remains unverified.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega500
- SHA256：`dc8e9bdc28ee535ee9a7fd9d183a1a3942767ea5acbbe2ab2d8af6ca4f45b576`

**083. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`452c82c68be9ac50857f337fd6e7eb7ab12191f906f888777e6031c007e5dcbd`
- 失败证据：`19c18be5`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**084. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/227ace85104287d877d8b6869210e573e06bd817/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/227ace85104287d877d8b6869210e573e06bd817/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`b407e0e03fabecf5cf93a6c0eec2da3f3d81045e2345c35b71c835386ff02268`
- 失败证据：`227ace85`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**085. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Targeted component tests were authored/adapted, compiled, discovered and trial-run. They do not execute this actual application workflow; UI automation/data prerequisites for this specific flow are still not completed. No UI PASS is claimed.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega500
- SHA256：`c5a41fdc4d27ef05636ce4d9452b799fc4d0e37aa9476a2cdfef793c70e73744`

**086. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`f3ab4fb5f5756da926bb40c9a14923602a0bb7e75b8a3594521b7c4c39934e95`
- 失败证据：`9fa008a9`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**087. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`962703c910f510210bcc312b18178a2a34c697104d92bfb278951d60a585c678`
- 失败证据：`b428717a`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**088. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：工具异常 1。
- 摘要：**工具异常**：CS0103: 当前上下文中不存在名称“FileName”
- SHA256：`f2ed28d2ff462635e392138b3b29ea043d3deee72bf5e471b42aedcab2f9e54f`

**089. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`01017c968667585be4264eee12824d643f9650918a066b019254e6da38f601fa`
- 失败证据：`c30defb8`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**090. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`5374751f5004d5fc14d5f841f965838bc430b25d9eec4251dcf44798834fd997`
- 失败证据：`d117a21d`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**091. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: daily-recent10；同簇提交见计划，需隔离/二分
- SHA256：`8a609f9278a6a668edbfec6558f90a873d28f215e920662d2e475506e5328973`
- 失败证据：`eba9945a`：用例失败: daily-recent10；同簇提交见计划，需隔离/二分

**092. [20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T133825_cac375/report.md)**

- 类型：复验汇总
- 原报告状态：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- 摘要：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- SHA256：`ca9be4467ddf939c04457980760df3b2e0c2ea410303732169e5c72aed1a00a9`

**093. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/00b3004a58decb90f1edba05b6bc2fb446c25e80/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`f1562add470a363f3a885cdc8818622ad86aa10516c3d70eb49a5baa13e205ca`

**094. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/19c18be54684cccc9b0c02b29a021300953f713d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`9b5cf2ae60ac79e6ee9352ffd2e56d59bbb282f5c9947a6906825da701c64230`

**095. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/227ace85104287d877d8b6869210e573e06bd817/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/227ace85104287d877d8b6869210e573e06bd817/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`635103d6c9d92c7e7c527f4dcd4dc9f1889d0fb21048c454256d33c2be1e3c40`

**096. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/6b4cf4af7928fc7173131df26627b24d6f386e97/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`b5404b09768a135550911f513b6b09314c9f7fa4ab6de35ad2481c4d18180040`

**097. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/9fa008a98a3547a9480f3196e7758f683ca9a5da/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`e5275ed8724f24f07b9a32a517c78c99cf854c9dc7134d5bd5c717a64a009505`

**098. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/b428717a56aaba31fc343a34dc76d5c776104dda/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`a955fd42117114d7f1b41b85192dcae5f8dc0d79f8f3019d1baf6da165c2ded2`

**099. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：工具异常 1。
- 摘要：**工具异常**：Target build failed: CS0103: 当前上下文中不存在名称“FileName”; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261027T233318+0800_9ad645\revalidation-20261028T164432_577c69\commits\bd9ccb55d6de34617bb8c676fef7d8e2c9b0b6e0\attempts\20261028T170818_ff7592\build
- SHA256：`2611b3d8270496d7afac38726a5063aec5bea96feadc8747cefe251d455eadea`

**100. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/c30defb85f294f0f66c60382a76c7fdda44cce54/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Exact committed lock does not pass locked restore; see actual restore log. Graph/component PASS cannot establish reproducible restore.
- SHA256：`4926b03010830d74b5f9e20a1fdcdc5084ac0482404fbc66de2dd045793e2257`

**101. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/d117a21d8befd0cd03a70aa9a95da223c33a0728/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`705ecf3e19e9ccbdf8cea0505e0f6156c9ccd0082fde78196ed85961514e486f`

**102. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/commits/eba9945afbca703d94c011bb664ec627fb2dd33d/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：无法判定 1。
- 摘要：**无法判定**：前置/环境不满足，无法判定
- SHA256：`bf354c492ebad6d49686f51f9ce099c92d15517471f23fade02704512effb048`

**103. [20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261027T233318+0800_9ad645/revalidation-20261028T164432_577c69/report.md)**

- 类型：复验汇总
- 原报告状态：验收完整性：INCOMPLETE；尚未收尾的提交数：0。
- 摘要：验收完整性：INCOMPLETE；尚未收尾的提交数：0。
- SHA256：`db1e339ee9ff4363f191f67c9ff7aa20c373a8ccbf57c61e8e2d46f00aa6a14e`

**104. [20261028T233512+0800_89fd3e/commits/055b805acc67c26aa4f534e04e0a7238e33fac85/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/055b805acc67c26aa4f534e04e0a7238e33fac85/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：No executable UI slot, authorized calibration input fixture, or output baseline exists for this SHA; product unit coverage cannot prove the BF calibration screen behavior.; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC
- SHA256：`8b773ee53815b686a4a4099523c4e60a1c16c121ce8654a2c04d500a749c8bc9`

**105. [20261028T233512+0800_89fd3e/commits/68810455e019a22db8e7a3578371e3526de54557/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/68810455e019a22db8e7a3578371e3526de54557/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`e5eab37241082257728338a0ceb5b5eccd64e9e80bf83af3941af45cc2a54fa1`

**106. [20261028T233512+0800_89fd3e/commits/7f843d76cd25baa7c3bc487dec85c0a1efaf96fe/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/7f843d76cd25baa7c3bc487dec85c0a1efaf96fe/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`77092bba4a7e68d977245a2bd95a3590a4513cc0b4f6eddd4e06cab8f0571b08`

**107. [20261028T233512+0800_89fd3e/commits/9acb461b7cf876f8087a3b76d53e0b98d87b7e11/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/9acb461b7cf876f8087a3b76d53e0b98d87b7e11/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`6b14d10f90f861428b944b1e20d7e05575d9b43c3f3c0231e297bc56a42c19e4`

**108. [20261028T233512+0800_89fd3e/commits/b3791912b0e6d133c19613a38c1f62a862812b19/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/b3791912b0e6d133c19613a38c1f62a862812b19/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`f1c698dc1318b3a15ffc55c994983f09bf86978572ed50527a6d2b29f01d3ace`

**109. [20261028T233512+0800_89fd3e/commits/e3ce821c89a17317b2b6962addc51fbf5109d4cb/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/commits/e3ce821c89a17317b2b6962addc51fbf5109d4cb/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：待执行 1。
- 摘要：**待执行**：等待需求分析和真实用例准备；候选计划未冻结，尚未执行
- SHA256：`94c5bb1bd92365e48dce82e6e0d97d624a58883fdf12983ab9e6ba5c419b1c45`

**110. [20261028T233512+0800_89fd3e/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：INCOMPLETE；尚未收尾的提交数：6。
- 摘要：验收完整性：INCOMPLETE；尚未收尾的提交数：6。
- SHA256：`cf9ee6f137380268e62f42abb52cec042c6844085c568d92d5f72cb167355001`

**111. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/055b805acc67c26aa4f534e04e0a7238e33fac85/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/055b805acc67c26aa4f534e04e0a7238e33fac85/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：工具异常 1。
- 摘要：**工具异常**：验证工具异常
- SHA256：`4783392ba24d6f40d4909e47cda6abc4087ddb6f6f48b7c571452cfbc68b6191`

**112. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/68810455e019a22db8e7a3578371e3526de54557/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/68810455e019a22db8e7a3578371e3526de54557/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\68810455e019a22db8e7a3578371e3526de54557\preparation\model\69311a734e-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI model/profile not specified
- SHA256：`c900c591e97a7192a1ce45007eefed34ac68452a1c22afc45d056552aaa587ba`

**113. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/7f843d76cd25baa7c3bc487dec85c0a1efaf96fe/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/7f843d76cd25baa7c3bc487dec85c0a1efaf96fe/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\7f843d76cd25baa7c3bc487dec85c0a1efaf96fe\preparation\model\66e1d21a0e-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI model/profile not specified
- SHA256：`f527eeb600f652e2b2ff682f4a3cbd192d3d8ac445adc14ea674c0988d91815f`

**114. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/9acb461b7cf876f8087a3b76d53e0b98d87b7e11/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/9acb461b7cf876f8087a3b76d53e0b98d87b7e11/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：The actual screen no-syncable-size prompt is covered through its production command with notification capture; the UI workflow exercises the missing-selection prompt. Both application display languages are not separately scheduled.; executable test preparation failed: Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\9acb461b7cf876f8087a3b76d53e0b98d87b7e11\preparation\model\da3e88918a-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega100-Coherent; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega300-HWv2; Required UI case/profile missing: Vega500; The prepared queue must contain two compatible configurations with a shared size. The UI test explicitly fails its fixture check if absent. Synthetic component fixtures establish the full mismatch matrix independently of profile defaults.; executable test preparation failed: Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\9acb461b7cf876f8087a3b76d53e0b98d87b7e11\preparation\model\da3e88918a-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega100-Coherent; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega300-HWv2; Required UI case/profile missing: Vega500; The UI needs a selectable stored recipe whose path or Full Deposit region differs from the target baseline; this prerequisite is asserted. No wafer, measurement ground truth or external hardware is required for this queue-editing feature.; executable test preparation failed: Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\9acb461b7cf876f8087a3b76d53e0b98d87b7e11\preparation\model\da3e88918a-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega100-Coherent; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega300-HWv2; Required UI case/profile missing: Vega500; executable test preparation failed: Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\9acb461b7cf876f8087a3b76d53e0b98d87b7e11\preparation\model\da3e88918a-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI case/profile missing: Vega100-PIC; Required UI case/profile missing: Vega100-Coherent; Required UI case/profile missing: Vega300-HWv1; Required UI case/profile missing: Vega300-HWv2; Required UI case/profile missing: Vega500
- SHA256：`a05b883ddef60f6c470028a4a64834b5f3c62bb176865c06f8d92812752b70d7`

**115. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/b3791912b0e6d133c19613a38c1f62a862812b19/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/b3791912b0e6d133c19613a38c1f62a862812b19/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Model generation failed; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\commits\b3791912b0e6d133c19613a38c1f62a862812b19\preparation\model\c86792476d-0\events.jsonl; No executable case prepared for this acceptance scenario; Required UI model/profile not specified
- SHA256：`311c40042b311139c9dd4b173a08f3182fb6293862d26779e227a4f8d5e8fcdc`

**116. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/e3ce821c89a17317b2b6962addc51fbf5109d4cb/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/commits/e3ce821c89a17317b2b6962addc51fbf5109d4cb/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Owned stage exceeded 480s; see C:\AutoTest\AutoTest\VegaAutoTest\daily\runs\20261028T233512+0800_89fd3e\revalidation-20261029T103639_eb3c92\controller\processes\55bb22030fab.json; No executable case prepared for this acceptance scenario; Required UI model/profile not specified
- SHA256：`6c19216f19a5310dca7267d0a67cef32e18d8be77c0376639fa00f633a18fd20`

**117. [20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261028T233512+0800_89fd3e/revalidation-20261029T103639_eb3c92/report.md)**

- 类型：复验汇总
- 原报告状态：验收完整性：INCOMPLETE；尚未收尾的提交数：0。
- 摘要：验收完整性：INCOMPLETE；尚未收尾的提交数：0。
- SHA256：`f667b9b65ab4e6d100b39ca180368ea8c94e94759837883d7b786061ae156ab2`

**118. [20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：Direct handler invocation covers its behavior without nondeterministic garbage collection. TaskScheduler subscription wiring, real finalizer delivery and operator-screen responsiveness are not exercised. Healthy primary logging output and statistics embedded in that output are not captured.; The private writer is executed directly; no claim is made that LogException's catch was reached through an injected production logging-service failure. Actual software-log export and daily cleanup inclusion remain untested.; Resolver failure, stale invalid roots, actual disk failure and marker behavior across a real hourly rollover are not injected. Crossing the fixture hour causes an explicit test failure; both candidate hourly artifacts are cleaned up.; Requires a physical Vega200 NI acquisition system, suitable calibration wafer and acquisition recipe. No supplied supported profile is Vega200, and all prepared profiles use Simulator. Synthetic handler inputs cannot establish native read/stop ordering, the benign-exit log, acquisition correctness or next-scan recovery.; No executable case prepared for this acceptance scenario; Required UI model/profile not specified; Only fallback-writer behavior with an explicit logging-error argument is covered. Galaxy LogService notification subscription APIs, implementation and acceptance criteria are absent, so the actual subscriber fault and its containment are not tested.; No functional change is attributed to the comment/documentation edits. This concise suite does not execute those existing observation networks, conditional Dispatcher/AppDomain dialogs, or operator Retry/Abort workflows.; No executable case prepared for this acceptance scenario
- SHA256：`5a721572b4da01f08469acdcc7f0b3d972a9a6afe992719e714b3ffd062d24eb`

**119. [20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160814Z_wi0_7d97de5/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160814Z_wi0_7d97de5/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 41/41 断言通过
- SHA256：`81db6a03efff4940084b7998cd978b683b9e9f2ae1fa2d462845fddc6f9edd59`
- 用例：authored-vc95a6a1a9d-8894b083ccb4-fallback-log

**120. [20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160858Z_wi0_b0b29df/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160858Z_wi0_b0b29df/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 6/6 断言通过
- SHA256：`3f8abef252c3a559435fa285c0deb2a20a3221c36205b482eb8d2a13114cdb40`
- 用例：authored-vc95a6a1a9d-8894b083ccb4-unobserved-handler

**121. [20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160912Z_wi0_19e96f5/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T160912Z_wi0_19e96f5/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过
- SHA256：`e07573348579aea05479bd6020b97132a91ae60d417ea2a62f0641e55f4a5f2e`
- 用例：daily-recent10-v3

**122. [20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T161025Z_wi0_5e70280/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/8894b083ccb44dc7abda592bfa7e42c813204712/runner/20261029T161025Z_wi0_5e70280/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过 | OK: 1/1 断言通过
- SHA256：`31c805eaaabbaf25629e80f78f33ab24ceb3628a1647ce342ffcb1c63490dc80`
- 用例：daily-supplement

**123. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**HAS_FAIL**。逐提交：失败 1。
- 摘要：**失败**：用例失败: authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics；同簇提交见计划，需隔离/二分
- SHA256：`8ceaff7c083e3a5b0d3fd7052e6cd5696bf91218c00fd9e9477cc849e889ebc3`
- 失败证据：`d07d0e70`：用例失败: authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics；同簇提交见计划，需隔离/二分

**124. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161210Z_wi0_19cce97/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161210Z_wi0_19cce97/report.md)**

- 类型：Runner
- 原报告状态：BLOCKED / ZERO_ASSERTIONS_MATCHED
- 摘要：ZERO_ASSERTIONS_MATCHED: 仅命中 65/70 条断言，未真正到达目标界面
- SHA256：`6a46f607e2ce48d91960cb1dcddea4ddcea52684088789b688d8069724d207b7`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-component-contract

**125. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161238Z_wi0_a79cbb7/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161238Z_wi0_a79cbb7/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过
- SHA256：`42cc09d3b3b7c8d09dfd5f8b449b856487ecd8a576a5efb18ded420a61e8ec3f`
- 用例：daily-recent10-v3

**126. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161349Z_wi0_982377a/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161349Z_wi0_982377a/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 33/33 断言通过 | OK: 33/33 断言通过 | OK: 33/33 断言通过
- SHA256：`6f5460b9fb4785c132c4013152a7cf1104792ea4587ec63810650cc513d8ff87`
- 用例：wi3644-core

**127. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161436Z_wi0_9820634/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161436Z_wi0_9820634/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过 | OK: 1/1 断言通过
- SHA256：`aeb451dd84cbca5d77b800c83712a72be0016dca77ba33f6d12b1539a5d528cf`
- 用例：daily-supplement

**128. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161513Z_wi0_88beba7/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161513Z_wi0_88beba7/report.md)**

- 类型：Runner
- 原报告状态：FAIL / ASSERTION_FAILED
- 摘要：ASSERTION_FAILED: 2 条断言失败
- SHA256：`fccef47c252444273e0c6d70185c8f50a9c689cc327c30ef6b98e76cc7bf8574`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics
- 失败证据：`Vega.AutoTest.Authored.UniformityWorkflowTests.ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics`（Failed）：登录/就绪超时，最后状态 '忙碌'
- 失败证据：FAIL | workflow.execution | actual Uniformity import/navigation workflow | import and reload business results | Xunit.Sdk.FailException: 登录/就绪超时，最后状态 '忙碌'

**129. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161846Z_wi0_677c361/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T161846Z_wi0_677c361/report.md)**

- 类型：Runner
- 原报告状态：FAIL / ASSERTION_FAILED
- 摘要：ASSERTION_FAILED: 2 条断言失败
- SHA256：`6a985d524da4d43ce2d50ebd935f7559a37818c34edf347410a474b5e83c44ad`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics
- 失败证据：`Vega.AutoTest.Authored.UniformityWorkflowTests.ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics`（Failed）：Assert.NotNull() Failure: Value is null
- 失败证据：FAIL | workflow.execution | actual Uniformity import/navigation workflow | import and reload business results | Xunit.Sdk.NotNullException: Assert.NotNull() Failure: Value is null

**130. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162206Z_wi0_d666895/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162206Z_wi0_d666895/report.md)**

- 类型：Runner
- 原报告状态：FAIL / ASSERTION_FAILED
- 摘要：ASSERTION_FAILED: 2 条断言失败
- SHA256：`3e65199abb1a52b82ca1d28d552e4fee1baa3df1db559d3f9d47675fc1135d2f`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics
- 失败证据：`Vega.AutoTest.Authored.UniformityWorkflowTests.ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics`（Failed）：登录/就绪超时，最后状态 '忙碌'
- 失败证据：FAIL | workflow.execution | actual Uniformity import/navigation workflow | import and reload business results | Xunit.Sdk.FailException: 登录/就绪超时，最后状态 '忙碌'

**131. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162533Z_wi0_3f45e86/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162533Z_wi0_3f45e86/report.md)**

- 类型：Runner
- 原报告状态：FAIL / ASSERTION_FAILED
- 摘要：ASSERTION_FAILED: 2 条断言失败
- SHA256：`bfdcb7a2ebb0034a6f51bfb070c0f20aeba1bf9b950bea3b41fbe874aedf1389`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics
- 失败证据：`Vega.AutoTest.Authored.UniformityWorkflowTests.ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics`（Failed）：登录/就绪超时，最后状态 '忙碌'
- 失败证据：FAIL | workflow.execution | actual Uniformity import/navigation workflow | import and reload business results | Xunit.Sdk.FailException: 登录/就绪超时，最后状态 '忙碌'

**132. [20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162858Z_wi0_d62ec28/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/d07d0e7045b747753a1d1a70769e9853ee35ddd2/runner/20261029T162858Z_wi0_d62ec28/report.md)**

- 类型：Runner
- 原报告状态：FAIL / ASSERTION_FAILED
- 摘要：ASSERTION_FAILED: 2 条断言失败
- SHA256：`ebba60a1dbfdc98ca80db71f6e86be298be7e2f93144b87e733347a880210cd6`
- 用例：authored-v62156ef44b-d07d0e7045b7-uniformity-import-reentry-statistics
- 失败证据：`Vega.AutoTest.Authored.UniformityWorkflowTests.ImportLegacyThenReenterReloadsDiskAndExcludesTailFromAllChannelStatistics`（Failed）：登录/就绪超时，最后状态 '忙碌'
- 失败证据：FAIL | workflow.execution | actual Uniformity import/navigation workflow | import and reload business results | Xunit.Sdk.FailException: 登录/就绪超时，最后状态 '忙碌'

**133. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/report.md)**

- 类型：提交报告
- 原报告状态：总体验收：**INCOMPLETE**。逐提交：缺用例 1。
- 摘要：**缺用例**：需求覆盖未齐：This establishes one production native imaging operation. Beam fitting and DFT numerical accuracy require separate documented numerical oracles; no measured beam reference data or accuracy acceptance criteria were supplied.; The codec probe executes the upgraded dependency directly. No Vega System.Drawing consumer implementation was supplied, so application image export integration remains unproven. Bitmap encoding also does not establish Microsoft.Win32.SystemEvents 9.0.1 event delivery.; These are STA component tests using real WPF controls. They do not establish physical mouse routing, grouped screen behavior or application navigation. The lock-only commit supplies no screen-specific acceptance workflow or business-output oracle.; The supplied candidate identifies graph coverage but contains no execution evidence. Tests against copied runtime assemblies cannot establish clean-cache NuGet restore, package hash validation or complete project/RID build closure.; Remaining source requests are zero. Consumer API signatures and independent expected outputs are missing for these families. Synthetic fixtures are suitable once contracts are available; this is not evidence that hardware or authorization is unavailable. Real device accuracy would additionally require specified hardware, calibrated inputs and acceptance tolerances, none of which this lock-only change defines.; No executable case prepared for this acceptance scenario
- SHA256：`1ece871810f098ab9aa7e71becefd11d970d89d47fe81353f92cccf8729ff8ee`

**134. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160356Z_wi0_3e8ef4a/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160356Z_wi0_3e8ef4a/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 21/21 断言通过
- SHA256：`aca0b689ef5d5c8747c735ddd890364b0acd17bbc03941cf2650ec177be35e45`
- 用例：authored-v27ffeb903e-e05bb7f732f6-native-saturation

**135. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160453Z_wi0_899028e/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160453Z_wi0_899028e/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 27/27 断言通过
- SHA256：`e4de7a129cf9ee98e54b008bb5982177fbfc43433c9edfe14f2bb51a8a85a751`
- 用例：authored-v27ffeb903e-e05bb7f732f6-xaml-selection

**136. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160506Z_wi0_3bada99/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160506Z_wi0_3bada99/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 1/1 断言通过
- SHA256：`296fed2b97e2f6823189b0ad22f339462f8eca4b4de7b517f3eeb6efec9b9a44`
- 用例：commit-e05bb7f732f6-lock-graph

**137. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160517Z_wi0_b341a7b/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160517Z_wi0_b341a7b/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过 | OK: 81/81 断言通过
- SHA256：`597afc8406e4cac31d1e804b72b645eb31e83f6d68371e3b24b26de19b8a786e`
- 用例：daily-recent10-v3

**138. [20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160632Z_wi0_188d67f/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/commits/e05bb7f732f699a153fb9117617c7567cb068dff/runner/20261029T160632Z_wi0_188d67f/report.md)**

- 类型：Runner
- 原报告状态：PASS / OK
- 摘要：OK: 17/17 断言通过 | OK: 1/1 断言通过
- SHA256：`6e0b2ea12d55183c17faa678beb5fa68f6345f8c2a89c4ed8417275d0b95d527`
- 用例：daily-supplement

**139. [20261029T233003+0800_18201c/report.md](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/report-md-archive-20261030T102244_08_00/report-md-archive-20261030T102244_08_00/20261029T233003+0800_18201c/report.md)**

- 类型：原批次汇总
- 原报告状态：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- 摘要：验收完整性：HAS_FAIL；尚未收尾的提交数：0。
- SHA256：`d346bddeff59c6eacd87571558576f62e2a515d5d4de5fc040355232934af0e1`
