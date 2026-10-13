import shlex
import subprocess
import psutil
import socket

# execute_cmd 白名单（外挂化集成技术方案 §4.1.2）：
# 只读命令直接放行；白名单外一律拦截，请人工在终端运行。
# 用 shell=False + 参数列表，避免管道/重定向/命令注入。
ALLOWED_PREFIXES = (
    "git status", "git diff", "git log", "git rev-parse",
    "dotnet --info", "dotnet build", "dotnet test", "dotnet run",
    "tasklist", "netstat", "ping",
)

# `git branch <name>` 会建分支，不能用前缀放行。只允许列出分支。
_GIT_BRANCH_READONLY = {
    "git branch",
    "git branch -vv",
    "git branch -a",
    "git branch -r",
    "git branch --show-current",
}


def _command_allowed(stripped: str) -> bool:
    if stripped in _GIT_BRANCH_READONLY or stripped.startswith("git branch --list"):
        return True
    return any(stripped.startswith(p) for p in ALLOWED_PREFIXES)


def execute_cmd(command: str, timeout: int = 30) -> dict:
    stripped = command.strip()
    if not _command_allowed(stripped):
        return {
            "status": "blocked",
            "message": f"命令不在白名单内: {command}。如确需执行，请人工在终端运行。",
        }
    try:
        result = subprocess.run(
            shlex.split(stripped, posix=False), shell=False,
            capture_output=True, text=True,
            timeout=timeout, encoding='utf-8', errors='replace'
        )
        output = result.stdout.strip() or result.stderr.strip()
        if len(output) > 8000:
            output = output[:8000] + "\n... (truncated)"
        return {
            "status": "success",
            "exit_code": result.returncode,
            "output": output
        }
    except subprocess.TimeoutExpired:
        return {"status": "error", "message": f"命令超时（{timeout}秒限制）"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def get_top_processes(count: int = 15) -> dict:
    procs = []
    for p in psutil.process_iter(['pid', 'name', 'cpu_percent', 'memory_info']):
        try:
            info = p.info
            procs.append({
                "pid": info['pid'],
                "name": info['name'],
                "cpu_percent": info.get('cpu_percent', 0) or 0,
                "memory_mb": round((info.get('memory_info') and info['memory_info'].rss or 0) / 1048576, 1),
            })
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    procs.sort(key=lambda x: x['cpu_percent'], reverse=True)
    return {"status": "success", "processes": procs[:count]}


def kill_process(pid: int) -> dict:
    try:
        p = psutil.Process(pid)
        name = p.name()
        p.kill()
        return {"status": "success", "message": f"已终止进程 {name} (PID: {pid})"}
    except psutil.NoSuchProcess:
        return {"status": "error", "message": f"PID {pid} 不存在"}
    except psutil.AccessDenied:
        return {"status": "error", "message": f"无权限终止 PID {pid}"}
    except Exception as e:
        return {"status": "error", "message": str(e)}


def check_port(port: int) -> dict:
    connections = []
    for conn in psutil.net_connections(kind='inet'):
        if conn.laddr and conn.laddr.port == port:
            try:
                proc_name = psutil.Process(conn.pid).name() if conn.pid else "unknown"
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                proc_name = "unknown"
            connections.append({
                "pid": conn.pid,
                "process": proc_name,
                "status": conn.status,
                "local": f"{conn.laddr.ip}:{conn.laddr.port}",
                "remote": f"{conn.raddr.ip}:{conn.raddr.port}" if conn.raddr else None,
            })
    return {
        "status": "success",
        "port": port,
        "in_use": len(connections) > 0,
        "connections": connections,
    }


def get_system_info() -> dict:
    cpu = psutil.cpu_percent(interval=1)
    mem = psutil.virtual_memory()
    disk = psutil.disk_usage('C:\\')
    hostname = socket.gethostname()
    return {
        "status": "success",
        "hostname": hostname,
        "cpu_percent": cpu,
        "memory": {
            "total_gb": round(mem.total / (1024**3), 1),
            "used_gb": round(mem.used / (1024**3), 1),
            "percent": mem.percent,
        },
        "disk_c": {
            "total_gb": round(disk.total / (1024**3), 1),
            "used_gb": round(disk.used / (1024**3), 1),
            "percent": round(disk.percent, 1),
        },
    }


TOOL_MAP = {
    "execute_cmd": lambda args: execute_cmd(args["command"], args.get("timeout", 30)),
    "get_top_processes": lambda args: get_top_processes(args.get("count", 15)),
    "kill_process": lambda args: kill_process(args["pid"]),
    "check_port": lambda args: check_port(args["port"]),
    "get_system_info": lambda args: get_system_info(),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "execute_cmd",
            "description": "执行系统命令（白名单：git 只读/dotnet 构建测试/tasklist/netstat/ping）。白名单外返回 blocked",
            "parameters": {
                "type": "object",
                "properties": {
                    "command": {"type": "string", "description": "要执行的命令，如 'git status'"},
                    "timeout": {"type": "integer", "description": "超时秒数，默认 30"}
                },
                "required": ["command"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_top_processes",
            "description": "获取资源占用最高的进程列表",
            "parameters": {
                "type": "object",
                "properties": {
                    "count": {"type": "integer", "description": "返回进程数量，默认 15"}
                }
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "kill_process",
            "description": "终止指定 PID 的进程",
            "parameters": {
                "type": "object",
                "properties": {
                    "pid": {"type": "integer", "description": "进程 PID"}
                },
                "required": ["pid"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "check_port",
            "description": "检查指定端口的占用情况",
            "parameters": {
                "type": "object",
                "properties": {
                    "port": {"type": "integer", "description": "端口号"}
                },
                "required": ["port"]
            }
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_system_info",
            "description": "获取系统信息：CPU 使用率、内存、磁盘空间",
            "parameters": {"type": "object", "properties": {}},
        }
    },
]
