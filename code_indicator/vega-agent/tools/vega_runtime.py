"""Vega 运行时只读探测（AgentService :18900，Prometheus 指标服务）。

实测结论（外挂化集成技术方案 §4.1.1）：该服务只有 3 个真实端点——
/ping、/metrics、/status。原 9 个工具中的 6 个虚构端点（wafer/motion/optics
状态、move/load/unload）在服务端 404，只会产出幻觉结论，已删除；
vega_raw_command 等价于任意 HTTP 命令执行，一并删除。

运行时真相的只读查询走 tools/vega_control.py（AgentControlService :18901）。
硬件动作命令（POST /cmd/*）永不进入 LLM 工具面。
"""

import os

import requests

AGENT_BASE_URL = os.environ.get("VEGA_AGENT_URL", "http://localhost:18900/vega-agent")
TIMEOUT = 10


def _call(method: str, endpoint: str, body: dict = None) -> dict:
    url = f"{AGENT_BASE_URL}{endpoint}"
    try:
        if method == "GET":
            resp = requests.get(url, timeout=TIMEOUT)
        else:
            resp = requests.post(url, json=body or {}, timeout=TIMEOUT)
        resp.raise_for_status()
        return resp.json()
    except requests.ConnectionError:
        return {"status": "error", "message": "无法连接 Vega AgentService(18900)，请确认 Vega 已启动"}
    except requests.Timeout:
        return {"status": "error", "message": "连接超时（10s）"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def vega_ping() -> dict:
    return _call("GET", "/ping")


def vega_get_status() -> dict:
    return _call("GET", "/status")


def vega_get_metrics() -> dict:
    return _call("GET", "/metrics")


TOOL_MAP = {
    "vega_ping": lambda args: vega_ping(),
    "vega_get_status": lambda args: vega_get_status(),
    "vega_get_metrics": lambda args: vega_get_metrics(),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "vega_ping",
            "description": "检查 Vega 上位机程序是否在线（AgentService :18900 /ping），返回连接状态和时间戳",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_get_status",
            "description": "获取 Vega 系统整体状态（AgentService :18900 /status）",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_get_metrics",
            "description": "获取 Vega Prometheus 指标（AgentService :18900 /metrics）",
            "parameters": {"type": "object", "properties": {}},
        }
    },
]
