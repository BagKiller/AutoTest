# 适配层验收记录（2026-10-26）

状态：PARTIAL。离线适配与门禁已实现、测试通过；未连接 Azure DevOps，未开展本轮产品 UIA/rebuild 或真实发布。

## 已运行

命令（仓库根）：`python -m unittest discover -s azure-devops-integration/tests -v`。

结果：19 tests，全部通过。测试使用临时合成数据，不代表产品验收或真实 ADO Test Point。

- T01–T04：保留四态、runId、退出码；ZIP 内原始 JSON 字节一致，源文件不变。
- T05–T08：旧构建/OK_STALE_BUILD 不能满足门禁；TRX Passed 不覆盖 Runner BLOCKED/ZERO_ASSERTIONS_MATCHED。
- T09–T12：缺 runId、非法/重复键 JSON、退出码冲突、路径越界、重复映射、缺失/多匹配 Point 被拒绝。
- T13–T14：离线无凭据/未验证兼容性保持 BLOCKED。**HTTP 401/403/404 场景未实现或测试**，当前无网络客户端。
- T15：本地同 run 输出拒绝覆盖。**创建远端 Run 后断网恢复与服务端幂等未实现或测试**。
- T16–T18：未发生 oracle 的提示、必要 oracle 门禁、独立 run 历史保留、execute 禁用。
- 额外测试：summary/重编真值表、四类门禁决策、缺层覆盖证明、Runner stdout 末行及退出码捕获、超时终止处理。

## 历史真实产物兼容性

读取现有 `VegaAutoTest/runs/20261013T054222Z_wi3549_effd304/result.json`，成功校验、生成 SHA-256 和 ZIP。

- 历史 runId：`20261013T054222Z_wi3549_effd304`，工作项 3549。
- 原始 verdict：PASS / OK，5/5 断言；commit `28f56fc69ec72a705972b09c8a820347b31a3deb`。
- builtFromCurrentSource=false：未使用当前源码重编，结论仅对旧二进制有效；未进行 L1 交叉验证。
- 本轮未启动该 Runner 进程；导入参数 exitCode=0 来自历史 JSON，**不是独立捕获的进程退出码证明**。本次只能证明读取/归档兼容性，不能证明本轮产品通过。
- 输出：`out/historical-preview/20261013T054222Z_wi3549_effd304/publication-manifest.json`、同目录 `evidence.zip` 和 `artifact-sha256-manifest.json`。
- publishStatus=BLOCKED；无真实 ADO Run/Result ID；未向服务器写入。

## 本机与远端限制

现有 Runner DLL 的 list-cases 实际执行成功。正确产品根下，经授权沙箱外 doctor 五项 OK。未运行 UIA、重编产品或硬件相关用例。

用户已提供 URL/项目并确认 PAT 未轮换。仅将非秘密连接信息保存在 config/ado-settings.local.json（gitignore）。旧凭据未读取或复用。需先轮换凭据，再确认 HTTP 例外/HTTPS、服务版本和权限，才能开展只读服务探测、P3/P4 隔离试点。

## 交付边界与回滚

仅新增 azure-devops-integration 目录。git diff 中原有 tracked 文件无改动；原 Runner/VerdictResolver/registry 未修改。已有未跟踪的方案和 daily/analysis 保留。

P1 已覆盖本地契约、调用入口、归档和 dry-run；P2 仅完成 doctor/list 与模板；P3/P4 未实施；P5 完成可信回执输入下的离线门禁，尚无现场回执签发、回归调度及真实发布评审。

回滚：停用新增 Pipeline，保留 out/runs 证据后撤销新增模块即可；未产生需要清理的 ADO 对象。完整操作见 README 与 discovery-report。
