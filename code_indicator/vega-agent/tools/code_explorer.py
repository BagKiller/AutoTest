import os
import re
import json
import subprocess


def _workspace() -> str:
    return os.environ.get("VEGA_WORKSPACE_ROOT", r"c:\Code\Vega\Vega_SW")


def read_file(file_path: str, start_line: int = None, end_line: int = None) -> dict:
    ws = _workspace()
    full = os.path.join(ws, file_path) if not os.path.isabs(file_path) else file_path
    if not os.path.isfile(full):
        return {"status": "error", "message": f"文件不存在: {full}"}
    try:
        with open(full, 'r', encoding='utf-8', errors='replace') as f:
            lines = f.readlines()
        total = len(lines)
        s = max(1, start_line or 1) - 1
        e = min(total, end_line or total)
        content = ''.join(lines[s:e])
        if len(content) > 15000:
            content = content[:15000] + "\n... (truncated)"
        return {"status": "success", "file": full, "total_lines": total,
                "showing": f"{s+1}-{e}", "content": content}
    except Exception as ex:
        return {"status": "error", "message": str(ex)}


def search_code(pattern: str, file_glob: str = "*.cs", max_results: int = 30) -> dict:
    ws = _workspace()
    try:
        cmd = ["rg", "--no-heading", "--line-number", "--max-count", str(max_results),
               "--glob", file_glob, pattern, ws]
        result = subprocess.run(cmd, capture_output=True, text=True, timeout=15,
                                encoding='utf-8', errors='replace')
        output = result.stdout.strip()
        if not output:
            return {"status": "success", "matches": 0, "output": "没有找到匹配结果"}
        lines = output.split('\n')
        return {"status": "success", "matches": len(lines), "output": output[:8000]}
    except FileNotFoundError:
        return _fallback_search(pattern, file_glob, max_results)
    except subprocess.TimeoutExpired:
        return {"status": "error", "message": "搜索超时（15秒限制）"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def _fallback_search(pattern: str, file_glob: str, max_results: int) -> dict:
    """ripgrep 不可用时的回退搜索"""
    ws = _workspace()
    matches = []
    ext = file_glob.replace("*", "")
    for root, _, files in os.walk(ws):
        if '.git' in root or 'bin' in root or 'obj' in root or 'node_modules' in root:
            continue
        for fname in files:
            if ext and not fname.endswith(ext):
                continue
            fpath = os.path.join(root, fname)
            try:
                with open(fpath, 'r', encoding='utf-8', errors='replace') as f:
                    for i, line in enumerate(f, 1):
                        if re.search(pattern, line):
                            rel = os.path.relpath(fpath, ws)
                            matches.append(f"{rel}:{i}:{line.rstrip()}")
                            if len(matches) >= max_results:
                                break
            except Exception:
                continue
            if len(matches) >= max_results:
                break
        if len(matches) >= max_results:
            break
    output = '\n'.join(matches) if matches else "没有找到匹配结果"
    return {"status": "success", "matches": len(matches), "output": output[:8000]}


def list_project_structure() -> dict:
    sln_path = os.path.join(_workspace(), "Vega.sln")
    if not os.path.isfile(sln_path):
        return {"status": "error", "message": "Vega.sln 未找到"}
    try:
        with open(sln_path, 'r', encoding='utf-8') as f:
            content = f.read()
        projects = re.findall(r'Project\(".*?"\)\s*=\s*"(.*?)",\s*"(.*?)"', content)
        categorized = {"UI": [], "BL": [], "LD": [], "PD": [], "CD": [],
                       "Data": [], "Platform": [], "Facade": [], "Algo": [],
                       "Utility": [], "ThirdParty": [], "Other": []}
        for name, path in projects:
            if not path.endswith('.csproj'):
                continue
            placed = False
            for prefix in categorized:
                if f"Vega.{prefix}." in name or f"Vega.{prefix}" == name:
                    categorized[prefix].append(name)
                    placed = True
                    break
            if not placed:
                categorized["Other"].append(name)
        result = {}
        for cat, items in categorized.items():
            if items:
                result[cat] = sorted(items)
        return {"status": "success", "project_count": len(projects), "layers": result}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def find_component(name: str) -> dict:
    config_path = os.path.join(_workspace(), "Vega", "Resources", "ComponentConfig.json")
    if not os.path.isfile(config_path):
        return {"status": "error", "message": "ComponentConfig.json 未找到"}
    try:
        with open(config_path, 'r', encoding='utf-8') as f:
            text = f.read()
            text = re.sub(r'//.*?\n', '\n', text)
            data = json.loads(text)
        components = data.get("Components", [])
        matches = [c for c in components if name.lower() in c.get("Name", "").lower()]
        if not matches:
            return {"status": "success", "message": f"未找到名称包含 '{name}' 的组件", "matches": []}
        return {"status": "success", "matches": matches}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def trace_class(class_name: str) -> dict:
    """在代码库中追踪一个类的定义、继承和方法签名"""
    result = search_code(f"class {class_name}", "*.cs", 5)
    if result.get("matches", 0) == 0:
        return {"status": "error", "message": f"未找到类 {class_name}"}

    lines = result["output"].split('\n')
    file_path = None
    for line in lines:
        if ':' in line:
            parts = line.split(':')
            candidate = parts[0]
            if os.path.isfile(candidate):
                file_path = candidate
                break
            candidate = os.path.join(_workspace(), candidate)
            if os.path.isfile(candidate):
                file_path = candidate
                break

    if not file_path:
        return {"status": "success", "definition_search": result["output"], "file": None}

    file_result = read_file(file_path)
    content = file_result.get("content", "")

    class_match = re.search(rf'class\s+{class_name}\s*[:(].*', content)
    class_decl = class_match.group(0) if class_match else "未找到类声明"

    methods = re.findall(
        r'(?:public|private|protected|internal)\s+(?:static\s+)?(?:virtual\s+)?'
        r'(?:override\s+)?(?:async\s+)?[\w<>\[\]?,\s]+\s+(\w+)\s*\(',
        content
    )

    return {
        "status": "success",
        "file": file_path,
        "class_declaration": class_decl,
        "methods": list(set(methods))[:30],
        "total_lines": file_result.get("total_lines"),
    }


TOOL_MAP = {
    "read_file": lambda args: read_file(args["file_path"],
                                         args.get("start_line"), args.get("end_line")),
    "search_code": lambda args: search_code(args["pattern"],
                                             args.get("file_glob", "*.cs"),
                                             args.get("max_results", 30)),
    "list_project_structure": lambda args: list_project_structure(),
    "find_component": lambda args: find_component(args["name"]),
    "trace_class": lambda args: trace_class(args["class_name"]),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "read_file",
            "description": "读取 Vega 代码库中的文件内容，支持指定行号范围",
            "parameters": {
                "type": "object",
                "properties": {
                    "file_path": {"type": "string", "description": "文件路径（相对于 Vega_SW 或绝对路径）"},
                    "start_line": {"type": "integer", "description": "起始行号（可选）"},
                    "end_line": {"type": "integer", "description": "结束行号（可选）"}
                },
                "required": ["file_path"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "search_code",
            "description": "在 Vega 代码库中搜索代码，支持正则表达式。用于查找类定义、方法调用、接口实现等",
            "parameters": {
                "type": "object",
                "properties": {
                    "pattern": {"type": "string", "description": "搜索模式（正则表达式）"},
                    "file_glob": {"type": "string", "description": "文件过滤，如 *.cs, *.json, *.xaml，默认 *.cs"},
                    "max_results": {"type": "integer", "description": "最大结果数，默认 30"}
                },
                "required": ["pattern"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "list_project_structure",
            "description": "列出 Vega 解决方案的完整项目结构，按层级（UI/BL/LD/PD/CD/Data等）分类",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "find_component",
            "description": "在 ComponentConfig.json 中查找组件注册信息（接口、实现、条件、运行顺序）",
            "parameters": {
                "type": "object",
                "properties": {
                    "name": {"type": "string", "description": "组件名称（支持模糊匹配）"}
                },
                "required": ["name"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "trace_class",
            "description": "追踪一个 C# 类的定义、继承关系和方法签名",
            "parameters": {
                "type": "object",
                "properties": {
                    "class_name": {"type": "string", "description": "要追踪的类名"}
                },
                "required": ["class_name"]
            }
        }
    },
]
