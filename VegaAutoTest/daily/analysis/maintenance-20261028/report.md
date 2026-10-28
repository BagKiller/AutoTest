# 项目复查、清理与提交记录

日期：2026-10-28。范围：Vega 自动化项目的编排、测试来源、Runner、MCP 桥接、工程引用和历史恢复工具。包含前次修复尚未提交的相关源码和报告；未修改被测产品源码。

## 本轮实际修复

1. `queue_driver.py` 与 `individual.py` 共用界面阻塞证据检查。真实阻塞证据有效后再次 drain 会继续后续提交；空文件、文件被更改或越界路径不能收尾。未执行的 UI 仍保留为未验，不变为通过。
2. `prepare_all` 逐提交捕获准备失败，记录 authoring.json 后继续准备其余提交。`case_authoring.py` 先核对目标 Git 树，跳过已删除测试源码，避免读取不存在的文件导致整批中断。失败仍需要 AI 审核，不能自动变成已覆盖。
3. CLI 拒绝在 merged/提交子目录上执行队列动作或完整性检查；拒绝将 check-complete 与执行动作混用，或将专项重验与队列动作混用。
4. `run_slot` 必须提供当前准备会话，核对目标目录、机型、profile 和配置文件校验值；有效会话进入执行后，即使用例调用异常也进行清理和恢复。
5. Runner 读取并执行 registry 中的 targetSha 约束。显式指定不匹配提交的用例会产生真实 `BLOCKED / CASE_TARGET_MISMATCH` 结果，且不运行测试层；按工单查找优先选择当前提交的用例，再选择通用用例，不选择其他提交的用例。
6. `requirement_assets.py` 修复中文项目名附件 URL 判断，并拒绝反斜杠形式的目录跳转。清理程序要求非空批次和提交标识，避免不完整记录被当成工作目录归属证明。
7. Runner 回归工程加入解决方案。移除版本控制内的 3 个 Python 缓存，补充缓存忽略规则。5 个历史恢复脚本移至 `analysis/recovery-20261027/tools/`，同步修正导入；旧基线版本与冻结测试仍有审计用途，保留原位置和内容。
8. 两套 MCP 工程固定 `@modelcontextprotocol/sdk` 为 1.32.2，autotest 桥接的 Zod 更新为 4.6.7，同步 npm 锁文件；清除审计发现的依赖漏洞。依据 [SDK 官方发布记录](https://github.com/modelcontextprotocol/typescript-sdk/releases) 和 [Zod 修复公告](https://github.com/advisories/GHSA-mrhx-c7xc-m3w5) 选择兼容版本。

## 验证

- 65 项 Python 流程回归通过，不访问工单、不启动 Vega、不推进每日游标。
- 13 项 Runner 回归通过，覆盖断言与工具错误分类、超时日志及目标提交约束。
- 真实 Runner CLI 在不匹配提交上调用绑定用例，退出 2，落盘 CASE_TARGET_MISMATCH，未执行任何测试层。
- 两套 MCP TypeScript 编译通过；真实 stdio 连接、初始化和 listTools 通过，分别返回 4 个及 19 个工具。依赖审计均为 0 项漏洞。
- 45 个 Python 文件语法解析、39 个 MSBuild 文件解析与本地工程引用检查、41 个登记用例及调度引用检查通过。4 个 daily PowerShell 脚本语法解析通过。
- 10 个专项复验冻结计划的测试源码、需求和附件指纹全部核对通过。check-complete 退出 0，pending=0；原有失败和未验范围仍保留。改动文件未发现本机配置中的已知凭据。

本轮没有重新进行完整软件界面、远端 Linux 采集或真实设备验收，不能把编排回归通过说成全部产品验收通过。前次产品真实编译失败与 UI 未验边界详见 [前次修复报告](../repair-20261028/report.md)。原始证据保留在 daily/runs；本轮额外验证产物在 `C:/AutoTest/validation/review-20261028/`。
