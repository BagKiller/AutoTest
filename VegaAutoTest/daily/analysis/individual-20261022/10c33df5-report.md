# #3677：离线后处理 pitch 缺失与 DC 不合并：逐提交复验报告

提交 `10c33df5e69aa932bea8d843a29d8f6530d1fd73`。

工作项原始步骤是导入 dual recipe、创建 job、选择离线 postprocess 并添加 Normal/Oblique JSON。提交为新后处理 collection 设置 recipe throughput 推导的 PitchInMicron，并校验 JSON pitch 覆盖值。

本次 Runner 原始 verdict：**PASS / OK**；结构化检查 22/22；runId `20261023T081242Z_wi0_afd6e2a`。

被测版本为昨天最终 HEAD `10c33df5e69aa932bea8d843a29d8f6530d1fd73`；本次按提交各自设计和执行独立测试，共用此确定版本的重新构建模块。未逐个 checkout 三个历史 SHA 做隔离二分。

## 实际执行的验证

- 真实 PopulateCollectionRunFromJson：使用独立合成配方和配置，将 Normal pitch=18 μm、Oblique pitch=36 μm 写入 CollectionRunInfo，并检查 300000 μm wafer size。校准服务为空结果、授权服务不授予特性，图像/采集通道在测试配方中关闭。
- 真实 SetupForCollectAsync 的 ReproPostProc 路径：保留已有 25 μm pitch 和唯一 collection，避免覆盖持久化实测值。
- JSON 16 个边界组合：缺字段、null、两个字段名、数字字符串、零/负数、非法字符串、NaN/Infinity、10 倍边界内外、字段优先级。
- de-DE 当前区域设置下，字符串 19.5 按 invariant culture 正确解析。
- 读取工单两个原始 Normal/Oblique JSON，确认顶层均无 pitch 字段，调用真实 ApplyJsonPitch 后保留 18 μm 兜底。
- 已从工作项下载两份原始算法 JSON 和加密 dual.recipe，记录哈希。配方附件未在桌面中导入。

## 覆盖边界与未验项

尚未执行 su 登录后的完整创建 job/离线后处理界面流程；未将原始 dual.recipe 与两份 JSON 送入整个处理引擎，也未量化最终 DC 合并数量或 SPA 聚类输出。本次证明 pitch 初始化、校验和保留逻辑，不能替代完整 DC 合并验收。

本项登记为 `partial-acceptance`，算法/逻辑 PASS 不等于整条工作项或整机验收。

## 来源与证据

- [原始 result.json](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/wi3677-logic/20261023T081242Z_wi0_afd6e2a/result.json)
- [Runner 原生报告](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/wi3677-logic/20261023T081242Z_wi0_afd6e2a/report.md)
- [本次独立构建及 DLL SHA256](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/build-reference.json)
- [提交原始 diff](/C:/AutoTest/AutoTest/VegaAutoTest/daily/analysis/individual-20261022/10c33df5.diff)

Runner 没有调用其自身 --rebuild，故 `builtFromCurrentSource=false` 原样保留；其内置字段不能证明本次模块构建。附加构建证据与测试内 SHA256 比对证明所调用 DLL 来自本次独立目录；未使用旧批次 DLL，未构建/启动完整 Vega.exe。

## 逐用例原始结果

| 用例 | 结果 |
| --- | --- |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.NewCollectionGetsRecipeThroughputPitch(incidence: Normal, expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitchSize\":21}", expected: 21) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.OriginalIssueJsonKeepsNonzeroFallback(fileName: "Carr003-01_261021194832_Normal_20261021195012.json") | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":20,\"pitchSize\":22}", expected: 20) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":-1}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":180.01}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":20}", expected: 20) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":null}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":\"bad\"}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.ParsingUsesInvariantCulture | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":\"Infinity\"}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":\"19.5\"}", expected: 19.5) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.NewCollectionGetsRecipeThroughputPitch(incidence: Oblique, expected: 36) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":0}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":null,\"pitchSize\":22}", expected: 22) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.ReproPostProcKeepsPersistedPitch | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":\"NaN\"}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":180}", expected: 180) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":1.79}", expected: 18) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.JsonPitchPreservesFallbackOrUsesValidValue(json: "{\"pitch_size\":1.8}", expected: 1.8) | Passed |
| Vega.AutoTest.IndividualCommits.WorkItem3677Logic.OriginalIssueJsonKeepsNonzeroFallback(fileName: "Wafer_1_202610215854_Oblique_20261021215958.json") | Passed |

## 历次执行记录

| runId | 原始 verdict | 检查数 |
| --- | --- | --- |
| `20261023T080443Z_wi0_89dc3a6` | PASS/OK | 19/19 |
| `20261023T080632Z_wi0_15ca0d6` | ERROR/TEST_HOST_FAILED | 0/0 |
| `20261023T080713Z_wi0_1591b01` | ERROR/TEST_HOST_FAILED | 0/0 |
| `20261023T080758Z_wi0_fe2a7cb` | ERROR/CONTRACT_VIOLATION | 19/21 |
| `20261023T080847Z_wi0_81c817e` | ERROR/CONTRACT_VIOLATION | 19/21 |
| `20261023T081026Z_wi0_ee704ea` | PASS/OK | 21/21 |
| `20261023T081104Z_wi0_91eb4c5` | ERROR/TEST_HOST_FAILED | 0/0 |
| `20261023T081144Z_wi0_4564c6a` | PASS/OK | 22/22 |
| `20261023T081242Z_wi0_afd6e2a` | PASS/OK | 22/22 |

早期锁文件冲突、测试适配代码编译/容器配置异常属于测试准备阶段，原始 ERROR 全部保留；正式结果取本文件列出的最终 runId，未编辑历史 Runner 结果。
