# 每晚模型逐项验证（2026-11-17 起）

这是当前每日入口。北京时间 23:30，本机计划任务 `VegaAutoTest-NightlyAgent` 运行 `Start-NightlyAgent.ps1`，把 `nightly-prompt.txt` 交给 Cursor CLI。模型负责观察真实界面、决定下一步、操作并读取证据；脚本只负责把这段固定提示发出去。不要删除 `VegaAutoTest-NightlyAgent`。旧任务 `VegaAutoTest-DailyValidation` 和已删除的夜间批次不要恢复。

## 1. 收集与构建

- 读本文件、AGENTS.md §1.4、ui-operation-lookup.md 和 AI操作手册.md。读 automation memory。固定计划目标日 T；跨午夜仍是 T。shell 一律 login=false，绝对路径，固定 Python。
- 当日已有模型批次就复用。不要运行 `autopilot.py --daily`。不要删除 `VegaAutoTest-NightlyAgent`，也不要恢复已删除的 `VegaAutoTest-DailyValidation`。
- 用固定 Python `-B C:\AutoTest\AutoTest\VegaAutoTest\daily\model_daily.py --begin YYYY-MM-DD` 收集（仅在实际日期仍为 T 时）。它只刷新远端 master、保存全部当日可达提交及完整 diff、固定最新 HEAD、生成配对清单，不生成测试、不执行 UI。重复调用只返回同一窗口的新模式批次；发现旧模式批次会拒绝创建重复批次。
- 读取 manifest、requirement-scopes.json、collected-diffs 下每个完整 diff 和 model-units.json。每项身份是 `(提交完整 SHA, 工作项 ID)`，一个提交多个工作项分别验证，同一工作项对应多次提交分别保留关联。不得把整个 HEAD 的工单集合归给每个提交。PR 编号不当工作项；无匹配项记 NOT_APPLICABLE，明确“未匹配工作项，不做校验，跳过”，不记 NEEDS_CASE 或 FAIL。只自动刷新 packages.lock.json 的提交同样忽略。
- 用 PAT 对 ADO 只读读取每个匹配工作项正文、复现/验收条件、修订号及附件（可复用 coverage_preparation.fetch_sources 和 requirement_assets.fetch）；凭据不落日志。完整阅读，不截断工单/diff。读取失败保留 BLOCKED，不能当无绑定项跳过。
- 对有待验单位的批次，用固定 Python `-B ...\daily\model_daily.py --build <本轮根目录>`。它复用现有工作区治理/构建入口，只构建固定 HEAD 一次并保存 SHA/DLL 指纹。构建失败保留证据，不自动换旧二进制或重新建批次。无提交/全部跳过分别报告 NO_COMMITS/ALL_SKIPPED，不宣称产品验收。

## 2. 模型执行每个工作项＋提交

- 只要有复现步骤就必须复现，不要等【复现流程】【预期结果】【实际结果】同时出现。复现步骤字段有内容，或正文里有【复现步骤】、【复现流程】，都算有。必须打开本轮构建的 Vega Simulator，按步骤做界面复现，并给出实际测试结果。单测和代码阅读不能代替。若同时有【前置条件】且这台机器做不到，可以跳过，说明里写清做不到的是哪一条。打开并登录、在 Simulator 里编辑配方或导入工单已给的数据，都算做得到。
- 没有复现步骤时，不要因为验收标准或附件字段为空就停。标题、描述、diff 或已有数据里已经能看出页面、操作和可核对结果时，仍要打开 Vega，官方认证登录，看当前界面后再往下做。材料过简时标 `INSUFFICIENT_INFO`，原因写明「缺少关键校验信息」并列出缺项，然后跳过；不要编一套工单没写过的业务。通用模块用一个代表机型；有机型差异按需求选择。
- 界面探查必须一步一停：每个短探针只做一件事，跑完就读这次的 UIA dump 和截图，再写下一个探针。从登录一路写到断言的长脚本等于盲跑，不符合本条。正式 Runner 留到路径看清之后。允许以已定位控件当前边界计算标题行内点击点；不使用未经当前界面确认的旧坐标。ui-snapshot.json 的前50行不等于看过实际业务界面。
- 有复现步骤就按原文做。没有复现步骤、但其他信息够用时，按那些信息操作界面，逐步留截图和实际值。可以用短的、可观察结果的测试/宿主调用完成机械动作；模型在步骤间检查证据和决定后续流程。
- 对需要正式判定的场景，在本单位的独立版本目录保存真实业务测试、独立 registry.json，再用 Runner `verify --case ... --work-item ... --layers uia --registry <本单位registry.json> --repo-root <本轮固定HEAD工作区> --runs-dir <本单位attempt目录>` 记录正式结果。不能在测试中写入“模型认为通过”的常量断言；断言必须来自实际 UI 数据/运行输出。完整场景的 Runner 通过才标 PASSED_AT_HEAD。模型的视觉观察与正式 verdict 分别报告。
- 找不到控件、中文/英文差异、滚动或时序错误：立即查看该次截图、UIA、日志，修正下一版本的定位/准备再执行；保留每次源文件和原始结果，最多4个执行尝试。若发现真实业务断言 FAIL，不改期望/删断言来制造 PASS；据实报告。各项独立处理，单项阻塞不把其他项批量判失败。
- 可复用现有正式 PASS 用例的操作经验，但必须重新在本轮构建上实跑。3774 的标题行右击、中文“空间分析”和 KLARF 页签定位见查表。
- 每次会话先 `Start-ModelValidation.ps1 -Action Prepare`，保存 SessionPath；不论结果如何 finally 停本轮进程、Restore，并核对配置 SHA256。认证前置可用现有官方宿主流程；不绕过授权、不触发真机。`RESTORE_EXIT` 不是 0，或会话状态不是 `RESTORED`，先查恢复日志和哈希，查清之前停止后续会话，不要接着写下一份长脚本。

## 3. 报告和结束

- 模型逐项更新本轮 model-units.json 中的状态、attempt证据、runId、result.json路径；每个 Runner 的 verdict/verdictCode 原样保留。PASS、业务FAIL、ERROR/HARNESS_EXCEPTION、环境BLOCKED、需求信息不足和无工单跳过分开统计。不得因一个失败把整晚提交都标 FAIL。
- summary.json/report.md 按 SHA＋工作项列需求、实际操作、构建HEAD、用例、正式runId、截图、结论、未验范围；testingPolicy=latest-head、perCommitAcceptance=false。原始提交数、绑定单位数、跳过数、实际执行数分别列出。Runner未自行重编时注明，另引用本轮构建指纹，不误称为各SHA独立构建验收。
- 有待执行项不能称完成。先写 `report.md` 并更新 `model-units.json`，再运行 `model_daily.py --finish`。由该命令检查证据并写出 `summary.json`、`model-state.json`、`completion.json`。不要手写这三份文件。有绑定单位但没有 Runner 结果时，收尾必须表明未完成校验。
- 自动化 memory 记录 T、根目录、HEAD、逐项状态计数、正式运行数、清理恢复及剩余范围。C:低于30GB提醒，不擅自清理。无 push/PR、工单写入或外发消息。


## 审查后补齐的机械校验入口

- 收集后调用固定Python `model_daily.py --requirements <本轮根>`，只获取匹配工单及附件，模型仍须实际完整阅读。
- `--begin T` 允许跨午夜返回已存在的T批次，但不允许跨午夜为T新建采集窗口。初始化中断会恢复缺失清单，不覆盖已有结果。
- `--build` 同时构建/核对当前Runner，防止旧Runner不支持独立`--registry`。产品仍只构建一次。
- 模型完成叙述报告并更新单位后，必须运行 `model_daily.py --finish <本轮根>`，由它核对全部配对、正式结果和恢复证据并产生summary/completion，不手写完成文件绕过检查。每个已执行单位填写`resultPath`及`sessionPaths`（本单位所有Prepare会话目录，含失败尝试）；未能执行的单位填写`reason`和`attemptEvidence: [{"path": "本轮实际非空证据文件绝对路径"}]`。正式运行结果必须位于本轮根目录，工作项ID和目标HEAD匹配；不能引用历史PASS作为本轮验收。
- `execution-policy.json` 阻止 `autopilot.py --daily` 和旧 `daily.py` 收集重新创建夜间队列。Windows 任务安装脚本已不存在，不要补回。
