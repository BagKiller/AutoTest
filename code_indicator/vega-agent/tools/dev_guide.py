from tools.code_explorer import read_file, search_code, list_project_structure


def explain_architecture(module_name: str) -> dict:
    """搜索与模块相关的代码，收集上下文信息供 LLM 分析"""
    results = {}

    proj = search_code(f"Vega\\..*{module_name}", "*.csproj", 10)
    results["related_projects"] = proj.get("output", "未找到")

    cls = search_code(f"class.*{module_name}", "*.cs", 10)
    results["class_definitions"] = cls.get("output", "未找到")

    iface = search_code(f"interface.*I{module_name}", "*.cs", 10)
    results["interfaces"] = iface.get("output", "未找到")

    return {"status": "success", "module": module_name, "context": results}


def review_code(file_path: str) -> dict:
    """读取文件内容供 LLM 进行代码审查"""
    content = read_file(file_path)
    if content.get("status") != "success":
        return content

    return {
        "status": "success",
        "file": file_path,
        "content": content.get("content", ""),
        "total_lines": content.get("total_lines"),
        "instruction": "请根据 Vega 项目规范审查此代码，关注命名规范、架构分层、异步模式和线程安全"
    }


def generate_code_snippet(description: str, layer: str = "BL") -> dict:
    """提供 Vega 各层的代码模板上下文，辅助 LLM 生成代码"""
    templates = {
        "PD": "Physical Device 层：实现 I*Device 接口，需有对应 *DeviceSim 仿真类。构造函数注入 IConnection。",
        "LD": "Logic Device 层：封装 PD 层设备，提供业务抽象。继承 XComponent。",
        "BL": "Business Layer：Service 模式，继承 XComponent 实现 I*Service。通过构造函数注入 LD 接口。",
        "UI": "UI 层：MVVM 模式，ViewModel 继承 ObservableObject，使用 [AutoResolveDependency] 注入服务。",
    }
    pattern = templates.get(layer, templates["BL"])
    structure = list_project_structure()

    return {
        "status": "success",
        "description": description,
        "target_layer": layer,
        "pattern_guide": pattern,
        "project_structure": structure.get("layers", {}),
        "instruction": f"请按照 Vega {layer} 层规范生成代码。使用 .NET 8.0、C# latest、nullable enable。"
    }


TOOL_MAP = {
    "explain_architecture": lambda args: explain_architecture(args["module_name"]),
    "review_code": lambda args: review_code(args["file_path"]),
    "generate_code_snippet": lambda args: generate_code_snippet(
        args["description"], args.get("layer", "BL")),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "explain_architecture",
            "description": "解释 Vega 中指定模块的架构：查找相关项目、类定义和接口，帮助理解模块职责和依赖关系",
            "parameters": {
                "type": "object",
                "properties": {
                    "module_name": {"type": "string", "description": "模块名称，如 WaferHandler, Motion, EFEM, Optics 等"}
                },
                "required": ["module_name"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "review_code",
            "description": "读取指定文件并按 Vega 规范进行代码审查（命名、分层、异步、线程安全）",
            "parameters": {
                "type": "object",
                "properties": {
                    "file_path": {"type": "string", "description": "要审查的文件路径"}
                },
                "required": ["file_path"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "generate_code_snippet",
            "description": "按 Vega 代码规范生成代码片段，自动匹配目标架构层的模式",
            "parameters": {
                "type": "object",
                "properties": {
                    "description": {"type": "string", "description": "要生成的代码描述"},
                    "layer": {"type": "string", "enum": ["UI", "BL", "LD", "PD", "CD"],
                              "description": "目标架构层，默认 BL"}
                },
                "required": ["description"]
            }
        }
    },
]
