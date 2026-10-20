# Vega 每日提交批次验证

每日 23:30（北京时间）由本机 Codex 定时任务触发。收集当天 master 全部提交，按提交说明里的工作项号和改动路径把相似提交归成簇，簇映射到 `cases/registry.json` 的登记用例；同一目标 HEAD 只构建一次，同一用例无论被多少提交引用只跑一次，界面用例按机型配置合并成会话。判定纪律同 `AI操作手册.md`：结论只来自 Runner 的 `result.json`。产物在 `daily/runs/<batchId>/`。

机器与桌面需保持运行；界面会话需要可交互桌面和有效的机型授权。

## 定时任务要做的三步

```powershell
$python='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
cd C:\AutoTest\AutoTest\VegaAutoTest

# 1. 收集 + 归簇编排 + 独立 worktree 构建 + 全部无界面用例
& $python .\daily\daily.py --execute

# 2. 逐个执行 plan.json 的 uiSlots（报告“界面会话”一节有现成命令）
& .\daily\Start-ModelValidation.ps1 -Model Vega100 -Profile PIC -RepoRoot <worktree> -Action Prepare
#    按 AGENTS.md 用官方认证器配置该机型并确认能登录，然后：
& $python .\daily\daily.py --resume .\daily\runs\<batchId> --run-slot ui-Vega100-PIC --session <SessionPath>
#    --session 给出时，会话结束后自动 Restore 并核对 SHA256；失败的会话记录后继续下一个

# 3. 读 daily\runs\<batchId>\report.md，原样转述逐提交状态与 runId
```

第 1 步的 stdout 末行是 JSON：`report`、`acceptanceState`、`statusCounts`、`pendingUiSlots`。`pendingUiSlots` 为空表示当晚不需要界面会话。

其他入口：

| 命令 | 作用 |
| --- | --- |
| `--last 10 --execute` | 示例：最近十个提交，不推进每日游标、不改待验队列 |
| `--offline` | 只用本地仓库分析，报告不得称为远端当天全量 |
| `--resume <batch> --execute` | 同一 manifest 重新构建并重跑无界面用例（新 attempt） |
| `--resume <batch> --plan-only` | 补登记用例或改 `scheduling.json` 后重算计划与报告，不执行 |
| `--ack <sha> --ack-reason <理由>` | 人工关闭一个待验提交（例如确认无需自动验证），理由写入 `state.json` |

## 收集

- 按 **committer time** 取北京时间当天 00:00 至执行时刻的 master 全部可达提交（不只 first-parent），按 SHA 去重。
- 补收上次 `collectedHead` 之后新出现的提交，覆盖 23:30 之后的提交和回填日期的提交。
- `state.json` 的 `pendingCommits` 里前批未验收的提交，只要仍在 master 历史上，就**重新编入本批**并在当晚 HEAD 上重验；已不在历史上的列入报告“不可达的待验提交”，保留待人工确认。
- 远端历史被改写时不执行，保留证据待人工审查，不会默默重置游标。
- `manifest.json` 固定目标 HEAD、窗口、每个提交的完整说明、工作项号、文件列表；`<sha8>.diff` 保存原始改动。

## 归簇与编排

每个提交按以下顺序归簇（一个提交可落在多个簇）：

1. **工作项簇** `wi:<id>`：从标题和正文提取工作项号（`#3661`、`AB#3661`、`Bug 3661`、`工作项 3661`、`Related work items: #3661`、`【3661】`；`Merged PR 4803` 里的 PR 号会排除）。同一工作项的多个提交合为一簇，映射 registry 中 `workItems` 含该号的用例。
2. **路径簇** `tag:<name>`：`classify()` 的路径规则，经 `scheduling.json` 的 `tagCases` 映射到回归探针。只算候选，不代表验收。
3. **未映射模块簇** `module:<目录>`：仅对没有工作项号的提交，把路径规则未覆盖的文件按模块分组，列入“需分析”。
4. 仅文档改动（`ve-docs/`、`*.md`）的提交记为无需验证。

有产品改动时，`baselineCases`（`daily-recent10`、`daily-supplement`）作为每日回归底线照常跑，单独标注，不计入任何提交的验收。

执行顺序：

- **无界面**：用例去重后排序——工作项用例在前，路径探针其次，底线最后；同级按覆盖提交数从多到少。每个用例一次 Runner 调用，跑该用例全部非界面层（`logic100`/`logic300`/`logic300-hwv2`/`logic500`/`logic-none`/`logic` 等，每层独立机型宿主）。总时长受 `headlessBudgetMinutes` 约束，超出的用例记 `NOT_RUN_BUDGET`，相关提交保持待执行。
- **界面会话**：界面层（`uia`、`l1`、`l2/` 工程）按机型配置分组，每个配置一次切换与授权覆盖组内全部用例，按 `profileOrder`（V100-PIC → V100-Coherent → V300-HWv1 → V300-HWv2 → V500）排列，**当晚没有用例的配置不启动**。用例机型来自 `scheduling.json` 的 `caseProfiles`；未声明的挂到当晚已计划的第一个配置（都没有则用 `defaultUiProfile`），报告标注“结论只代表该机型”。V300 HWv1 与 HWv2 永远是两个会话。
- 界面用例仍由 `L2TestBase` 每条自行拉起并关闭 Vega；会话合并省的是机型配置切换和授权，不是进程启动次数。
- **真机**：`hardwareTags` 命中的提交列入“需真机验证”，写明需要的设备，从不自动执行。

## 逐提交判定

每个提交一行，列出所属簇、用到的用例与 runId、自己的状态：

| 状态 | 条件 | 移出待验队列 |
| --- | --- | --- |
| 已验收 `ACCEPTED` | 有工作项号，该工作项的登记用例（非 `partial-acceptance`）在目标 HEAD 构建上全部 PASS，且关联探针无 FAIL | 是 |
| 无需验证 `NOT_APPLICABLE` | 仅文档改动 | 是 |
| 失败 `FAIL` | 任一关联用例 FAIL（含路径探针）；报告给出同簇提交，需隔离或二分 | 否 |
| 工具异常 `ERROR` / 无法判定 `BLOCKED` | 工作项用例的 Runner 结论 | 否 |
| 待执行 `PENDING` | 工作项用例还有层或界面会话未执行 | 否 |
| 部分验收 `PARTIAL` | 只有路径探针，或登记用例带 `partial-acceptance` | 否 |
| 缺用例 `NEEDS_CASE` | 工作项未登记用例，或无工作项号且无可映射探针 | 否 |

`summary.json` 的 `acceptanceState`：无提交 `NO_COMMITS`；有失败 `HAS_FAIL`；全部关闭 `COMPLETE`；其余 `INCOMPLETE`。它表示验收完整性，不是第二套 verdict。多个提交合并验证通过，不代表能定位哪个提交引入缺陷；失败后才隔离。

要让一个新工作项能被每晚自动验收：写用例并登记 `registry.json`（`workItems` 填工作项号）；界面用例需固定机型时，在 `scheduling.json` 的 `caseProfiles` 里声明。提交说明带上工作项号即可被自动归簇。

## 构建与环境纪律

不使用旧 DLL 验证新提交。`--execute` 先清理本项目专用 Simulator 遗留进程，从 manifest 的源缓存创建每次独立的 detached worktree，构建目标 HEAD 并核对提交与 Vega.dll SHA256。不 reset/覆盖用户的 `C:\Vega` 工程，不复用旧的共享输出目录。每轮日志在 `attempts/<attempt>/`。

构建使用 SDK10、运行目标 .NET8；Simulator 构建排除 NI 原生 C++ 项目；未采用 locked restore，不能算锁文件门禁通过。Runner 自身未重编，`builtFromCurrentSource=false` 原样保留，报告附独立构建证据说明 DLL 来源。

远端 master 刷新到独立 bare 缓存，PAT 只在 git 子进程环境中使用，不进 URL、命令参数或产物。

## 界面会话纪律

1. 配置修改前保存内容与 SHA256，Vega 停止后才改 `SelectedToolType`、`RootCondition`、`Features`、`SelectedProfiles` 和独立 `UserDataFolder`（`Start-ModelValidation.ps1 -Action Prepare`）。RootCondition 为 `<机型>,Simulator,GemSim,Gen2`；官方认证器里 Simulator 勾选、Gem 不勾选、Gen2。不得只改机型名称而漏掉 OuterToInner、硬件能力或用户数据目录，不得修改正式授权绕过机型检查。
2. 认证与登录按 `AGENTS.md` 和 `authenticator-flow.json` 用 computer-use 操作官方认证器；密码只在内存与子进程环境中使用（L2 登录读取 `VEGA_AUTOTEST_USER`/`VEGA_AUTOTEST_PASSWORD`），不写入报告或仓库。取不到合法授权时该会话记 BLOCKED，继续下一个。
3. 每个会话有界执行，失败最多恢复重试一次，继续其他会话；`finally` 停止本轮进程、恢复配置并核对 SHA256。无论成功失败都生成报告。禁止触发真实硬件，GemSim 结果不能当作 SEPLAT/E87 Host 验收。
4. 不 push、不创建 PR、不写工作项状态。原始 Runner 报告不手改，历史批次不删除。
