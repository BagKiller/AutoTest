# #3804 单独验证

构建：`workspaces/manual-20261111`，HEAD `58606ec9c84080feb715145c8100262097b60ef0`，Vega.exe 构建时间 2026-11-19T16:00:47+08:00。机型 Vega500 Simulator。

正式结论来自 Runner `result.json`：runId `20261119T103813Z_wi3804_1886041`，**PASS / OK**，4/4。

工单没有复现步骤字段。验收写在描述里，按这四条在 `Default Job / WI3689_UI_20261026` 上核对。

| 验收 | 实际 | 断言 |
| --- | --- | --- |
| 输入部分路径后列表只显示匹配项 | 输入 `BFScan` 后剩 2 条：当前选中的 `JRH_EPI_Particle_dual`（不含该文字）和 `Cali Recipe Library/BFScanRecipe`。其余 3 条消失 | PASS。断言按「未选中的不匹配项消失，当前选中项保留」 |
| 清空后恢复全量 | 清空后回到 5 条 | PASS |
| 过滤结果中选中、保存、再进入 | 选中 `Cali Recipe Library/BFScanRecipe`，点保存后页签仍带未保存标记；再点关闭并选「保存且关闭」，重新进入后显示该项 | PASS |
| 输入未匹配文字后离开 | 输入 `ZZZNOMATCH` 后点到 Disabled，文本回到 `Cali Recipe Library/BFScanRecipe`。直接关闭不保存，再进入仍是该项 | PASS |

验完已把该 Lot 的配方存回 `JRH_EPI_Particle_dual`。会话 `daily/sessions/20261119T181824_Vega500_832d9e` 已 Restore，配置 SHA256 已核对。

上次 CLI 试跑把「每一项都必须包含关键词」当成断言，因此记了 FAIL。这次界面仍会留下当前选中项，这和工单里的「过滤不影响已选中项」一致。
