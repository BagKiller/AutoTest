# 工作项 #3811 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 3/3 断言通过
- runId：`20261119T052034Z_wi3811_1e93d0d`
- 用例：wi3811-klarf-ui
- 断言：3 过 3（失败 0，跳过 0）
- 被测：commit `c6bedced`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T13:19:07+08:00
- 起止：2026-11-19T13:20:33+08:00 → 2026-11-19T13:22:17+08:00（104.1 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | ppd-polarity-visible | BF PPD 下可见 PPD_Polarity | present | PPD_Polarity |
| PASS | ppd-polarity-under-ppd | PPD_Polarity 位于 PPD 之下 | path contains PPD | BF Default / Defects / PPD / PPD_Polarity |
| PASS | ppd-polarity-checkable | PPD_Polarity 可勾选 | toggle then restore | toggled |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\manual-20261113\runner\20261119T052034Z_wi3811_1e93d0d`

