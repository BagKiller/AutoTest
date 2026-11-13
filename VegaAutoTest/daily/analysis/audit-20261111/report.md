# daily 最近运行审查（2026-11-11）

本文件保留修复前的审查记录；后续整改和验证结果见 [fix-report.md](fix-report.md)。

结论：存在可确认的验证框架逻辑问题。最近有提交的两晚主要失败在生成测试的编译、夹具初始化和预检阶段，不能据此判定产品功能普遍失败。应先修复编译修正被屏蔽、生成工程隔离和生成器 API 契约，再评估剩余业务问题。

本次只做审查和隔离复现，没有修改控制器、产品、注册表、调度配置或历史 Runner 结果，没有启动新批次或历史复验。新增文件仅在本报告目录。仓库原有 `cases/registry.json`、`daily/scheduling.json` 的 staged/unstaged 修改保持原状。

## 1. 范围与实际结果

主样本为北京时间 11 月 7、8、9、10 日 23:30 启动的四个 nightly 批次；跨午夜结果归属启动当晚。没有使用旧版本批次推断新版本表现。另查看 11 月 6 日作为旁证，不计入下表。

| 启动日期 | 当天收集提交 | 正式 Runner run | 生成候选预检 | 最终状态 |
| --- | ---: | --- | --- | --- |
| 11-07 | 0 | 0 | 无 | NO_COMMITS |
| 11-08 | 0 | 0 | 无 | NO_COMMITS |
| 11-09 | 6 | 4，全部 PASS | 15 个：8 编译/发现失败、4 TIMEOUT、1 HARNESS_EXCEPTION、2 PASS | INCOMPLETE / NEEDS_CASE |
| 11-10 | 3 | 2，全部 PASS | 9 个：6 编译/发现失败、3 HARNESS_EXCEPTION | INCOMPLETE / NEEDS_CASE |

注意：run 数不是 xUnit 方法数或断言数。上面的正式 PASS 大多是既有基线，不能替代新增需求验收。11-09 正式执行了一个生成用例；另一个预检 PASS 的锁文件用例未进入旧批次正式计划。11-10 没有生成用例进入正式执行。两晚各保留 4 项缺失 UI 覆盖，冻结计划均没有可执行 UI slot，所以这两晚不是“已经操作软件、全部业务操作失败”，而是 UI 覆盖根本未形成。

机器可读证据见 [evidence.json](evidence.json)，可用 [audit_runs.py](audit_runs.py) 重新从原文件提取。批次目录分别为 `runs/20261109T233003+0800_9869e6`、`runs/20261110T233003+0800_01d26a`。

## 2. 已确认的问题

### P1：运行预检异常屏蔽其他用例的编译修正

位置：`queue_driver.py:108-113`、`autopilot.py:286-304`。

`check_cases` 收集完编译结果后继续试运行可发现的用例，这一行为正确；但结束时先抛 `CaseRuntimePreflightError`，然后才处理 `discovery_failed`。控制器对前者直接退出修正循环，对普通编译异常才调用一次生成器修正。

结果是：只要同一批候选中存在一个运行异常，其他独立候选的编译错误就失去原本允许的一次修正。11-09、11-10 均同时满足两类失败；两晚保存的模型 request 中均没有 `compilerRepair=true`。11-10 的 CS7036、CS1744、CS9035 等错误因此直接变成缺口冻结。

已通过 [reproduce_mixed_failure.py](reproduce_mixed_failure.py) 隔离复现：同一个编译失败，另一个用例 PASS 时进入编译修正分支；另一个用例 ERROR 时变成 `CaseRuntimePreflightError` 并绕过编译修正。所有产品调用、编译和 Runner 都是临时 fixture 的 mock。

修复建议：编译错误与运行预检结果独立返回、独立处理。只对编译失败的 scope 进行有界修正；保留运行异常及全部原始结果。不能把真实业务 FAIL 交给模型改预期。修正通过后重新核对受影响源码指纹并预检，最后冻结。

### P1：UI 与 logic 工程编译全部源码，导致跨层连带失败

位置：`model_author.py:396-402`、`project_text():419`。

每个 scope 的 `Authored.logic.csproj` 与 `Authored.uia.csproj` 都使用 SDK 默认的 `**/*.cs` 编译规则。filter 只能限制运行，不能限制编译。一个 UI 文件编译错误会使同目录中所有逻辑用例也无法发现。

11-10 的 Haze scope 是清晰实例：`HazeModeUiTests.cs` 三处调用 `UiFind.Wait` 未传必需的 `timeout`，导致 `haze-page-mode` UI 和 `haze-calibration`、`haze-evaluator`、`haze-transport` 三个逻辑用例一并编译失败。逻辑源码是否还存在其他问题需要隔离编译才能判断，不能从这次失败断言它们全部无效。

同晚 reprocess 的 UI/logic 工程也互相受错误影响；11-09 同层多个类共用工程时，一个 `EventInfo` 歧义使四个用例全部发现失败。

修复建议：为生成 schema 增加显式的源码归属和共享 helper 列表；各层只编译所属源码与必要 helper，进一步按独立 fixture/用例组隔离。不能简单按文件名猜测归属，也不能仅靠运行 filter。

### P1：模型缺少被强制使用的 UiFind API 契约

位置：`model_author.py` 的 POLICY、`context():197-230`、`add_sources():270`；实际契约在 `harness/UiFind.cs:25,58,66`。

POLICY 要求只能用 UiFind，却把 `Require/Wait` 概括成相同参数形式，没有给出 Wait 的必需 `TimeSpan timeout`，也没有明确 Optional 的第二参数就是 automationId。context 注入 AssertionRecorder 和 L2TestBase，但没有注入 UiFind。额外源码请求又只允许产品 Git inventory，不能通过它读取自动化仓库的 `harness/UiFind.cs`。

11-10 的全部已保存模型请求都没有 UiFind 实现；生成结果恰好出现两类对应错误：Haze UI 的 Wait 漏 timeout（CS7036），reprocess UI 的 Optional 把第二个位置参数当 description 后再次传 automationId（CS1744）。这是生成输入缺陷，不是产品 API 本身错误。

修复建议：把真实 UiFind、WpfTestHost 的签名和短小的可编译示例作为固定版本的 harness contract 注入，和实际源码指纹绑定。分别展示 Require、Wait、Optional、All，不能再用一个伪共同签名描述。

### P2：取源码轮数先耗尽，内部信息不足被长期固化为覆盖缺口

位置：`autopilot.py:163-182`、`model_author.py:197-229,252-302`。

源码读取最多三轮；单文件默认摘要、上下文总量受限，新增内容会驱逐旧内容。轮数耗尽后，无论 20 分钟预算还有多少，模型都必须输出测试和 gap。11-10 三个 scope 只使用约 405、415、549 秒，均在第四次推理时 `remainingSourceRequests=0`，仍有约 11–13 分钟预算未用。

冻结审核中的多项 gap 明确是“没有提供完整构造/API/定位器/方法后半段”“source-request budget is zero”，不等价于验证机缺权限、缺硬件。特别是已有本地源码的内部依赖，应优先由控制器补充，而不应被笼统当作外部资源缺口。

修复建议：仍保留总体时间和大小上限，但按剩余预算及具体依赖允许有界补查；对关键 API 签名、required 成员、构造器和 UI 定位器做确定性扩展。为 gap 增加类别（内部上下文不足、生成错误、真实输入不足、外部硬件不足），只对真实外部缺口停止自动补查。不能因此声称真实测量或独立验收基准已具备。

### P2：顶层提交统计实际统计 HEAD 槽位，容易误读

位置：`individual.py:272-304`。

latest-head 下，`commitCount=len(rows)`、`statusCounts`、`commits` 仍从唯一 HEAD child 汇总。11-09 实际收集 6 个提交、11-10 收集 3 个提交，但两晚都显示 `commitCount=1`、`statusCounts={NEEDS_CASE:1}`。真正逐提交信息在另一个 `commitResults` 字段内。

这不会制造 PASS，但面向监控或读 summary.json 的消费者时，字段名字与含义不一致，会低估受影响提交数。建议显式区分 `executionTargetCount` 与收集提交数，逐提交状态统计直接来自 `commitResults`；兼容旧消费方时增加新字段而非静默改义。

## 3. 其他实际失败与尚需验证的风险

11-10 三个可编译 Graphic 候选的四个测试方法，在真实业务断言前均因 `System.Object` 不能转换为 `Vega.Utility.Common.IConfigsManager` 而失败。生成的 `GraphicRegressionSupport.cs` 使用 DispatchProxy 构造通用服务替身，未正确处理泛型服务解析的具体返回类型。这里应修复 fixture 的类型契约，不能把它记为 Graphic 业务行为不满足需求，也不能仅重试登录。

11-09 的锁文件兼容用例缺 `Microsoft.Extensions.Http, Version=8.0.0.0`；另四个用例每项耗到 600 秒层超时，约浪费 40 分钟。这些都是预检基础设施问题。当前模板已增加 AspNetCore 框架引用，WPF 宿主也已增加异步执行与工作项超时；不能把这两项旧运行表现直接断言为当前版本仍完全未修复。当前控制器 Python 哈希与 11-10 启动时完全相同；与 11-09 相比 `daily.py`、`model_author.py` 已变化。控制器 provenance 没有保存 harness 文件哈希，不能仅靠它追溯当晚 WPF host 的完整实现。

当前 WPF 代码仍有一个静态可确认的边界漏洞：`WpfTestHost.cs:93` 在设置 operation 超时前调用 EnsureStarted，而后者 `:176` 使用无超时 `ready.Wait()` 并在锁内等待。Application/资源字典启动若卡住，120 秒上限不覆盖这段，只能等外层 600 秒杀进程。本次未构造 WPF 启动卡死来复现，也不能断言它就是 11-09 四次超时的唯一根因。建议启动与执行共享一个总 deadline，并对初始化失败设置不可复用状态。

`FINISHED` 和 `--check-complete=0` 目前只证明流程已收尾，报告保留 `acceptanceState=INCOMPLETE` 是正确的；不能把前者当作需求已通过。控制器/启动器成功退出与质量验收结果分离是现行设计。若外部监控只看退出码，应同时读取 acceptanceState、commitResults 和 UI 缺口，而非修改历史 verdict。

## 4. 已有修复，不应重复归咎

11-09 无工作项号的锁文件用例预检 PASS 却没有正式运行，当前已通过 `coverageSelectionExplicit/reviewCaseIds` 纳入计划选择，并有 `test_reviewed_case_without_work_item_is_executed_on_latest_head_target` 覆盖。旧批次不应被改计划或重算成通过。

把业务断言 FAIL 保留为可执行、而把 HARNESS_EXCEPTION/前置 BLOCKED 剔出正式计划，本身符合现行规则。问题是生成的正常 fixture 尚未跑起来，以及这些准备失败触发了上述连带影响。不能靠降低 minAssertions、把异常改成 SKIP、只跑基线来“修绿”。

## 5. 验证与处理顺序

已运行 `python.exe -B -m unittest test_autopilot test_individual test_latest_head test_requirement_scopes`：128 项通过，耗时约 7.8 秒。现有测试分别覆盖编译失败继续预检和运行异常不改阈值，但没有覆盖两种失败同时出现导致编译修正被屏蔽；新增隔离复现确认该组合问题。

按项目规则，对最新批次运行 `daily.py --resume <20261110 批次> --check-complete`：退出 0。详情见 [completion-check.json](completion-check.json) 与 [completion-check.log](completion-check.log)。前后核对 22 个 manifest、plan、Runner result 和 TRX 文件，哈希全部不变。该检查不代表验收通过，当前仍是 INCOMPLETE。

建议按以下顺序修复：

1. 先独立处理编译/预检失败，让编译修正真正执行。
2. 补齐 harness API 契约，并隔离 UI/logic 编译单元。
3. 改善有限源码补查，修复可确认的生成 fixture 类型错误。
4. 补齐 WPF 启动超时和报告统计口径。
5. 在新批次或用户单独授权的独立复验目录重新验证，保留历史输入、断言和原始结果。

上述前两项修复前，单纯延长超时、反复启动夜间任务或重配认证器，预计不能解决目前主要失败来源。修复完成后仍须执行真实业务与必需 UI，不能保证现有未覆盖需求全部通过。
