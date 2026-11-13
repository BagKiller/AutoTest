# daily 可靠性修复与计划任务验证（2026-11-11）

已修复审查中确认的编译修正屏蔽、生成工程连带编译失败、缺失 harness API 契约、源码补查轮数过紧、WPF 启动无超时和统计口径混淆问题。Windows 计划任务已更新并处于 Ready/Enabled 状态。

## 实际改动

- `queue_driver.py` 独立记录编译/发现与运行预检结果；混合失败也会进入一次编译修正，发现调用超时不再取消其他候选。原始运行 FAIL/ERROR 保留。
- `autopilot.py`、`requirement_scopes.py` 只修正发生编译失败的 scope，向模型提供该 scope 的编译诊断，保留初次发现日志和 receipt。可编辑文件限制为失败用例自己的源码，已经用于其他运行用例的共享 helper 也禁止改动。
- `model_author.py` 的结构化输出要求显式 `sourceFiles`，每个用例生成独立工程并关闭默认扫描；注入带 SHA256 的真实 UiFind/WPF 契约和正确示例。修正 generic/object 服务解析的生成指引，禁止依赖修改 Lazy 私有字段的夹具。
- 真实模型冒烟暴露的大小写 key 问题已通过 schema 的 key/className 格式约束补齐。校验修正可纠正源码归属、机型和选择依据，不能修改原验收预期、步骤、oracle、测试类/layer 或最小断言数。
- 源码补查由三轮放宽至最多八轮，保留最后三分钟生成预算，累计推理仍限制 20 分钟；恢复摘要保留原始行号。覆盖缺口增加内部上下文、测试准备、外部数据/硬件及未知基准分类。
- `WpfTestHost.cs` 让启动和执行共用同一 deadline，初始化等待移出全局锁；超时宿主立即失效，迟到的初始化不能重新发布可用宿主。
- summary 新增执行目标/收集提交的显式统计和口径标签，保留旧字段兼容；控制器最终状态也带逐提交统计。补充记录 harness 全部源码指纹。
- 启动入口运行前检查 Git、三套 .NET8 共享运行时、registry/机型调度、模型客户端及现有登录。`-RuntimeCheck -LaunchDirectory` 可以保存实际调度环境检查结果。

## 验证结果

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| daily 全部 Python 回归 | 151 项通过 | [python-tests-final.log](python-tests-final.log) |
| Runner 契约回归 | 54 项通过，0 失败/跳过 | [Runner 日志](runner-contracts.log)、[TRX](runner-contracts/runner-contracts.trx) |
| WPF 独立进程 | startup、work、shared-budget、async 四项通过 | [构建日志](wpf-build.log)、[观察记录](wpf-contracts.json) |
| 真实 SDK 工程隔离 | 逻辑测试运行通过、UiFind 四个 API 编译通过；故意损坏的 UI 工程独立编译失败 | [authored-isolation.json](authored-isolation.json) |
| 真实模型生成 | 新 schema 成功返回，保存的源码不改动后安装、编译、执行通过，三个比较全部 PASS | [model-smoke.json](model-smoke.json)、[实际断言](model-smoke-assertions.json)、[生成响应](model-smoke-d065653d7c/response.json) |
| 原混合失败复现 | PASS/ERROR 两种组合现在均可进入编译修正 | `reproduce_mixed_failure.py` 当前运行退出 0 |
| 实际 Windows 调度环境 | 同用户、同入口、同工作目录执行 RuntimeCheck，LastTaskResult=0 | [调度检查记录](scheduler-runtime-probe.json)、[运行时结果](runtime-check.json) |
| 最新批次收尾门禁 | check-complete 退出 0，验收仍为原来的 INCOMPLETE | [门禁结果](fix-completion-check.json) |

真实模型检查最初产生不合法的大写 key，促成 schema 修正；另一次执行暴露合成验证脚本未设置断言落盘环境，已把脚本补齐为 Runner 使用的 RUNDIR/OUTDIR 形式，再运行保存的同一份模型响应通过。没有修改生成源码、预期或断言数量来消除失败。首次失败日志仍保留。

上述编译/生成/WPF 测试使用合成产品和框架 fixture，只用于证明自动化链路，不作为 Vega 产品或必需 UI 场景验收。

## 当前定时任务

- 名称：`VegaAutoTest-DailyValidation`。
- 入口：本仓库 `daily/Start-DailyValidation.ps1 -Watch`，使用已核对的 PowerShell/Python 路径。
- 每日触发配置：北京时间 23:30；Windows 当前报告下一次预计 **2026-11-11 23:30:30 +08:00**。
- Interactive 登录会话、IgnoreNew 防重复、StartWhenAvailable、23 小时任务上限保持有效。
- 调度器实际执行环境的 PATH 没有 Codex，仍通过本机安装目录找到客户端，并确认既有登录和三套 .NET8 运行时可用。
- 临时 RuntimeCheck 任务已删除。生产任务没有提前收集今日提交，也没有重放历史批次。

配置及最终状态见 [scheduled-task.json](scheduled-task.json)、[任务 XML](scheduled-task.xml)、[scheduler-final.json](scheduler-final.json)。原配置备份在 `scheduled-task-before.xml`。

最近四个批次的 87 个 manifest、冻结 plan、原始 Runner result、TRX 与 diff 在完成检查前后哈希全部一致。未修改产品源码、历史冻结计划、原始 Runner verdict，未降低断言要求。原有 registry/scheduling 的 staged/unstaged 修改保留；合成安装写入的是临时 registry。

今晚任务将使用修正后的控制器和生成器。真实新增需求、产品行为、数据前置及 UI 的最终验收结论仍由今晚 Runner 原始结果决定。
