工作项 3774 专项验证：未完成验收。

Runner: ERROR/HARNESS_EXCEPTION; runId=20261117T055837Z_wi3774_4f0d871

目标固定 HEAD ab2ced6a19a731912257401588f48a07d23de55c；Vega500 Simulator。复用该 SHA 已构建产品，Runner 未自行重编（builtFromCurrentSource=false）；专项测试独立目录构建。

已通过官方认证，以 su 登录，UI readiness=READY、设备空闲。配方已打开；KLARF 已有手工勾选且非全选前置断言 1/1 通过。

阻塞：新增 SPA 时右键未出现 Insert Element 菜单，UiElementNotFoundException；未执行到新增属性默认不勾选及原勾选保持的业务断言。不是产品 FAIL，也不是 PASS。

会话：daily/sessions/20261117T135836_Vega500_de9eb4；finally Stop/Restore，v2-restore.txt 明确 SHA256 verified。

工单已使用 PAT 只读获取 revision 8，快照 preparation/workitems.json；原 daily 冻结记录保持不变。

早期尝试 runner/20261117T055413Z_wi3774_960b1e4 与 retry/20261117T055640Z_wi3774_76264e6 均停在旧 KLARF 标签定位，保留历史，未合并为通过。

后续需修正 SPA 插入菜单的 UI 定位/滚动和配方编辑前置，再新建测试版本验证四步完整场景。

正式报告：C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\v2\20261117T055837Z_wi3774_4f0d871\report.md

当前 C: 可用 22087401472 bytes。