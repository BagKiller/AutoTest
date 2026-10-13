# VegaAutoTest 外挂化验证体系说明文档

> 版本：2.0 ｜ 2026-09-17（独立仓库版）
> 适用代码基线：`Vega.AutoTest.sln` 含 4 工程（harness / spike / l2 / runner）
> **与 code_indicator 同属工程** `D:\Code\Vega\VegaAutomation`（本目录是其中的 L1），对被测主仓库零写入
> 关联文档：`设计文档.md`（三通道架构）、`外挂化集成技术方案.md`（设计原理与契约）、`工作进展与计划.md`（进展与遗留）

---

## 1. 这是什么

VegaAutoTest 是 Vega 半导体检测设备控制软件的**确定性自动化验证体系**，面向两类使用者：

1. **人**：开发/测试在本地手跑 UIA 回归用例，验证某工作项的修复效果。
2. **外部编排层**（Claude Code 的 `wi-verify` skill、code_indicator/vega-agent）：通过 **Runner CLI** 以进程边界方式调用，消费机器可读的 `result.json` 得到 PASS/FAIL/BLOCKED/ERROR 判定。

核心纪律：**模型只决定「要不要验、验哪个」；程序决定「怎么验、判什么」**。判定逻辑全部固化在 `VerdictResolver`，任何 LLM 不得临场决定断言与结论。

### 1.1 三层结构

```
┌──────────────────────────────────────────────────────────┐
│ L0 编排层  code_indicator / wi-verify skill（有 LLM）      │
│   读工作项 → 决定验证策略 → 调 Runner → 转述 verdict        │
└───────────────────────┬──────────────────────────────────┘
                        │ 进程边界（子进程 + 退出码）
                        │ 数据契约（result.json）
                        ▼
┌──────────────────────────────────────────────────────────┐
│ L1 执行层  Vega.AutoTest.Runner（C# · 无 LLM）             │
│   case registry → 可选构建 → dotnet test → result.json    │
└───────────────────────┬──────────────────────────────────┘
                        │ UIA(FlaUI) / HTTP 18901 / 文件系统
                        ▼
┌──────────────────────────────────────────────────────────┐
│ L2 被测  Vega.exe（零侵入，不因测试改一行产品代码）          │
└──────────────────────────────────────────────────────────┘
```

### 1.2 工程布局

```
VegaAutoTest\
├── Vega.AutoTest.sln              4 工程的独立解决方案（不进 Vega.sln）
├── cases\
│   └── registry.json              工作项 → 用例映射（显式登记，不让模型猜）
├── harness\                       公共测试原语（classlib，零 Vega.* 引用）
│   ├── VegaEnv.cs                 环境常量唯一出处（exe 路径/账号/L1 基址，均可用环境变量覆盖；独立仓库下 exe 定位经 VEGA_AUTOTEST_EXE/VEGA_SW_ROOT 注入）
│   ├── RunContext.cs              落盘目录上下文（VEGA_AUTOTEST_RUNDIR 回落 OutDir）
│   ├── AssertionRecorder.cs       结构化断言记录器（写 assert-{n:3d}.json）
│   ├── EvidenceLog.cs             人读取证日志（Step 追加 + Flush 落盘）
│   ├── L2TestBase.cs              xUnit 用例基类（拉起 Vega + Dispose 落盘 + 杀进程；静态构造声明 PerMonitorV2 DPI 感知——高 DPI 下物理点击坐标 1:1）
│   ├── VegaApp.cs                 Vega 进程生命周期（KillLeftover/Launch/WaitForMainWindow）
│   ├── LoginFlow.cs               登录+就绪编排
│   ├── DialogHandler.cs           弹窗分类处理（含 WaitForDialog 期望弹窗断言 + HandleAllExcept 观察期旁路）
│   ├── DialogClassifier.cs        弹窗纯分类（Login/Confirm/Prompt/Question/Unknown）
│   ├── UiExpect.cs                期望原语（WindowAppears/WindowClosed → kind="ui-expect" 结构化断言；签名工厂 DialogExpectations）
│   ├── Waits.cs / UiProps.cs / UiTreeDumper.cs   等待/读值/树 dump
│   ├── L1Client.cs                18901 只读 API 客户端（ping/opened-recipe 等）
│   ├── DeviceStateReader.cs       设备状态双通道（API 优先 → UIA 状态栏兜底）
│   └── InspectionRegionPage.cs    #3478 页面对象（操作+观察+oracle 交叉验证）
├── l2\Vega.AutoTest.L2\           正式 UIA 回归用例工程（WorkItem3478L2 / WorkItem3547L2 / WorkItem3549L2）
├── spike\Vega.AutoTest.Spike\     探索工程（ViewModel 级探针 + UIA 勘察）
├── runner\Vega.AutoTest.Runner\   Runner CLI（本说明的主角）
└── runs\                          每次运行独立目录（gitignore）
```

**隔离性**：整个 VegaAutoTest 零 `Vega.*` ProjectReference（spike 仅在设 `VEGA_SW_ROOT` 时引用 `Vega.UI.Inspection` 做 ViewModel 探针），Runner 只引用 harness——L2/Spike 由 `dotnet test` 子进程驱动，测试宿主崩溃不会带崩 Runner，`result.json` 仍能落盘。

**跨仓库定位约定**：本仓库独立后不再与 Vega 主仓库共用目录树，被测仓库根通过三级解析（优先级从高到低）：

1. `verify` / `doctor` 的 `--repo-root <path>` 参数；
2. 环境变量 `VEGA_SW_ROOT`（与 spike 工程同一约定，**推荐一次性设好**）；
3. 兜底：本仓库的上级目录（仅当两仓又放回同一父目录时生效）。

---

## 2. 环境准备（一次性）

### 2.1 软件前置

| 前置 | 说明 |
| --- | --- |
| .NET 8 SDK | `dotnet --version` 可用 |
| Visual Studio（含 MSBuild） | 仅 `--rebuild` 时需要；脚本经 vswhere 自动探测 |
| 被测 Vega.exe | `<VEGA_SW_ROOT>\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.exe`（RID 专属布局）。首次需构建，见 §4.3 |
| MongoDB（127.0.0.1:27017） | Vega 主程序启动的前置；缺失会卡 Busy |
| URLACL（仅 L1 oracle 需要） | `netsh http add urlacl url=http://+:18901/vega-agent/ user=Everyone` |

### 2.2 跨仓库环境变量（独立仓库必设）

`VEGA_SW_ROOT` 指向被测 Vega 主仓库检出根——exe 定位、`--rebuild` 构建脚本、TargetProbe 的 git 指纹都依赖它：

```powershell
# PowerShell（当前会话）
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"

# CMD
set VEGA_SW_ROOT=D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR

# 或写入用户级环境变量（一劳永逸）
setx VEGA_SW_ROOT "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
```

不设时的替代：每次命令带 `--repo-root D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR`。

### 2.3 环境变量覆盖表（全部可选）

| 变量 | 默认 | 用途 |
| --- | --- | --- |
| `VEGA_SW_ROOT` | 无 | **被测 Vega 主仓库根（独立仓库必设）**；Runner 与 spike 共用 |
| `VEGA_AUTOTEST_EXE` | 按 `VEGA_SW_ROOT` 推导 | 指定被测 Vega.exe 路径（Runner 会自动下传，手跑才需要） |
| `VEGA_AUTOTEST_OUTDIR` | `<VegaAutoTest>\runs\manual` | 手跑时取证/断言落盘目录（默认锚定本仓库，对主仓库零写入；Runner 会自动下传） |
| `VEGA_AUTOTEST_RUNDIR` | 不设 | Runner 注入用；手跑不要设 |
| `VEGA_AUTOTEST_USER` / `VEGA_AUTOTEST_PASSWORD` | 1 / 1 | 测试登录凭据 |
| `VEGA_AUTOTEST_AGENT_URL` | `http://localhost:18901/vega-agent/` | L1 API 基址 |
| `VEGA_AUTOTEST_JOB` | 空 | 目标 Job 名称（树内 Contains 匹配） |

---

## 3. 快速上手

以下命令已按 2026-09-17 独立仓库实测打通（doctor 5 项全 OK → uia 层端到端 PASS）：

```powershell
cd D:\Code\Vega\VegaAutomation\VegaAutoTest

# 0. 一次性：指向被测主仓库（也可 setx 写入用户环境变量）
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"

# 1. 构建全部 4 工程
dotnet build Vega.AutoTest.sln

# 2. 环境自检（exe/18901/构建脚本/git/dotnet）
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- doctor

# 3. 秒级验证：只跑 ViewModel 逻辑层（不拉 GUI；需先设 VEGA_SW_ROOT 重编 spike，见 §8.2）
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers logic

# 4. 全量验证：UIA 层（拉起 Vega 真实界面，约 1.5–4 分钟）
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers uia

# 5. 查看判定
type runs\<上一步打印的 runId>\result.json
```

**约定**：Runner 的 stdout **末行**永远是 `result.json` 的绝对路径，脚本可直接 `tail -1` 取用。同目录的 `report.md` 由 Runner 自动写出，是这份判定的可读稿；倒数第二行以 `[report]` 开头。

**uia 层实测参考输出**：

```
[layer:uia] dotnet test --filter "FullyQualifiedName~WorkItem3478L2"
[layer:uia] PASS (OK) 7/8 断言通过
D:\Code\Vega\VegaAutomation\VegaAutoTest\runs\20260917T055731Z_wi3478_08a70f4\result.json
```

---

## 4. Runner CLI 详解

### 4.1 命令与退出码

| 命令 | 作用 |
| --- | --- |
| `verify --work-item <id> [选项]` | 验证某工作项：registry 查用例 →（可选重建）→ 逐层 dotnet test → 判定 → result.json |
| `doctor` | 环境自检：Vega.exe / 18901 端口 / 构建脚本 / git / dotnet，逐行 `[OK]/[MISS]` |
| `list-cases [--json]` | 列出 registry.json 里所有可验证工作项与层配置 |
| `prune [--keep 50] [--dry-run]` | 清理 `runs/`：保留最近 N 次 + 全部 FAIL/ERROR，空目录一律删。跳过 `manual` |

**退出码即判定**（编排层无需解析 JSON 也能路由）：

| 退出码 | verdict | 含义 |
| --- | --- | --- |
| 0 | `PASS` | 断言全过 + 命中数达标 + 用例绿 |
| 1 | `FAIL` | 断言失败或用例失败——被测行为与预期不符 |
| 2 | `BLOCKED` | 环境/前置不满足，**无法判定**（不是 Bug！） |
| 3 | `ERROR` | Runner 自身异常，人工介入 |

### 4.2 verify 参数

| 参数 | 默认 | 说明 |
| --- | --- | --- |
| `--work-item <id>` | 必填 | ADO 工作项号，按 registry.json 的 `workItems` 匹配用例 |
| `--case <id>` | 由 work-item 推导 | 直接指定用例 id（如 `wi3478`、`l1-recipe-read`）。基础设施用例无 workItem 时可单独用 `--case`；与 `--work-item` 同时给出时两者必须对应同一登记条目 |
| `--layers logic,uia` | `logic,uia` | 跑哪些层（逗号分隔，必须都是该用例已登记的层） |
| `--rebuild` | off | 先调 `run-vega-with-plugin.ps1 -NoLaunch` 重编主程序再验 |
| `--configuration` | `Debug` | 构建配置 |
| `--repo-root <path>` | `VEGA_SW_ROOT` > 上级目录 | 被测 Vega 主仓库根（独立仓库推荐直接设 `VEGA_SW_ROOT`） |
| `--runs-dir <path>` | `<VegaAutoTest>\runs` | 运行产物根 |
| `--json` | — | `list-cases` 时输出原始 JSON |

### 4.3 `--rebuild` 的降级语义

| 情形 | 行为 | target 字段 |
| --- | --- | --- |
| 构建成功 | 继续，结论强 | `rebuilt=true, builtFromCurrentSource=true` |
| 构建失败但有旧 exe | **继续跑但降级**：PASS 会标 `verdictCode=OK_STALE_BUILD`，reason 附"沿用旧二进制（结论降级）" | `rebuilt=true, builtFromCurrentSource=false` |
| 构建失败且无旧 exe | 直接 `ERROR / BUILD_FAILED_NO_FALLBACK`，退 3 | — |
| 不带 `--rebuild`（默认） | 直接用现有 exe | `rebuilt=false, builtFromCurrentSource=false`（恒 false，见下方注） |

构建复用仓库沉淀的 `Vega.BL.AgentControlService\scripts\run-vega-with-plugin.ps1`（它封装了"不能传 `/p:Platform=x64`""必须经 sln 建 `/t:Vega`""插件必须编译进主程序"等踩坑结论），Runner 不另起炉灶。全量输出写 `runs\<runId>\build.log`。

> 默认（不带 `--rebuild`）行为是**不重编**：有 Vega.exe 就直接测。这是本地规则——快速回归优先。未重编时 `target.builtFromCurrentSource` **恒为 false**（§2.4 真值表：该字段 = `rebuilt && buildSucceeded`，只有"本次重编成功"才能证明结论对当前源码有效；exe 存在只说明"有东西可测"），消费方必须按"结论仅对旧二进制有效"转述并报出 `vegaExeBuiltAt`。

### 4.4 判定矩阵（verdictCode 全表）

判定唯一出处：`runner\...\VerdictResolver.cs`。按层独立判定后聚合（任一层 BLOCKED → BLOCKED；任一 FAIL → FAIL；有 ERROR → ERROR；否则 PASS）。

| verdictCode | verdict | 触发条件 |
| --- | --- | --- |
| `OK` | PASS | 各层全部通过 |
| `OK_STALE_BUILD` | PASS | 通过但构建失败沿用了旧 exe（结论降级） |
| `ASSERTION_FAILED` | FAIL | 有断言 FAIL，或有用例失败 |
| `NO_CASE_REGISTERED` | BLOCKED | 工作项未在 registry.json 登记 |
| `NO_TEST_MATCHED` | BLOCKED | 层过滤器零匹配（用例未写，或探针被条件编译剔除——见 §8.2） |
| `VEGA_EXE_NOT_FOUND` | BLOCKED | 层需要被测 exe（`requiresVegaExe`）但不存在 |
| `MAIN_WINDOW_TIMEOUT` | BLOCKED | trx 失败消息含"主窗口/MainWindow" |
| `DEVICE_NEVER_IDLE` | BLOCKED | trx 失败消息含"空闲/idle"（典型：MongoDB 缺失） |
| `L1_UNAVAILABLE_REQUIRED` | BLOCKED | 层声明 `requiresL1Api` 但 API 未就绪 |
| `ZERO_ASSERTIONS_MATCHED` | BLOCKED | 用例绿但命中断言数 < 该层 `minAssertions`（假阳性防线） |
| `TIMEOUT` | BLOCKED | 层执行超过 `timeoutSeconds` |
| `HARNESS_EXCEPTION` | ERROR | 用例失败且消息无法归因为上述环境问题，也**不含 `.L2.` 帧**（堆栈含 `.L2.` = 用例自身断言，归 ASSERTION_FAILED；反例 runId `20260921T053324Z_wi3547` 曾把 5 条断言失败误判成此码） |
| `BUILD_FAILED_NO_FALLBACK` | ERROR | 重编失败且无旧二进制 |
| `CONTRACT_VIOLATION` | ERROR | `result.json` 契约自检违例（`ResultContractCheck`）——这份结论的元数据自己就不可信，原判定保留在 `verdictReason` 里。**违例也会落盘**，便于诊断 |

**两层防线的设计意图**：
- `ZERO_ASSERTIONS_MATCHED`：测试"绿"但一条断言没命中 = 根本没进到目标界面，是最危险的假阳性，必须 BLOCKED 而非 PASS。
- `MAIN_WINDOW_TIMEOUT`/`DEVICE_NEVER_IDLE` 单列：环境问题不是 Bug，混进 FAIL 会污染回归基线。

---

## 5. result.json 契约

每次 verify 在 `runs\<runId>\result.json` 落盘（camelCase，Newtonsoft 序列化）：

```jsonc
{
  "schemaVersion": "1.0",
  "runId": "20260916T085024Z_wi3478_20af65a",
  "workItemId": 3478,
  "caseId": "wi3478",
  "verdict": "PASS",                 // PASS | FAIL | BLOCKED | ERROR
  "verdictReason": "OK: 7/8 断言通过",
  "verdictCode": "OK",               // 见 §4.4
  "startedAt": "...", "finishedAt": "...", "durationMs": 88214,   // run 级墙钟，落盘前必赋值

  // 被测对象指纹——防止"验的不是你改的那份"
  "target": {
    "repoRoot": "...", "gitCommit": "ae6b27f7...", "gitBranch": "master", "gitDirty": true,
    "vegaExe": "...\\Vega.exe", "vegaExeBuiltAt": "2026-09-16T16:13:43+08:00",
    "rebuilt": false,
    "builtFromCurrentSource": false  // = rebuilt && buildSucceeded；未重编恒 false（结论仅对旧二进制有效）
  },

  // 通道可用性——三字段各答一问（§2.5），不得复用
  "channels": {
    "l1Api": { "available": false, "baseUrl": "...", "reason": "用例未产出 oracle-crosscheck 断言..." },
    "l2Uia": { "available": true, "reason": null },
    "assertions": { "available": true, "reason": null }
  },
  "assertions": [ { "id": "...", "kind": "ui-value|oracle-crosscheck|invariant|logic",
                    "description": "...", "expected": "...", "actual": "...",
                    "delta": null, "tolerance": null, "result": "PASS|FAIL|SKIP" } ],
  "summary": { "total": 8, "passed": 7, "failed": 0, "skipped": 1 },
  "tests": [ { "name": "WorkItem3478L2.NavigateToInspectionRegion",
               "outcome": "Passed", "durationMs": 82000, "message": null } ],
  "evidence": { "dir": "runs\\<runId>", "files": ["uia/trx/uia.trx", "uia/_wi3478_l2.txt"] },
  "exitCode": 0
}
```

**消费规则**（编排层/转述人必须遵守）：
1. verdict 四态**原样转述**，BLOCKED 不得写成"存在 Bug"也不得写成"通过"。
2. `target.builtFromCurrentSource == false` 时必须显式标注"未使用当前源码重编，结论仅对旧二进制有效"（未重编跑出的 run 该字段恒 false——见 §4.3 注）。
3. `channels.l1Api.available == false` 时交叉验证未实际发生，摘要不得表述为"强验证"；`l2Uia`/`assertions` 各答各自那一问（§2.5）。
4. 引用验证结论必须带 `runId`；无 runId 的"已验证"视为无效。

---

## 6. 用例登记（registry.json）

`cases\registry.json` 是工作项到用例的**显式映射**。没有登记 = 无法自动验证（诚实返回 `NO_CASE_REGISTERED`），这是特性不是缺陷——提示人类"该补用例了"，而不是让模型现编一个看起来像验证的东西。

```jsonc
{
  "schemaVersion": "1.0",
  "cases": [
    {
      "id": "wi3478",
      "title": "Inspection 检测区域参数联动",
      "workItems": [3478],                    // 一个用例可关联多个工作项
      "tags": ["inspection", "recipe", "df-region"],
      "layers": {
        "logic": {                            // ViewModel 级：秒级、无 GUI、无 exe 依赖
          "project": "spike/Vega.AutoTest.Spike/Vega.AutoTest.Spike.csproj",  // 相对 VegaAutoTest 根
          "filter": "FullyQualifiedName~WorkItem3478Spike",
          "timeoutSeconds": 300,
          "requiresVegaExe": false, "requiresL1Api": false,
          "minAssertions": 0                  // 本层要求的最少命中断言数
        },
        "uia": {                              // UIA 级：拉起真实 Vega
          "project": "l2/Vega.AutoTest.L2/Vega.AutoTest.L2.csproj",
          "filter": "FullyQualifiedName~WorkItem3478L2",
          "timeoutSeconds": 900,
          "requiresVegaExe": true, "requiresL1Api": false,
          "minAssertions": 3
        }
      },
      "evidenceFiles": ["_wi3478_l2.txt"]     // 证据命名契约 _wi{id}_l2.txt
    }
  ]
}
```

**新增一个可验证工作项的步骤**：
1. 在 l2（UIA 级）或 spike（逻辑级）写用例，类名含 `WorkItem{id}`；
2. 在 registry.json 登记条目（层、filter、minAssertions、evidenceFiles）；
3. `verify --work-item <id>` 立即可用。

> `minAssertions` 是 **layer 级**的：logic 层不产结构化断言所以为 0；uia 层 3 = "4 控件读值 + oracle 交叉验证中至少 3 条命中"。filter 只允许 `FullyQualifiedName~WorkItem{id}L2` 形式，禁止裸 id（会误命中 spike 探针）。

---

## 7. 断言与证据：双通道落盘

每次用例运行产出两路人读/机读证据：

| 通道 | 文件 | 给谁看 | 写入方 |
| --- | --- | --- | --- |
| 人读取证 | `<layerDir>\_wi{id}_l2.txt` | 人 / 报告引用 | `EvidenceLog.Step` + `L2TestBase.Dispose → Flush` |
| 机读断言 | `<layerDir>\assert-001.json, assert-002.json, ...` | Runner 聚合进 result.json | `AssertionRecorder.Pass/Fail/Skip` + `Flush` |

落盘目录由 `RunContext.Dir` 决定：
- **Runner 调用**：注入 `VEGA_AUTOTEST_RUNDIR=runs\<runId>\<layer>\`——两层同跑互不覆盖；
- **手跑**（不设该变量）：回落 `VegaEnv.OutDir`（默认 `<VegaAutoTest>\runs\manual\`，可用 `VEGA_AUTOTEST_OUTDIR` 覆盖）——落盘始终锚定本仓库，对主仓库零写入。

`assert-{n:3d}.json` 用静态 Interlocked 序号命名：同一测试进程里多个 xUnit 用例各自 Flush 不会互相覆盖，Runner glob `assert-*.json` 聚合。

**断言种类**（`kind` 字段）：
- `ui-value`：UIA 读到的控件值（`ReadRegionValues` 每命中一个控件记一条）；
- `oracle-crosscheck`：UI 显示值 vs 配方内部值交叉验证（经 18901 `query/opened-recipe`；API 不可用记 SKIP——部署问题不算 UI 回归失败，**弱断言语义不变**）；
- `invariant` / `logic`：预留（逻辑层用例将来可接入 AssertionRecorder）。

---

## 8. 常用场景手册

### 8.1 我改了代码，想验证某个工作项修没修好

```powershell
# 强验证：用当前源码重编主程序（首次约数分钟），结论对当前源码有效
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --rebuild
```
看 `result.json` 的 `verdict`；若 `OK_STALE_BUILD` 说明重编失败、跑的是旧 exe，不能下"已修复"结论。

### 8.2 快速冒烟（不拉 GUI，秒级）

logic 层（ViewModel 探针）引用被测主项目的 `Vega.UI.Inspection`，属**可选编译**。首次跑需设 `VEGA_SW_ROOT` 重编 spike：

```powershell
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
dotnet build spike\Vega.AutoTest.Spike\Vega.AutoTest.Spike.csproj
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers logic
```

不设 `VEGA_SW_ROOT` 时 spike 退化为仅 UIA 勘察探针，logic 层 filter 零匹配 → Runner 返回 `BLOCKED / NO_TEST_MATCHED`（防线：零匹配不算 PASS）。

### 8.3 环境"怎么跑都是 BLOCKED"时

```powershell
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- doctor
```
常见原因：Vega.exe 不存在（先跑一次 `--rebuild` 或构建脚本）、MongoDB 未起、18901 URLACL 未注册。

### 8.4 在 IDE / 命令行手跑单个用例（不经 Runner）

```powershell
cd D:\Code\Vega\VegaAutomation\VegaAutoTest

# 独立仓库下 VegaEnv 无法向上找 Vega.sln，必须显式指定被测 exe 与落盘目录
$env:VEGA_AUTOTEST_EXE = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR\Vega\bin\Debug\net8.0-windows10.0.19041.0\win-x64\Vega.exe"
$env:VEGA_AUTOTEST_OUTDIR = "D:\Code\Vega\VegaAutomation\VegaAutoTest"

dotnet test l2\Vega.AutoTest.L2\Vega.AutoTest.L2.csproj --filter "FullyQualifiedName~WorkItem3478L2"
```
产物落在 `VEGA_AUTOTEST_OUTDIR`（上例即 `VegaAutoTest\`），`_wi3478_l2.txt` + `assert-001.json`（均已 gitignore）。体验与 Runner 调用一致，只是没有 result.json。不设环境变量会得到明确报错提示（`从 ... 向上未找到含 Vega.sln 的仓库根`），属预期行为。

### 8.5 编排层（Python/PowerShell）接入

```python
import subprocess, json, os
env = {**os.environ, "VEGA_SW_ROOT": r"D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"}
p = subprocess.run(
    ["dotnet", "run", "--project", RUNNER_PROJ, "--no-build", "--",
     "verify", "--work-item", "3478", "--layers", "uia"],
    capture_output=True, text=True, timeout=1200, env=env)
result_path = p.stdout.strip().splitlines()[-1]     # 约定：末行 = result.json 路径
verdict = json.load(open(result_path, encoding="utf-8"))
print(verdict["verdict"], verdict["verdictCode"], verdict["verdictReason"])
```

> 注意 L2 用例约 90 秒+，带重编数分钟——编排层必须异步（后台任务 + 轮询），不能同步卡死请求。**现成桥接已就位**：code_indicator 的 `tools/autotest.py`（start/status/list 三件套，2026-09-18 落地，见方案文档 §4.2 与 `工作进展与计划.md` 第 -2 节）。

### 8.6 Claude Code 里验证工作项

直接用 `/wi-verify <id>`（已改为调 Runner 判定，SKILL.md 于 2026-09-18 重写：判定唯一出处 `VerdictResolver`，本 skill 只做转述与回帖）。

---

## 9. 约束与注意事项（改代码前必读）

1. **xUnit 并行必须关闭**：`spike\...\TestAssemblySetup.cs` 的 `[assembly: CollectionBehavior(DisableTestParallelization = true)]` 不得移除——UIA 并行会撞 Vega 单实例 Mutex 与 18901 端口。
2. **InvokePattern 优先**：所有可 Invoke 的按钮一律 InvokePattern，物理 Click 仅兜底（无人值守环境物理点击不可靠）。
3. **oracle 是弱断言**：L1 API 缺失/未就绪只 SKIP 不断言失败；报告引用时须注明"oracle 未命中，弱断言"。
4. **Runner 不引用 L2/Spike 工程**：进程隔离是崩溃容错的前提，新增依赖前先想清楚。
5. **不动仓库共享的 `Directory.Packages.props`**：4 个工程的包版本全部自带（`ManagePackageVersionsCentrally=false`）。
6. **`packages.lock.json` 不提交**（仓库约定）。
7. **证据文件命名契约** `_wi{id}_l2.txt` 保持——wi-verify 等下游工具依赖它定位证据。
8. **每层超时独立**：logic 300s / uia 900s（registry.json `timeoutSeconds`），超时杀进程并 BLOCKED。
9. **运行产物** `runs\`、`assert-*.json`、`_*.txt` 均 gitignore，不要手工清理"看起来没用"的 FAIL 目录（回归基线要靠它们比对）。
10. **判定矩阵只有一个出处**：改判定逻辑只能改 `VerdictResolver.cs`，禁止在编排层/skill 提示词里另写一套。

---

## 10. 故障排查（FAQ）

| 现象 | 原因与处置 |
| --- | --- |
| 手跑 L2 报"向上未找到含 Vega.sln 的仓库根" | 独立仓库的正常行为：VegaEnv 无法自动定位主仓库。设 `VEGA_AUTOTEST_EXE`+`VEGA_AUTOTEST_OUTDIR`（见 §8.4），或改经 Runner 调用（自动下传） |
| `doctor` 里 vega.exe `[MISS]` | `VEGA_SW_ROOT` 未设或指错，或主程序还没构建过。设好后重跑；首次构建见 §4.3 |
| `verify` 退 3，`BUILD_FAILED_NO_FALLBACK` | 重编失败且无旧 exe。先看 `runs\<runId>\build.log`；常见为 NuGet 源不可达或 NiDQACard C++ 工具链缺失 |
| PASS 但 `verdictCode=OK_STALE_BUILD` | 重编失败但有旧 exe 兜底。查 build.log 修构建，重跑 `--rebuild` 才能下"当前源码已修复"结论 |
| logic 层 `NO_TEST_MATCHED`（BLOCKED） | spike 未以 `VEGA_SW_ROOT` 重编，探针类被条件编译剔除（§8.2）。设变量重编 spike 即恢复 |
| uia 层 `MAIN_WINDOW_TIMEOUT` | 主窗口 180s 未就绪。查 Vega 日志、MongoDB；偶发可先 `doctor` 再重跑 |
| uia 层 `DEVICE_NEVER_IDLE` | 设备状态卡 Busy（典型：MongoDB 缺失 / 初始化未完成）。`DeviceStateReader` 证据里有状态时间线 |
| `ZERO_ASSERTIONS_MATCHED` | 用例绿但没真正到目标界面（弹窗竞态/树结构变化）。看 `_wi{id}_l2.txt` 的 dump 段 |
| logic 层直接绿但 0 断言 | 正常——logic 层 `minAssertions=0`，判定只看 trx 用例结果 |
| `--case` 报"未关联工作项" | 用例 id 与 `--work-item` 不匹配；两者必须对应同一登记条目 |
| 18901 拒绝连接（oracle 全 SKIP） | AgentControlService 未部署/未就绪（DelayStartSeconds=60）。oracle 是弱断言，SKIP 不判败；要强验证先部署插件 |

---

## 11. 当前状态与遗留

- **已验收**：V1–V9 全过（详见 `工作进展与计划.md` 验收表）；wi3478 全链路 uia 层实测 PASS（7/8 断言，oracle 3/3）。
- **独立仓库验收**（2026-09-17）：`doctor` 5 项全 OK；`verify --work-item 3478 --layers uia` 端到端 PASS（7/8 断言，1 SKIP），全程对主仓库零写入。测试中修复两处接线缺口：TestRunner 向 `dotnet test` 子进程下传 `VEGA_AUTOTEST_EXE/OUTDIR`；`--repo-root` 解析顺序对齐 `VEGA_SW_ROOT` 约定。
- **P4/P5/P6 闭环**（2026-09-18，详见 `工作进展与计划.md` 第 -2 节）：
  - code_indicator 工具面整改完成：删 6 虚构工具+任意 HTTP，新增 `vega_control.py`（18901 只读）；`execute_cmd` 白名单；`_last_blocked` 确认绕过修复（新用户消息回执解锁）。
  - `tools/autotest.py` 异步桥接（start/status/list 三件套，自动注入 `VEGA_SW_ROOT`+`--repo-root`）；端到端实测 `verify 9999 → BLOCKED/NO_CASE_REGISTERED/exit 2`。
  - 策略层 `policy.json` + `tools/policy.py` 五闸门代码级强制（fail-closed；push/PR/hardware 硬关）；`update_work_item_status` 接闸门。
  - **wi-verify skill 改调 Runner**（主仓库 `.claude/skills/wi-verify/SKILL.md` 重写），判定双轨制消灭。
  - `AI操作手册.md` 就位：AI 读它即可直接操作验证体系。
- **遗留**（后续阶段）：
  - B.3：AgentControlService 转正入库——方案已备（.gitignore 删 3 行 + git add + sln/csproj 固化），**待用户确认执行**；
  - B.4：类级 fixture（每用例独立拉 Vega 1.5-4 分钟，接 CI 阻塞项）；
  - C.1 ✅（2026-09-18）：`UiExpect` 期望原语落地（WindowAppears/WindowClosed → `ui-expect` 断言；签名工厂；`UiExpectPrimitiveL2` 负路径自检 PASS，18s）；
  - C.2 ✅（2026-09-21）：#3547/#3549 两条 L2 用例落地并登记 registry。**#3547**（操作菜单强制中断替换四项，5 断言，纯结构零交互）：Runner 端到端 FAIL（ASSERTION_FAILED）= 本机二进制 8f4e2898 不含 PR 4803 的真实红基线；**#3549**（BSS default 保存加载往返，5 断言，硬判定锚在 BSSConfig.json 空串持久化）：Runner 端到端 PASS 5/5 = 本机二进制已含 PR 4805 修复。过程修三处通用问题：① L2TestBase 静态声明 PerMonitorV2 DPI 感知（150% 缩放下 UIA 物理坐标被虚拟化放大，双击落错节点）；② VerdictResolver 归因修正（堆栈含 `.L2.` 帧 = 用例自身断言 FAIL，不再误报 HARNESS_EXCEPTION）；③ WPF DisplayMemberPath 下拉读法（ListItem UIA Name 是 CLR 类型名，展开后读选中项子 TextBlock）。wi3478 回归 PASS（交叉验证 SKIP 系 L1 未在线，与判定口径一致）。
  - C.3：L1 API 主通道正式用例（Sim 建开 Recipe→扫描→取结果）；
  - P7：Runner 封装 MCP server；
  - PAT 轮换：已泄露 PAT 需本人到 ADO 网页作废（代码已不读明文）。
