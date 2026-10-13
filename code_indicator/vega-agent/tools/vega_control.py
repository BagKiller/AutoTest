"""Vega 运行时真相只读查询（AgentControlService :18901，Vega 内插件）。

端点与服务端实现一一对应（Vega.BL.AgentControlService/AgentControlService.cs）：
  GET /vega-agent/ping                       健康检查 + allowHardwareCommands 开关
  GET /vega-agent/query/tool-state           当前设备状态（ToolState）
  GET /vega-agent/query/recipe-list?folderId 指定文件夹 Recipe 列表
  GET /vega-agent/query/opened-recipe        当前打开 Recipe 完整内容
  GET /vega-agent/query/recipe?id={id}       按 ID 读 Recipe 完整内容

纪律（硬约束）：
- 只暴露 GET /query/* 只读端点；POST /cmd/*（硬件动作）永不进 LLM 工具面；
- opened-recipe / recipe 返回完整 JSON，可能很大——调用方（agent_core）会截断，
  优先用 recipe-list 先拿 ID/名字，确有需要再取完整内容。
"""

import os

import requests

BASE = os.environ.get("VEGA_CONTROL_URL", "http://localhost:18901/vega-agent/")
TIMEOUT = 10


def _get(endpoint: str) -> dict:
    try:
        r = requests.get(f"{BASE}{endpoint}", timeout=TIMEOUT)
        return {"status": "success", "data": r.json()}
    except requests.ConnectionError:
        return {"status": "error",
                "message": "AgentControlService(18901) 未就绪，确认 Vega 已启动且插件已装配"}
    except requests.Timeout:
        return {"status": "error", "message": "连接超时（10s）"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def vega_control_ping() -> dict:
    return _get("ping")


def vega_control_tool_state() -> dict:
    return _get("query/tool-state")


def vega_control_recipe_list(folder_id: int = 0) -> dict:
    return _get(f"query/recipe-list?folderId={folder_id}")


def vega_control_opened_recipe() -> dict:
    return _get("query/opened-recipe")


def vega_control_recipe(recipe_id: int) -> dict:
    return _get(f"query/recipe?id={recipe_id}")


TOOL_MAP = {
    "vega_control_ping":          lambda args: vega_control_ping(),
    "vega_control_tool_state":    lambda args: vega_control_tool_state(),
    "vega_control_recipe_list":   lambda args: vega_control_recipe_list(args.get("folder_id", 0)),
    "vega_control_opened_recipe": lambda args: vega_control_opened_recipe(),
    "vega_control_recipe":        lambda args: vega_control_recipe(args["recipe_id"]),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "vega_control_ping",
            "description": "检查 AgentControlService(18901) 是否就绪（Vega 启动且插件已装配时可用），返回服务状态",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_control_tool_state",
            "description": "查询当前设备状态（ToolState，如 Idle/Busy），用于判断 Vega 运行时真相，区别于 UI 显示",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_control_recipe_list",
            "description": "列出指定文件夹下的 Recipe（ID/Name/Path/修改时间），先用它拿 ID 再按需取详情",
            "parameters": {
                "type": "object",
                "properties": {
                    "folder_id": {"type": "integer", "description": "Recipe 文件夹 ID，默认 0（根）"},
                },
            },
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_control_opened_recipe",
            "description": "读取当前打开 Recipe 的完整内容（JSON）。返回可能很大，优先用 recipe-list；排查'UI 显示 vs 数据本体'类问题时用它",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "vega_control_recipe",
            "description": "按 ID 读取指定 Recipe 的完整内容（JSON）。返回可能很大",
            "parameters": {
                "type": "object",
                "properties": {
                    "recipe_id": {"type": "integer", "description": "Recipe ID（从 recipe-list 获得）"},
                },
                "required": ["recipe_id"],
            },
        }
    },
]
