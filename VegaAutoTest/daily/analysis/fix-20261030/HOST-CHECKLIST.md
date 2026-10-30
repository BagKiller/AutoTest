# 2026-10-30 修复集上机验证清单

本机(D:\Code\Vega\VegaAutomation)已完成代码修复与本地回归;验证机(C:\AutoTest\AutoTest\VegaAutoTest)需按下表执行。
修复集基线提交: `a79b7376f03debd3332fdb660bf350febc08f63c`。2026-10-30 验证机已执行 fast-forward pull 并核对；随后补齐启动器缺失的 `-RuntimeCheck` 参数。

## 0. 部署前置(不完成则以下全部无效)

1. 本仓库修复提交并推送到验证机拉取的同一远端。
2. 验证机 `git -C C:\AutoTest\AutoTest\VegaAutoTest pull` 后核对 HEAD。
3. `& C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe -File .\daily\Start-DailyValidation.ps1 -RuntimeCheck`(不收集不执行,只查环境)。
4. `& $python -B .\daily\autopilot.py --status` 确认无残留活动控制器。

## 1. 当晚批次预期(对照旧失败模式)

以下是执行到相应场景时的预期，不是今晚必然出现的结果。每日仅收集当日新提交，不自动重放历史 Uniformity SHA；验证该历史修复需要独立 revalidation，并显式选用 fix01 用例。五个 UI slot 中，仅当首个 slot 的所有用例均返回共享登录/就绪 BLOCKED 且控制器没有额外异常时，才触发其余四个 slot 跳过。成功进入业务测试时应记录真实结果，不要求凑成“1 个 BLOCKED + 4 个跳过”。

| 旧失败模式(归档证据) | 修复后预期 | 判定点 |
| --- | --- | --- |
| Uniformity UI 在 5 个机型上重复 FAIL"登录/就绪超时,最后状态'忙碌'"(20261029T161513Z 等 5 个 runId) | 第一个配置 BLOCKED/DEVICE_NEVER_IDLE,其余配置记 blocker.json + attemptEvidence 跳过;总失败数 1 而非 5 | 根报告该提交状态、`ui/<slot>/blocker.json` |
| 前置失败误报 ASSERTION_FAILED | verdict=BLOCKED,verdictCode=LOGIN_NOT_READY/DEVICE_NEVER_IDLE/UI_NOT_READY/VEGA_PROCESS_EXITED 之一 | 子目录 runner result.json |
| 组件 65 条全 PASS 仍 BLOCKED 65/70 | fix01 case 门槛 65,PASS/OK | `authored-v20261030fix01-*` 的 result.json |
| 同一失败断言出现两行 | 单次 workflow.execution 只计一次 | result.json assertions 明细 |
| 公共基线失败拖 N 个提交全 FAIL | 提交状态保留自身结论;报告底部"公共基线"表列出一次 + 受影响 SHA | 根 report.md 基线表 |

## 2. "忙碌"若复现:取证定位顺序

修复后每次 BLOCKED 会落 `ui-readiness.json`,按此读:

1. `timeline`:设备状态时间线。到过"空闲"又回"忙碌"= 扫描后置任务;一直"忙碌"= 初始化链卡死。
2. `mongoReachability`:`unreachable` = MongoDB 未启动(README 记载的 Busy 首因),先查 `Ensure-DailyDatabase.ps1`。
3. `loginSubmitted=false` 时结合 `dialogError`、Runner 的 LOGIN_NOT_READY 原因及认证器日志判断是否读密失败，再核对 `C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe` 与被测 Vega 的 RootCondition/机器 ID 对齐(见 `authenticator-flow.json`)；该字段本身不能证明密码已正确或登录已成功。
4. `verdictCode=VEGA_PROCESS_EXITED` = Vega 启动后自灭,查取证日志的启动段与 Windows 事件日志,不是登录问题。
5. `stateChannel` 确认状态读取通道;UIA 通道下状态栏结构变化会让 state 恒空,需对照 `_wi*_l2.txt` 的树 dump。

## 3. 需要复验的三个历史真实问题(独立 revalidation,不改历史批次)

### 3.1 d07d0e70(Uniformity,#3722)— fix01 版本首次实跑

- 显式选用 `authored-v20261030fix01-d07d0e7045b7-uniformity-component-contract`(门槛 65)与 `...-import-reentry-statistics`(uia,5 配置)。
- 目标 SHA `d07d0e7045b7...`;须完成 logic + 五配置 UI + 会话 finally 恢复核对 + `--check-complete` 退 0。
- 注意:五配置 UI 依赖第 2 节的"忙碌"已解决;若仍 BLOCKED,按第 2 节取证后先修环境再跑。

### 3.2 bd9ccb55(PMT 内置兜底,#3705)— 产品源码真实编译错误

- **确认**:两轮复验 CS0103"当前上下文中不存在名称 FileName"。取该提交 `Vega.Data.Calibration/CaliCommonDataDefinition.cs` `LoadRaw()`:两处 `Path.Combine(ConfigsSubPath, FileName)` 中 `FileName` 未定义(应使用 `PmtCaliConfigPaths.ConditionsFileName`)。
- 处置:产品缺陷,归产品团队修;修复合入后按独立 revalidation 复跑该 SHA。**不得**由自动化工程改产品源码绕过。

### 3.3 5b822733(组件配置编辑器,WI 3690)— 2026-10-30 验收标准决定

- 事实:编辑器保存后 18 个 Condition 字符串顺序变化;未发现显示属性泄漏;死条件/Feature UI 未验。
- 决定：Condition 按逻辑表达式语义等价验收，允许不改变含义的条件项重排，不要求表达式字符串逐字节一致。其余配置内容必须保持；Profile 展示字段不得写回。
- 依据：WI 3690 第 5 条要求保存前后配置内容一致、Profile 列只做展示，未明确要求文本格式或表达式排列顺序原样保留。本决定将“内容一致”解释为装配行为与配置数据保持一致。
- 独立复验必须保留 AND/OR 优先级、括号、否定以及机型/Profile/Feature 条件含义；不能靠简单排序字符串判等。还需覆盖有效/无效条件的装配与过滤结果、死条件提示和未启用 Feature 提示。
- 历史 FAIL、冻结计划和原始 Runner 结果保持不变。该标准决定本身不构成通过证据；补充独立复验通过后才能报告相应验收完成。本次未运行复验。

### 3.4 f7ed2b5d(后处理修复,PR 5000)— 缺独立对照基准

- 现状:真实执行得到 waferRunId 93355853799751680、GC/DC359、KLARF 23037 字节并归档,但无独立完整输出对照 oracle → BLOCKED。
- 处置:需要工单方提供可核对基准(预期 GC/DC 数量或对照 golden KLARF);到位后按独立 revalidation 复跑 `ui-Vega300-HWv1` 场景。

## 4. 回归底线

- `daily-recent10-v3`、`daily-supplement` 应持续 PASS(预取消期望已改 0、PMT 路径已统一分隔符)。
- 若再出公共基线 FAIL:根报告基线表只出现一行;修复基线后按表内受影响 SHA 清单安排复验,不做整批重放。

## 5. 收尾判定

- `daily.py --resume <根> --check-complete` 退出码 0 才算队列收尾;FINISHED 不等于验收 COMPLETE。
- 验收不通过项(NEEDS_CASE/FAIL/BLOCKED)按原样转述,保留 runId 与取证路径。
