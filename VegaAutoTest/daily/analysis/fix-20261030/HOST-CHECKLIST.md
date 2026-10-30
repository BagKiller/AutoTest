# 2026-10-30 修复集上机验证清单

本机(D:\Code\Vega\VegaAutomation)已完成代码修复与本地回归;验证机(C:\AutoTest\AutoTest\VegaAutoTest)需按下表执行。
修复集对应提交:<提交后填 SHA>;适用验证机副本 HEAD >= 该提交。

## 0. 部署前置(不完成则以下全部无效)

1. 本仓库修复提交并推送到验证机拉取的同一远端。
2. 验证机 `git -C C:\AutoTest\AutoTest\VegaAutoTest pull` 后核对 HEAD。
3. `& C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe -File .\daily\Start-DailyValidation.ps1 -RuntimeCheck`(不收集不执行,只查环境)。
4. `& $python -B .\daily\autopilot.py --status` 确认无残留活动控制器。

## 1. 当晚批次预期(对照旧失败模式)

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
3. `loginSubmitted=false` + `dialogError` = 认证器读密失败,核对 `C:\AutoTest\runtime\AuthenticatorApp\Authenticator.exe` 与被测 Vega 的 RootCondition/机器 ID 对齐(见 `authenticator-flow.json`)。
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

### 3.3 5b822733(组件配置编辑器,WI 3690)— 需要验收标准拍板

- 事实:编辑器保存后 18 个 Condition 字符串顺序变化;未发现显示属性泄漏;死条件/Feature UI 未验。
- 待拍板:验收要求是"保存后字符串逐字节保持原序"还是"逻辑表达式语义等价"。
  - 若逐字节:维持 FAIL,转产品确认排序是否可接受。
  - 若语义等价:补一个语义等价比较的独立复验用例(不修改历史冻结计划),PASS 后闭环。
- 未拍板前该提交保持 FAIL,不得重跑刷绿。

### 3.4 f7ed2b5d(后处理修复,PR 5000)— 缺独立对照基准

- 现状:真实执行得到 waferRunId 93355853799751680、GC/DC359、KLARF 23037 字节并归档,但无独立完整输出对照 oracle → BLOCKED。
- 处置:需要工单方提供可核对基准(预期 GC/DC 数量或对照 golden KLARF);到位后按独立 revalidation 复跑 `ui-Vega300-HWv1` 场景。

## 4. 回归底线

- `daily-recent10-v3`、`daily-supplement` 应持续 PASS(预取消期望已改 0、PMT 路径已统一分隔符)。
- 若再出公共基线 FAIL:根报告基线表只出现一行;修复基线后按表内受影响 SHA 清单安排复验,不做整批重放。

## 5. 收尾判定

- `daily.py --resume <根> --check-complete` 退出码 0 才算队列收尾;FINISHED 不等于验收 COMPLETE。
- 验收不通过项(NEEDS_CASE/FAIL/BLOCKED)按原样转述,保留 runId 与取证路径。
