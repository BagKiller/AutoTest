# Vega_SW × Azure DevOps 测试管理集成实施方案（Agent 执行版）

> **文档状态**：实施规范 / 待按目标环境验证（不是“已实施”报告）  
> **编写日期**：2026-10-26  
> **实施对象**：`VegaAutomation/VegaAutoTest`、独立 Azure DevOps 适配层、Azure Boards / Test Plans / Pipelines  
> **目标读者**：Claude Code、vega-agent 或具备项目文件及本机命令执行能力的开发 Agent；兼供测试负责人审核  
> **实施原则**：先只读探测 → 本地 dry-run → 隔离试点 → 经授权写入 ADO → 分阶段上线。

## 0. Agent 立即执行指令（必读）

你是本方案的**实施 Agent**，不是重新设计 Vega 测试框架的 Agent。执行时：

1. **先读原始依据**：本文件、`VegaAutoTest/README.md`、`VegaAutoTest/AI操作手册.md`，再检查真实源码和目录。若本文件与后两份既有 Runner 契约冲突，**以实际实现及现有 Runner 契约为准，不擅自修改 Runner 判定语义**；记录差异。
2. **先做环境盘点，不猜部署方式**：识别 Azure DevOps Services / Azure DevOps Server / TFS、实际版本、Collection URL、项目名称、REST `api-version`、许可证与 API 权限。无法验证时，只实现离线组件，不调用不兼容的接口。
3. **不要一上来改代码或写 ADO**：先输出 `discovery-report.md`、变更清单、回滚步骤和 dry-run 计划；所有外部写操作默认为 `enabled=false`。只有环境真实、最小权限具备且取得明确授权后才进行 ADO POST/PATCH。
4. **实现仅新增适配层，优先不修改旧代码**：复用 `Vega.AutoTest.Runner`、`cases/registry.json`、`result.json`、`report.md`、`VerdictResolver`、`tools/autotest.py`。不在 Vega_SW 主仓库添加测试证据、配置或集成代码。
5. **自动化 verdict 只能读 Runner**：不得重新基于 TRX、stdout、断言计数、AI 推理或 Azure Test Result 倒推 PASS/FAIL。必须保留 `PASS / FAIL / BLOCKED / ERROR` 原始四态、`verdictCode`、`runId`、目标指纹。
6. **不越过安全边界**：不自动 push / 开 PR / 改工作项状态 / 创建产品 Bug / 操作硬件；不向 LLM 暴露 `POST /cmd/*`、硬件动作或任意 HTTP/命令执行。所有新写操作先做权限与人工审批。
7. **遇到信息缺失 fail-closed**：无有效映射、找不到 Test Point、多点匹配、API 不兼容、凭据缺失、结果契约不合法、旧二进制冒充新构建时不得发布 Passed、不得改变已有 Test Plan 结果。
8. **分阶段交付**：按本文第 13 节 P0–P5 顺序实施，逐阶段执行验收测试并输出报告。不要自行宣称环境已打通；只有真实运行证据才能证明。

**执行完成时至少交付**：代码改动列表、`discovery-report.md`、`compatibility.json`、`integration-test-report.md`、dry-run 样例、真实 `runId` / ADO Test Run ID（若已授权并实际执行）、失败或阻塞清单、回滚说明。

---

## 1. 范围、事实与不确定项

### 1.1 从现有文档确认的项目事实

| 维度 | 已确认事实 |
| --- | --- |
| 主程序 | Windows `Vega.exe`；主仓库与自动化工程相互独立 |
| 技术栈 | .NET 8、C#、WPF；测试用 xUnit、FlaUI / UI Automation |
| 主仓库根 | `D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR`（为原文档路径，在目标机器上实查） |
| 自动化根 | `D:\Code\Vega\VegaAutomation\VegaAutoTest` |
| 现有工程 | `Vega.AutoTest.sln`：`harness` / `spike` / `l2` / `runner` 四工程 |
| 测试登记 | `cases/registry.json`：工作项 ID → `logic`、`uia` 具体用例 |
| 唯一判定 | `Vega.AutoTest.Runner` → `VerdictResolver.cs` → `runs/<runId>/result.json` |
| 报告 | 同目录 `report.md`，仅为 `result.json` 的人读版本 |
| L0 编排 | `code_indicator/vega-agent`，已有 list/start/status 异步验证工具 |
| 运行时查询 | 18901 仅只读，18900 仅文档明确的三个真实指标端点 |
| 外部依赖 | MongoDB `127.0.0.1:27017`；18901 插件/URLACL 在部分测试中有要求 |
| 已登记样例 | `wi3478`、`wi3547`、`wi3549`（以目标机器真实 `list-cases` 为准） |
| 当前文档记录的遗留 | AgentControlService 正式入库待确认、类级 Fixture、L1 API 主通道用例、MCP server 等 |

### 1.2 目前**未确认**，Agent 必须探测或登记为阻塞项

- 具体 Azure DevOps 产品与版本：截图出现 `DefaultCollection`，**提示**可能是 Server/TFS，但不能仅凭截图下定论。
- 目标 ADO 实例完整 `collectionBaseUrl`、`project`（截图为 `Vega_SW`）、使用的 Process 模板、REST API 兼容版本。
- Test Plans 许可、测试计划创建/管理权限、项目中当前已有的 Plan/Suite/Test Case/Point、Team 上下文。
- ADO 测试结果 API 在实际版本的可用性，以及字段行为（尤其 `Blocked`、`Error`、Test Run/Point 历史关系）。
- Azure Repos 中 `Vega_SW` 和 `VegaAutomation` 的实际仓库名称、默认分支及 Agent 能否读取；截图不能证明二者均已在 Repos。
- Pipelines YAML / Classic 是否可用、Windows 自托管 Agent 能否配置交互式桌面、工控机访问权限。
- 现有 PAT 是否已作废和轮换；README 记录过泄露风险，**不得复用已泄露凭据**。
- Test Case ID、Plan ID、Suite ID、Point ID：本文件中的演示 ID 均为占位，**禁止写入生产系统**。

### 1.3 明确范围

**要实现**：
- Azure Test Plans 的手工用例组织与版本回归；
- 原有 Runner 的本机/Pipeline 调度与四态结果展示；
- `Work Item ↔ Runner Case ↔ ADO Test Case ↔ Test Point` 显式映射；
- 结果证据归档、可追溯构建指纹和版本报告；
- 在授权和 API 兼容前提下的 Test Run 结果同步；
- Bug 的证据关联和人工闭环，发布门禁。

**不做**：
- 新写一套 xUnit/FlaUI 测试框架，或改 `VerdictResolver` 判定；
- 自动创建/关闭 Bug、自动推送代码、自动发 PR；
- AI 执行真实硬件动作、绕过安全互锁；
- 未经批准修改 `Vega_SW` 产品源码或把测试文件写进被测主仓库；
- 在未确认服务端版本时强制调用 7.1 REST API；
- 将旧 EXE 的 PASS 等价为当前源码发布验收通过。

---

## 2. 最终架构和职责边界

```text
                               ┌────────────────────────────┐
                               │ Azure Boards               │
                               │ Requirement / Bug / Task   │
                               │ WI 3478 / 3547 / 3549     │
                               └────────────┬───────────────┘
                                            │ 明确关联
                               ┌────────────▼───────────────┐
                               │ Azure Test Plans           │
                               │ Plan / Suite / Test Case   │
                               │ Point = Case × Configuration│
                               └────────────┬───────────────┘
                                            │ 手工 / 自动化
                  ┌─────────────────────────┴─────────────────────────┐
                  ▼                                                   ▼
        ┌───────────────────┐                          ┌─────────────────────────┐
        │ 手工 Test Runner  │                          │ Pipelines / 本地 Agent   │
        │ 操作、记录、Bug证据│                          │ 触发/调度/归档            │
        └───────────────────┘                          └──────────┬──────────────┘
                                                                  ▼
                                                     ┌─────────────────────────┐
                                                     │ ADO Integration Adapter │
                                                     │ 配置映射/验证/发布       │
                                                     └──────────┬──────────────┘
                                                                  │ CLI 调用
                                                     ┌────────────▼────────────┐
                                                     │ 原有 Vega.AutoTest.Runner│
                                                     │ registry → logic/uia     │
                                                     │ xUnit + FlaUI + 18901    │
                                                     └────────────┬────────────┘
                                                                  ▼
                                                     ┌─────────────────────────┐
                                                     │ 原有 VerdictResolver    │
                                                     │ PASS/FAIL/BLOCKED/ERROR │
                                                     └────────────┬────────────┘
                                                                  ▼
                                                     ┌─────────────────────────┐
                                                     │ result.json / report.md │
                                                     │ TRX / assert-* / logs   │
                                                     └─────────────────────────┘
```

### 2.1 两套“层”的区别

- **原架构 L0/L1/L2**：L0 AI 编排、L1 Runner 确定性验证、L2 被测 Vega.exe。
- **Runner 的 `logic` / `uia`**：registry 下的不同测试执行层；不是上面架构层级的同义词。
- **项目测试管理分层**（可选）：单元/组件/真实 UIA/设备集成/版本回归；仅作测试计划分类，不重命名或替换 Runner 原本的层。

### 2.2 真相来源（Single Source of Truth）

| 事实 | 唯一可信来源 |
| --- | --- |
| 自动验证是否通过 | Runner `result.json.verdict` |
| 原因和错误分类 | `verdictCode`、`verdictReason` |
| 验证对象 | `result.json.target`（Commit、分支、dirty、EXE、构建时间等） |
| 实际断言 | `result.json.assertions[]` |
| 可追溯执行 | `runId` 和完整证据目录 |
| 测试设计/人工步骤/验收范围 | ADO Test Case / Test Plan（不覆盖 Runner 判定） |
| 工作项状态 | Azure Boards 当前实际记录（仅人或批准流程可改） |
| ADO 结果显示 | Test Run / Test Result / Test Point（是投影，不可反推 Runner verdict） |

### 2.3 四态判定和发布门禁分离

| Runner verdict | 对外原文 | 展示建议 | CI 门禁 | 创建产品 Bug？ |
| --- | --- | --- | --- | --- |
| `PASS` | 已验证通过（附 runId；并说明构建指纹） | Passed | 还须检查新构建、配置、证据完整性 | 否 |
| `FAIL` | 未通过，列明失败断言 | Failed | 阻断 | **不自动**；人工确认后关联/提交 |
| `BLOCKED` | 无法验证，列明阻塞原因 | Blocked/Inconclusive（按实际 API） | 阻断 | 否；可提环境问题工单 |
| `ERROR` | Runner 异常，需人工介入 | Error/Inconclusive（按实际 API） | 阻断 | 否；可提测试基础设施工单 |

**特别规则**：
- `PASS` 并不自动代表“本版本可发布”。`target.builtFromCurrentSource=false` 时必须显示：**未使用当前源码重编，结论仅对旧二进制有效**。`OK_STALE_BUILD` 一律不能满足正式发布门禁。
- `channels.l1Api.available=false` 时，若本次 oracle 是可跳过的弱断言，必须明确显示“未进行 L1 交叉验证”，不得写“强验证通过”。
- `summary.passed` 可以少于 `summary.total`，因为有 SKIP；不能把 `7/8` 误解为有 1 个 FAIL。
- `ZERO_ASSERTIONS_MATCHED` 是 `BLOCKED` 而不是 PASS，即使 TRX 测试显示绿。
- `NO_CASE_REGISTERED` 表示自动化覆盖缺失，不意味着产品失败；先登记用例再重跑，不能直接标绿。
- `runId` 缺失、契约不合法、ExitCode 不一致时一律不允许结果发布。

---

## 3. 对现有工程的不可破坏契约

### 3.1 目录与命令（以现场校验为准）

```powershell
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
Set-Location "D:\Code\Vega\VegaAutomation\VegaAutoTest"

dotnet build Vega.AutoTest.sln
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- doctor
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- list-cases --json
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers logic
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers uia
# 当前源码验证（仅在隔离、批准的构建工位使用）
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --rebuild
```

Runner stdout **最后一行**为 `result.json` 的路径；同目录 `report.md`。退出码 `0/1/2/3` 分别对应 `PASS/FAIL/BLOCKED/ERROR`。具体代码以现场 CLI 实现为准。

### 3.2 result.json 输入契约

下方为**字段结构示例**（来自 README 结构，示例值不是实时测试结论）：

```json
{
  "schemaVersion": "1.0",
  "runId": "20260916T085024Z_wi3478_20af65a",
  "workItemId": 3478,
  "caseId": "wi3478",
  "verdict": "PASS",
  "verdictCode": "OK",
  "verdictReason": "OK: 7/8 断言通过",
  "startedAt": "2026-09-16T08:50:24Z",
  "finishedAt": "2026-09-16T08:51:52Z",
  "target": {
    "repoRoot": "...",
    "gitCommit": "<real-commit>",
    "gitBranch": "master",
    "gitDirty": false,
    "vegaExe": "...\\Vega.exe",
    "rebuilt": false,
    "builtFromCurrentSource": false
  },
  "channels": {
    "l1Api": { "available": false, "reason": "..." },
    "l2Uia": { "available": true, "reason": null },
    "assertions": { "available": true, "reason": null }
  },
  "assertions": [
    { "id": "a1", "kind": "ui-value", "result": "PASS", "expected": "...", "actual": "..." }
  ],
  "summary": { "total": 1, "passed": 1, "failed": 0, "skipped": 0 },
  "tests": [],
  "evidence": { "dir": "runs/<runId>", "files": [] },
  "exitCode": 0
}
```

适配层必须 **parse → validate → project**，不能“读 stdout 中有 PASS 就算成功”。校验项：

1. 文件必须存在，UTF-8 JSON 合法，`schemaVersion` 可识别。
2. `runId` 非空且目录与 Runner 实际输出一致；`workItemId` / `caseId` 与本次请求及 registry 匹配。
3. `verdict ∈ {PASS, FAIL, BLOCKED, ERROR}`，`exitCode` 与 Runner 进程状态一致；对外退出码可统一为门禁失败，但源值必须记录。
4. `target.gitCommit`、`builtFromCurrentSource`、`target.gitDirty` 和目标 EXE 信息存在，证据路径**必须限制在允许的 runs 根内**，阻止路径遍历。
5. `assertions`、`summary` 结构与契约自检一致；不得替 Runner 重算结果。`CONTRACT_VIOLATION` 视为 ERROR。
6. 原始 `result.json` 必须逐字节保留；适配层另生成 `publication-manifest.json`，不能回写原始 JSON。
7. 若 stdout、退出码或 JSON 互相矛盾，将此次执行标为 **integration failure**，不得写 ADO Passed。

### 3.3 registry 契约

- 登记仍采用 `cases/registry.json` 的原有结构；工作项号如 3478 对应 `wi3478`。
- `logic` filter 仅匹配 `WorkItem{id}Spike`；`uia` filter 仅匹配 `WorkItem{id}L2`，禁止只填裸工作项号造成误匹配。
- `minAssertions` 是 Runner 的防假阳性保护，适配层不能放宽。
- 新用例优先使用现有 `UiExpect`、`AssertionRecorder` 和 `L2TestBase`；运行证据不写主仓库。
- UIA 测试关闭 xUnit 并行，遵守单实例 Mutex、18901 端口和窗口生命周期约束。

### 3.4 与“主仓库零写入”的边界

- **绝不写入**产品源码、测试日志、映射文件、集成配置到 `Vega_SW*` 主仓库。
- 原 `--rebuild` 会通过现有构建脚本产生 `Vega.exe`/编译输出；这属于原流程的已知构建副作用，不等于可以任意写主仓库。正式 CI 优先使用**一次性隔离 checkout/工作目录**并校验 `git status`；若现场重建脚本必须在特定目录运行，先经过构建负责人批准并保证无 tracked 源文件改动。
- 不改变 `Vega.BL.AgentControlService/scripts/run-vega-with-plugin.ps1` 的既有构建约束（比如 sln 构建方式、插件编译）而另起一套构建命令。

---

## 4. Azure DevOps 资源模型与映射策略

### 4.1 四种 ID，不得混用

| ID | 示例 | 来源 | 作用 |
| --- | --- | --- | --- |
| Work Item ID | `3478` | Boards 的需求/Bug/Task | 表示待验证目标 |
| Runner Case ID | `wi3478` | `registry.json` | 选中原有自动化用例 |
| ADO Test Case ID | `<actual-case-id>` | Test Plans 创建的测试工作项 | 测试设计与追溯 |
| Test Point ID | `<actual-point-id>` | 某 Plan/Suite 中 Case×Configuration（及分配信息） | 一次计划内具体执行位置 |

**禁止**用 `Work Item ID` 直接作为 Test Case ID；禁止假设一个 Test Case 只对应一个 Test Point。必须精确到目标 Plan/Suite/Configuration/Point。

### 4.2 Test Plans 结构（第一批试点）

```text
Test Plan: Vega_SW_Automation_Integration_Pilot
  ├─ Static Suite: Inspection
  │    └─ Test Case: [WI3478] 检测区域参数联动
  ├─ Static Suite: Operation
  │    └─ Test Case: [WI3547] 操作菜单强制中断替换四项
  └─ Static Suite: BSS
       └─ Test Case: [WI3549] BSS default 保存加载往返
```

以上是**待创建的示例计划**，创建前先检查是否已存在等价计划，避免重复建设。正式 V1.x 回归计划再按模块或需求套件设计。

### 4.3 独立映射配置（示例，不含真实 ADO ID）

新建 `azure-devops-integration/config/test-case-map.sample.json`：

```json
{
  "schemaVersion": "1.0",
  "project": "Vega_SW",
  "mappings": [
    {
      "workItemId": 3478,
      "runnerCaseId": "wi3478",
      "adoTestCaseId": null,
      "planId": null,
      "suiteId": null,
      "configurationId": null,
      "testPointId": null,
      "layers": ["logic", "uia"],
      "requiredForRelease": true
    },
    {
      "workItemId": 3547,
      "runnerCaseId": "wi3547",
      "adoTestCaseId": null,
      "planId": null,
      "suiteId": null,
      "configurationId": null,
      "testPointId": null,
      "layers": ["uia"],
      "requiredForRelease": true
    },
    {
      "workItemId": 3549,
      "runnerCaseId": "wi3549",
      "adoTestCaseId": null,
      "planId": null,
      "suiteId": null,
      "configurationId": null,
      "testPointId": null,
      "layers": ["uia"],
      "requiredForRelease": true
    }
  ]
}
```

实施规则：

- `null` 是必须由 ADO 实查后填充的字段；**禁止凭空猜值**。
- `workItemId` / `runnerCaseId` 必须与真实 `list-cases --json` 可交叉验证。
- 在相同 `(planId,suiteId,adoTestCaseId,configurationId)` 范围内查找 Point，匹配 **0 个或多个**时 fail-closed；禁止取列表第一条。
- 单个 Runner case 可关联多个 ADO 配置/Point，但**同一次结果只能投影到实际执行配置一致的点**；若 Runner 结果中没有硬件/环境配置证据，不要将它扇出到多个配置。
- 映射变化需代码审查/人工确认，生产映射不可自动覆盖。

### 4.4 手动用例与自动用例的分工

- ADO 手动 Test Case 写清：前置条件、操作步骤、期望、环境配置、验收标准。测试人员在 Test Runner 记录 Passed / Failed / Blocked 与截图。
- 已自动化的关键需求可同时有 Runner case，但手工执行和自动运行要分开留痕，不互相伪造执行历史。
- 普通开发单元测试可只发布到 Pipeline Tests；关键验收/回归测试按映射进入 Test Plans。
- 已有关联的需求/Bug 用对应的 Work Item 关系关联 Test Case；在实际 Process 和权限确认后使用受支持的链接类型，不能把所有对象简单设置为 Parent/Child。

---

## 5. ADO 服务兼容性与权限探测（P0 强制）

### 5.1 识别服务端

1. 读取现有配置中**非秘密**的 ADO URL/项目名；不要打印 PAT。
2. 调用能成功授权的**只读接口**检查 `collectionBaseUrl`、Project、服务版本；若接口版本不明确，从保守 API 版本试探，失败时记录状态码与响应摘要（脱敏），不要暴力探测。
3. 如检测到 `DefaultCollection`，按 Server/TFS URL 规范处理，例如 `http(s)://host:port/tfs/DefaultCollection` 或现场实际路径，**不可硬拼** `https://dev.azure.com/...`。
4. 检查使用的是 YAML Pipeline、Classic Pipeline 或仅有 Builds；据此生成适配入口。不要以 YAML 一定支持为前提。
5. 检查 Test Plans 许可及创建/读取 Test Plan、Test Suite、Test Case、Test Run 的权限；权限不足则完成本地集成，标为 `ADO_PERMISSION_BLOCKED`。

### 5.2 REST API 版本基线（微软文档，必须与现场核对）

| 目标服务 | 典型兼容的 REST API 基线 |
| --- | --- |
| Azure DevOps Services | 按当下官方稳定 API 版本（示例 7.1） |
| Azure DevOps Server 2022.1 | 7.1 |
| Azure DevOps Server 2022 | 7.0 |
| Azure DevOps Server 2020 | 6.0 |
| Azure DevOps Server 2019 | 5.0 |
| TFS 2018 Update 3 | 4.1 |

不保证旧版每个 `TestPlan` 新命名空间都支持；实现时对目标操作逐个做兼容性测试，优先采用该版本官方支持的 `Test` API。不能使用时只启用 Artifact + Pipeline Tests，不做 Test Point 同步。

### 5.3 推荐配置

`azure-devops-integration/config/ado-settings.sample.json`：

```json
{
  "schemaVersion": "1.0",
  "collectionBaseUrl": "<FILL_FROM_DISCOVERY>",
  "project": "Vega_SW",
  "apiVersion": "<NEGOTIATED_API_VERSION>",
  "readOnly": true,
  "writeTestResultsEnabled": false,
  "createTestPlansEnabled": false,
  "modifyWorkItemsEnabled": false,
  "createBugsEnabled": false,
  "publishArtifactsEnabled": false
}
```

生产配置需忽略到 `.gitignore`（根据实际是否包含敏感信息确定）；不提交凭据或真实私有服务地址到公共仓库。所有设置采用**显式启用、最小权限**原则。

### 5.4 凭据管理

- 本地只从会话环境变量 `AZURE_DEVOPS_PAT` 读取 PAT；不要回显、写日志、写 Markdown、写样例配置。
- Pipeline 优先使用受支持的作业身份 / `System.AccessToken`，权限不足时由管理员配置最小权限凭据；服务端版本不同需验证支持与认证策略。
- README 曾记录 PAT 泄露与待轮换：**先核实已轮换，未轮换则立即停用旧凭据并通知管理员**。
- 日志脱敏：Authorization Header、PAT、密码、设备登录凭据、Connection String、个人信息。
- 只允许 HTTPS（除非企业内部受控环境获明确例外）；验证服务端证书，不设置全局跳过 TLS 校验。

---

## 6. 适配层设计（独立于原 Runner）

### 6.1 建议工程目录

```text
D:\Code\Vega\VegaAutomation\
├── VegaAutoTest\                        # 原有，不重构
├── code_indicator\                       # 原有，不扩权
└── azure-devops-integration\             # 新增
    ├── README.md
    ├── config\
    │   ├── ado-settings.sample.json
    │   ├── test-case-map.sample.json
    │   └── regression-suites.sample.json
    ├── src\
    │   ├── discovery\                     # 服务兼容性、许可/权限、映射只读验证
    │   ├── runner_adapter\                # 启动 Runner、读结果、契约校验
    │   ├── mapping\                       # ADO IDs 与 registry 映射验证
    │   ├── test_publisher\                # 可选 Test Run/Result 同步
    │   ├── artifact_publisher\            # 证据压包、manifest、发布
    │   ├── release_aggregator\            # 多 run 的版本报告
    │   └── cli\                           # discover / verify / publish / aggregate
    ├── pipelines\
    │   └── vega-ui-regression.example.yml
    ├── tests\
    │   ├── fixtures\                      # 合成 result.json 与假 ADO 响应
    │   ├── unit\
    │   └── contract\
    └── out\                               # gitignore: dry-run、兼容探测、非秘密报告
```

可选 C# / .NET 8 或 Python（以现场依赖及团队维护能力选择）；**不要为了接入 ADO 给 Runner 本体引入 Azure SDK 依赖**。优先原生 HTTP Client + 适当封装，避免对旧 Server 强依赖新 SDK。

### 6.2 命令行接口（新适配层目标，不是现有命令）

```text
vega-ado discover --read-only
vega-ado validate-map --map <file>
vega-ado verify --work-item 3478 --layers uia --mode local --dry-run
vega-ado verify --work-item 3478 --layers uia --mode local --execute
vega-ado publish --result <result.json> --map <file> --dry-run
vega-ado publish --result <result.json> --map <file> --execute --approval <token-or-approved-context>
vega-ado aggregate --suite <suite.json> --results-dir <path>
```

`--approval` 的具体机制必须接入真实用户授权或受控 Pipeline approval；不能以硬编码字符串、伪造令牌代替审批。若当前无法实现可信审批，**只实现 dry-run**，不要提供不可控的 `--execute`。

### 6.3 模块职责

**RunnerAdapter**：
- 使用绝对路径定位 `VegaAutoTest`、`VEGA_SW_ROOT`，调用 `dotnet run ... -- verify`。
- 记录进程 exit code、stdout/stderr 和耗时；保留完整原始输出到适配层日志。
- 从有效的 stdout 最后一条路径提取 `result.json`，必须检查文件确在受控 runs 根内。
- 运行超时要终止对应**已启动的子进程树**（不要误杀其他人的 Vega.exe），记录集成层 TIMEOUT；若无 Runner JSON 则不得伪造 Runner verdict。
- 验证结果契约并产生 `publication-manifest.json`。不要修改旧结果。

**MappingResolver**：
- 检查映射与 registry 匹配；按 `planId`、`suiteId`、`configurationId`、`adoTestCaseId` 查唯一 Point。
- 验证 Suite/Case/Configuration/Point 关系；任何歧义拒绝发布。

**EvidencePackager**：
- 将 `result.json`、`report.md`、TRX、`assert-*.json`、`_wi*_l2.txt`、构建日志和 UI 截图（如存在）打包。
- 生成 SHA-256 索引，含相对路径、文件大小、hash；不读取证据目录外的文件，不泄露凭据。
- `runs/` 本地基线保护，归档成功前不清理；保留期由项目负责人确认。

**AdoTestPublisher**：
- 默认只做预览；只在认证/版本/授权/唯一 Point 校验通过时发布。
- 创建新的 ADO **Test Run**，追加/更新对应 Test Result，完成 Run，保留历史；默认**不直接 PATCH Test Point 的 outcome**以免覆盖历史上下文。
- 原始 Runner verdict、`verdictCode`、runId、旧构建警告写入 Test Result 评论和 Artifact；需要时关联已存在 Bug，但不自动新建。
- 发布失败不改变本地 Runner verdict；单独记录 `publishStatus=FAILED`，可幂等重试。

**ReleaseAggregator**：
- 逐个收集已完成的 `result.json`，按工作项和环境聚合，不重新判定单项结果。
- 计算版本是否满足人为定义的**发布规则**，输出 `release-gate.json`、`release-report.md`；“门禁决策”与 Runner 的“四态验证”必须不同字段。

### 6.4 publication-manifest.json（建议新定义）

```json
{
  "schemaVersion": "1.0",
  "runnerRunId": "<runId-from-real-result>",
  "runnerResultPath": "<absolute-result-json-path>",
  "runnerVerdict": "BLOCKED",
  "runnerVerdictCode": "DEVICE_NEVER_IDLE",
  "builtFromCurrentSource": false,
  "sourceGitCommit": "<from-result.target.gitCommit>",
  "mapping": {
    "workItemId": 3478,
    "runnerCaseId": "wi3478",
    "adoTestCaseId": null,
    "planId": null,
    "suiteId": null,
    "configurationId": null,
    "testPointId": null
  },
  "ado": {
    "publicationMode": "dry-run",
    "testRunId": null,
    "testResultId": null,
    "publishStatus": "NOT_ATTEMPTED"
  },
  "evidence": {
    "sha256Manifest": "<artifact-sha256-manifest-file>"
  }
}
```

此文件作为新适配层的**发布状态记录**，不是 Runner 的 `result.json` 的替代品。

---

## 7. Test Run 同步：正确的 REST 实施顺序

> 以下基于微软公开 `Test` REST 7.1 的**接口形态**，不是对你们目标服务器的接口已验证承诺。Agent 必须将 `api-version` 替换为 P0 探测支持版本，并对各个字段在实际 Server 上测试。

### 7.1 首选：“建 Run → 写 Result → 完成 Run”

1. `GET` 查询计划、套件、测试用例及 Test Points（**只读**）。
2. 将 `(planId,suiteId,adoTestCaseId,configurationId)` 解析为**唯一** `testPointId`。
3. 查询本地 `publication-manifest` / 已发布记录，按 `(runnerRunId,planId,pointId)` 幂等防重；若已有 ADO Run，优先读取确认状态，避免重复创建。
4. 在授权前输出 `POST/PATCH` 预览及目标 ID，人工审批。
5. `POST /_apis/test/runs`，body 包含 `name`、`automated=true`、`plan.id`、`pointIds:[pointId]`；可用的 Build/Configuration 字段按服务端兼容情况填写。
6. **先 GET 创建 Run 后的结果列表**；若已有与 Point 对应的结果，按该结果真实 `id` `PATCH /_apis/test/runs/{runId}/results`；若没有，再依据服务端支持情况 `POST` Add Result。绝不盲目既 POST 又 PATCH 导致重复测试结果。
7. 将 Runner 结果按兼容语义投影：`PASS→Passed`，`FAIL→Failed`，`BLOCKED→Blocked`（不支持时 Inconclusive），`ERROR→Error`（不支持时 Inconclusive）。原始四态必须在评论/manifest 保留。
8. `PATCH /_apis/test/runs/{runId}` 设置 `state=Completed`（或 API 要求的结束语义）；再 GET 校验服务器保存的记录与 Point / Run / Result 关联正确。
9. 将真实 `adoTestRunId`、`adoTestResultId`、链接和 `publishStatus` 写入适配层 manifest；归档原始 run 证据。
10. 任一阶段失败：记录失败阶段、HTTP 状态（脱敏）、可恢复状态；**不自动创建 Bug、不修改 Work Item State、不将 Runner verdict 改写为 ERROR**。

### 7.2 7.1 API 路径参考（路径仅作规范）

```text
GET   {collectionBaseUrl}/{project}/_apis/test/Plans/{planId}/Suites/{suiteId}/points?api-version={version}
POST  {collectionBaseUrl}/{project}/_apis/test/runs?api-version={version}
GET   {collectionBaseUrl}/{project}/_apis/test/Runs/{adoRunId}/results?api-version={version}
POST  {collectionBaseUrl}/{project}/_apis/test/Runs/{adoRunId}/results?api-version={version}
PATCH {collectionBaseUrl}/{project}/_apis/test/Runs/{adoRunId}/results?api-version={version}
PATCH {collectionBaseUrl}/{project}/_apis/test/runs/{adoRunId}?api-version={version}
```

**注意**：`collectionBaseUrl` 在 Services 与 Server 完全不同；不能复制示例 endpoint 后直接执行。API 返回字段和 Server 支持状况以实际探测为准。

### 7.3 不能直接 PATCH Point 的原因

虽然 Azure DevOps 存在修改 Test Point outcome 的 API，但直接更新 Point 可能缺少完整的 Run/Result/Build/断言证据关联，且可能覆盖当前汇总状态。**本方案要求真实 Test Run 优先，直接 PATCH Point 非默认且须单独设计和审批**。

### 7.4 同步冲突与幂等

- ADO 侧多次发布**同一个** `runnerRunId + planId + pointId`：只允许一条逻辑发布，失败重试时读远端比对，不生成多条“重复通过”。
- 如果 Runner 重新执行获得新 `runId`：这是一次新的验证，允许新 Test Run，不能覆盖原始证据。
- 同一 Point 若有人工执行记录，自动化同步应新建独立 Run，不能直接改人工历史。
- 远端 Run 创建成功但本地断网：重新检查远端 Run/名称/评论标识和本地 manifest，对齐后恢复，而非无脑重建。
- 若查询结果数量与预期不符或 API 不支持，停留在 dry-run / Artifact-only 模式。

---

## 8. Azure Pipelines 与交互式 UIA 工位

### 8.1 两条执行通道

| 通道 | 内容 | Agent 模式 | 建议触发 |
| --- | --- | --- | --- |
| `logic` | Spike/ViewModel 快速验证 | 可在合适的 Windows Build Agent 中执行；可能依赖主仓库编译条件 | PR/每日/人工 |
| `uia` | FlaUI 真实 Vega GUI | **Windows 交互式自托管 Agent**（自动登录、真实桌面） | 人工/非工作时段/版本回归 |

不建议从一开始就把所有 UIA 用例塞进 PR 必过门禁。先保证环境稳定、case 速度和证据可靠性，再逐渐提高覆盖。

### 8.2 UIA 工位最低条件

- .NET 8 SDK、正确的 Visual Studio/MSBuild 和 Vega 依赖工具链；目标 EXE 与插件、MongoDB 就绪。
- Agent 以**交互式进程**运行，而非普通不可交互的服务；确认自动登录安全政策、物理访问控制和锁屏策略。
- 保持固定分辨率/DPI，避免已知的 150% 缩放/坐标虚拟化问题重现；优先 FlaUI `InvokePattern`，Click 仅兜底。
- 确保用户会话可见；RDP 断开可能锁定会话，应按公司 IT 安全策略部署；禁止未经许可更改系统登录安全设置。
- 一台工位同一时刻一个 UIA Job（Agent Pool 并发、运行锁二级约束）；不得同时人工操作测试桌面。
- 不用此 Pipeline 接入运动控制等硬件动作；18901 保持只读，避免把工控机当普通 CI 机器任意调用。

### 8.3 Pipeline 实施原则

- **试点 1**：手动触发，执行单个 WI（如 #3478），`--layers uia`，不自动 `--rebuild`，确保结果能落盘和归档，并明确提示旧 EXE。
- **试点 2**：隔离构建环境执行 `--rebuild`，确认 `target.builtFromCurrentSource=true`，再启用“当前源码”验收。
- **试点 3**：发布 TRX（如目标 Server 支持 `PublishTestResults@2`）到 Pipeline Tests，**但展示为 Runner 原始判定的辅助证据**。
- **试点 4**：授权后将结果同步至 Test Plans；Test Run 关联成功后再启用版本计划。
- **正式流水线**：主仓库和测试仓库的 Commit 都应锁定；若采用现有工位固定路径，必须注明其不可复现风险，不可将 Build ID 直接等同于 EXE 的构建来源。

### 8.4 示例 YAML（**仅示意，先核对是否支持 YAML / Task 版本**）

```yaml
trigger: none
pr: none

pool:
  name: Vega-UIA-Test-Agents

parameters:
- name: workItemId
  type: number
  default: 3478

steps:
# 前提：此固定工位已预部署被测工程、VegaAutoTest 与新适配层
# 正式流水线须改为可追溯 checkout + 受控构建/部署
- task: PowerShell@2
  displayName: 'Vega Runner verification'
  inputs:
    targetType: inline
    script: |
      $env:VEGA_SW_ROOT = 'D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR'
      Set-Location 'D:\Code\Vega\VegaAutomation\VegaAutoTest'
      # 不把 Runner 四态直接替换成自定义结论
      dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item ${{ parameters.workItemId }} --layers uia
      exit $LASTEXITCODE

# 实际集成时添加：
# - always() 条件下的 out/证据归档任务；
# - 独立、只读的 Runner JSON 契约验证任务；
# - 兼容时 PublishTestResults@2，读取 runs/<runId>/uia/trx/*.trx；
# - 默认 dry-run 的 Test Plans 结果映射及发布任务；
# - 凭据与审批控制；
# 注意：此示例不是最终生产流水线，不能照抄后声称“已完成集成”。
```

**为何不直接给出“万能可运行 YAML”**：Server/TFS 版本、Agent 模式、仓库授权、Build Task 版本及产物 Task 能力尚未确认。Agent 应在 P0 探测后生成匹配环境的生产 YAML / Classic 定义并真实运行验收。

---

## 9. Bug 修复闭环与手工测试

### 9.1 手工测试设计

按照实际业务模块组织 Test Suites，而不是单纯按照程序工程名字划分：

```text
Vega_SW_<Release>_Regression
├── Inspection / Recipe / Region
├── UI / 操作菜单 / 弹窗
├── BSS 参数保存与加载
├── Camera / 采图 / 缓存（有实际模块时添加）
├── Motion / 限位 / 安全（仅受控设备测试）
├── Communication / PLC / 设备状态（有实际模块时添加）
├── Data / MongoDB / 结果持久化
└── Stability / Recovery / LongRun
```

**只有文档确有证据的模块可以直接作为已实现功能陈述**；Camera/Motion/PLC 等作为后续按源码确认的可选分类，不得凭通用设备经验宣称 Vega_SW 当前均已实现。

每条手动 Test Case 包含：需求/工作项关联、前置条件、操作步骤、期望结果、设备/软件配置、构建号、失败证据。Test Runner 执行记录与自动化结果共存。

### 9.2 Bug 修复纪律（继承现有 AI 操作手册）

修复前先验证可复现：`FAIL` 红基线；如果 `PASS`，先排查是否是旧二进制/预期已达成；若 `BLOCKED/ERROR`，先处理环境/测试工具。经允许修改后，再 `--rebuild` 验证。闭环结果使用已有 `loop-report.json` 的 `FIXED_CLEAN / FIXED_UI_DRIFT / NOT_FIXED`，不可自行更名为 Runner verdict。

- `FIXED_UI_DRIFT`：即使断言转绿，界面可能变化，**必须人工复核**。
- 不由 AI 自动 push / 开 PR / 改 Work Item 状态；仅输出建议和证据链接。
- 通过人工操作在 Bug 中链接运行证据、对应 Commit、ADO Test Run 和关联 Test Case。
- 正式“已修复”结论应具备修复前 FAIL 与修复后 PASS 证据，且确认修复后 `builtFromCurrentSource=true`；若仅旧 EXE PASS，不能声称“当前源码修复已验证”。

---

## 10. 版本回归与发布准入

### 10.1 测试级别（管理层，不改 Runner 结构）

- **Smoke**：基本启动、关键界面、主要业务路径；每个候选构建。
- **Core Regression**：新增修改所涉工作项 + 历史严重缺陷 + 核心功能；每天/发布候选。
- **Full Regression**：所有高风险功能、兼容配置、异常恢复、稳定性；正式发布前。

### 10.2 新增回归清单（示例）

`config/regression-suites.sample.json`：

```json
{
  "schemaVersion": "1.0",
  "release": "<REAL_RELEASE_ID>",
  "suiteName": "CoreRegression",
  "items": [
    { "workItemId": 3478, "layers": ["logic", "uia"], "required": true },
    { "workItemId": 3547, "layers": ["uia"], "required": true },
    { "workItemId": 3549, "layers": ["uia"], "required": true }
  ]
}
```

调度器逐项调用原 Runner；每项保存独立 `runId/result.json`。**不要**将单次 Runner 验证中的子测试汇总成一个伪造的 Runner `result.json`；版本级汇总另用 `release-gate.json`。

### 10.3 发布门禁规则（新业务规则，≠ Runner verdict）

满足以下条件才能给出 `releaseDecision=READY_FOR_APPROVAL`：

1. 所有 required 工作项都有本次 release 的真实 `runId`、结果与证据。
2. 所有 required 工作项 Runner verdict 均 `PASS`；`BLOCKED/ERROR/FAIL` 一律不能通过。
3. 每个 required 项均满足正式构建策略：`target.builtFromCurrentSource=true`；`OK_STALE_BUILD` 不通过。
4. 被测目标 Git Commit、Build、环境指纹对应**指定 release baseline**；不能只因为本地编过就认定与候选发布包一致。
5. 重要 UIA 用例明确判断 L1 oracle 是否属于“必须交叉验证”；如果某用例必需，则 `l1Api.available=false` 不能作为强验收通过。
6. 高严重级别 Bug、设备安全验收、手工测试签核完成（由 ADO 人员/批准流程提供证据）。
7. 相关测试工位、设备配置和构建产物可追溯；不存在未批准的硬件动作。
8. 最终仍需要发布负责人审批；Agent 输出的是 **READY_FOR_APPROVAL**，不是“已发布”。

结果分类：`READY_FOR_APPROVAL` / `NOT_READY` / `INCOMPLETE_EVIDENCE` / `NEEDS_HUMAN_REVIEW`。这套 Release Decision 不得覆盖 Runner 单项 verdict。

### 10.4 版本报告字段

```text
releaseId / suiteName / generatedAt
workItems: WI, runnerCase, adoTestCase, point, runId, verdict, verdictCode
source: commit, branch, dirty, builtFromCurrentSource, EXE builtAt, BuildID
assertions: passed / failed / skipped, L1 oracle status
artifacts: run evidence link + SHA-256 manifest
ado: Test Run ID / Test Result ID / publication status
quality: product failures / environment blocks / infrastructure errors
releaseDecision / blockerReasons / pendingApprovals
```

---

## 11. 安全、配置和可追溯性

### 11.1 硬性约束清单

- 不修改 `VerdictResolver.cs` / 不在适配层重新定义 PASS。
- 不把 LLM 接入 18901 `/cmd/*` 或任何未获授权的设备控制接口。
- 不把 Vega_SW 主仓库作为证据落盘根，不提交生成的 `runs/`、`assert-*.json`、`_wi*_l2.txt`。
- 不自动删历史 FAIL/ERROR 基线；用 `prune` 之前按保留策略确认归档与验收。
- 不在代码、日志、配置、Pipeline 输出、测试结果中明文保存 PAT 和密码。
- 结果发布目标必须精确匹配 Point；发现多个配置匹配时 fail-closed。
- ADO 写入必须按服务端版本/角色授权与人工审批，不自动创建/关闭产品 Bug。
- CI 不得自动执行真实硬件运动命令；涉及安全测试另由设备负责人建立隔离工位和审批。

### 11.2 构建指纹的额外注意

现有 `target.builtFromCurrentSource=true` 的定义是“此次使用 `--rebuild` 且构建成功”，**并不自动证明构建产物就是某个远端 Pipeline Commit 的不可变 Release Artifact**。正式发布需要再校验：

- 运行时 checkout Commit = Release 基准 Commit；
- Git 工作区应符合规定（例如不允许未知 dirty 变更）；
- 测试实际 EXE 的哈希/版本与发布包对应（必要时在**适配层 manifest** 中增加哈希，不能篡改 Runner 原始 target）；
- 多仓库关联：主程序 Commit 与 VegaAutoTest Commit 都必须可追溯。

---

## 12. 测试矩阵（Agent 必须执行的验收）

### 12.1 无 ADO 凭据的离线单元测试

| 用例 | 输入 | 期待 |
| --- | --- | --- |
| T01 | Runner PASS，exit 0，schema 合法 | manifest 保留 PASS / runId |
| T02 | Runner FAIL，exit 1 | 显示 FAIL；不自动创建 Bug |
| T03 | BLOCKED，exit 2，`DEVICE_NEVER_IDLE` | 显示 BLOCKED；Release Gate 不通过 |
| T04 | ERROR，exit 3 | 显示 ERROR；归类基础设施异常 |
| T05 | PASS + `builtFromCurrentSource=false` | 可记录“旧二进制 PASS”，不能发布版本 READY |
| T06 | PASS + `OK_STALE_BUILD` | 禁止正式验收通过 |
| T07 | xUnit TRX Passed、Runner BLOCKED | 以 Runner BLOCKED 为准 |
| T08 | `ZERO_ASSERTIONS_MATCHED` | BLOCKED，不误判 PASS |
| T09 | 缺 `runId` / 非法 JSON | 拒绝同步 ADO Passed |
| T10 | ExitCode 0、JSON verdict FAIL | 报契约冲突，不发布 |
| T11 | `evidence.dir=../../` 路径穿越 | 拒绝打包/发布 |
| T12 | 映射重复 / Point 缺失 | fail-closed，不自动改任意点 |
| T13 | 无 PAT / 401 / 403 | 保留本地证据并标 `publishStatus=BLOCKED` |
| T14 | API 版本不支持 / 404 | 降级 Artifact-only；不假装成功 |
| T15 | Test Run 创建后断网重试 | 不重复创建逻辑同一结果 |
| T16 | `channels.l1Api.available=false` | 报告“oracle 未发生”，不宣称强验证 |
| T17 | 新版同 WI 两次独立 `runId` | 两份历史均保留，不覆盖 |
| T18 | 不具备 execute 授权 | 只能 dry-run，不能 POST/PATCH |

### 12.2 本机真实 Runner 验收

1. `doctor` 结果保留原始报告；有 `[MISS]` 不能强制进入 UIA 正式验收。
2. `list-cases --json` 确认 3478/3547/3549 真实登记情况；若有差异，报告而非自动造映射。
3. 选择一条环境可执行用例运行 `verify`；检查 `result.json/report.md`/TRX 等真实文件、退出码、`runId`。
4. 运行两次验证必须产生两个不同 `runId`；证据目录互不覆盖。
5. 人工观察 UIA Agent 的桌面会话，不应干扰生产设备和其他测试人员。
6. 受控构建工位执行一次经批准的 `--rebuild`；检查 `builtFromCurrentSource` 和主仓库 tracked 文件变更。
7. 若 `#3547` 的旧文档结果为 FAIL、`#3549` 为 PASS，只能用于设定**历史参考**，不得预置本轮预期结果。

### 12.3 ADO 隔离试点验收（需要授权）

1. 只在 `Vega_SW_Automation_Integration_Pilot` 计划中创建或复用试点对象。
2. 取真实 Plan/Suite/Test Case/Configuration/Point ID，唯一性校验通过。
3. 先 dry-run 展示将写入的所有目标与原始 verdict；人工批准后创建真实 Test Run。
4. 写入一次 PASS、一条可控的 BLOCKED/FAIL **合成测试仅限隔离专用对象**，不能写生产回归 Test Point。
5. 在 Web 界面核对 Test Run/Result 和关联 Point 可见；历史执行不被覆盖。
6. 验证 Artifact 里有原始 `result.json` 和 report、哈希索引；runId 一致。
7. 重跑发布验证幂等；无权限/网络故障时不误改已存在记录。
8. 人工 Test Runner 可独立执行并记录结果；自动化写入不会覆盖人工历史。

---

## 13. 里程碑、任务与完成标准（Agent 待办）

### P0 — Discovery（只读；必须先完成）

**任务**：
- [ ] 读取两份原始文档，实查四工程、CLI、registry、旧产物与 Build 脚本。
- [ ] 核对实际 ADO URL、Server/Services/TFS 版本、API 版本、Projects、测试许可/权限。
- [ ] 探测 Repos/Pipelines/Agent 支持情况和可用的测试工位。
- [ ] 检查 PAT 是否已轮换（只记录是否合规，不读取/传播明文）。
- [ ] 输出 `discovery-report.md` + `compatibility.json` + 实施风险清单。

**通过标准**：没有把推测写成事实；未知服务端条件有明确记录和降级方式。

### P1 — 离线 Adapter + 契约测试

**任务**：
- [ ] 新增独立 `azure-devops-integration` 模块、配置样例、CLI。
- [ ] 实现 Runner 调用、timeout/exit code 捕获、JSON 严格校验。
- [ ] 实现 registry 映射校验、受控证据打包、SHA-256 索引。
- [ ] 实现四态业务展示与发布门禁分离、dry-run publication manifest。
- [ ] 完成 T01–T18 可离线覆盖项。

**通过标准**：无 ADO 凭据可完全测试；原 Runner 代码与判定保持不变。

### P2 — 本机真实验证 + Pipeline Artifact-only

**任务**：
- [ ] 在专用 Windows 工位执行 doctor / list / verify。
- [ ] 配置交互式 UIA Agent；确认 MongoDB 和窗口/端口占用条件。
- [ ] 生成兼容现场版本的 Pipeline（YAML 或 Classic）。
- [ ] always() 条件归档 Runner 证据；兼容时发布 TRX 到 Pipeline Tests。
- [ ] 明确旧二进制模式与 `--rebuild` 模式不同的门禁。

**通过标准**：真实 `runId` 和证据可追溯；Pipeline 测试显示不覆盖 Runner 四态；无授权 ADO 写入。

### P3 — Test Plans 手工体系 + 映射试点

**任务**：
- [ ] 由授权人员创建或复用试点 Plan/Suites/Test Cases。
- [ ] 关联真实 #3478/#3547/#3549 工作项；填写步骤/预期/前置环境。
- [ ] 只读解析真实 Test Points，填充映射并复查一对一关系。
- [ ] 执行一次手动用例并保留截图/构建号。

**通过标准**：真实对象/ID 被记录且无重复创建；混淆 WI/TestCase/Point 的风险消除。

### P4 — ADO Test Run 发布（人工审批后）

**任务**：
- [ ] 用目标 Server 兼容 REST API 实现 Create Run / Get-or-Add Result / Update / Complete。
- [ ] 添加幂等防重复发布与读后校验；错误可重试、可审计。
- [ ] 完成隔离试点 PASS/FAIL/BLOCKED/ERROR 投影及 Web 验证。
- [ ] 默认不改 Bug 状态、不创建产品 Bug、不直接 PATCH Point。

**通过标准**：真实 ADO Run/Result/Point 正确关联；原始四态保留；重试无重复记录。

### P5 — 版本回归 + 发布门禁

**任务**：
- [ ] 实现 regression suite 清单、逐 WI 调度与结果汇总。
- [ ] 加入 Build/Commit/EXE 哈希追溯和旧二进制拦截。
- [ ] 加入手工验收/重要 Bug/设备安全的人工审批字段。
- [ ] 生成 release-report.md，完成至少一次受控模拟发布评审。

**通过标准**：能够回答“哪个工作项在什么构建、什么工位、使用哪个 runId、结果怎样、是否具备提交发布审批条件”。

---

## 14. 最终交付清单

Agent 每个里程碑结束必须输出以下条目，而不是只写“完成”：

1. **实际文件改动清单**（新增/修改/未修改，附 Git Diff 摘要）；明确原 Runner、VerdictResolver 未改变。
2. **现场环境报告**（ADO 版本、API 版本、权限、Agent 模式、缺失前置）。
3. **命令与可重现步骤**（不含秘钥；命令路径指向实际 checkout）。
4. **自动化测试结果**（单元/契约测试、真实 Runner `runId`；BLOCKED 时明确阻塞）。
5. **ADO 写操作日志**（若授权：Run/Result/Point 的真实 ID，发布的元数据和时间，脱敏）。
6. **证据和报告路径**（原始 `result.json`、`report.md`、Artifact 哈希）。
7. **回滚步骤**（停 Pipeline / 禁用写入 / 撤销适配层，保留历史证据；不可随意删除已发布 Run）。
8. **剩余问题与下一步**（区分“已实现”“已测试”“待授权”“待兼容验证”）。

**Agent 最终汇报模板**：

```text
项目：Vega_SW × Azure DevOps Test Integration
阶段：P?
状态：IMPLEMENTED | VERIFIED | BLOCKED | PARTIAL
代码变更：<paths>
环境：<ADO Server/Services + version + api-version>
Runner：<runId / verdict / verdictCode / target fingerprint>
ADO：<Test Plan / Suite / Case / Point / Run IDs, if genuinely created>
证据：<actual local paths / pipeline artifact URLs>
旧二进制声明：<builtFromCurrentSource=true/false and caveat>
验收：<T01..T18 results>
阻塞：<concrete missing prerequisites>
下一步：<single highest priority step>
```

---

## 15. 官方接口资料（实施时按服务端版本选择页面）

以下用于 Agent 校验 Azure DevOps 行为，**不替代目标系统现场探测**。

- REST API 总览与 Server/TFS 版本映射：<https://learn.microsoft.com/en-us/rest/api/azure/devops/?view=azure-devops-rest-7.2>
- Test 管理 REST 概念：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/?view=azure-devops-rest-7.1>
- Test Points List：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/points/list?view=azure-devops-rest-7.1>
- Test Runs Create：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/runs/create?view=azure-devops-rest-7.1>
- Test Results List：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/list?view=azure-devops-rest-7.1>
- Test Results Add：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/add?view=azure-devops-rest-7.1>
- Test Results Update：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/results/update?view=azure-devops-rest-7.1>
- Test Runs Update：<https://learn.microsoft.com/en-us/rest/api/azure/devops/test/runs/update?view=azure-devops-rest-7.1>
- Test Plans 许可与权限：<https://learn.microsoft.com/en-us/azure/devops/test/manual-test-permissions?view=azure-devops>
- UIA / 交互式 Agent 限制：<https://learn.microsoft.com/en-us/azure/devops/pipelines/test/ui-testing-considerations?view=azure-devops>
- PublishTestResults@2：<https://learn.microsoft.com/en-us/azure/devops/pipelines/tasks/reference/publish-test-results-v2?view=azure-pipelines>

## 16. 来源说明与实施依据

**用户提供的项目原始文档**（实际落地时以目标工程同版本文件为准）：

1. `VegaAutoTest/README.md` v2.0，2026-09-17：三层结构、四工程、环境、Runner 参数/退出码/result 契约、registry、证据、FAQ、当前遗留。
2. `VegaAutoTest/AI操作手册.md` v1.0，2026-09-17：验证纪律、AI 工作项决策树、修复闭环、vega-agent 工具、安全红线。

**本文中新增、非现成能力**：Azure DevOps Integration Adapter、ADO Test Case/Point 映射、Test Run 发布、版本回归聚合、发布门禁、CI 配置示例。本文明确要求逐步实施与验证，**不代表这些模块已在现有工程中实现**。

---

> **最终原则**：**VegaAutoTest 判定“软件行为验证结果”；Azure DevOps 负责“组织、留痕、关联、发布流程”；Release Gate 判定“证据是否足以进入发布审批”。三者各司其职，不得互相覆盖。**
