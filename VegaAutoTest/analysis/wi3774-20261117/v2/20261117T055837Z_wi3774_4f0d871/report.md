# 工作项 #3774 验证报告

- 结论：验证工具异常（ERROR / HARNESS_EXCEPTION）
- 说明：HARNESS_EXCEPTION: 1 个用例异常（非断言失败）
- runId：`20261117T055837Z_wi3774_4f0d871`
- 用例：wi3774-special-v2-20261117
- 断言：1 过 1（失败 0，跳过 0）
- 被测：commit `ab2ced6a`，分支 HEAD，Runner 未自行重编（builtFromCurrentSource=false；结论针对本次记录的二进制，来源另核对独立构建及指纹）
- 二进制构建时间：2026-11-17T11:58:20+08:00
- 起止：2026-11-17T13:58:36+08:00 → 2026-11-17T14:00:05+08:00（89.0 秒）

## 失败用例

- `Vega.AutoTest.Authored.SpaKlarfReproTests.AddingSpaPreservesExistingKlarfSelection`（Failed）：Vega.AutoTest.Harness.UiElementNotFoundException : UI element not found: Insert recipe element menu (automationId=-, name=Insert Element, type=MenuItem, waited 15s). Nearby elements: [Menu '系统'; MenuItem '还原' #61728; MenuItem '移动(M)' #61456; MenuItem '大小(S)' #61440; MenuItem '最小化' #61472; MenuItem '最大化(X)' #61488; MenuItem '关闭' #61536; Pane '任务栏'; Button '开始'; Pane '' #4100; Button '在此键入进行搜索' #4101; Button '任务视图' #4113; ToolBar '运行中的应用程序'; Button 'Microsoft Edge' #MSEdge; MenuItem '文件资源管理器 - 4 个运行窗口' #Microsoft.Windows.Explorer; Button 'ChatGPT - 1 个运行窗口' #OpenAI.Codex_2p2nqsd0c76g0!App; Button 'v2rayN - 1 个运行窗口' #C:\Users\Administrator\Desktop\v2rayN-windows-64\v2rayN.exe; Button 'CC Switch - 1 个运行窗口' #com.ccswitch.desktop; Button 'Fork - 1 个运行窗口' #velopack.Fork; Button 'Nimbalyst - 1 个运行窗口' #com.nimbalyst.electron; MenuItem 'Cursor - 2 个运行窗口' #Anysphere.Cursor; Button '记事本 - 1 个运行窗口' #{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\notepad.exe; Button 'Vega - 1 个运行窗口' #C:\AutoTest\workspaces\daily-latest\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.exe; Pane '' #303; Button '通知 V 形' #1502; Pane ''; ToolBar '用户提示通知区域' #1504; Button '你的设备中缺少重要更新。

## 断言

| 结果 | 编号 | 说明 | 期望 | 实际 |
| --- | --- | --- | --- | --- |
| PASS | precondition | KLARF 已有手工勾选项且非全选 | 0 < checked < total | 3 / 35 |

## 取证

`C:\AutoTest\AutoTest\VegaAutoTest\analysis\wi3774-20261117\v2\20261117T055837Z_wi3774_4f0d871`

