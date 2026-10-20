# Vega 每日提交批次验证

每日 23:30（北京时间）的 Codex 本地定时任务已配置。任务使用本目录收集器和 AI操作手册.md 的 Runner 判定纪律；收集、规划、执行、总结保存在 daily/runs/<batchId>/。机器及桌面应用需要保持运行；UI 阶段需要可交互会话和有效机型授权。

## 运行入口

```powershell
$python='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
& $python C:\AutoTest\AutoTest\VegaAutoTest\daily\daily.py --execute
# 示例：最近十次提交，不推进每日游标
& $python C:\AutoTest\AutoTest\VegaAutoTest\daily\daily.py --last 10 --execute
```

默认从 C:\Vega\Vega_SW 读取本地目标。远端 master 刷新到独立 bare 缓存，PAT 仅在 git 子进程环境中使用，不进入 URL、命令参数或产物。日志不打印认证头。`--offline` 只允许离线分析，不得将其报告称为远端当天全量。

收集按 SHA 去重，覆盖可达 DAG 全部提交（不只 first-parent）；按 committer time 选当天 00:00~执行时的提交，并补收上次 collectedHead 之后新增可达提交，覆盖跨日及回填日期。author time 也保留在 manifest，便于审查。manifest 固定了目标 HEAD、截止时间、提交列表、文件与原始 diff。示例模式不会更新 state.json。每日收集游标与 pendingCommits 分离；验收阻塞项不会因游标推进而删除。历史重写必须人工审查，不能默默重置游标。首次每日运行仅收当天，若需首轮历史补收使用明确的历史示例/清单。

## 目标落后时的处理

不使用旧 DLL 验证新提交。daily.py --execute 现自动清理专用验证 Simulator 遗留进程，从 manifest 的源缓存创建每次独立的 detached worktree，固定目标构建并核对提交/DLL指纹；无需人工先切换目录。不要 reset/覆盖用户的 C:\Vega 工程，也不复用旧的共享输出目录。每轮日志保存在批次 attempts/<attempt>/build；历史重写等事实仍保留为阻塞。

```powershell
& C:\AutoTest\AutoTest\VegaAutoTest\daily\Build-DailyVega.ps1 -RepoRoot C:\AutoTest\workspaces\Vega_SW
$env:VEGA_SW_ROOT='C:\AutoTest\workspaces\Vega_SW'
& $python C:\AutoTest\AutoTest\VegaAutoTest\daily\daily.py --last 10 --execute
```

构建使用 SDK10、运行目标 .NET8；仅 Simulator 构建排除 NI 原生 C++ 项目。build/ 下保存 restore/build 日志和 commit+DLL SHA256。此构建未采用 locked restore，不能算锁文件门禁通过。构建脚本不修改产品 tracked 文件，也不复制旧 DLL 冒充新构建。

Runner 仍通过既有 verify 输出 result.json/report.md。外部构建不改变其 builtFromCurrentSource=false 标记；在总报告补充独立构建证据，保留正式标记与限制。运行后必须检查测试文件数、断言、minAssertions、二进制指纹与失败明细，不按进程退出0自行判定。

核心执行后自动运行 daily-supplement：logic（17条行为断言）与 logic-channels100（1条V100 LPD过滤/保存场景）。源自目标提交的原始Binning测试和真实DLL交付/会话行为均独立记录。V100通道测试单独进程，避免Binning测试的配置服务影响机型单例；测试替身仅提供隔离服务，不操作真实桌面授权。原始Runner结果保存在supplement-runner，supplement-reference.json保存引用/指纹。

## 动态规划和执行纪律

1. 逐一阅读提交正文、diff、关联工作项的预期；新需求不能只靠标题/路径规则。classify 只是保守的候选分组，unmappedFiles 必须继续人工/Agent 分析。
2. 固定同一个目标 HEAD 构建累积效果，不逐提交 checkout；互相覆盖的改动合并回归。提交合并验证不等于能定位哪一提交引入缺陷；失败后才做隔离或二分。
3. 先执行无 GUI 的三机型/四配置核心批次（V100、V300 HWv1、V300 HWv2、V500），宿主使用真实 DLL 与现有原始产品测试，任何替身仅表示逻辑边界，不表示真实设备通过。
4. 按机型+硬件 profile+Gen+Simulator/GemSim+数据前置分组，一组一次启动。V300 HWv2 不可与 HWv1 合并成一个配置结果。可先复用已打开的同版本/同配置会话，再切其它配置，最后恢复原会话配置。
5. UI 配置修改前保存内容与 SHA256，停止测试进程后再改 SelectedToolType、RootCondition、Features、SelectedProfiles 和独立 UserDataFolder；设置 Simulator/GemSim/Gen2。不可仅改机型名称而漏掉 OuterToInner、硬件能力或用户数据目录。不得修改正式授权以绕过模型检查。
6. 使用 computer-use 技能操作窗口。用户于本任务明确授权自动操作现有官方认证器、按机型配置并完成启动授权/登录；可以执行该范围内的合法认证流程，无须每机型再请用户手工操作。仅使用官方工具生成/保存的凭据，不猜密码、不伪造或绕过授权；不得将密钥/密码写入测试报告或仓库。确实无法取得合法授权时记 BLOCKED。禁止触发真实硬件，禁止将 GemSim 结果当作 SEPLAT/E87 Host 验收。
7. 无人值守执行不等待人工关闭。自动清理本项目专用 workspaces 下已确认 Simulator 的遗留进程，记录 PID/路径/启动时间；与本项目无关进程采用隔离目录避开。每次构建创建全新 worktree、全新 attempts 证据目录并核对目标提交与 DLL SHA256，不复用运行中的输出。UI 每步超时、最多一次恢复重试，失败继续其他组，finally 停止本轮进程、恢复配置并核对 SHA256；无论成功失败都生成报告。Start 保存进程身份，Stop 等待实际退出。
8. 只转述 Runner 四态和 runId；静态检查、手动观察、Simulator 部分覆盖、真机待验分开记录。没有完整验收就保留 pending，不能把多个局部 PASS 汇成工作项 PASS。
9. 每个 batch 的报告必须包含所有提交及未覆盖项。所有 diff 未映射改动必须补计划。硬件验收要写出原因、需要的设备、操作及预期证据。不要 push、创建 PR 或写工作项状态。

## 当前示例中的重点场景

- 采集隔离：正常 run、同晶圆失败后重跑、取消后重跑、停止/重启栅栏、超时与短读；避免仅测缓存键格式。
- AFS：采集/监控开启后反复关页重开，无卡死/残留帧；MS 30秒只可在真实电机验证物理行为。
- V100 XY：给 SimCenterX/Y/Theta 配置非零值后，执行结果仍为零补偿；旧 EnableNotchScan 自愈；V300/500 继续应用其 Sim 偏移。需要具备完整校准前置数据，缺失则记录 BLOCKED。
- V300 HWv1/HWv2：配置 Slit/W1FF 显示差异、位号、旧副本加载；真实 IO 接线只能真机确认。
- GEM：返回 false/异常/连续点击仅一次调用；Load 完成早于等待、晚于等待、取消、失败和超时路径；Host/E87 需真机或协议联调台。

## 报告解释

summary.json 的 acceptanceState 表示验收完整性，并非第二套 verdict；正式 verdict 始终来自 Runner 原始 result.json。默认 INCOMPLETE 表示仍有待验项，不等于 FAIL。编译/工具异常保持 ERROR，前置不足保持 BLOCKED。原始报告不手改；附加的批次总报告可补充真实观察、构建证据和覆盖矩阵。历史批次不删除。
