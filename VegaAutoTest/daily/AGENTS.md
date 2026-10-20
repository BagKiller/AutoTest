# 本项目验证授权

每日流程必须无人值守。用户已明确授权自动停止本项目验证遗留的 Vega Simulator 进程。对 C:\AutoTest\workspaces 下且当前选中配置为 Simulator 的 Vega 自动清理并记录 PID、路径、启动时间；不等待人工关闭。每次构建创建独立 worktree 与证据目录，禁止复用正在运行的软件输出。无界面测试独立继续，UI 失败有界重试后记录失败并继续其他组，不调用人工问答等待；结束时停止本轮启动的进程并恢复配置。与本项目无关的进程不终止，换独立目录隔离。不得因单个组阻塞而省略总报告。

用户已明确要求启动现有 Authenticator，并根据 Vega100/300/500 机型自行配置及完成认证。此前“认证必须用户手工完成”的技能默认限制已经用户要求修改；本任务可自动操作官方认证器的机型、Simulator、Gem、Gen2、资源目录、从本地加载、生成/保存启动验证密钥和授权范围内的登录。

只在这台验证机与本项目使用官方认证流程。不伪造/绕过授权，不猜密码，不将密钥、密码或 PAT 写入报告。遇到工具系统批准提示仍按工具要求获取权限。用户原配置必须备份，验证后恢复并核对 SHA256。

界面验证只执行 `plan.json` 的 `uiSlots`：每个会话先 `Start-ModelValidation.ps1 -Action Prepare`，再完成认证，然后 `daily.py --resume <batch> --run-slot <slotId> --session <SessionPath>`。不要为 plan 之外的机型配置启动会话，也不要临场改用例或断言；没有登记用例的工作项只在报告中记“缺用例”。

认证器资源目录必须与 Start-ModelValidation.ps1 的 userDataFolder 一致：ResourcesDaily_Vega100-PIC、ResourcesDaily_Vega300-HWv1、ResourcesDaily_Vega300-HWv2、ResourcesDaily_Vega500。所有组均为 Simulator=true、Gem=false、Gen2；生成日期/有效期以官方工具合法设置为准。密钥文件存在不等于启动已经通过，必须观察 Vega 的实际启动结果。
