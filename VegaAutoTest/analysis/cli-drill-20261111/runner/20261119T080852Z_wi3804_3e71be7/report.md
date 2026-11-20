# 工作项 #3804 验证报告

- 结论：未通过（FAIL / ASSERTION_FAILED）
- 说明：ASSERTION_FAILED: 1 条断言失败
- runId：`20261119T080852Z_wi3804_3e71be7`
- 用例：wi3804-lot-recipe-filter
- 断言：1 过 2（失败 1，跳过 0）
- 被测：commit `58606ec9`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T16:00:47+08:00
- 起止：2026-11-19T16:08:52+08:00 → 2026-11-19T16:09:36+08:00（44.1 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| FAIL | filter-match | 输入部分路径后列表只保留匹配项 | count<5 and all contain 'Default Recipe' | count=2; items=Default Recipe \| JRH_EPI_Particle_dual |
| PASS | filter-clear | 清空输入后恢复全量列表 | count>=5 | count=5 |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\cli-drill-20261111\runner\20261119T080852Z_wi3804_3e71be7`

