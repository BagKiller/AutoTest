# 工作项 #3804 验证报告

- 结论：已验证通过（PASS / OK）
- 说明：OK: 4/4 断言通过
- runId：`20261119T103813Z_wi3804_1886041`
- 用例：wi3804-lot-recipe-recheck
- 断言：4 过 4（失败 0，跳过 0）
- 被测：commit `58606ec9`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-19T16:00:47+08:00
- 起止：2026-11-19T18:38:13+08:00 → 2026-11-19T18:39:20+08:00（67.6 秒）

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | filter-match | 输入 BFScan 后列表收缩；不含关键词的未选中项消失；当前选中项仍在 | count<5; keep 'JRH_EPI_Particle_dual'; other items contain BFScan | count=2; items=JRH_EPI_Particle_dual \| Cali Recipe Library/BFScanRecipe |
| PASS | filter-clear | 清空输入后恢复全量列表 | count>=5 | count=5 |
| PASS | save-reopen | 过滤结果中选中 BFScanRecipe，保存并关闭后再进入，显示该项 | contains BFScanRecipe | Cali Recipe Library/BFScanRecipe |
| PASS | unmatched-leave | 输入未匹配文字后离开，回显实际选中项，且不改变已保存内容 | echo='Cali Recipe Library/BFScanRecipe' saved='Cali Recipe Library/BFScanRecipe' | echo='Cali Recipe Library/BFScanRecipe' saved='Cali Recipe Library/BFScanRecipe' |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\manual-wi3804\runner\20261119T103813Z_wi3804_1886041`

