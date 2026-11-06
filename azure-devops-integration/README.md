# Azure DevOps 测试管理适配层

当前交付：离线 Adapter（P1），P5 发布门禁的离线部分。Python 3.10+，仅标准库，不安装依赖。
不修改原 Runner、VerdictResolver、registry 或 L0 工具。真实 ADO 服务兼容性未确认，网络发布尚未实现；`publish` **只生成离线预览**，`--execute` 始终拒绝。配置样例的开关不是绕过此限制的途径。

## 本地使用

在仓库根运行：

```powershell
python -m unittest discover -s azure-devops-integration/tests -v
python azure-devops-integration/adapter.py discover

# 使用原 Runner 结果，必须提供捕获到的进程退出码；不可从 verdict 猜退出码。
python azure-devops-integration/adapter.py publish --work-item 3549 --case wi3549 --result VegaAutoTest/runs/<runId>/result.json --process-exit-code 0 --out azure-devops-integration/out/preview

# 专用测试工位准备好后执行。层集合必须和 mapping 相同；默认不重编。
python azure-devops-integration/adapter.py verify --work-item 3478 --case wi3478 --layers logic,uia --repo-root <isolated-product-checkout> --out azure-devops-integration/out/local-run

# 配置实际 release baseline、结果路径、可信执行回执及人工审批后聚合。
python azure-devops-integration/adapter.py aggregate --spec azure-devops-integration/config/regression-suites.local.json --out azure-devops-integration/out/release-001
```

退出码：`discover` 返回 2（未确认）；`publish/verify` 保留非零 Runner 退出码，即使 Runner PASS 也返回 2（远端发布 BLOCKED）；契约/适配错误返回 3。聚合仅 READY_FOR_APPROVAL 返回 0，其他返回 2。**这些是适配层退出码，不得据此重写 Runner verdict。**

输出每个 run 独立目录：`publication-manifest.json`、`evidence.zip`、`artifact-sha256-manifest.json`。原始 JSON 逐字节归档，未修改。重复输出目录拒绝覆盖；重做预览请换 `--out`。归档仅允许指定 runs 根下的该 run，拒绝路径和链接外逃。证据可能含敏感运行信息，仅在本地或受控 Artifact 存储；发布前由负责人审查保留策略。

`--points` 可指定**规范化的只读快照**，结构为：

```json
{"planId":20,"suiteId":30,"complete":true,"points":[{"id":50,"testCaseId":10,"configurationId":40}]}
```

上述数字仅说明结构，不是真实映射；快照必须已处理全部分页。校验唯一 Case×Configuration 并核对 Point ID。快照验证不是在线权限或 API 验证，仍保持 BLOCKED。

## 发布门禁输入的信任边界

复制 `config/regression-suites.sample.json` 为 `.local.json`；每个 required item 填写真实 resultPath、processExitCode、layers 和 executionReceipt。回执应由可信 CI/受控构建流程在**执行时**记录，不可事后对当前 EXE 做哈希来假装当次执行证据。当前适配层不自动签发回执，也不提供远端真实性证明。

回执字段：`releaseId`、`runId`、`resultSha256`、`layers`、`gitCommit`、`exeSha256`、`environmentId`、`automationCommit`、`buildId`。baseline 要明确相同五项构建/环境指纹。审批需分别提供 manualTests、criticalBugs、deviceSafety、stationConfiguration，每项包含 `approved:true`、`approvedBy`、`evidence`。缺失回执、基准、覆盖证明均不能 READY；审批文件应由受信任人员维护。

门禁还检查 Runner PASS、非旧构建、clean 工作区、指定 commit 和必要 L1 oracle。最后仍需要发布负责人审批。手工签核只消费显式证据，不自动查询或改变 Bug 状态。

## 尚未完成的现场阶段

- P0：实际 URL、服务版本、API、许可证、角色和 PAT 轮换确认；目前 discover 只输出本地兼容性记录，不调用服务端。
- P2：真实 UIA/rebuild、交互式 Agent、Pipeline/Task 兼容验证。模板仅供确认环境后部署。
- P3/P4：创建手工测试体系、真实映射、网络发布、服务端幂等恢复、读后校验和 Web 验证。未实现 HTTP 写客户端，不能声称已打通。
- P5：可信 CI 签发执行回执、逐项回归调度及受控版本评审；当前聚合消费已有独立结果。

配置不保存 PAT；不从旧配置读取凭据。真实实例确认后再实现版本适配和发布。参见 `docs/discovery-report.md`（含变更计划与回滚）、`docs/integration-test-report.md`。
