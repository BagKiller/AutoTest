# Vega 每日提交验证（2026-11-03 统一版）

规则以同目录 `AGENTS.md` 为准；本文说明用法和数据格式。旧批次保留原计划和结果，不迁移、不重跑。

## 现行策略：最新 HEAD 构建一次，逐提交测试

- 每晚北京时间 23:30，本机 Windows 计划任务 `VegaAutoTest-DailyValidation` 直接运行项目程序（不经过 AI 聊天）。
- 仅收集北京时间当天 00:00 至触发时刻 master 上全部可达提交（按 committer time），不补收跨日提交。夜间只续跑当天 daily 批次，不自动恢复历史待验或补验；历史专项需另行显式 `--resume`。
- 每个提交单独读取完整 diff 和工单，单独生成测试和需求场景（`requirement-scopes.json`、`preparation/scopes/<sha>/`）。
- 全部测试在收集时固定的最新 HEAD 上**只构建一次**并执行。根报告"逐提交测试结果"表逐提交列出用例、runId 和状态，`summary.json` 的 `commitResults` 是同一内容的机读版本。
- 报告标注 `testingPolicy=latest-head`、`perCommitAcceptance=false`：结果说明各提交的需求在最新 HEAD 上是否通过，不代表各提交在自己 SHA 上独立验收，也不做失败二分。
- 磁盘只占一个共享工作区 `C:\AutoTest\workspaces\daily-latest`（约 9-11G）。回收规则见 `AGENTS.md` §2。
- `--strategy individual`（每个 SHA 独立构建）只用于用户单独要求的逐提交专项复验；`--strategy merged` 仅兼容旧批次。

## 无人值守入口

```powershell
$python='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
cd C:\AutoTest\AutoTest\VegaAutoTest

# 安装/修复计划任务（不保存用户密码；以当前用户交互会话运行，需保持登录）
& .\daily\Install-DailyValidationTask.ps1
# 只检查运行环境（Codex CLI 与登录），不收集、不运行测试
& .\daily\Start-DailyValidation.ps1 -RuntimeCheck
# 手动启动一次（隐藏的独立监督进程）
& .\daily\Start-DailyValidation.ps1
# 查看真实进度（只读）
& $python -B .\daily\autopilot.py --status
# 恢复一个已有批次
& .\daily\Start-DailyValidation.ps1 -Batch <根批次路径>
```

启动器的 `STARTED` 只证明监督进程已启动。批次 `controller/state.json` 记录真实阶段，`controller/processes/` 保留子进程归属和超时，`controller-launches/<启动>/result.json` 记录监督重试，每次控制器调用的日志单独保存为 `controller-<n>.*.log`。已有活动进程时只返回当前状态，避免重复构建。

控制器执行顺序：收集 → 拉取工单/附件 → 逐提交生成测试与审核 → HEAD 构建一次并编译/发现（最多一次编译修正）→ 冻结 → drain 执行无界面与必需 UI → finally 恢复 → `--check-complete`。`FINISHED` 只证明流程收尾；是否验收通过看根 report/summary 和原始 Runner 结果。`FINISHED_NO_TESTS`（summary `testsExecuted=false`）表示流程收尾但 0 个 Runner 结果，当晚等于没有校验。

共享工作区 `daily-latest` 只给夜间批次和 `--last` 样例使用。回收前会停止工作区内的本项目进程并关闭 .NET 构建服务；若上次 `git worktree remove` 中途失败留下没有 `.git` 的目录，且能证明是本项目的工作区，会自动清理并 prune 后重建（见 `AGENTS.md` §2）。专项复验使用自己的 worktree。

模型生成器 `model_author.py` 使用已登录的本机 Codex CLI（高推理），不读取或复制 API 密钥。模型只返回结构化测试源码及需求审核，没有 shell、写文件、多代理或 MCP 工具；程序校验相对路径、机型、用例 ID 和来源后写入独立版本的测试项目。`preparation/model/` 保存请求、响应和真实错误。每提交模型推理累计最多 20 分钟。

## 手动排障命令

控制器运行时持有操作锁，以下命令会返回 already running；先用 `--status` 确认。

```powershell
& .\daily\Ensure-DailyDatabase.ps1                          # 数据库未运行时复用原数据启动
& $python .\daily\daily.py --execute                         # 收集（默认 latest-head），返回不等于已执行
& $python .\daily\daily.py --resume <根> --prepare-all       # 退出 2 是中间态
& $python .\daily\daily.py --resume <根> --check-cases <HEAD> --case-ids <ID列表>
& $python .\daily\daily.py --resume <根> --freeze-commit <HEAD> --review-file <review.json>
& $python .\daily\daily.py --resume <根> --drain             # 返回 2 时按 ui-request.json 完成 UI 后再 drain
& $python .\daily\daily.py --resume <根> --check-complete    # 必须返回 0
```

| 命令 | 作用 |
| --- | --- |
| `--last 10 --execute` | 样例：最近十个提交，同样按 latest-head；不推进每日游标。占用的共享工作区可被夜间任务优先回收 |
| `--offline` | 只用本地仓库分析，报告不得称为远端当天全量 |
| `--strategy individual` | 显式逐提交独立构建（专项复验） |
| `--revalidate-batch <原批次>` | 用户授权的历史专项复验，在原批次下新建 `revalidation-*`，在独立 worktree 构建（不占用 `daily-latest`），不改原计划、原结果和游标 |
| `--resume <根>` | 仅刷新汇总及下一步 |
| `--resume <旧 merged 批次> --plan-only` | 仅兼容旧模式 |
| `--ack <sha> --ack-reason <理由>` | 兼容旧批次的归档命令 |

每次 stdout 末行为 JSON。根汇总含 `report`、`batchId`、`acceptanceState`、`statusCounts`、`commitResults`、`pendingUiSlots`、`pendingPreparations`、`pendingCommitValidations`、`unverifiedUiSlots`、`workflowState`、`nextAction`。首次 `AWAITING_PREPARATION` 是正常中间态。`pendingUiSlots=[]` 不证明无需 UI。

本地流程回归（不访问工单、不启动软件、不推进游标）：

```powershell
& $python -B -m unittest discover -s .\daily -p 'test_*.py'
& C:\AutoTest\runtime\dotnet\dotnet.exe test .\runner\Vega.AutoTest.Runner.Tests\Vega.AutoTest.Runner.Tests.csproj
```

## 覆盖审核 JSON

控制器自动生成；手动排障时由 AI 阅读真实需求后填写。`scope.json.diff` 指向真实 diff。用 `Get-FileHash -Algorithm SHA256` 获取 diff 和 `preparation/workitems.json` 的哈希（十六进制小写）。每条验收场景分别登记：

```json
{
  "sha": "完整 HEAD SHA",
  "diffSha256": "实际diff的sha256",
  "workItemsSha256": "preparation/workitems.json的sha256",
  "requirements": [
    {
      "id": "wi1234-import-and-result",
      "workItems": [1234],
      "steps": ["合法授权登录目标 Simulator", "导入工单数据并执行目标流程"],
      "expected": "来自工单的可核对预期；注明输入、基准及断言对应关系",
      "uiRequired": true,
      "profiles": ["Vega300-HWv1"],
      "caseIds": ["已存在且真正覆盖该场景的用例ID"]
    }
  ]
}
```

- latest-head 批次中每条场景带 `sourceCommit`（控制器自动填写），用于逐提交结果归属。
- 没有工作项号时 `workItems=[]`，仍需分析 diff、写场景并选择用例。无 UI 的场景需在 steps/expected 说明为何逻辑测试足够。
- 补不齐的场景使用 `caseIds=[]` 和具体 `gap`，并附 `attemptEvidence`：

```json
"attemptEvidence": [{"path":"preparation/checks/case-logic.log","sha256":"实际文件SHA256","action":"编译并发现针对修复点的测试","outcome":"组件测试已发现，远端采集/UI场景仍缺具体环境或数据"}]
```

- `case-check.json` 由 `--check-cases` 生成，不手工伪造。附件只读保存并纳入冻结指纹，不执行附件内容。硬编码旧 worktree 的源码不允许进入冻结计划。冻结后拒绝测试定义/源码变化；冻结不会创建 PASS。

## 收集

- 按 **committer time** 取北京时间当天 00:00 至执行时刻的 master 全部可达提交（不只 first-parent），按 SHA 去重。
- 补收上次 `collectedHead` 之后新出现的提交，覆盖 23:30 之后的提交和回填日期的提交。
- 不重放历史待验提交：旧批次的 `NEEDS_CASE`、`ERROR`、`PENDING` 只保留在原批次报告中。
- 远端历史被改写时不执行，保留证据待人工审查，不会默默重置游标。
- `manifest.json` 固定目标 HEAD、窗口、每个提交的说明、工作项号、文件列表；原始 diff 保存在 `collected-diffs/`。

## 用例匹配

每个提交在需求分析时按以下线索选择用例：

1. **工作项** `wi:<id>`：从标题和正文提取工作项号（`#3661`、`AB#3661`、`Bug 3661`、`工作项 3661`、`Related work items: #3661`、`【3661】`；`Merged PR 4803` 里的 PR 号会排除），映射 registry 中 `workItems` 含该号的用例。
2. **路径标签** `tag:<name>`：`classify()` 的路径规则经 `scheduling.json` 的 `tagCases` 映射到回归探针，只算候选，不代表验收。
3. **未映射模块** `module:<目录>`：路径规则未覆盖的文件按模块分组，列入"需分析"。
4. 仅文档改动（`ve-docs/`、`*.md`）的提交记为 `NOT_APPLICABLE`。

有产品改动时，`baselineCases`（`daily-recent10-v3`、`daily-supplement`）作为回归底线照常跑，单独标注，不代替工作项验收。

执行：无界面用例去重后排序（工作项用例 → 路径探针 → 底线），每个用例一次 Runner 调用，总时长受 `headlessBudgetMinutes` 约束，超出记 `NOT_RUN_BUDGET`。界面层按机型配置分组，每个配置一次切换与授权，按 `profileOrder`（V100-PIC → V100-Coherent → V300-HWv1 → V300-HWv2 → V500）排列，当晚没有用例的配置不启动；用例机型来自 `scheduling.json` 的 `caseProfiles`。V300 HWv1 与 HWv2 永远是两个会话。`hardwareTags` 命中的提交列入"需真机验证"，从不自动执行。

## 判定

正式 verdict 只来自 Runner 的 `result.json`。

逐提交状态（根报告"逐提交测试结果"表 / `commitResults`）：

| 状态 | 条件 |
| --- | --- |
| `PASSED_AT_HEAD` | 该提交的全部场景都有用例，且这些用例在 HEAD 构建上全部 PASS；不代表在自己 SHA 上独立验收 |
| `FAIL` / `ERROR` / `BLOCKED` | 该提交任一用例的 Runner 原始结论 |
| `NEEDS_CASE` | 有场景缺可执行用例、需求数据或必需 UI/机型覆盖 |
| `PENDING` | 尚未冻结或用例尚未执行 |
| `NOT_RUN` | 流程已收尾但用例未得到 PASS 结果（如超出预算） |
| `NOT_APPLICABLE` | 仅文档改动 |

HEAD 槽位整体状态（原有表格）：`ACCEPTED`、`PARTIAL`、`FAIL`、`ERROR`、`BLOCKED`、`PENDING`、`NEEDS_CASE`、`NOT_APPLICABLE`。`summary.json` 的 `acceptanceState`：无提交 `NO_COMMITS`；有失败 `HAS_FAIL`；全部验收且已收尾 `COMPLETE`；其余 `INCOMPLETE`。它表示验收完整性，不是第二套 verdict。

要让一个新工作项能被每晚自动验收：写用例并登记 `registry.json`（`workItems` 填工作项号）；界面用例需固定机型时，在 `scheduling.json` 的 `caseProfiles` 里声明。提交说明带上工作项号即可被自动归属。

## 构建与环境纪律

- 不使用旧 DLL 验证新提交。构建前先停止本项目 Simulator 遗留进程并恢复会话配置，再在共享工作区 `daily-latest` 重建目标 HEAD 的 detached worktree，构建并核对提交与 Vega.dll、RecipeBatchEditor、ComponentConfigEditor 的 SHA256。不 reset/覆盖用户的 `C:\Vega` 工程。每轮日志在 `attempts/<attempt>/`。
- 准备阶段的构建经指纹核对后直接用于执行，同一批次不重复编译。
- 构建使用 SDK10、运行目标 .NET8；Simulator 构建排除 NI 原生 C++ 项目；未采用 locked restore，不能算锁文件门禁通过。Runner 自身未重编时 `builtFromCurrentSource=false` 原样保留，报告附独立构建证据说明 DLL 来源。
- `Build-DailyVega.ps1` 包含 `Vega.UI.RecipeBatchEditor` 构建步骤（主工程不自动生成该输出）。
- 远端 master 刷新到独立 bare 缓存。PAT 只在 git 子进程环境中使用，不进 URL、命令参数或产物。服务器目前只提供 HTTP，manifest 记录 `remoteTransport`；启用 TLS 后设 `VEGA_DAILY_REMOTE=https://...`。

## 界面会话纪律

UI 验证必须实际启动目标版本的 Vega.exe，在合法授权的 Simulator 会话中完成登录和目标界面操作，并核对实际输出。logic/单元测试、DLL 反射、构建成功、模拟服务或仅实例化 WPF 控件均不能替代。是否需要 UI 由用户要求和工作项场景决定，不能只看 registry 或 uiSlots。详细规则见 `AGENTS.md` 和 `AI操作手册.md` §0.1。

1. 配置修改前保存内容与 SHA256，Vega 停止后才改 `SelectedToolType`、`RootCondition`、`Features`、`SelectedProfiles` 和独立 `UserDataFolder`（`Start-ModelValidation.ps1 -Action Prepare`）。RootCondition 为 `<机型>,Simulator,GemSim,Gen2`；官方认证器里 Simulator 勾选、Gem 不勾选、Gen2。不得只改机型名称而漏掉 OuterToInner、硬件能力或用户数据目录，不得修改正式授权绕过机型检查。
2. 登录由测试宿主打开 `C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe`，按当前 Vega 的 RootCondition 和机器 ID，在「超级用户密码」页生成并读取密码，账号使用 `su`。密码只留在内存，不写入报告、剪贴板或仓库。`VEGA_AUTOTEST_USER` / `VEGA_AUTOTEST_PASSWORD` 仍可覆盖。认证器缺失或读不到密码时该会话记 BLOCKED。启动验证密钥按 `authenticator-flow.json` 处理。
3. 实际界面执行必须传入 `--session`，程序核对准备会话的提交目录、机型、profile 和配置文件校验值。每个会话有界执行，失败最多恢复重试一次，继续其他会话；finally 停止本轮进程、恢复配置并核对 SHA256。禁止触发真实硬件，GemSim 结果不能当作 SEPLAT/E87 Host 验收。
4. 不 push、不创建 PR、不写工作项状态。原始 Runner 报告不手改，历史批次不删除。

历史专项脚本归档在 `analysis/recovery-20261027/tools/`，不作为日常入口；旧版本测试工程仍被历史冻结计划引用，保留用于审计。
