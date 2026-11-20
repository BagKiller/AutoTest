# 工作项 #3774 验证报告

- 结论：验证工具异常（ERROR / HARNESS_EXCEPTION）
- 说明：HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- runId：`20261117T063850Z_wi3774_0cd740a`
- 用例：wi3774-special-v4-20261117
- 断言：1 过 1（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-17T11:58:20+08:00
- 起止：2026-11-17T14:38:50+08:00 → 2026-11-17T14:39:59+08:00（69.3 秒）

## 失败用例

- `Vega.AutoTest.Authored.SpaKlarfReproTests.AddingSpaPreservesExistingKlarfSelection`（Failed）：System.ArgumentException : Unknown UI prerequisite code (Parameter 'code')

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | precondition | KLARF 已有手工勾选项且非全选 | 0 < checked < total | 3 / 35 |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\v4\runner\20261117T063850Z_wi3774_0cd740a`

