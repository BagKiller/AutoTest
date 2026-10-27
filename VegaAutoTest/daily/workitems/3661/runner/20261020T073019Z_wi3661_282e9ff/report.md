# 工作项 #3661 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过 | OK: 45/45 断言通过
- runId：`20261020T073019Z_wi3661_282e9ff`
- 用例：wi3661-acceptance
- 断言：180 过 180（失败 0，跳过 0）
- 被测：commit `985a08aa`，分支 HEAD，未重编（builtFromCurrentSource=false，结论仅对旧二进制有效）
- 二进制构建时间：2026-10-20T10:16:19+08:00
- 起止：2026-10-20T15:30:17+08:00 → 2026-10-20T15:31:35+08:00（77.2 秒）

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
| PASS | wi3661.calculator.does-not-save | calculator.does-not-save | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" |
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
| PASS | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | true |
| PASS | wi3661.batch.channels.DW1-filter | batch.channels.DW1-filter | false | false |
| PASS | wi3661.batch.channels.DN | batch.channels.DN | true | true |
| PASS | wi3661.batch.hardware.render | batch.hardware.render | "DW" | "DW" |
| PASS | wi3661.batch.channel.render | batch.channel.render | "DW" | "DW" |
| PASS | wi3661.batch.save-DW2-hardware | batch.save-DW2-hardware | 2345.0 | 2345.0 |
| PASS | wi3661.batch.save-DW2-channel | batch.save-DW2-channel | 0.456 | 0.456 |
| PASS | wi3661.batch.hidden-hardware | batch.hidden-hardware | 111.0 | 111.0 |
| PASS | wi3661.batch.hidden-channel | batch.hidden-channel | 0.111 | 0.111 |
| PASS | wi3661.batch.no-display-key | batch.no-display-key | false | false |
| PASS | wi3661.batch.json-DW2-roundtrip | batch.json-DW2-roundtrip | 2345.0 | 2345.0 |
| PASS | wi3661.summary.render.DW2 Mask | summary.render.DW2 Mask | "DW2 Mask" | "DW2 Mask" |
| PASS | wi3661.summary.render.DW2 Polarizer | summary.render.DW2 Polarizer | "DW2 Polarizer" | "DW2 Polarizer" |
| PASS | wi3661.summary.render.DW2 Ideal Gain | summary.render.DW2 Ideal Gain | "DW2 Ideal Gain" | "DW2 Ideal Gain" |
| PASS | wi3661.summary.render.DW2-0 | summary.render.DW2-0 | "DW2-0" | "DW2-0" |
| PASS | wi3661.summary.render.DN Mask | summary.render.DN Mask | "DN Mask" | "DN Mask" |
| PASS | wi3661.summary.bare-keys-unchanged | summary.bare-keys-unchanged | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" |
| PASS | wi3661.hardware.bare-DW2 | hardware.bare-DW2 | true | true |
| PASS | wi3661.hardware.DW1-filter | hardware.DW1-filter | true | true |
| PASS | wi3661.hardware.DN | hardware.DN | true | true |
| PASS | wi3661.hardware.render | hardware.render | "DW2" | "DW2" |
| PASS | wi3661.hardware.save-DW2 | hardware.save-DW2 | 0.123 | 0.123 |
| PASS | wi3661.hardware.hidden-preserved | hardware.hidden-preserved | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" |
| PASS | wi3661.hardware.no-display-key | hardware.no-display-key | false | false |
| PASS | wi3661.calculator.bare-DW2 | calculator.bare-DW2 | true | true |
| PASS | wi3661.calculator.DW1-filter | calculator.DW1-filter | true | true |
| PASS | wi3661.calculator.DN | calculator.DN | true | true |
| PASS | wi3661.calculator.render | calculator.render | "DW2" | "DW2" |
| PASS | wi3661.calculator.does-not-save | calculator.does-not-save | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" |
| PASS | wi3661.lpd.pir.bare-DW2 | lpd.pir.bare-DW2 | true | true |
| PASS | wi3661.lpd.pir.DW1-filter | lpd.pir.DW1-filter | true | true |
| PASS | wi3661.lpd.pir.DN | lpd.pir.DN | true | true |
| PASS | wi3661.lpd.sir.bare-DW2 | lpd.sir.bare-DW2 | true | true |
| PASS | wi3661.lpd.sir.DW1-filter | lpd.sir.DW1-filter | true | true |
| PASS | wi3661.lpd.sir.DN | lpd.sir.DN | true | true |
| PASS | wi3661.lpd.pir.render | lpd.pir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.sir.render | lpd.sir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.save.pir-DW2 | lpd.save.pir-DW2 | 7.0 | 7.0 |
| PASS | wi3661.lpd.save.sir-DW2 | lpd.save.sir-DW2 | 8.0 | 8.0 |
| PASS | wi3661.lpd.hidden.pir-DW1 | lpd.hidden.pir-DW1 | 11.0 | 11.0 |
| PASS | wi3661.lpd.hidden.sir-DW1 | lpd.hidden.sir-DW1 | 12.0 | 12.0 |
| PASS | wi3661.lpd.no-display-key | lpd.no-display-key | false | false |
| PASS | wi3661.batch.hardware.bare-DW2 | batch.hardware.bare-DW2 | true | true |
| PASS | wi3661.batch.hardware.DW1-filter | batch.hardware.DW1-filter | true | true |
| PASS | wi3661.batch.hardware.DN | batch.hardware.DN | true | true |
| PASS | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | true |
| PASS | wi3661.batch.channels.DW1-filter | batch.channels.DW1-filter | true | true |
| PASS | wi3661.batch.channels.DN | batch.channels.DN | true | true |
| PASS | wi3661.batch.hardware.render | batch.hardware.render | "DW2" | "DW2" |
| PASS | wi3661.batch.channel.render | batch.channel.render | "DW2" | "DW2" |
| PASS | wi3661.batch.save-DW2-hardware | batch.save-DW2-hardware | 2345.0 | 2345.0 |
| PASS | wi3661.batch.save-DW2-channel | batch.save-DW2-channel | 0.456 | 0.456 |
| PASS | wi3661.batch.hidden-hardware | batch.hidden-hardware | 111.0 | 111.0 |
| PASS | wi3661.batch.hidden-channel | batch.hidden-channel | 0.111 | 0.111 |
| PASS | wi3661.batch.no-display-key | batch.no-display-key | false | false |
| PASS | wi3661.batch.json-DW2-roundtrip | batch.json-DW2-roundtrip | 2345.0 | 2345.0 |
| PASS | wi3661.summary.render.DW2 Mask | summary.render.DW2 Mask | "DW2 Mask" | "DW2 Mask" |
| PASS | wi3661.summary.render.DW2 Polarizer | summary.render.DW2 Polarizer | "DW2 Polarizer" | "DW2 Polarizer" |
| PASS | wi3661.summary.render.DW2 Ideal Gain | summary.render.DW2 Ideal Gain | "DW2 Ideal Gain" | "DW2 Ideal Gain" |
| PASS | wi3661.summary.render.DW2-0 | summary.render.DW2-0 | "DW2-0" | "DW2-0" |
| PASS | wi3661.summary.render.DN Mask | summary.render.DN Mask | "DN Mask" | "DN Mask" |
| PASS | wi3661.summary.bare-keys-unchanged | summary.bare-keys-unchanged | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" |
| PASS | wi3661.hardware.bare-DW2 | hardware.bare-DW2 | true | true |
| PASS | wi3661.hardware.DW1-filter | hardware.DW1-filter | true | true |
| PASS | wi3661.hardware.DN | hardware.DN | true | true |
| PASS | wi3661.hardware.render | hardware.render | "DW2" | "DW2" |
| PASS | wi3661.hardware.save-DW2 | hardware.save-DW2 | 0.123 | 0.123 |
| PASS | wi3661.hardware.hidden-preserved | hardware.hidden-preserved | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" |
| PASS | wi3661.hardware.no-display-key | hardware.no-display-key | false | false |
| PASS | wi3661.calculator.bare-DW2 | calculator.bare-DW2 | true | true |
| PASS | wi3661.calculator.DW1-filter | calculator.DW1-filter | true | true |
| PASS | wi3661.calculator.DN | calculator.DN | true | true |
| PASS | wi3661.calculator.render | calculator.render | "DW2" | "DW2" |
| PASS | wi3661.calculator.does-not-save | calculator.does-not-save | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" |
| PASS | wi3661.lpd.pir.bare-DW2 | lpd.pir.bare-DW2 | true | true |
| PASS | wi3661.lpd.pir.DW1-filter | lpd.pir.DW1-filter | true | true |
| PASS | wi3661.lpd.pir.DN | lpd.pir.DN | true | true |
| PASS | wi3661.lpd.sir.bare-DW2 | lpd.sir.bare-DW2 | true | true |
| PASS | wi3661.lpd.sir.DW1-filter | lpd.sir.DW1-filter | true | true |
| PASS | wi3661.lpd.sir.DN | lpd.sir.DN | true | true |
| PASS | wi3661.lpd.pir.render | lpd.pir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.sir.render | lpd.sir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.save.pir-DW2 | lpd.save.pir-DW2 | 7.0 | 7.0 |
| PASS | wi3661.lpd.save.sir-DW2 | lpd.save.sir-DW2 | 8.0 | 8.0 |
| PASS | wi3661.lpd.hidden.pir-DW1 | lpd.hidden.pir-DW1 | 11.0 | 11.0 |
| PASS | wi3661.lpd.hidden.sir-DW1 | lpd.hidden.sir-DW1 | 12.0 | 12.0 |
| PASS | wi3661.lpd.no-display-key | lpd.no-display-key | false | false |
| PASS | wi3661.batch.hardware.bare-DW2 | batch.hardware.bare-DW2 | true | true |
| PASS | wi3661.batch.hardware.DW1-filter | batch.hardware.DW1-filter | true | true |
| PASS | wi3661.batch.hardware.DN | batch.hardware.DN | true | true |
| PASS | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | true |
| PASS | wi3661.batch.channels.DW1-filter | batch.channels.DW1-filter | true | true |
| PASS | wi3661.batch.channels.DN | batch.channels.DN | true | true |
| PASS | wi3661.batch.hardware.render | batch.hardware.render | "DW2" | "DW2" |
| PASS | wi3661.batch.channel.render | batch.channel.render | "DW2" | "DW2" |
| PASS | wi3661.batch.save-DW2-hardware | batch.save-DW2-hardware | 2345.0 | 2345.0 |
| PASS | wi3661.batch.save-DW2-channel | batch.save-DW2-channel | 0.456 | 0.456 |
| PASS | wi3661.batch.hidden-hardware | batch.hidden-hardware | 111.0 | 111.0 |
| PASS | wi3661.batch.hidden-channel | batch.hidden-channel | 0.111 | 0.111 |
| PASS | wi3661.batch.no-display-key | batch.no-display-key | false | false |
| PASS | wi3661.batch.json-DW2-roundtrip | batch.json-DW2-roundtrip | 2345.0 | 2345.0 |
| PASS | wi3661.summary.render.DW2 Mask | summary.render.DW2 Mask | "DW2 Mask" | "DW2 Mask" |
| PASS | wi3661.summary.render.DW2 Polarizer | summary.render.DW2 Polarizer | "DW2 Polarizer" | "DW2 Polarizer" |
| PASS | wi3661.summary.render.DW2 Ideal Gain | summary.render.DW2 Ideal Gain | "DW2 Ideal Gain" | "DW2 Ideal Gain" |
| PASS | wi3661.summary.render.DW2-0 | summary.render.DW2-0 | "DW2-0" | "DW2-0" |
| PASS | wi3661.summary.render.DN Mask | summary.render.DN Mask | "DN Mask" | "DN Mask" |
| PASS | wi3661.summary.bare-keys-unchanged | summary.bare-keys-unchanged | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" | "{\"BaseSummaryDic\":{\"DW2 Mask\":\"Full\",\"DW2 Polarizer\":\"P\",\"DW2 Ideal Gain\":\"123\",\"DW2-0\":\"4\",\"DN Mask\":\"Full\"},\"CollectionsSummary\":[],\"ResultSummary\":{\"ResultSummaryDic\":{\"Results\":\"\",\"Grade\":\"Rejected\"}},\"Title\":\"Pre Scan Info\",\"SummaryType\":1}" |
| PASS | wi3661.hardware.bare-DW2 | hardware.bare-DW2 | true | true |
| PASS | wi3661.hardware.DW1-filter | hardware.DW1-filter | true | true |
| PASS | wi3661.hardware.DN | hardware.DN | true | true |
| PASS | wi3661.hardware.render | hardware.render | "DW2" | "DW2" |
| PASS | wi3661.hardware.save-DW2 | hardware.save-DW2 | 0.123 | 0.123 |
| PASS | wi3661.hardware.hidden-preserved | hardware.hidden-preserved | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" | "{\"HazeNoise\":555.0,\"MinDefect\":11.0,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}" |
| PASS | wi3661.hardware.no-display-key | hardware.no-display-key | false | false |
| PASS | wi3661.calculator.bare-DW2 | calculator.bare-DW2 | true | true |
| PASS | wi3661.calculator.DW1-filter | calculator.DW1-filter | true | true |
| PASS | wi3661.calculator.DN | calculator.DN | true | true |
| PASS | wi3661.calculator.render | calculator.render | "DW2" | "DW2" |
| PASS | wi3661.calculator.does-not-save | calculator.does-not-save | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" | "{\"PreTrack\":26139.925,\"MainTrack\":26139.925,\"LPMThreshold\":0.2922,\"LaserPowerRange\":\"Full\",\"RotationAngle\":25.1041,\"HardwareParamsDic\":{\"DW1\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DW2\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0},\"DN\":{\"HazeNoise\":555.0,\"MinDefect\":0.01,\"MaxDefect\":555.0,\"MinSIT\":0.1,\"MaxXT\":555.0,\"RefMaxHazeNppm\":555.0,\"RefOD\":5,\"RefPMTVoltage\":550.0,\"SystemGain\":555.0,\"SingleThreshold\":65535.0,\"SirSingleThreshold\":65535.0}}}" |
| PASS | wi3661.lpd.pir.bare-DW2 | lpd.pir.bare-DW2 | true | true |
| PASS | wi3661.lpd.pir.DW1-filter | lpd.pir.DW1-filter | true | true |
| PASS | wi3661.lpd.pir.DN | lpd.pir.DN | true | true |
| PASS | wi3661.lpd.sir.bare-DW2 | lpd.sir.bare-DW2 | true | true |
| PASS | wi3661.lpd.sir.DW1-filter | lpd.sir.DW1-filter | true | true |
| PASS | wi3661.lpd.sir.DN | lpd.sir.DN | true | true |
| PASS | wi3661.lpd.pir.render | lpd.pir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.sir.render | lpd.sir.render | "DW2" | "DW2" |
| PASS | wi3661.lpd.save.pir-DW2 | lpd.save.pir-DW2 | 7.0 | 7.0 |
| PASS | wi3661.lpd.save.sir-DW2 | lpd.save.sir-DW2 | 8.0 | 8.0 |
| PASS | wi3661.lpd.hidden.pir-DW1 | lpd.hidden.pir-DW1 | 11.0 | 11.0 |
| PASS | wi3661.lpd.hidden.sir-DW1 | lpd.hidden.sir-DW1 | 12.0 | 12.0 |
| PASS | wi3661.lpd.no-display-key | lpd.no-display-key | false | false |
| PASS | wi3661.batch.hardware.bare-DW2 | batch.hardware.bare-DW2 | true | true |
| PASS | wi3661.batch.hardware.DW1-filter | batch.hardware.DW1-filter | true | true |
| PASS | wi3661.batch.hardware.DN | batch.hardware.DN | true | true |
| PASS | wi3661.batch.channels.bare-DW2 | batch.channels.bare-DW2 | true | true |
| PASS | wi3661.batch.channels.DW1-filter | batch.channels.DW1-filter | true | true |
| PASS | wi3661.batch.channels.DN | batch.channels.DN | true | true |
| PASS | wi3661.batch.hardware.render | batch.hardware.render | "DW2" | "DW2" |
| PASS | wi3661.batch.channel.render | batch.channel.render | "DW2" | "DW2" |
| PASS | wi3661.batch.save-DW2-hardware | batch.save-DW2-hardware | 2345.0 | 2345.0 |
| PASS | wi3661.batch.save-DW2-channel | batch.save-DW2-channel | 0.456 | 0.456 |
| PASS | wi3661.batch.hidden-hardware | batch.hidden-hardware | 111.0 | 111.0 |
| PASS | wi3661.batch.hidden-channel | batch.hidden-channel | 0.111 | 0.111 |
| PASS | wi3661.batch.no-display-key | batch.no-display-key | false | false |
| PASS | wi3661.batch.json-DW2-roundtrip | batch.json-DW2-roundtrip | 2345.0 | 2345.0 |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\daily\workitems\3661\runner\20261020T073019Z_wi3661_282e9ff`

