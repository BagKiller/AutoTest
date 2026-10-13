import json
import time
import logging
from concurrent.futures import ThreadPoolExecutor, as_completed
from model_manager import ModelManager
from tools.vega_runtime import TOOL_MAP as VEGA_TOOLS, TOOLS_SCHEMA as VEGA_SCHEMA
from tools.vega_control import TOOL_MAP as VCONTROL_TOOLS, TOOLS_SCHEMA as VCONTROL_SCHEMA
from tools.autotest import TOOL_MAP as AUTOTEST_TOOLS, TOOLS_SCHEMA as AUTOTEST_SCHEMA
from tools.code_explorer import TOOL_MAP as CODE_TOOLS, TOOLS_SCHEMA as CODE_SCHEMA
from tools.dev_guide import TOOL_MAP as DEV_TOOLS, TOOLS_SCHEMA as DEV_SCHEMA
from tools.system_ops import TOOL_MAP as SYS_TOOLS, TOOLS_SCHEMA as SYS_SCHEMA
from tools.azure_devops import TOOL_MAP as ADO_TOOLS, TOOLS_SCHEMA as ADO_SCHEMA
from tools.policy import TOOL_MAP as POLICY_TOOLS, TOOLS_SCHEMA as POLICY_SCHEMA
from tools.repair import TOOL_MAP as REPAIR_TOOLS, TOOLS_SCHEMA as REPAIR_SCHEMA
from tools.close_loop import TOOL_MAP as LOOP_TOOLS, TOOLS_SCHEMA as LOOP_SCHEMA
from tools.mcp_client import TOOL_MAP as MCP_TOOLS, TOOLS_SCHEMA as MCP_SCHEMA

logger = logging.getLogger(__name__)

ALL_TOOL_MAP = {}
ALL_TOOL_MAP.update(VEGA_TOOLS)
ALL_TOOL_MAP.update(VCONTROL_TOOLS)
ALL_TOOL_MAP.update(AUTOTEST_TOOLS)
ALL_TOOL_MAP.update(CODE_TOOLS)
ALL_TOOL_MAP.update(DEV_TOOLS)
ALL_TOOL_MAP.update(SYS_TOOLS)
ALL_TOOL_MAP.update(ADO_TOOLS)
ALL_TOOL_MAP.update(POLICY_TOOLS)
ALL_TOOL_MAP.update(REPAIR_TOOLS)
ALL_TOOL_MAP.update(LOOP_TOOLS)
ALL_TOOL_MAP.update(MCP_TOOLS)

ALL_TOOLS_SCHEMA = (VEGA_SCHEMA + VCONTROL_SCHEMA + AUTOTEST_SCHEMA + CODE_SCHEMA
                    + DEV_SCHEMA + SYS_SCHEMA + ADO_SCHEMA + POLICY_SCHEMA
                    + REPAIR_SCHEMA + LOOP_SCHEMA + MCP_SCHEMA)

# --------------- 全局配置 ---------------

MAX_ROUNDS = 15
MAX_PARALLEL_TOOLS = 4
MAX_CONTEXT_CHARS = 120_000
MAX_TOOL_RESULT_CHARS = 8_000
MAX_LLM_RETRIES = 2
LLM_RETRY_BACKOFF = 2

# 只留真实存在的危险面：任意命令、杀进程、工作项状态改写。
# 原 vega_move_axis/vega_load_wafer/vega_unload_wafer/vega_raw_command
# 随虚构工具一并删除（外挂化集成技术方案 §4.1.1）。
DANGEROUS_TOOLS = frozenset({
    "execute_cmd",
    "kill_process",
    "update_work_item_status",
    "start_code_repair",
    "write_repair_file",
    "commit_repair",
})

SYSTEM_PROMPT = """你是 **VegaAgent** —— Vega 晶圆检测上位机系统的专属 AI 助手。

## 你的能力

### 1. Vega 运行时只读探测
两个 HTTP 服务，分工不同，都只读：
- `vega_ping` / `vega_get_status` / `vega_get_metrics` — AgentService(:18900) 指标服务
- `vega_control_ping` — AgentControlService(:18901) 是否就绪（Vega 启动且插件已装配）
- `vega_control_tool_state` — 查询真实设备状态（区别于 UI 显示）
- `vega_control_recipe_list` / `vega_control_opened_recipe` / `vega_control_recipe` — Recipe 数据本体查询（排查"UI 显示 vs 数据错误"类问题的关键工具）
硬件动作命令（POST /cmd/*）不在工具面内，永远不会被调用。

### 2. 代码探索
直接操作 Vega 源码仓库：
- `read_file` — 读取文件内容
- `search_code` — 用正则搜索代码
- `list_project_structure` — 列出项目结构
- `find_component` — 查找 ComponentConfig.json 中的组件
- `trace_class` — 追踪类的定义、继承和方法

### 3. Vega 架构分析（MCP 高级工具）
通过 MCP 服务器调用，提供深度代码库分析能力：
- `vega_list_components` / `vega_find_component` / `vega_active_components` — 组件注册与激活分析
- `vega_trace_class` — 深度追踪类/接口（层级、DI、继承、方法）
- `vega_dependency_chain` — 项目级依赖链追踪（向上/向下）
- `vega_find_device` / `vega_list_devices` — 设备「接口-实现-仿真」三件套查找
- `vega_solution_structure` — 解决方案全貌分析
- `vega_project_deps` — 项目依赖树（项目引用、NuGet、反向依赖）
- `vega_search_code` — 智能代码搜索（按层级/文件类型过滤）
- `vega_trace_binding` — WPF XAML 绑定追踪（View↔ViewModel）
- `vega_architecture_overview` — 架构概览

### 4. 开发指导
- `explain_architecture` — 解释模块架构
- `review_code` — 代码审查
- `generate_code_snippet` — 按规范生成代码

### 5. 系统运维
- `execute_cmd` — 执行系统命令
- `get_top_processes` — 查看高资源占用进程
- `kill_process` — 终止进程
- `check_port` — 检查端口占用
- `get_system_info` — 获取系统信息

### 6. Azure DevOps 工作项处理
自动化处理 Azure DevOps 工作项：
- `fetch_new_work_items` — 获取新建且未分配的工作项列表
- `get_work_item_detail` — 获取工作项详细信息
- `post_diagnosis_result` — 发布诊断结果到工作项评论
- `update_work_item_status` — 更新工作项状态和指派人（危险操作）

### 7. 确定性验证（VegaAutoTest Runner）
「某工作项修没修好」的唯一裁决来源，判定逻辑在 Runner 内固化，不由你临场决定：
- `list_verifiable_work_items` — 列出已登记自动化用例的工作项
- `start_verification` — 启动验证（异步，返回 job_id；L2 层会拉起真实 Vega 界面）
- `get_verification_status` — 轮询结果，拿到 verdict 与断言摘要

### 8. 策略闸门与自动修复（policy.json，代码级强制）
改被测仓库只能走修复工具，工具内部已经调用闸门，不要自己用命令写文件或提交：
- `start_code_repair` — 工作区干净才建/切到 `auto/wi-<id>`
- `write_repair_file` — 受保护路径、超文件数或超行数时拒绝且不落盘
- `commit_repair` — 只提交本次写过的文件。不 push、不开 PR、不改工作项状态
查询闸门（不执行动作）：
- `policy_check_write` / `policy_check_commit` / `policy_check_clean_worktree`
- `policy_check_push` / `policy_check_pull_request` — 永久拒绝，只能人工
- `policy_check_work_item_state` — ADO 状态流转闸门（当前拒绝）

### 9. 未提交工作项闭环
新建、修复还没进本地历史的工作项，按这个顺序，不能跳步：
- `start_loop_baseline` — 用当前未重编的 Vega 复现，并记住界面指纹
- `get_loop_status` — 等到基线结束。只有 verdict=FAIL 才允许修复
- `start_code_repair` / `write_repair_file` / `commit_repair` — 闸门内改代码
- `finish_loop` — 重编重验，对比两份 result.json，并对比修复前后的界面指纹
- 停在人工闸门。`FIXED_UI_DRIFT` 表示行为从失败变成通过，但有控件被增删或明显位移，必须人看报告后再决定

## Vega 架构知识

Vega 是 .NET 8.0 WPF 项目，用于半导体晶圆检测设备（SP1/SP3/SP5）控制。

**四层架构**（依赖方向：上→下）：
```
UI (Vega.UI.*) — WPF MVVM, CommunityToolkit.Mvvm
 ↓
BL (Vega.BL.*) — Service 模式, I*Service 接口
 ↓
LD (Vega.LD.*) — 逻辑设备, 硬件抽象
 ↓
PD (Vega.PD.*) — 物理设备, *Sim 仿真类
 ↓
CD (Vega.CD.*) — 通信通道, IConnection
```

**DI 容器**: Autofac，通过 `ComponentConfig.json` 注册组件
**基类**: `XComponent`（GalaxyPlatform），提供 `Start()/Stop()` 生命周期
**组件创建**: `XActivator.Activator.CreateInstance<T>()`
**配置**: `Resources/MachineConfiguration/*.json`
**事件模式**: `BeforeXxx / AfterXxx` 事件对

## 工作策略

- 收到复杂问题时，先简要说明分析思路和将要采取的步骤
- 每轮工具调用后，简要说明发现了什么、下一步打算做什么
- 如果一个工具失败或超时，不要反复重试同一个工具，尝试其他工具或换个角度搜索
- 最终综合所有工具返回的信息给出完整答案

## 工作原则

- **危险操作必须确认**：调用 `execute_cmd`、`kill_process`、`update_work_item_status`、`start_code_repair`、`write_repair_file`、`commit_repair` 前，先向用户说明将要执行的具体操作，等用户明确确认后再调用
- 代码解释时结合架构层级，说明模块在整体中的位置
- 回复使用 Markdown 格式（代码块、表格、列表）让信息清晰
- 中文回复

## 验证纪律（硬约束，违反即无效结论）

1. 「是否修好了」只能由 `start_verification` + `get_verification_status`
   返回的 verdict 决定。**禁止根据代码阅读自行宣布"已修复/验证通过"。**
2. verdict 四态必须**原样转述**：
   - PASS → 已验证通过
   - FAIL → 未通过，附失败断言
   - BLOCKED → **无法验证**（环境问题，如 Vega.exe 缺失、MongoDB 未启动、超时）。
     不得写成"存在 Bug"，也不得写成"通过"
   - ERROR → 验证工具自身故障，请求人工介入
3. `target.builtFromCurrentSource == false` 时，必须显式标注
   "未使用当前源码重编，结论仅对旧二进制有效"（未重编跑出的 run 该字段
   恒为 false，只有 `--rebuild` 成功才 true；`vegaExeBuiltAt` 必须一并报出）。
4. `channels.l1Api.available == false` 时 oracle 交叉验证未实际发生，
   摘要不得表述为"强验证"（SKIP 的 oracle 断言注明"弱断言"）。
5. 引用验证结论必须带 `runId`；无 runId 的"已验证"视为无效。
   每次验证结束，Runner 会在 `result.json` 同目录写出 `report.md`。回复必须给出该路径，
   结论与报告一致。不要等用户索要，也不要另写一份和 `result.json` 不一致的结论。
6. 工作项未登记用例（verdictCode=NO_CASE_REGISTERED 或 `list_verifiable_work_items`
   里没有）时，如实说明"该工作项尚未登记自动化用例，需人工补充"，
   **不要伪造验证过程，不要现编一个看起来像验证的东西**。
7. 未提交、修复尚未进入本地 git 历史的工作项，必须走闭环而不是单次验证：
   `start_loop_baseline` → 仅当基线 FAIL 才修复 → `finish_loop`。
   没有失败基线就不要改代码。`finish_loop` 之后停住，不 push、不开 PR、不改工作项状态。
   界面指纹有增删或明显位移时，loopVerdict=`FIXED_UI_DRIFT`，交给人确认，不得自己宣布界面无误改。

## 工作项自动处理流程

当用户说"处理工作项"、"检查新任务"或"分析工作项 #ID"时：

1. **获取工作项**
   - 调用 `fetch_new_work_items` 获取新建未分配的工作项列表
   - 或调用 `get_work_item_detail(ID)` 获取指定工作项详情

2. **分析问题**
   - 仔细阅读工作项的标题、描述、复现步骤
   - 识别关键词（如组件名、功能模块、错误信息）

3. **代码检索**
   - 使用 `vega_search_code` 智能搜索相关代码（按层级/文件类型过滤）
   - 使用 `vega_trace_class` 深度追踪类的层级、DI、继承和方法
   - 使用 `vega_dependency_chain` 分析模块间耦合关系
   - 使用 `vega_find_device` 查找设备的接口-实现-仿真三件套
   - 使用 `read_file` 查看具体实现

4. **运行时排查（如需要）**
   - `vega_ping` 检查 Vega 是否在线；`vega_control_tool_state` 查真实设备状态
   - `vega_control_opened_recipe` / `vega_control_recipe` 拿数据本体，区分"UI 显示问题"与"数据错误"

5. **未提交工作项走闭环（修复还没进本地历史时必做）**
   - `start_loop_baseline(work_item_id)` 复现当前界面并记住指纹，`get_loop_status` 等到结束
   - 基线不是 FAIL：不要改代码，按验证纪律转述
   - 基线是 FAIL：`start_code_repair` → `write_repair_file` → `commit_repair`（危险操作，先向用户确认）
   - `finish_loop(work_item_id)` 重编重验并对比界面。报告 loopVerdict、两份 runId、uiDrift
   - 到此停止。不要 push、不要开 PR、不要改工作项状态

6. **生成诊断报告**
   报告应包含：
   - **问题分析**：对问题的理解和分类
   - **相关代码位置**：涉及的文件和代码行
   - **根本原因**：技术层面的原因分析
   - **验证结论**（如有 runId）：verdict 原样转述 + runId + 断言摘要
   - **建议方案**：修复建议或进一步调查方向
   - **风险评估**：修改可能影响的范围

7. **发布结果**
   - 调用 `post_diagnosis_result(work_item_id, report)` 发布到工作项
   - 可选：调用 `update_work_item_status` 更新状态为"进行中"（危险操作需确认）
"""


class VegaAgent:
    """Vega 专属 AI Agent，支持流式 LLM 输出和并行工具调用"""

    def __init__(self, model_manager: ModelManager):
        self.mm = model_manager
        self._rebuild_client()
        self.messages: list[dict] = [{"role": "system", "content": SYSTEM_PROMPT}]
        # 危险操作待确认状态：(call_sig, 拦截时的用户输入序号)。
        # 只有新的用户消息（用户侧回执）才解锁一次执行；模型同轮重试无效。
        self._pending_dangerous: tuple | None = None
        self._user_input_seq = 0

    def _rebuild_client(self):
        self.client, self.model_name = self.mm.build_client()

    def switch_model(self, model_id: str) -> bool:
        if self.mm.set_active(model_id):
            self._rebuild_client()
            return True
        return False

    def clear_history(self):
        self.messages = [{"role": "system", "content": SYSTEM_PROMPT}]
        self._pending_dangerous = None

    # ==================================================================
    # 上下文窗口管理 — 防止 token 溢出
    # ==================================================================

    def _estimate_chars(self) -> int:
        return sum(len(json.dumps(m, ensure_ascii=False)) for m in self.messages)

    def _trim_context(self):
        """当上下文超过 MAX_CONTEXT_CHARS 时，丢弃最老的完整对话轮次"""
        while self._estimate_chars() > MAX_CONTEXT_CHARS and len(self.messages) > 3:
            idx = 1
            while idx < len(self.messages) and self.messages[idx]["role"] != "user":
                idx += 1
            if idx >= len(self.messages) - 1:
                break
            next_user = idx + 1
            while next_user < len(self.messages) and self.messages[next_user]["role"] != "user":
                next_user += 1
            if next_user >= len(self.messages):
                break
            del self.messages[idx:next_user]

        if len(self.messages) > 2 and self.messages[1].get("role") != "user":
            self.messages.insert(1, {
                "role": "user",
                "content": "[系统提示：由于对话过长，更早的上下文已被截断]",
            })
            self.messages.insert(2, {
                "role": "assistant",
                "content": "好的，我了解。请继续提问。",
            })

    # ==================================================================
    # 工具结果截断
    # ==================================================================

    @staticmethod
    def _truncate_result(result: dict) -> dict:
        """限制工具返回文本大小，防止单条结果吃满上下文"""
        serialized = json.dumps(result, ensure_ascii=False)
        if len(serialized) <= MAX_TOOL_RESULT_CHARS:
            return result
        if "data" in result and isinstance(result["data"], str):
            overflow = len(serialized) - MAX_TOOL_RESULT_CHARS
            result = dict(result)
            result["data"] = result["data"][:-(overflow + 50)] + "\n… (结果已截断)"
        elif "content" in result and isinstance(result["content"], str):
            overflow = len(serialized) - MAX_TOOL_RESULT_CHARS
            result = dict(result)
            result["content"] = result["content"][:-(overflow + 50)] + "\n… (结果已截断)"
        return result

    # ==================================================================
    # LLM 调用 — 带重试
    # ==================================================================

    def _call_llm_stream(self):
        """调用 LLM 并返回 stream，网络/限流错误自动重试"""
        last_err = None
        for attempt in range(1 + MAX_LLM_RETRIES):
            try:
                return self.client.chat.completions.create(
                    model=self.model_name,
                    messages=self.messages,
                    tools=ALL_TOOLS_SCHEMA,
                    stream=True,
                )
            except Exception as e:
                last_err = e
                err_text = str(e).lower()
                is_transient = any(kw in err_text for kw in [
                    "timeout", "rate", "limit", "429", "500", "502", "503",
                    "connection", "network", "reset", "retry",
                ])
                if not is_transient or attempt >= MAX_LLM_RETRIES:
                    raise
                wait = LLM_RETRY_BACKOFF ** attempt
                logger.warning(f"LLM 调用失败（第 {attempt+1} 次），{wait}s 后重试: {e}")
                time.sleep(wait)
        raise last_err

    # ==================================================================
    # 危险操作拦截
    # ==================================================================

    def _run_tool(self, name: str, args: dict, handler):
        if handler is None:
            return {"status": "error", "message": f"未知工具: {name}"}

        if name in DANGEROUS_TOOLS:
            call_sig = (name, json.dumps(args, sort_keys=True))
            pending = self._pending_dangerous
            # 放行条件（缺一不可）：存在待确认操作 + 签名一致 + 拦截后有过新的用户消息
            # （pending 记录的 seq 严格小于当前 seq）。模型同轮重试不放行。
            if (pending is not None and pending[0] == call_sig
                    and pending[1] < self._user_input_seq):
                self._pending_dangerous = None  # 一次性回执
            else:
                self._pending_dangerous = (call_sig, self._user_input_seq)
                return {
                    "status": "blocked",
                    "message": (
                        f"⚠️ {name} 是危险操作，已被拦截。"
                        f"请向用户说明你要执行的具体操作内容和参数，"
                        f"等用户回复确认后，在**下一条用户消息**之后再调用此工具"
                        f"（同一次拦截后重复调用不会放行）。"
                    ),
                }

        try:
            return handler(args)
        except Exception as ex:
            return {"status": "error", "message": str(ex)}

    # ==================================================================
    # Streaming chat (Generator) — LLM 逐 token 流式输出
    # ==================================================================

    def chat(self, user_input: str):
        """
        Generator：逐步 yield 每个处理阶段。
        事件类型: status | content_delta | thinking | tool_call | tool_result | answer | error
        """
        self._rebuild_client()
        self._user_input_seq += 1  # 新用户消息 = 危险操作确认回执窗口开启
        self.messages.append({"role": "user", "content": user_input})

        for round_num in range(1, MAX_ROUNDS + 1):
            self._trim_context()

            yield {"type": "status", "content": f"第 {round_num}/{MAX_ROUNDS} 轮 — 正在调用 LLM…"}

            try:
                stream = self._call_llm_stream()
            except Exception as e:
                yield {"type": "error", "content": f"API 调用失败: {e}"}
                return

            # --- 流式累积 LLM 响应 ---
            content_parts = []
            tool_calls_acc = {}

            for chunk in stream:
                if not chunk.choices:
                    continue
                choice = chunk.choices[0]
                delta = choice.delta
                if delta is None:
                    continue

                if delta.content:
                    content_parts.append(delta.content)
                    yield {"type": "content_delta", "content": delta.content}

                if delta.tool_calls:
                    for tc_delta in delta.tool_calls:
                        idx = tc_delta.index
                        if idx not in tool_calls_acc:
                            tool_calls_acc[idx] = {
                                "id": "", "name": "", "arguments": "",
                            }
                        acc = tool_calls_acc[idx]
                        if tc_delta.id:
                            acc["id"] = tc_delta.id
                        if tc_delta.function:
                            if tc_delta.function.name:
                                acc["name"] += tc_delta.function.name
                            if tc_delta.function.arguments:
                                acc["arguments"] += tc_delta.function.arguments

            full_content = "".join(content_parts)
            has_tools = len(tool_calls_acc) > 0

            # --- 写入对话历史 ---
            assistant_dict = {"role": "assistant", "content": full_content or ""}
            sorted_tcs = []
            if has_tools:
                sorted_tcs = [tool_calls_acc[i] for i in sorted(tool_calls_acc.keys())]
                assistant_dict["tool_calls"] = [
                    {
                        "id": tc["id"],
                        "type": "function",
                        "function": {
                            "name": tc["name"],
                            "arguments": tc["arguments"],
                        },
                    }
                    for tc in sorted_tcs
                ]
            self.messages.append(assistant_dict)

            # --- 无工具调用 → 最终回答 ---
            if not has_tools:
                yield {"type": "answer", "content": full_content or ""}
                return

            # --- 有思考文本 → 通知前端折叠 ---
            if full_content:
                yield {"type": "thinking", "content": full_content}

            # --- 解析并分发工具调用 ---
            tasks = []
            for tc_data in sorted_tcs:
                func_name = tc_data["name"]
                try:
                    args = json.loads(tc_data["arguments"]) if tc_data["arguments"] else {}
                except json.JSONDecodeError:
                    args = {}

                handler = ALL_TOOL_MAP.get(func_name)
                tasks.append({
                    "tc_id": tc_data["id"],
                    "name": func_name,
                    "args": args,
                    "handler": handler,
                })

                yield {
                    "type": "tool_call",
                    "tool": func_name,
                    "content": json.dumps(args, ensure_ascii=False),
                }

            # --- 执行工具 ---
            if len(tasks) > 1:
                yield {"type": "status", "content": f"并行执行 {len(tasks)} 个工具…"}
                yield from self._execute_parallel(tasks)
            else:
                yield from self._execute_single(tasks[0])

        yield {"type": "error", "content": f"已达到最大轮次（{MAX_ROUNDS}），Agent 停止。"}

    # ------------------------------------------------------------------
    # 兼容旧接口
    # ------------------------------------------------------------------

    def chat_collect(self, user_input: str) -> list[dict]:
        """非流式调用，返回全部步骤列表（兼容旧 /api/chat）"""
        return list(self.chat(user_input))

    # ------------------------------------------------------------------
    # 工具执行
    # ------------------------------------------------------------------

    def _make_tool_message(self, task: dict, result: dict) -> dict:
        return {
            "role": "tool",
            "tool_call_id": task["tc_id"],
            "name": task["name"],
            "content": json.dumps(result, ensure_ascii=False),
        }

    def _execute_single(self, task: dict):
        result = self._run_tool(task["name"], task["args"], task["handler"])
        result = self._truncate_result(result)
        yield {
            "type": "tool_result",
            "tool": task["name"],
            "content": result,
        }
        self.messages.append(self._make_tool_message(task, result))

    def _execute_parallel(self, tasks: list[dict]):
        results = {}
        with ThreadPoolExecutor(max_workers=min(len(tasks), MAX_PARALLEL_TOOLS)) as pool:
            future_to_task = {
                pool.submit(self._run_tool, t["name"], t["args"], t["handler"]): t
                for t in tasks
            }
            for future in as_completed(future_to_task):
                task = future_to_task[future]
                try:
                    result = future.result()
                except Exception as ex:
                    result = {"status": "error", "message": str(ex)}
                result = self._truncate_result(result)
                results[task["tc_id"]] = (task, result)
                yield {
                    "type": "tool_result",
                    "tool": task["name"],
                    "content": result,
                }

        for task in tasks:
            _, result = results[task["tc_id"]]
            self.messages.append(self._make_tool_message(task, result))
