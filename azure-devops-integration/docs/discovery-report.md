# 现场盘点（2026-10-26）

状态：P0 本地已盘点，远端兼容性未确认；实施范围为离线 Adapter。

- 已阅读实施方案、VegaAutoTest README / AI 操作手册、Runner 的 Program、ResultModels、ResultWriter、ResultContractCheck 和现有 tools/autotest.py。
- solution 仍含 harness/spike/l2/runner 四工程；registry 已超出方案中的三个示例，包含自定义 logic100/500、无工作项基础设施用例。映射按真实 registry 校验，不限制 case 必须叫 wi{id}，不重写已有过滤器。
- Python 3.12.10、dotnet SDK 10.0.401 可用；现有 net8.0-windows Runner DLL 可运行，list-cases 输出已保存到 out/registry-observed.json。
- 默认 doctor：EXE 和构建脚本 MISS，18901 空闲、git/dotnet OK。显式使用历史结果中的产品根后，EXE/端口/脚本/dotnet OK，沙箱内 Git 因不同所有者而 MISS。经批准在沙箱外重跑只读 doctor，五项全部 OK；未修改 Git 全局 safe.directory。这仍不证明 MongoDB、交互式工位及设备测试就绪。
- 用户提供了 Collection URL 和项目 Vega_SW（地址保存在被忽略的本地配置）。用户明确确认 PAT 尚未轮换，必须停用旧凭据。产品/版本、API、许可证、角色、Repos/Pipelines/交互式 Agent 仍未确认。未读取旧配置中的凭据，未发送网络请求。提供的是 HTTP 地址，受控内网 HTTP 例外亦未确认。
- result.evidence.dir 实际为绝对路径；兼容文档中相对 VegaAutoTest 根的 runs/<runId> 写法。所有路径需解析后仍落在指定 run 目录，拒绝链接外逃。
- builtFromCurrentSource 不证明不可变发布包来源；发布门禁额外要求指定 commit、EXE 哈希、环境、测试仓库 commit 与人工签核证据。

## 变更清单（实施前）

仅新增 azure-devops-integration：标准库 Python CLI、契约校验、映射校验、受控归档、dry-run 发布预览、版本门禁、样例配置、离线测试和人工触发 Pipeline 模板。原 Runner / VerdictResolver / registry / L0 工具不变。

## Dry-run 计划

1. 合成四态及故障数据执行离线测试；原始 JSON 字节保留。
2. 对本机历史 Runner 证据执行严格检查；历史运行不能冒充本轮验证。
3. 验证缺映射、重复 Point、退出码冲突、路径遍历、旧构建、缺审批均不能误报可发布。
4. 目标环境确认后另行开展专用工位 verify / rebuild、兼容 API 和隔离发布试点；当前不操作 UI 或设备。

## 回滚

停用新增 Pipeline 入口，保留 out 和 runs 的历史证据，然后撤销新增适配层目录。原组件无需回滚。外部发布默认禁用，当前不存在新增 ADO 对象；未来上线后不得通过删除 Test Run 回滚历史。
