# 每日验证失败原因分析（2026-10-19—2026-10-29）

分析日期：2026-10-30，北京时间。范围为指定归档目录内全部 139 份 report.md，并结合当前自动化工程、历史修复记录及可读取的产品历史源码交叉核对。

**核心结论：长期无法完整验收的主因，是用例准备、执行推进、测试宿主、UI 前置与结果分类这几条链路没有同时闭环。报告中的 ERROR、BLOCKED、NEEDS_CASE、PENDING 经常一起表现为“又失败了”，但对应的处理办法完全不同。确有产品构建失败和行为差异，不能把所有问题都归为测试工具，也不能把所有非 PASS 都归为产品回归。**

**1. 分析口径与证据限制**

139 份报告包括：13 份原批次根报告、4 份专项复验根报告、65 份提交报告、57 份独立 Runner 报告。它们有父子引用关系，不能相加当作 139 次独立测试。

57 份独立 Runner 报告的原始状态是：PASS 33、ERROR 18、FAIL 5、BLOCKED 1；其中 17 次 ERROR 为 CONTRACT_VIOLATION、1 次为 TEST_HOST_FAILED。这只是归档中实际存在的 Runner 文档统计。复验汇总还引用了大量未打包进来的 Runner 结果，不能据此计算整个工程的通过率。

归档只有 Markdown，没有所引用的 result.json、TRX、截图、模型 events.jsonl、构建日志或冻结指纹文件。以下将报告直接陈述、源码可解释的机制，以及尚待原始证据确认的深层原因分开。当前工程源码包含后续修正，不能据当前代码推断过去运行时已使用该修正。本次为静态分析，没有重新构建或启动 Vega。

日期按原批次归属整理；例如 10 月 29 日批次的 UI 运行实际发生在北京时间 10 月 30 日 00:15—00:32 左右。复验结果另列，不覆盖原批次事实。

**2. 每天具体发生了什么**

**10 月 19 日（两个批次，17 份报告）**

15:47 示例批次初次核心运行出现 Slit 字典键不存在，随后核心运行 PASS 320 条断言，但只覆盖部分逻辑，总体验收仍 INCOMPLETE。23:30 批次的根报告记录：当晚因遗留 Vega PID 4240 占用共享构建输出而中断；次日清理进程并改用独立 worktree 补测。

补测过程出现 TEST_HOST_FAILED、反射参数数量不匹配、NullReferenceException、INavigationService 未注册以及机型单例污染。最终核心 320/320、补充 18/18 PASS，并实际观察四配置 Simulator UI。剩余问题包括 XY 自动校准参数缺失、Beam/Uniformity 有效队列或数据缺失、完整任务与真机/Linux/Host 链路未覆盖。这里既有已经恢复的宿主故障，也有尚未完成的验收范围。

**10 月 20 日（两个批次，10 份报告）**

16:35 批次为“缺用例 9、工具异常 1”；23:26 批次为“缺用例 11、工具异常 1”。两个批次的 daily-supplement 都在 `DirectSend_EmptyBytes_CountConsumedTracks(tracks: 1, cancelled: True, expected: 1)` 失败，17/18 断言通过。

目标产品 `cebf91e9` 的 GetDataGroup 源码在读取缓存前检查取消。测试调用前已经取消，因此应消费 0 条，旧期望 1 与目标版本契约冲突。更严重的是，这个普通断言先被归为 HARNESS_EXCEPTION，再被“没有 UIA 却存在通过断言”的旧契约检查覆盖成 CONTRACT_VIOLATION。Binning 断言本身通过，却因共用整个 daily-supplement 的 verdict，使 #3652 在根报告中也表现为工具异常。

UI 没有排入实际用例，多个新改动只得到通用探针覆盖。缺 registry 映射并不会因为再次运行同一探针自行消失。

**10 月 21 日（5 份报告）**

新增 #3651，加上前批待验项，汇总为“缺用例 12、工具异常 1”。daily-supplement 仍是同一个预取消期望错误；其他三类已运行探针 PASS。新增功能没有针对性验收，旧失败又被带入新批次，形成稳定重复。

**10 月 22 日（5 份报告）**

新增 #3677 离线 Pitch/DC、#3672 Notch offset，以及无工作项号的 FindNotch 修复，汇总变为“缺用例 15、工具异常 1”。旧预取消断言第四次出现在这组归档中。新需求未转化成对应测试，路径探针通过无法验收这些变化。

**10 月 23 日（11 份报告）**

已切换 individual 模式。10 个提交全部 NEEDS_CASE、尚未收尾 10；各提交报告明确“暂无成功的目标构建记录；用例未执行”。这一天主要停在候选计划/登记阶段，不能解释为 10 个产品提交都执行失败。仅要求“逐提交测试”没有解决自动生成、编译发现、冻结和队列推进的问题。

**10 月 24 日、25 日（每天 1 份报告）**

两天均 NO_COMMITS、待收尾 0。它们表示当日没有收集到新提交，不能计为失败，也不表示历史未验项目已修复。

**10 月 26 日（18 份报告，包含 10 月 27 日复验）**

原运行出现基线静态初始化异常、旧预取消断言错误和重复执行。工程内恢复说明记录了共享测试 bin/obj 污染、把等待返回误当进程结束、第二项后台命令尚未完成就结束编排等原因。原根/提交报告后来追加了专项结果，因此同一文档会同时出现“当前已收尾”和历史“待执行”。

专项将 5 个提交全部收尾，但验收结果仍为：

- `5b822733`：FAIL。实际编辑器保存后 18 个 Condition 字符串顺序变化；未发现显示属性泄漏。需要进一步判断验收要求是“保持原字符串”还是“逻辑表达式语义等价”，不能直接把顺序差异认作 18 个功能缺陷。
- `f7ed2b5d`：BLOCKED。实际后处理生成了输出，但缺少独立、完整的结果对照基准；得到 GC/DC 数字和 KLARF 文件本身不能证明正确。
- `e99f34a1`：BLOCKED。登录后导航阶段 UIA 多次为空，Binning/Counters 删除与即时统计目标操作未完成。
- `cfe9594b`：ERROR。锁图测试错误要求合法 Project 节点必须有 resolved 字段。
- `38b7b397`：ERROR。下一版锁图测试遗漏 RID 从基础框架继承依赖，同时自定义 Xunit 消息又触发旧分类问题。

**10 月 27 日（35 份报告，包含两轮 10 月 28 日复验）**

原批次只处理首个 `00b3004a`，因远端采集/同片重跑的测试与数据缺口保留 NEEDS_CASE；其余 9 项仍 PENDING。根因首先是编排没有继续完成整批，而不是后续 9 项产品测试失败。

第一轮复验全部收尾：7 FAIL、2 NEEDS_CASE、1 ERROR。7 个 FAIL 都来自公共 daily-recent10 基线。工程修复记录定位为 PMT 路径 `Configs\\Vega100Configs` 与 `Configs/Vega100Configs` 被按原始字符串比较；针对改动的组件测试实际通过。v3 统一分隔符后相关基线通过，历史 FAIL 保留。

第二轮复验全部收尾：8 BLOCKED、1 ERROR、1 NEEDS_CASE。公共基线已恢复，但 UI 证据校验仍未满足。`TargetUiAcceptance` 的职责是读取当前会话 evidence.json 并审核步骤；缺文件时只记 SKIP 并返回，它本身不操作软件。只有把实际操作和证据生产接上，重复调用它才可能取得不同结果。仅凭归档无法判断每个 slot 具体缺哪个文件或哪一步操作。

两个独立问题需要保留：`bd9ccb55` 的目标构建两轮均出现 CS0103/FileName 未定义，是报告明确记录的产品源码构建失败；`c30defb8` 的 exact committed lock 未通过 locked restore，但归档未包含恢复日志，不能进一步断言是包源、网络、缓存还是锁内容不一致。本地产品仓库未能读取 `bd9ccb55` 的历史树，本次没有独立复现该编译错误。

**10 月 28 日（14 份报告，包含 10 月 29 日复验）**

原批次首项 BF 因真实 UI 用例/输入/基准缺口为 NEEDS_CASE，后面 5 项 PENDING，根报告仍有 6 项未收尾。独立控制器专项随后把 6 项全部收尾：1 ERROR、5 NEEDS_CASE。

BF 新生成用例在 5 个 logic 调用和 5 个 UI 配置调用中均为 `ERROR / DAILY_RUNNER_INVOCATION`，runId=None。该状态由 Python 调用包装器产生，说明没有取得可核验的本次 Runner 结果；它不能证明 BF 业务断言失败。其余项目出现模型生成失败、一次 Owned stage exceeded 480s、编译修正/准备失败。基线 PASS 只证明基线运行正常。由于模型日志和 invocation 原因明细不在归档，不能把生成失败擅自归为额度、认证或某个服务故障。

**10 月 29 日（22 份报告，实际运行跨到 10 月 30 日）**

3 个提交全部流程收尾：2 NEEDS_CASE、1 FAIL。与此前相比，控制器已经能够生成真实组件/UI 测试并跑完队列，但验收质量问题仍在。

- `e05bb7f7`：5 个已执行 case 均 PASS；依赖升级的完整消费者行为、clean-cache/locked restore 等没有闭环，仍 NEEDS_CASE。
- `8894b083`：4 个已执行 case 均 PASS；直接调用异常处理器不能证明真实订阅、终结器投递、日志通知隔离及 Vega200 真机采集场景，仍 NEEDS_CASE。
- `d07d0e70`：组件层 65 条全部 PASS、0 条失败，但 registry 要求至少 70 条，故 BLOCKED/ZERO_ASSERTIONS_MATCHED。源码四个 Fact 的正常路径记录数为 14+13+19+19=65，可解释此门槛错配。应审核预定覆盖、确认缺失的是场景还是计数；不能直接删除门禁换取 PASS。
- 同一 Uniformity UI case 分别在 Vega100-PIC、Vega100-Coherent、Vega300-HWv1、Vega300-HWv2、Vega500 执行，共五个 FAIL。四个是登录/就绪等待 3 分钟后仍“忙碌”，Coherent 是 Assert.NotNull。源码在业务操作前调用 LaunchAndPrepare 和 WaitReady；与主窗口为空/启动前置失败相符。没有堆栈与截图，Coherent 的确切空值位置仍需原始证据确认。
- 因为尚未进入导入、重入和统计比较，五个 FAIL 不能证明 Uniformity 数值逻辑有回归。它们是五个配置，不是同一配置连续重试五次。

**3. 为什么会反反复复失败**

**旧缺口被重放，重跑没有改变前置条件。** 10 月 20—22 日旧规则明确把前批未验收提交重新编入新批次，缺用例数量从 9/11 增到 12、15。同一错误探针重复运行，既没有新测试，也没有新的输入或基准。后来改为不自动重放历史，减少了重复执行，但历史欠账仍应保留在独立跟踪列表中。

**通用基线与业务验收耦合过重。** 一个大 daily-supplement 同时承载 Binning、交付计数、Run 栅栏、LPD；一个取消测试失败拖累多个引用它的工作项。同理，一处 PMT 路径比较错误让 7 个不同 SHA 都出现 FAIL。应按失败断言所属需求归因，同时单独报告公共基线的健康状态。

**用例“能编译、能发现”与“能完成验收”之间缺少执行校验。** Uniformity 的 minAssertions 来自模型输出，生成器只检查正数后写入 registry；65/70 是具体后果。BF 的 runId=None、UI 就绪超时又说明，发现测试并不证明调用入口、宿主和 UI 链路可运行。新用例需要在冻结前的独立试运行中确认启动、测试匹配、断言数量与业务目标对应关系。

**历史编排依赖聊天持续推进，后来的控制器主要解决了“跑完”，没有自动补齐所有测试资产。** 10 月 23、27、28 日存在明显的候选计划或首项之后停滞。当前 queue_driver/autopilot 已把队列、子进程和收尾放进程序，10 月 29 日 pending=0 体现了进展；但 FINISHED 与完整验收仍是两个指标。一次泛化的模型生成无法凭空补齐真实业务基准、交互步骤和远端环境。

**公共 UI 前置失败被标成业务 FAIL，并在每种配置上重复消耗时间。** `LoginFlow.WaitReady` 使用 Assert.Fail 抛出启动超时；`L2TestBase` 使用 Assert.NotNull 表示主窗口未取得；Runner 又优先将 Xunit 类型认作断言失败。生成策略明确要求复用这条登录链，因此公共前置缺陷会传播到新生成 UI 用例。需要用明确的启动/认证/就绪错误类型或阶段码区分 BLOCKED/ERROR 与目标业务断言 FAIL，不能依赖中文/英文错误文本猜测。

**“实际 UI 操作”与“审核 UI 证据”曾混淆。** DailyUi20261028 只验收 evidence.json，不会启动目标流程。重复调度证据校验器无法代替生产证据的操作端。当前 authored Uniformity 已有实际 FlaUI 动作，但停在公共登录前置；两代问题不能合并说成同一个故障。

**证据与展示也会放大失败感。** Uniformity 测试 finally 调用 Flush，基类 Dispose 再次 Flush，而 AssertionRecorder 每次将完整列表写成新文件、没有清空或去重；所以单次 workflow.execution 被统计为两条 FAIL。这与五份 Runner 报告中重复的相同失败行一致。再加上旧的“未重编=旧二进制”模板、对逐 SHA 仍显示“合并批次需二分”的模板，以及“no UI session created by drain”固定收尾文字，用户容易把程序内部职责说明误解成整个流程未执行。需要分别展示历史原 verdict、后续复验结果与当前有效结论。

**4. 已有修正与仍需优先处理的事项**

当前工程已经能看到：预取消期望改为 0 并验证缓存未消费；删除错误的 l2Uia 契约推断；PMT v3 统一路径分隔符；LockGraphV3 支持 Project/RID；按 SHA 隔离测试与构建；确定性队列与独立控制器。历史专项也报告了部分修复验证。它们不能自动改写旧报告，也不能证明所有运行机已部署相同版本。

建议按下列顺序修复，并给每项设置可观察的退出条件：

1. **先修公共登录/就绪与错误分类。** 把四个 Busy 的状态通道、PID、主窗口、登录标记、菜单状态、初始化日志一起核对；Coherent 核对主窗口等待结果。先让一条授权的 Simulator 基础 UI 流程稳定走到目标页面，再恢复五配置业务矩阵。出口是能到达目标动作且前置异常被正确分类。
2. **修 Uniformity 的计数与证据重复。** 对齐 65 条实际断言和预定需求，补齐真正遗漏的场景或纠正有证据的登记错误；Flush 使用增量或幂等写入。新版本、独立复验，保留原冻结记录。出口是四个组件 Fact 的覆盖和门槛一致，单次失败只计一次。
3. **给新生成测试加入冻结前的完整调用试运行。** 验证 Runner 能落 result.json、test filter 命中、目标 DLL 匹配、断言数量可靠，UI 最少能走通前置。BF 的 DAILY_RUNNER_INVOCATION 应先取回 outcomes.verdictReason 和调用日志，再定位，而非盲目重生成业务代码。
4. **把测试资产变成持久可复用输入。** 为后处理、校准、Recipe/Job、UI 路径保存版本化 fixture、来源和独立 oracle；真机/Linux/Host 场景单列外部验证队列。缺数据时继续其他范围，但欠账需要明确负责人和下一动作。
5. **拆分汇总与故障归属。** 分别统计执行完整性、业务覆盖率、环境阻塞、工具错误、产品断言失败、构建失败；共同基线失败只计一个根因并列受影响 SHA。对相同失败指纹做聚合，只有代码/环境/fixture 发生变化才安排有意义的复验。
6. **专项追踪真实产品问题。** 保留 `bd9ccb55` 构建失败；对 18 个 Condition 排序差异做语义与验收契约核对；取回 `c30defb8` locked restore 原日志。它们不应被公共探针噪声淹没。

**5. 关键工程证据**

- [预取消测试的当前契约](D:/Code/Vega/VegaAutomation/VegaAutoTest/probes/Vega.AutoTest.CommitProbe/DailySupplementLogic.cs:75)：当前期望 0；与旧报告 expected:1 对照。另只读检查了产品历史 `cebf91e9` 的 QT1426ProxyDataCollector.GetDataGroup，确认取消检查先于 TryGetData。
- [Runner 契约检查](D:/Code/Vega/VegaAutomation/VegaAutoTest/runner/Vega.AutoTest.Runner/ResultContractCheck.cs:37)：当前已移除无 UIA 与逻辑断言混淆的旧检查。
- [PMT v3 路径比较](D:/Code/Vega/VegaAutomation/VegaAutoTest/probes/Vega.AutoTest.CommitProbeV3/DailyCommitBatchLogic.cs:89)、[历史修复记录](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/analysis/repair-20261028/report.md:18)：7 个旧基线 FAIL 的归因及独立补测。
- [UI 证据审核器](D:/Code/Vega/VegaAutomation/VegaAutoTest/probes/Vega.AutoTest.DailyUi20261028/TargetUiAcceptance.cs:28)：无当前 receipt 时 SKIP，不执行目标界面操作。
- [登录就绪等待](D:/Code/Vega/VegaAutomation/VegaAutoTest/harness/LoginFlow.cs:28)、[主窗口前置](D:/Code/Vega/VegaAutomation/VegaAutoTest/harness/L2TestBase.cs:45)、[Runner 分类](D:/Code/Vega/VegaAutomation/VegaAutoTest/runner/Vega.AutoTest.Runner/VerdictResolver.cs:145)：前置失败进入 Xunit 断言的机制。
- [Uniformity UI 源码](D:/Code/Vega/VegaAutomation/VegaAutoTest/probes/authored/v62156ef44b/d07d0e7045b7/UniformityWorkflowTests.cs:42)、[组件断言源码](D:/Code/Vega/VegaAutomation/VegaAutoTest/probes/authored/v62156ef44b/d07d0e7045b7/UniformityComponentTests.cs:16)、[70 条门槛](D:/Code/Vega/VegaAutomation/VegaAutoTest/cases/registry.json:2014)。
- [重复 Flush 的记录器](D:/Code/Vega/VegaAutomation/VegaAutoTest/harness/AssertionRecorder.cs:39)、[基类再次 Flush](D:/Code/Vega/VegaAutomation/VegaAutoTest/harness/L2TestBase.cs:80)：单条失败写入两份证据的机制。
- [Runner 调用异常包装](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/daily.py:424)、[模型门槛登记](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/model_author.py:275)、[有界源码请求](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/autopilot.py:136)。
- [程序化队列](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/queue_driver.py:77)、[实际 UI 调度](D:/Code/Vega/VegaAutomation/VegaAutoTest/daily/autopilot.py:402)：当前执行职责与历史对话推进的区别。

所有原报告路径、状态摘要和 SHA256 见同目录 `report-index.md` 与 `report-inventory.json`。原始报告未修改。
