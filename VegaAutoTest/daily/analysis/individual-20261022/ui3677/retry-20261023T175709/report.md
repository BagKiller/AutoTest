# #3677 实际 UI 再测试与操作失败定位

2026-10-23 18:12 北京时间。结论：**部分界面步骤成功，完整验收仍 BLOCKED**。

Runner 原始 verdict：`BLOCKED / ZERO_ASSERTIONS_MATCHED`，命中 2/6，runId `20261023T101032Z_wi0_81db622`。没有改断言、最低断言数或历史结果。

## 实际执行范围

| 步骤 | 本轮结果 |
| --- | --- |
| 启动目标版本 Vega.exe | 成功，PID 600，2026-10-23 17:57:31 +08:00 |
| 官方授权及登录 | 成功，标题含 su，设备空闲；密码只存内存，未写文件 |
| 导入工单 JRH_EPI_Particle_dual.recipe | 成功，Recipe Library 实际显示该文件及 Oblique/Normal 配置 |
| 创建专项 Job | 未完成，切换任务页后桌面查询连续超时 |
| 配置原始 Normal/Oblique JSON | 未执行 |
| 运行后处理、核对产出 pitch/DC 合并 | 未执行，无本轮结果数据 |

[实际登录截图](loggedIn.jpg)；[实际配方导入截图](recipeImported.jpg)。截图 SHA256、进程及完整诊断在 [ui-evidence.json](ui-evidence.json)。

## 为什么之前启动 UI 并操作失败

不能把几个不同阶段合称为软件启动失败。本轮已经证明该目标版本能够启动并合法登录。此前 20 秒启动检查的空标题和 Responding=false 不是产品启动故障证据；本轮实际进程开始于 17:57:31，约 17:58:28 出现主界面/初始化日志，之前的观察窗口不足以排除正常初始化。

本轮确认的桌面自动化问题：

1. `list_windows` 首次请求超时，重试恢复；认证器第一次截屏出现 `FrameArrived timed out: timed out waiting on channel`，重新观察后恢复。
2. 被遮挡窗口的截图有时显示前台其他内容；显式激活目标后，截图才与目标一致。
3. 主窗口嵌套登录控件索引出现 `coordinate input geometry is unavailable` / `element 130 is not available in cached app state`；普通 type_text 两次未填入用户名。重新通过 list_windows 取得独立 VegaLoginWindow，使用该窗口的控件索引和支持的 set_value 后，官方生成的密码登录成功。登录按钮提交后对旧窗口刷新报 `window is not a usable app window`，实际是登录窗关闭，重新列举确认主窗口标题含 su，并非登录失败。
4. 配方导入文件对话框也存在嵌套控件缓存异常，改用最新模态截图坐标及正常输入后导入成功。
5. 导入成功后切换任务页，get_window_state 再次超时；list_windows 超时，等2秒重试仍超时，重置 JavaScript 会话并重导入 @oai/sky 后最后一次仍超时。依 computer-use 技能的有界恢复规则停止继续输入。

因此已证实的直接阻塞是**桌面控制/捕获和窗口绑定链路不稳定**，不是磁盘容量不足，也没有证据证明是 #3677 pitch/DC 逻辑导致启动或登录失败。helper 为何失去响应尚不能进一步确定，不能凭这些错误断定为权限、锁屏或 Vega 卡死。未绕过官方授权，未操作真机。

## 版本与恢复

目标 HEAD `10c33df5e69aa932bea8d843a29d8f6530d1fd73`，专属目录 `C:\AutoTest\workspaces\individual-20261022-20261023`。本轮使用同一 #3677 专项扩容后构建并重新核对指纹，未新建构建、未取其他批次 DLL。Vega.dll SHA256 `676C049E73D9D6595DC35F246E2F11B718B0BC8FDB44BEB3A7505FBCD14EA15D`，与17:30构建证明一致。Runner builtFromCurrentSource=false 保持原样，源码来源由独立构建证明补充；不声称 Runner 自身已重编。

会话 `daily/sessions/20261023T175709_Vega300-HWv1_4fa055`：Vega300-HWv1、Simulator、Gem=false、Gen2，隔离 ResourcesDaily_Vega300-HWv1。结束时停止本轮 Vega PID600，恢复 ComponentConfig、HardwareProfiles 与 MachineConfiguration，SHA256 均与备份一致；详见 [restoration.json](restoration.json)。恢复审计记录的剩余日志子进程 PID7088，随后已核对其完整项目路径及本轮启动时间并停止，最终进程查询确认 PID600/7088 均不存在。没有改用户 checkout、每日游标或原报告。导入的测试配方保留于隔离数据环境，不删除证据。

完整 UI 验收必须继续到 Job、两份原始 JSON、后处理和 pitch/DC 输出对比；启动/登录/导入成功本身不能宣布 #3677 已修复。

[Runner 原始结果](runner/20261023T101032Z_wi0_81db622/result.json) · [Runner 原始报告](runner/20261023T101032Z_wi0_81db622/report.md)
