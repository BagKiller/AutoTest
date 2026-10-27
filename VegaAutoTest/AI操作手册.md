# AI 操作手册 — VegaAutoTest 验证体系

> 读者：**AI 助手（Claude Code / vega-agent / 后续任何 LLM 编排层）**。人类请读 `README.md`。
> 目的：让 AI 在**不再问人**的情况下，正确完成"验证某工作项修没修好"这件事。
> 版本：1.0 ｜ 2026-09-17 ｜ 配套 `README.md` v2.0

---

## 0. 三条铁律（先读这个，其余都是细节）

1. **判定只能来自 Runner**。你说"已修复/验证通过"的唯一依据是
   `result.json` 的 `verdict`（经 `runner verify` 或 `tools/autotest.py` 获得）。
   禁止根据代码阅读自行宣布修复效果。
2. **verdict 四态原样转述**：
   | verdict | 含义 | 你该怎么说 |
   | --- | --- | --- |
   | `PASS` | 断言全过 | "已验证通过"（附 runId） |
   | `FAIL` | 行为与预期不符 | "未通过"，附失败断言明细 |
   | `BLOCKED` | **环境问题无法判定** | "无法验证，原因 XXX"。**不是 Bug，也不是通过** |
   | `ERROR` | Runner 自身故障 | "验证工具异常，需人工介入" |
3. **引用结论必须带 `runId`**，且 `target.builtFromCurrentSource == false`
   时必须注明"未用当前源码重编，结论仅对旧二进制有效"。
   无 runId 的"已验证"一律视为无效。

---

## 0.1 UI 验证的强制实测要求（2026-10-23）

每日批次的现行入口与准备/冻结门禁以 `daily/AGENTS.md` 首节和 `daily/README.md` 为准（2026-10-26）：默认逐提交，不跨 SHA 复用构建或 runId；收集后由 AI 阅读 diff/工单、自行复用或补齐真实用例，再冻结执行。`daily.py --execute` 的首次返回是候选队列，不是已执行。不要在缺登记处结束，不把自动追加工作项编号当成补测试；也不能在冻结后改断言或重算旧计划。下文旧的单次验证示例不适用于每日逐提交构建指纹要求。

适用于日常批次、工作项专项验证和逐提交复验。用户要求 UI 验证，或工作项复现/验收步骤包含界面流程时，必须实际启动目标版本的 Vega.exe（允许 L2 启动），完成官方授权及目标账号登录，在 Simulator 中执行目标操作并核对实际结果。仅启动进程、到达登录页或主窗口不等于完成验收。

代码分析、构建成功、单元/逻辑测试、DLL 反射、模拟服务、仅实例化 WPF 控件均不能替代 UI 实测。需要界面验证的工作项，即使逻辑层全部 PASS，也必须继续 UI 流程；`registry.json` 缺用例、仅有 logic 层或 `uiSlots=[]` 只能说明覆盖缺口，不能证明无需 UI。

用户单独要求复验/补测时，应在独立验证目录准备并登记对应 L2/UI 用例，声明机型与步骤，完成计划后实际执行；不得用旧每日计划缺少 slot 推脱，也不得在运行中改断言或覆盖历史结果。每日已冻结批次缺少 UI 用例时，诚实记录 NEEDS_CASE/必需 UI 未验，不得完整验收。

每项 UI 报告应包含：目标提交及 EXE/DLL 指纹、PID/启动时间、机型及 Simulator 配置、输入数据、关键操作截图/UI 快照、实际结果与预期的对比、Runner result.json 路径及 runId。正式 verdict 四态原样保留；未完成的必需 UI 必须写明原因，不得以 logic PASS 宣布整个任务完成。真实环境阻塞按 Runner BLOCKED/ERROR 报告，失败有界重试一次后继续其他项，禁止无限等待人工。

按 daily/AGENTS.md 使用官方认证与隔离配置；finally 清理本轮进程、恢复配置并验证 SHA256，即使认证或启动失败也要执行。禁止真机动作。

例如 #3677：必须启动 Vega、合法登录、导入原始 dual.recipe、创建 Job、进入离线 postprocess、加载原始 Normal/Oblique JSON、运行并检查目标参数和 DC 合并结果；方法级 pitch 测试只能作为补充。

## 1. 环境地图（什么在哪）

```
D:\Code\Vega\VegaAutomation\           ← 自动化工程根（L0 + L1）
  VegaAutoTest\                       ← L1 确定性验证
    runner\Vega.AutoTest.Runner\      ← 判定 CLI（唯一判定出口）
    cases\registry.json               ← 工作项→用例登记表（没登记=不能验）
    runs\<runId>\result.json          ← 每次验证的机器可读结论（判定唯一出处）
    runs\<runId>\report.md            ← 同期自动写出的可读报告，只转写 result.json
  code_indicator\                     ← L0 编排层（vega-agent）
D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR\   ← 被测主仓库（不在本工程内）
```

**关键环境变量**（AI 可直接设置，会话级即可）：

| 变量 | 必设? | 值 |
| --- | --- | --- |
| `VEGA_SW_ROOT` | **必设** | `D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR`（被测主仓库根） |
| `AZURE_DEVOPS_PAT` | 读 ADO 时必设 | PAT 值（不要写进任何文件） |
| `VEGA_AUTOTEST_RUNNER` | 可选 | Runner 工程路径（vega-agent 的 autotest.py 用） |

PowerShell 会话内设置：

```powershell
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
```

---

## 2. 标准任务：验证工作项 X（决策树）

```
用户："验证工作项 3478 修没修好"
│
├─ ① 读工作项，再查本地 git
│    用 config.json 的 PAT 拉标题 / 复现步骤 / 预期结果，从正文里取出提交号。
│    在 VEGA_SW_ROOT 上 `git merge-base --is-ancestor <commit> HEAD`。
│    ├─ 提交已在本地历史上 → 本地工程已包含该改动。不要停在「未登记」。
│    │    直接用现有 Vega.exe 启动（不要先重编），按工作项预期操作界面并判定。
│    │    没有登记用例时，先补一条能表达该预期的 L2/logic 用例再跑，结论必须来自这次实跑。
│    └─ registry 已有对应用例 → 走下面的 Runner ↓
├─ ② 环境自检
│    dotnet run --project runner\Vega.AutoTest.Runner --no-build -- doctor
│    ├─ 有 [MISS] → 先修环境（见 §4 FAQ），修不好就如实报告 BLOCKED
│    └─ 全 [OK] ↓
├─ ③ 跑验证（先 logic 快筛，再 uia 全量）
│    # logic 层：秒级，不拉 GUI
│    dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers logic
│    # uia 层：拉起真实 Vega 界面，2-5 分钟，期间不要动这台机器的 UI
│    dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers uia
│    ↓
├─ ④ 读结果：stdout 末行就是 result.json 路径，读它
│    同目录 report.md 已由 Runner 写出（stdout 倒数第二行以 [report] 开头）。
│    判定只认 result.json；report.md 是它的可读稿，禁止手改报告来改结论。
│    ├─ verdict=PASS 且 builtFromCurrentSource=true → 报告"已验证通过" + runId
│    ├─ verdict=PASS 且 builtFromCurrentSource=false → 报告通过但注明"旧二进制"
│    │    ↓ （若用户要"对当前源码的结论"）
│    │    加 --rebuild 重跑：verify --work-item 3478 --rebuild
│    ├─ verdict=FAIL → 报告"未通过"+ verdictReason + 失败断言（assertions[] 里 result=FAIL 的条目）
│    ├─ verdict=BLOCKED → 报告"无法验证"+ verdictReason；按 §4 修环境后可重试
│    └─ verdict=ERROR → 读 runs\<runId>\build.log 等证据，报告工具故障
│    ↓
└─ ⑤ 汇报（每次验证必做，不要等用户再要文档）：
     回复里给出 report.md 的路径，并转述其中的：
     verdict / verdictCode / runId / 断言 N 过 M / target 指纹（commit+builtFromCurrentSource）
```

修复**已经在本地 git 历史里**的工作项，走上面这条单次验证。

修复**还没提交**的新建工作项，走闭环，不能跳步：

```
① start_loop_baseline(工作项号)
     用当前未重编的 Vega.exe 按复现步骤跑一遍，Dispose 时写下 ui-snapshot.json / .png
     指纹归档到 baselines/wi<id>/<commit>/before/
② get_loop_status 直到结束
     ├─ verdict=FAIL → 问题在当前界面上复现了，界面已记住
     ├─ verdict=PASS → 当前二进制已经符合预期，停止，不要改代码
     └─ BLOCKED/ERROR → 先处理环境或用例，不要进入修复
③ 仅 FAIL 时，经策略闸门改代码：start_code_repair → write_repair_file → commit_repair
     不 push、不开 PR、不改工作项状态
④ finish_loop(工作项号)
     --rebuild 后再跑同一条用例，归档 after 指纹，对比两份 result.json 和两份界面
⑤ 读 loops/wi<id>/loop-report.json
     FAIL→PASS 且界面指纹无增删、无超过 16px 的位移 → FIXED_CLEAN
     FAIL→PASS 但有控件增删或明显位移 → FIXED_UI_DRIFT，交给人确认，不得宣布界面无误改
     修复后仍 FAIL → NOT_FIXED
     到此停止
```

界面指纹只认带 AutomationId 的控件。无 Id 的文本（状态栏、时间）不参与「是否误改其他组件」的判断。

**退出码速查**（编排层不解析 JSON 也能路由）：`0=PASS 1=FAIL 2=BLOCKED 3=ERROR`。

---

## 3. vega-agent 侧（对话入口，已按方案 P4/P5 接线）

如果工作在 `D:\Code\Vega\VegaAutomation\code_indicator\vega-agent`（Flask + LLM agent），
验证相关工具已注册，语义与本手册一致：

| 工具 | 作用 | 备注 |
| --- | --- | --- |
| `list_verifiable_work_items` | 列出已登记工作项 | 对应步骤① |
| `start_verification(work_item_id, rebuild, layers)` | 异步启动，返回 job_id | 对应步骤③ |
| `get_verification_status(job_id)` | 轮询；done 时回 verdict 摘要、runId、reportPath | 对应步骤④⑤ |
| `vega_control_*`（5 个） | 18901 只读查询（ping/tool-state/recipe） | 运行时真相，无写操作 |
| `vega_ping` / `vega_get_status` / `vega_get_metrics` | 18900 指标服务 | 只有这 3 个真实端点 |

P4/P5 已完成的整改（AI 不要回退）：
- **已删除** 6 个虚构工具：`vega_get_wafer_status/motion/optics`、`vega_move_axis`、
  `vega_load_wafer`、`vega_unload_wafer`（18900 上 404），以及 `vega_raw_command`（任意 HTTP）。
- **18901 只接只读**：`/query/*` 与 `/ping`。`POST /cmd/*`（硬件动作）永不进工具面。
- `execute_cmd` 有白名单（git 只读 / dotnet / tasklist / netstat / ping），白名单外返回 blocked。
- SYSTEM_PROMPT 含「验证纪律」节；verdict 解释以本手册 §0 为准，两处不得分叉。

---

## 4. 环境故障排查（BLOCKED 时按此表处理）

| verdictCode / 现象 | 原因 | AI 处置 |
| --- | --- | --- |
| `VEGA_EXE_NOT_FOUND` / doctor 里 exe [MISS] | `VEGA_SW_ROOT` 未设/指错，或主程序没编过 | 设好变量重试；仍缺则跑 `verify --rebuild`（数分钟） |
| `NO_CASE_REGISTERED` | 用例没登记 | 不是故障。如实说明，可提议补用例（见 §5） |
| `DEVICE_NEVER_IDLE` | MongoDB(127.0.0.1:27017) 未启动 | 检查/启动 MongoDB 后重试 |
| `MAIN_WINDOW_TIMEOUT` | 主窗口 180s 未就绪 | 查 Vega 日志；偶发重试一次 |
| `NO_TEST_MATCHED`（logic 层） | spike 未以 `VEGA_SW_ROOT` 重编 | `dotnet build spike\Vega.AutoTest.Spike\Vega.AutoTest.Spike.csproj` 后重试 |
| `ZERO_ASSERTIONS_MATCHED` | 用例绿但没到目标界面 | 不要报 PASS。读 `_wi{id}_l2.txt` dump 段分析 |
| `OK_STALE_BUILD` | 重编失败但旧 exe 兜底 | 如实转述"旧二进制"；修构建看 `runs\<runId>\build.log` |
| `L1_UNAVAILABLE_REQUIRED` / oracle 全 SKIP | 18901 插件未部署 | oracle 是弱断言，SKIP≠FAIL；强验证需先部署 AgentControlService |
| 手跑 L2 报"向上未找到 Vega.sln" | 独立仓库正常行为 | 设 `VEGA_AUTOTEST_EXE`+`VEGA_AUTOTEST_OUTDIR`（README §8.4），或一律走 Runner |

---

## 5. 新增一个可验证工作项（人提需求后 AI 可代办）

1. **写用例**（类名必须含 `WorkItem{id}`）：
   - UIA 级 → `l2\Vega.AutoTest.L2\WorkItem{id}L2.cs`，继承 `L2TestBase`，
     构造传 `"_wi{id}_l2.txt"`；
   - 逻辑级 → `spike\Vega.AutoTest.Spike\WorkItem{id}Spike.cs`（可 `#if VEGA_SOURCE_AVAILABLE`
     引用被测项目，注意 spike 只在设 `VEGA_SW_ROOT` 时编译这些探针）。
2. **弹窗期望用 expect 原语，不要手写轮询**（harness\UiExpect.cs，C.1）：
   ```csharp
   var expect = new UiExpect(Dialogs, Assertions, Log);
   // 期望某弹窗出现（等待期间其他弹窗照常托管；命中/超时自动落 kind="ui-expect" 断言）
   var dlg = expect.WindowAppears("wi{id}.warning", DialogExpectations.TitleContains("标定完成"),
       TimeSpan.FromSeconds(30), MainWindow!);
   // 期望某弹窗关闭（观察期绝不动目标窗——替它点掉=伪通过）
   expect.WindowClosed("wi{id}.warning.closed", DialogExpectations.TitleContains("标定完成"),
       TimeSpan.FromSeconds(15), MainWindow!);
   ```
   原语只记录不断言——要不要 `Assert` 由用例决定；签名工厂有
   `TitleContains / MessageContains / TitleOrMessageContains`（contains 语义）。
3. **登记** `cases\registry.json`：id=`wi{id}`、workItems=[{id}]、
   layers（project/filter/timeoutSeconds/requiresVegaExe/requiresL1Api/minAssertions）、
   evidenceFiles=["`_wi{id}_l2.txt"`]。filter 只允许 `FullyQualifiedName~WorkItem{id}L2`
   形式，禁止裸 id（会误命中 spike 探针）。
4. **验证** `verify --work-item {id}`，按 §2 步骤④读 result.json，并按步骤⑤交出同目录 `report.md`。
5. **纪律**：minAssertions 是假阳性防线——用例绿但断言命中不足 = BLOCKED，别放宽它来"让测试变绿"。

---

## 6. 红线（违反任何一条 = 结论无效 / 事故）

1. 对被测主仓库**零写入**：取证/产物只落 `VegaAutoTest\runs\`。
   任何往 `Vega_SW*` 写文件的冲动都是错的。
2. 不改 `VerdictResolver.cs` 的判定语义；更不得在提示词/编排层另写一套判定。
3. 不把 `POST /cmd/*`、硬件动作、`--allow-hardware` 之类暴露给 LLM（根本不实现）。
4. PAT 只进 `AZURE_DEVOPS_PAT` 环境变量；发现任何文件里有明文 PAT → 提醒本人轮换。
5. `runs\`、`assert-*.json`、`_wi*_l2.txt` 都 gitignore 但**不要删**——回归基线靠它们比对。
6. 不 push、不开 PR、不改工作项状态——这些动作只停留在"建议人去做"。
7. 代码改动验证流程：**先跑失败基线（改前 FAIL），再跑修复后（PASS）**。
   没红过的绿不可信。

---

## 7. 一分钟速查卡

```powershell
# 会话初始化
$env:VEGA_SW_ROOT = "D:\Code\Vega\Vega_SW.ChenLR\Vega_SW.chen.ChenLR"
cd D:\Code\Vega\VegaAutomation\VegaAutoTest

# 有哪些可验 → 环境 OK 吗 → 验 → 读结果
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- list-cases
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- doctor
dotnet run --project runner\Vega.AutoTest.Runner --no-build -- verify --work-item 3478 --layers uia
# stdout 末行 = result.json 路径；倒数第二行 = [report] report.md 路径
# verdict/verdictCode/runId/target 都在 result.json；可读稿在同目录 report.md
```

汇报一句话格式（并给出 report.md 路径，不必等用户索要）：
「工作项 #3478 验证 **PASS**（OK，7/8 断言，1 SKIP），runId=`20260917T...`，
被测 commit `4da5aa0a`（未重编，builtFromCurrentSource=false，结论仅对旧二进制有效）。
报告：`runs\<runId>\report.md`。」
