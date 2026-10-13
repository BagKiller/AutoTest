"""
MCP 客户端包装器
使用官方 MCP Python SDK 与 vega-mcp-server 通信
通过后台 asyncio 事件循环桥接同步 Flask 环境
"""
import asyncio
import os
import threading
import atexit
import logging
from contextlib import AsyncExitStack
from typing import Dict, Any, Optional

logger = logging.getLogger(__name__)


class MCPClient:
    """MCP 客户端，使用官方 SDK 管理与 vega-mcp-server 的 stdio 连接"""

    CONNECT_TIMEOUT = 30
    CALL_TIMEOUT = 120

    def __init__(self, server_path: str):
        self.server_path = server_path
        self._loop: Optional[asyncio.AbstractEventLoop] = None
        self._thread: Optional[threading.Thread] = None
        self._session = None
        self._ready = threading.Event()
        self._stop_event: Optional[asyncio.Event] = None
        self._connect_error: Optional[str] = None
        atexit.register(self.stop)

    # ==================== 生命周期管理 ====================

    def start(self):
        """启动后台事件循环并连接到 MCP 服务器"""
        if self._ready.is_set() and self._session is not None:
            return

        self.stop()
        self._ready.clear()
        self._connect_error = None

        self._thread = threading.Thread(target=self._run_loop, daemon=True)
        self._thread.start()

        if not self._ready.wait(timeout=self.CONNECT_TIMEOUT):
            err = self._connect_error or f"连接超时（{self.CONNECT_TIMEOUT}s）"
            raise TimeoutError(f"MCP 服务器启动失败: {err}")

        if self._connect_error:
            raise RuntimeError(f"MCP 连接失败: {self._connect_error}")

    def stop(self):
        """停止 MCP 连接并清理资源"""
        if self._stop_event and self._loop and not self._loop.is_closed():
            try:
                self._loop.call_soon_threadsafe(self._stop_event.set)
            except RuntimeError:
                pass
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=10)
        self._ready.clear()
        self._session = None
        self._loop = None
        self._thread = None
        self._stop_event = None

    # ==================== 后台事件循环 ====================

    def _run_loop(self):
        """后台线程：运行 asyncio 事件循环"""
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        try:
            self._loop.run_until_complete(self._manage_session())
        except Exception as e:
            logger.error(f"MCP 事件循环异常: {e}")
            if not self._ready.is_set():
                self._connect_error = str(e)
                self._ready.set()
        finally:
            try:
                self._loop.run_until_complete(self._loop.shutdown_asyncgens())
            except Exception:
                pass
            self._loop.close()
            self._loop = None
            self._session = None
            self._ready.clear()

    async def _manage_session(self):
        """管理 MCP 会话的完整生命周期"""
        from mcp import ClientSession, StdioServerParameters
        from mcp.client.stdio import stdio_client

        self._stop_event = asyncio.Event()

        index_js = os.path.join(self.server_path, "dist", "index.js")
        if not os.path.exists(index_js):
            raise FileNotFoundError(
                f"MCP 服务器未构建，请先运行 npm run build: {index_js}"
            )

        env = os.environ.copy()
        env.setdefault("VEGA_WORKSPACE_ROOT", r"c:\Code\Vega\Vega_SW")

        server_params = StdioServerParameters(
            command="node",
            args=[index_js],
            env=env,
        )

        async with AsyncExitStack() as stack:
            transport = await stack.enter_async_context(
                stdio_client(server_params)
            )
            read_stream, write_stream = transport
            session = await stack.enter_async_context(
                ClientSession(read_stream, write_stream)
            )
            await session.initialize()
            self._session = session
            self._ready.set()
            logger.info("MCP 会话已建立")

            await self._stop_event.wait()

    # ==================== 公共 API ====================

    def _ensure_connected(self):
        """确保与 MCP 服务器的连接存活"""
        if (not self._ready.is_set()
                or self._session is None
                or self._loop is None
                or self._loop.is_closed()):
            self.start()

    def call_tool(self, tool_name: str, arguments: Dict[str, Any]) -> Dict[str, Any]:
        """同步调用 MCP 工具（内部桥接到 async SDK）"""
        try:
            self._ensure_connected()

            future = asyncio.run_coroutine_threadsafe(
                self._session.call_tool(tool_name, arguments),
                self._loop,
            )
            result = future.result(timeout=self.CALL_TIMEOUT)

            texts = [c.text for c in result.content if hasattr(c, "text")]

            if getattr(result, "isError", False):
                return {
                    "status": "error",
                    "message": "\n".join(texts) or "工具返回错误",
                }

            return {"status": "success", "data": "\n".join(texts)}

        except (TimeoutError, asyncio.TimeoutError):
            self.stop()
            return {
                "status": "error",
                "message": f"MCP 响应超时（{self.CALL_TIMEOUT}s），服务器已重启",
            }
        except Exception as e:
            self.stop()
            return {"status": "error", "message": f"MCP 调用失败: {e}"}


# ==================== 全局单例 ====================

_mcp_client: Optional[MCPClient] = None


def get_mcp_client() -> MCPClient:
    global _mcp_client
    if _mcp_client is None:
        server_path = os.environ.get(
            "VEGA_MCP_SERVER_PATH",
            os.path.join(os.path.dirname(__file__), "..", "..", "vega-mcp-server"),
        )
        _mcp_client = MCPClient(server_path)
    return _mcp_client


# ==================== 工具处理函数工厂 ====================

def _make_handler(mcp_tool_name: str):
    """为每个 MCP 工具生成统一的调用包装"""
    def handler(args: Dict[str, Any]) -> Dict[str, Any]:
        return get_mcp_client().call_tool(mcp_tool_name, args)
    return handler


# ==================== TOOL_MAP ====================

TOOL_MAP = {
    "vega_list_components":      _make_handler("vega_list_components"),
    "vega_find_component":       _make_handler("vega_find_component"),
    "vega_active_components":    _make_handler("vega_active_components"),
    "vega_trace_class":          _make_handler("vega_trace_class"),
    "vega_dependency_chain":     _make_handler("vega_dependency_chain"),
    "vega_find_device":          _make_handler("vega_find_device"),
    "vega_list_devices":         _make_handler("vega_list_devices"),
    "vega_solution_structure":   _make_handler("vega_solution_structure"),
    "vega_project_deps":         _make_handler("vega_project_deps"),
    "vega_search_code":          _make_handler("vega_search_code"),
    "vega_trace_binding":        _make_handler("vega_trace_binding"),
    "vega_architecture_overview": _make_handler("vega_architecture_overview"),
}


# ==================== TOOLS_SCHEMA ====================

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "vega_list_components",
            "description": "列出 ComponentConfig.json 中注册的所有组件，可按层级或条件过滤",
            "parameters": {
                "type": "object",
                "properties": {
                    "layer": {
                        "type": "string",
                        "description": "按层级过滤: Communication/Physical/Logic/Business/Platform/Facade",
                    },
                    "condition": {
                        "type": "string",
                        "description": "按条件过滤: Simulator, Tool, Vega200, Vega300, Vega500, Gen1, Gen2, Gen3",
                    },
                },
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_find_component",
            "description": "按关键词搜索 ComponentConfig.json 中的组件，返回完整注册信息和所有实现变体",
            "parameters": {
                "type": "object",
                "properties": {
                    "search_term": {
                        "type": "string",
                        "description": "搜索关键词，如 'EFEM', 'Motion', 'Laser', 'Camera'",
                    },
                },
                "required": ["search_term"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_active_components",
            "description": "显示指定工具类型下实际激活的组件列表，根据 ToolConfig 的 RootCondition 和 Features 解析",
            "parameters": {
                "type": "object",
                "properties": {
                    "tool_type": {
                        "type": "string",
                        "description": "工具类型: Vega200, Vega300, 或 Vega500",
                    },
                },
                "required": ["tool_type"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_trace_class",
            "description": "深度追踪 C# 类或接口：层级位置、DI 依赖、继承关系、公开方法",
            "parameters": {
                "type": "object",
                "properties": {
                    "class_name": {
                        "type": "string",
                        "description": "类名或接口名，如 'WaferHandlerService', 'IEFEM', 'MotionDevice'",
                    },
                },
                "required": ["class_name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_dependency_chain",
            "description": "追踪类的项目级依赖链。向下查看依赖了哪些项目，向上查看被哪些项目依赖",
            "parameters": {
                "type": "object",
                "properties": {
                    "class_name": {
                        "type": "string",
                        "description": "起始类名",
                    },
                    "direction": {
                        "type": "string",
                        "enum": ["up", "down"],
                        "description": "追踪方向: down=向下依赖, up=被谁依赖（默认 down）",
                    },
                },
                "required": ["class_name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_find_device",
            "description": "查找设备的「接口-实现-仿真」三件套（I*接口、*Device实现、*Sim仿真），并列出接口方法",
            "parameters": {
                "type": "object",
                "properties": {
                    "device_name": {
                        "type": "string",
                        "description": "设备关键词: Camera, EFEM, Motion, Laser, DAQ, Optics, FFU 等",
                    },
                },
                "required": ["device_name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_list_devices",
            "description": "按层级分组列出所有设备/服务概览，标注每个设备是否有完整的接口/实现/仿真三件套",
            "parameters": {
                "type": "object",
                "properties": {
                    "layer": {
                        "type": "string",
                        "description": "可选层级过滤: PD, LD, BL, CD",
                    },
                },
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_solution_structure",
            "description": "分析 Vega.sln 的完整结构：所有项目按层级分组、依赖关系、输出类型",
            "parameters": {
                "type": "object",
                "properties": {},
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_project_deps",
            "description": "查看特定项目的完整依赖树：项目引用、NuGet 包、反向依赖",
            "parameters": {
                "type": "object",
                "properties": {
                    "project_name": {
                        "type": "string",
                        "description": "项目名称，如 'Vega.BL.WaferRun', 'Vega.LD.EFEM'",
                    },
                },
                "required": ["project_name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_search_code",
            "description": "在 Vega 代码库中搜索代码，支持正则、文件类型和层级过滤，自动排除 bin/obj",
            "parameters": {
                "type": "object",
                "properties": {
                    "pattern": {
                        "type": "string",
                        "description": "搜索模式（支持正则），如 'SetOpticalMode', 'IWaferRunService'",
                    },
                    "file_type": {
                        "type": "string",
                        "description": "文件类型过滤: cs, xaml, json",
                    },
                    "layer": {
                        "type": "string",
                        "description": "层级过滤: UI, BL, LD, PD, CD",
                    },
                    "max_results": {
                        "type": "number",
                        "description": "最大结果数，默认 30",
                    },
                },
                "required": ["pattern"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_trace_binding",
            "description": "追踪 WPF XAML 绑定：同时搜索 XAML 和 C# 代码，理解 View↔ViewModel 绑定关系",
            "parameters": {
                "type": "object",
                "properties": {
                    "binding_name": {
                        "type": "string",
                        "description": "绑定名称，如 'StartInspectionCommand', 'IsRunning'",
                    },
                },
                "required": ["binding_name"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "vega_architecture_overview",
            "description": "获取 Vega 项目架构概览：分层设计、设计模式、关键概念、配置体系",
            "parameters": {
                "type": "object",
                "properties": {},
            },
        },
    },
]
