# Vega 每日提交批次验证

## 2026-10-29 无人值守入口

实际夜间触发器是本机 Windows 任务 `VegaAutoTest-DailyValidation`，北京时间23:30直接执行项目程序，采用当前用户交互会话以支持真实 UI；原 Codex 定时聊天只读取结果。安装/修复任务使用 `daily/Install-DailyValidationTask.ps1`，不保存用户密码。控制器会自己定位桌面应用安装的 CLI，不依赖聊天临时注入的 PATH；`--runtime-check` 可以只检查该环境而不收集或运行测试。

```powershell
# 定时任务只启动独立控制进程；该进程负责整批准备、生成业务测试、执行和收尾
& .\daily\Start-DailyValidation.ps1
# 查看真实进度（不构建、不收集、不修改游标）
& $python -B .\daily\autopilot.py --status
# 明确恢复一个已有队列；历史专项验证应先新建 revalidation 批次
& .\daily\Start-DailyValidation.ps1 -Batch <根批次路径>
```

启动器的 STARTED 只证明监督进程已启动。批次 `controller/state.json` 记录真实阶段，`controller/processes/` 保留子进程归属和超时，`controller-launches/<启动>/result.json` 记录监督重试。聊天会话结束不影响后台推进。已有活动进程时只返回当前状态，避免重复构建。

模型生成器 `model_author.py` 使用已登录的本机 Codex CLI 默认可用模型（高推理），不额外读取或复制 API 密钥。模型只返回结构化测试源码及需求审核，没有 shell、写文件、多代理或 MCP 工具；程序校验相对路径、机型、用例ID和来源，再写入每次独立版本的测试项目。`preparation/model/` 保存请求、响应、真实错误；`authored.json` 保存生成的需求到用例映射。先生成业务测试，再编译/发现和实际运行，不把生成文字当成通过。

每个提交模型推理累计最多20分钟，源码请求有界且最后一轮必须产出，一次编译修正。单项失败保留实际错误、未验范围，继续其他提交。必需 UI 使用可执行 L2/FlaUI 测试实际启动目标软件、登录并操作目标流程，结束后核对配置恢复。没有实际业务结果或缺真实输入/硬件时不宣称完整验收。

控制进程会直接执行 `daily.py --resume <根> --check-complete` 的同一 CLI 实现，并保存原输出和退出检查。`FINISHED` 只证明队列没有未收尾项；是否验收通过仍看根 report/summary 和原始 Runner 结果。下方旧手动命令保留用于故障排查。

## 2026-10-28 新执行入口

新批次先收集，接着 `--resume <根> --prepare-all`，AI 审核并补齐**全部**提交的测试。对每项运行 `--check-cases <SHA> --case-ids <ID列表>`，通过实际编译/发现后再 `--freeze-commit <SHA> --review-file <JSON>`。全部计划准备好后统一 `--resume <根> --drain`；队列推进、失败后继续及收尾由代码执行。需要实际 UI 时 drain 返回 2，完成对应 ui-request 后再次 drain，最后 `--check-complete` 必须返回 0。准备阶段允许先处理后续 SHA，执行仍严格串行。

schemaVersion=5 的未覆盖场景除 gap 外必须包含真实尝试证据，例如：

```json
"attemptEvidence": [{"path":"preparation/checks/case-logic.log","sha256":"实际文件SHA256","action":"编译并发现针对修复点的测试","outcome":"组件测试已发现，远端采集/UI场景仍缺具体环境或数据"}]
```

`case-check.json` 由 `--check-cases` 生成，不手工伪造。新增适配测试按 SHA 和原产品测试程序集隔离，原断言保持不变，默认标记 partial-acceptance。附件在 preparation/attachments 中只读保存并纳入冻结指纹；不执行附件内容。硬编码旧 worktree 的源码不允许进入新冻结计划。详细规则以 AGENTS.md 的 2026-10-28 章节为准，下方旧命令用于兼容已有批次。

准备时某个提交失败，会在其 `preparation/authoring.json` 留下失败原因并继续准备其余提交；失败项仍须由 AI 补齐真实测试或有证据的缺口，不能当作准备成功。删除的测试源码不会再尝试从目标提交读取。

实际界面执行必须传入 `--session`，程序会核对准备会话的提交目录、机型、profile 和配置文件校验值。需要界面操作但确实受阻时，按 AGENTS.md 留下真实非空尝试文件及 `ui/<slot>/blocker.json`，重新 `--drain` 会收尾并推进下一提交，未验界面仍保留在 `unverifiedUiSlots`，不会变成通过。`--check-complete` 只用于队列根目录，不能与执行动作混用。

历史专项脚本归档在 `analysis/recovery-20261027/tools/`，不作为日常入口；旧版本测试工程仍被历史冻结计划引用，保留用于审计。

本地流程回归（不访问工单、不启动软件、不推进每日游标）：

```powershell
& $python -B -m unittest discover -s .\daily -p 'test_*.py'
& C:\AutoTest\runtime\dotnet\dotnet.exe test .\runner\Vega.AutoTest.Runner.Tests\Vega.AutoTest.Runner.Tests.csproj
```

其中 `$python` 使用下方示例中的固定 Python 路径。Runner 回归工程已加入 `Vega.AutoTest.sln`。

每日 23:30（北京时间）由本机 Codex 定时任务触发。默认 `individual`：收集当天及游标之后新出现的提交，每个提交在自己的 SHA 上独立准备覆盖、构建、运行及报告；不得跨提交共享 DLL 或 runId。根批次只是串行队列，产物在 `daily/runs/<batchId>/commits/<SHA>/`。正式 verdict 只来自 Runner 的 `result.json`。`--strategy merged` 仅保留旧的总体合并编排，不是当前每日默认流程。

机器与桌面需保持运行；界面会话需要可交互桌面和有效的机型授权。

## 定时任务必须完成的闭环

AI 负责在冻结前理解需求并复用/补写真实测试，Python 不会自动生成业务测试。新提交不必新增测试，但必须审核现有覆盖；不能只加工作项编号。准备最多 20 分钟/项、一次修正，补不齐则明确记录 gap，继续已有测试及其他项。完整纪律见 `AGENTS.md` 首节。

```powershell
$python='C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
cd C:\AutoTest\AutoTest\VegaAutoTest

# 1. 收集 + 候选计划。此命令返回不等于已执行！
# 数据库未运行时复用原数据启动（已运行则直接返回；不创建空数据库）
& .\daily\Ensure-DailyDatabase.ps1
& $python .\daily\daily.py --execute

# 2. 按根 summary.nextAction 顺序，为每个 SHA 准备并审核覆盖
& $python .\daily\daily.py --resume <根批次> --prepare-commit <SHA>
# 读 diff、preparation/workitems.json 和候选计划；AI 复用/补写真实测试、数据、机型和登记
# 保存下述 review.json，然后冻结；有缺口也必须具体说明，不能假登记
& $python .\daily\daily.py --resume <根批次> --freeze-commit <SHA> --review-file <review.json>

# 3. 在该 SHA 上独立构建和执行无界面测试
& $python .\daily\daily.py --resume <根批次> --execute --run-commit <SHA>

# 4. 读取该提交子目录的 execution-target.json 和 pendingUiSlots，逐个实际执行
& .\daily\Start-ModelValidation.ps1 -Model Vega100 -Profile PIC -RepoRoot <worktree> -Action Prepare
#    按 AGENTS.md 用官方认证器配置该机型并确认能登录，然后：
& $python .\daily\daily.py --resume <提交子目录> --run-slot ui-Vega100-PIC --session <SessionPath>
#    --session 给出时，会话结束后自动 Restore 并核对 SHA256；失败的会话记录后继续下一个

# 5. 认证失败也必须 finally Stop/Restore 并核对 SHA256；记录未验范围及证据后收尾
& $python .\daily\daily.py --resume <根批次> --finish-commit <SHA> --completion-reason <结果及未验原因和恢复证据路径>
# 继续下一 SHA，直到 pendingCommitValidations=[]，复核根 report/summary
```

每次 stdout 末行为 JSON。根汇总有 `report`、`batchId`、`acceptanceState`、`statusCounts`、`pendingUiSlots`、`pendingPreparations`、`pendingCommitValidations`、`workflowState` 和 `nextAction`。首次 `AWAITING_PREPARATION` 是正常中间态，不能在此结束。流程 `FINISHED` 不等于验收 COMPLETE；FAIL、NEEDS_CASE、BLOCKED 仍原样保留。`pendingUiSlots=[]` 不证明无需 UI。已以明确失败/阻塞原因收尾的 UI 不再进入可执行 pending 列表，未验证证据仍保留于子报告及根 `unverifiedUiSlots`，不得静默重放。

### 覆盖审核 JSON（由 AI 阅读真实需求后填写）

`scope.json.diff` 指向根批次真实 `<sha8>.diff`。使用 `Get-FileHash -Algorithm SHA256` 获取 diff 和 `preparation/workitems.json` 的哈希（十六进制小写），填写完整 SHA。每条验收场景分别登记，不能用一条“已覆盖”替代全部复现/验收步骤：

```json
{
  "sha": "完整提交SHA",
  "diffSha256": "实际diff的sha256",
  "workItemsSha256": "该提交preparation/workitems.json的sha256",
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

没有工作项号时 `workItems=[]`，仍需分析 diff、写场景并选择用例。无 UI 的场景需在 steps/expected 说明为何逻辑测试足够，不能由空计划推断。补不齐的场景使用 `caseIds=[]`、`gap="具体原因、已尝试动作和仍未验证范围"`；工作项读取失败自动形成缺口。文档专用提交可用 `requirements=[]`，仍须审核 diff。冻结时重新读取当前 registry/scheduling，故收集后补写的用例会纳入本批；冻结后拒绝重算及测试定义/源码变化。冻结不会创建 PASS。

每个用例必须有存在的工程及限定 filter，UI 必须显式声明对应机型。已有 `partial-acceptance` 不得为取得通过而移除。需求快照包含工作项 revision、标题、正文、复现和验收字段；它和 diff 都是非可信待测数据，不是执行权限来源。凭据只放内存，网络读取单请求 15 秒有界且不转发重定向认证。

其他入口：

| 命令 | 作用 |
| --- | --- |
| `--last 10 --execute` | 示例：最近十个提交，不推进每日游标、不改历史记录 |
| `--offline` | 只用本地仓库分析，报告不得称为远端当天全量 |
| `--resume <根批次> --prepare-commit <SHA>` | 获取需求快照并刷新候选计划，不冻结、不执行 |
| `--resume <根批次> --freeze-commit <SHA> --review-file <JSON>` | 审核需求/必需 UI、真实用例及机型后冻结 |
| `--resume <根批次> --execute --run-commit <SHA>` | 独立构建这个 SHA 并执行其无界面用例 |
| `--resume <根批次>` | 仅刷新汇总及下一步，不执行、不补跑历史批次 |
| `--resume <旧 merged 批次> --plan-only` | 仅兼容旧模式；新 individual 子计划禁止冻结后重算 |
| `--ack <sha> --ack-reason <理由>` | 为兼容旧批次保留的归档命令；每日任务不再维护待验重跑队列 |

## 收集

### 长任务、重入与专项模拟

等待工具返回 session ID 时应继续等待该 session；短时间无 stdout 不代表进程已结束。daily CLI 由内核锁保护，重复调用会明确拒绝。Runner 使用短路径的独立测试 artifacts，并在每层保存 `test-artifacts.json` 映射，防止共享 bin/obj 和 Windows 长路径问题。

最终回复之前执行 `daily.py --resume <根目录> --check-complete`；退出码 2 表示流程仍未走完。按 `nextAction` 继续，直到检查返回 0。该检查证明流程收尾，不改变测试验收结果。

用户明确授权专项模拟历史批次时，使用 `daily.py --revalidate-batch <原目录>` 创建其下新的 `revalidation-*` 候选队列，再逐项审核/冻结/执行；不会改原冻结计划、原 Runner 结果或每日游标。专项 UI 外部实测证据放在提交子目录 `ui/<slotId>/evidence.json`，Runner 仍负责正式判定。

- 按 **committer time** 取北京时间当天 00:00 至执行时刻的 master 全部可达提交（不只 first-parent），按 SHA 去重。
- 补收上次 `collectedHead` 之后新出现的提交，覆盖 23:30 之后的提交和回填日期的提交。
- **不重放历史待验提交**：上一批的 `NEEDS_CASE`、`ERROR`、`PENDING` 等结果只保留在原批次报告中，不会混入后续每日批次。每日批次只验证当天提交与上次游标之后新出现的提交。
- 远端历史被改写时不执行，保留证据待人工审查，不会默默重置游标。
- `manifest.json` 固定目标 HEAD、窗口、每个提交的完整说明、工作项号、文件列表；`<sha8>.diff` 保存原始改动。

## 归簇与编排

以下匹配规则在**每个提交内部**独立应用。合并模式才在多提交间去重；当前每日模式不会把多个 SHA 合并为一次 HEAD 验收。

每个提交按以下顺序归簇（一个提交可落在多个簇）：

1. **工作项簇** `wi:<id>`：从标题和正文提取工作项号（`#3661`、`AB#3661`、`Bug 3661`、`工作项 3661`、`Related work items: #3661`、`【3661】`；`Merged PR 4803` 里的 PR 号会排除）。同一工作项的多个提交合为一簇，映射 registry 中 `workItems` 含该号的用例。
2. **路径簇** `tag:<name>`：`classify()` 的路径规则，经 `scheduling.json` 的 `tagCases` 映射到回归探针。只算候选，不代表验收。
3. **未映射模块簇** `module:<目录>`：仅对没有工作项号的提交，把路径规则未覆盖的文件按模块分组，列入“需分析”。
4. 仅文档改动（`ve-docs/`、`*.md`）的提交记为无需验证。

有产品改动时，`baselineCases`（当前为 `daily-recent10-v3`、`daily-supplement`）作为每日回归底线照常跑，单独标注，不代替工作项验收。旧冻结计划保留原基线定义和结果；v3 只修正目录分隔符等价比较及版本化工程的测试宿主绑定，不降低断言数量。

执行顺序：

- **无界面**：用例去重后排序——工作项用例在前，路径探针其次，底线最后；同级按覆盖提交数从多到少。每个用例一次 Runner 调用，跑该用例全部非界面层（`logic100`/`logic300`/`logic300-hwv2`/`logic500`/`logic-none`/`logic` 等，每层独立机型宿主）。总时长受 `headlessBudgetMinutes` 约束，超出的用例记 `NOT_RUN_BUDGET`，相关提交保持待执行。
- **界面会话**：界面层（`uia`、`l1`、`l2/` 工程）按机型配置分组，每个配置一次切换与授权覆盖组内全部用例，按 `profileOrder`（V100-PIC → V100-Coherent → V300-HWv1 → V300-HWv2 → V500）排列，**当晚没有用例的配置不启动**。用例机型来自 `scheduling.json` 的 `caseProfiles`；未声明的挂到当晚已计划的第一个配置（都没有则用 `defaultUiProfile`），报告标注“结论只代表该机型”。V300 HWv1 与 HWv2 永远是两个会话。
- 界面用例仍由 `L2TestBase` 每条自行拉起并关闭 Vega；会话合并省的是机型配置切换和授权，不是进程启动次数。
- **真机**：`hardwareTags` 命中的提交列入“需真机验证”，写明需要的设备，从不自动执行。

## 逐提交判定

每个提交一行，列出所属簇、用到的用例与 runId、自己的状态：

| 状态 | 条件 | 完整验收 |
| --- | --- | --- |
| 已验收 `ACCEPTED` | 有工作项号，该工作项的登记用例（非 `partial-acceptance`）在目标 HEAD 构建上全部 PASS，且关联探针无 FAIL | 是 |
| 无需验证 `NOT_APPLICABLE` | 仅文档改动 | 是 |
| 失败 `FAIL` | 任一关联用例 FAIL（含路径探针）；报告给出同簇提交，需隔离或二分 | 否 |
| 工具异常 `ERROR` / 无法判定 `BLOCKED` | 工作项用例的 Runner 结论 | 否 |
| 待执行 `PENDING` | 尚待覆盖准备/冻结，或工作项用例还有层或界面会话未执行 | 否 |
| 部分验收 `PARTIAL` | 只有路径探针，或登记用例带 `partial-acceptance` | 否 |
| 缺用例 `NEEDS_CASE` | 经审核仍缺可执行用例、需求数据或必需 UI/机型覆盖 | 否 |

`summary.json` 的 `acceptanceState`：无提交 `NO_COMMITS`；有失败 `HAS_FAIL`；全部验收且队列已收尾 `COMPLETE`；其余 `INCOMPLETE`。它表示验收完整性，不是第二套 verdict。每日不维护历史待验队列，未验只留原报告。旧 merged 多提交合并结果不能直接定位缺陷引入者。

要让一个新工作项能被每晚自动验收：写用例并登记 `registry.json`（`workItems` 填工作项号）；界面用例需固定机型时，在 `scheduling.json` 的 `caseProfiles` 里声明。提交说明带上工作项号即可被自动归簇。

## 构建与环境纪律

不使用旧 DLL 验证新提交。`--execute` 先清理本项目专用 Simulator 遗留进程，从 manifest 的源缓存创建每次独立的 detached worktree，构建目标 HEAD 并核对提交与 Vega.dll SHA256。不 reset/覆盖用户的 `C:\Vega` 工程，不复用旧的共享输出目录。每轮日志在 `attempts/<attempt>/`。

构建使用 SDK10、运行目标 .NET8；Simulator 构建排除 NI 原生 C++ 项目；未采用 locked restore，不能算锁文件门禁通过。Runner 自身未重编，`builtFromCurrentSource=false` 原样保留，报告附独立构建证据说明 DLL 来源。

独立 worktree 还须构建 `Vega.UI.RecipeBatchEditor`（主工程不自动生成该输出）。`Build-DailyVega.ps1` 已包含该步骤，并将主程序及批量编辑器 DLL 的 SHA256 一并记入构建来源，避免 #3661 等探针在全新目录缺少引用。

远端 master 刷新到独立 bare 缓存，PAT 只在 git 子进程环境中使用，不进 URL、命令参数或产物。

## 界面会话纪律

**UI 验证必须实际启动目标版本的 Vega.exe，在合法授权的 Simulator 会话中完成登录和目标界面操作，并核对实际输出。** logic/单元测试、DLL 反射、构建成功、模拟服务或仅实例化 WPF 控件均不能替代。仅启动进程或打开主窗口也不算目标流程通过。

是否需要 UI 由用户要求和工作项场景决定，不能只看 registry 或 uiSlots。每日冻结计划缺少必需 UI 时保留缺口并记录 NEEDS_CASE/未验；用户单独要求专项复验时，应先在独立目录补齐 L2/UI 用例、机型和计划后执行，不能以旧计划无 slot 为由跳过，也不能改写历史批次。详细规则见 `daily/AGENTS.md` 和 `AI操作手册.md` §0.1。

报告须保存版本指纹、实际进程 PID/启动时间、机型/Simulator 配置、输入数据、关键操作截图或 UI 快照、结果对比及 Runner runId/result.json。未实际完成必需 UI 的项目不得完整验收，须明确原因；既有 logic PASS 仍只代表其原执行范围。

1. 配置修改前保存内容与 SHA256，Vega 停止后才改 `SelectedToolType`、`RootCondition`、`Features`、`SelectedProfiles` 和独立 `UserDataFolder`（`Start-ModelValidation.ps1 -Action Prepare`）。RootCondition 为 `<机型>,Simulator,GemSim,Gen2`；官方认证器里 Simulator 勾选、Gem 不勾选、Gen2。不得只改机型名称而漏掉 OuterToInner、硬件能力或用户数据目录，不得修改正式授权绕过机型检查。
2. 认证与登录按 `AGENTS.md` 和 `authenticator-flow.json` 用 computer-use 操作官方认证器；密码只在内存与子进程环境中使用（L2 登录读取 `VEGA_AUTOTEST_USER`/`VEGA_AUTOTEST_PASSWORD`），不写入报告或仓库。取不到合法授权时该会话记 BLOCKED，继续下一个。
3. 每个会话有界执行，失败最多恢复重试一次，继续其他会话；`finally` 停止本轮进程、恢复配置并核对 SHA256。无论成功失败都生成报告。禁止触发真实硬件，GemSim 结果不能当作 SEPLAT/E87 Host 验收。
4. 不 push、不创建 PR、不写工作项状态。原始 Runner 报告不手改，历史批次不删除。
