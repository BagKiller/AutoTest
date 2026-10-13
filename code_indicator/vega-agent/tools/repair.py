"""自动修复闭环的执行点（外挂化集成技术方案 §5⑥ / §6，P6）。

策略检查工具只回答「允不允许」。本模块是真正会改仓库的那一侧，
每次写文件、建分支、提交都先走 policy，调用方绕不开：

  start_code_repair(work_item_id)  清洁工作区 → 建/切到 auto/wi-<id>
  write_repair_file(path, content) 受保护路径与改动量上限，通过才落盘
  commit_repair(message)           只提交本次会话写过的文件，不 push

push / PR / 工作项状态不在这里，也没有入口。停在人工闸门。
"""

from __future__ import annotations

import json
import os
import subprocess
import threading
from pathlib import Path

from tools.policy import (
    check_clean_worktree,
    check_commit_allowed,
    check_push_allowed,
    check_write_allowed,
    load_policy,
)

_lock = threading.Lock()
_session: dict | None = None


def reset_repair_session() -> None:
    """测试用。正式工具面不暴露。"""
    global _session
    with _lock:
        _session = None


def _repo_root(explicit: str | None) -> str | None:
    if explicit:
        return explicit
    from_env = os.environ.get("VEGA_SW_ROOT")
    if from_env:
        return from_env
    try:
        cfg = Path(__file__).resolve().parent.parent / "config.json"
        with open(cfg, encoding="utf-8") as f:
            return json.load(f).get("vega_workspace_root") or None
    except Exception:  # noqa: BLE001
        return None


def _git(repo: str, *args: str, timeout: int = 30) -> subprocess.CompletedProcess:
    return subprocess.run(
        ["git", *args], cwd=repo, capture_output=True, text=True,
        encoding="utf-8", errors="replace", timeout=timeout,
    )


def _resolve_inside(repo: Path, relative: str) -> Path | None:
    raw = relative.replace("\\", "/").strip()
    if not raw or raw.startswith("/") or (len(raw) >= 2 and raw[1] == ":"):
        return None
    parts = Path(raw).parts
    if any(part == ".." for part in parts):
        return None
    root = repo.resolve()
    full = (root / raw).resolve()
    try:
        full.relative_to(root)
    except ValueError:
        return None
    return full


def _line_count(text: str) -> int:
    if text == "":
        return 0
    return text.count("\n") + (0 if text.endswith("\n") else 1)


def start_code_repair(work_item_id: int, repo_root: str | None = None) -> dict:
    """开工：工作区必须干净，然后切到 auto/wi-<id>。不改任何文件。"""
    root = _repo_root(repo_root)
    if not root or not Path(root).is_dir():
        return {"status": "error", "message": f"找不到被测仓库: {root}"}
    if not (Path(root) / ".git").exists():
        return {"status": "error", "message": f"不是 git 仓库: {root}"}

    gate = check_clean_worktree(root)
    if not gate.get("allowed"):
        return {"status": "blocked", "message": gate.get("reason", "工作区不洁")}

    prefix = load_policy().get("branchPrefix", "auto/wi-")
    branch = f"{prefix}{work_item_id}"
    current = _git(root, "rev-parse", "--abbrev-ref", "HEAD")
    if current.returncode != 0:
        return {"status": "error", "message": (current.stderr or "无法读取当前分支")[:400]}
    current_name = (current.stdout or "").strip()

    if current_name != branch:
        exists = _git(root, "show-ref", "--verify", "--quiet", f"refs/heads/{branch}")
        if exists.returncode == 0:
            switched = _git(root, "checkout", branch)
        else:
            switched = _git(root, "checkout", "-b", branch)
        if switched.returncode != 0:
            detail = (switched.stderr or switched.stdout or "建分支失败")[:400]
            return {"status": "error", "message": detail}

    global _session
    with _lock:
        _session = {
            "repo_root": str(Path(root).resolve()),
            "work_item_id": work_item_id,
            "branch": branch,
            "files": {},
        }
    result = {
        "status": "success",
        "branch": branch,
        "repo_root": str(Path(root).resolve()),
        "message": f"已在分支 {branch} 上开工。写文件用 write_repair_file，提交用 commit_repair。不会 push。",
    }
    if gate.get("known_local_fixes"):
        result["known_local_fixes"] = gate["known_local_fixes"]
    return result


def write_repair_file(relative_path: str, content: str, repo_root: str | None = None) -> dict:
    """落盘前走 check_write_allowed。未开工、越界路径、超限、受保护路径一律不写。"""
    with _lock:
        session = dict(_session) if _session else None
        files = dict(session["files"]) if session else {}
    if session is None:
        return {"status": "blocked", "message": "尚未开工。先调用 start_code_repair。"}

    root = Path(session["repo_root"])
    if repo_root and Path(repo_root).resolve() != root:
        return {"status": "blocked", "message": "repo_root 与当前修复会话不一致"}

    target = _resolve_inside(root, relative_path)
    if target is None:
        return {"status": "blocked", "message": f"拒绝路径（必须是仓库内相对路径）: {relative_path}"}

    rel = target.relative_to(root).as_posix()
    old = target.read_text(encoding="utf-8") if target.is_file() else ""
    delta = _line_count(old) + _line_count(content)
    files[rel] = delta
    gate = check_write_allowed(list(files.keys()), sum(files.values()))
    if not gate.get("allowed"):
        return {"status": "blocked", "message": gate.get("reason", "策略拒绝写入")}

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8", newline="\n")
    with _lock:
        if _session is None or _session.get("branch") != session["branch"]:
            return {"status": "error", "message": "修复会话在写入期间被替换，文件已落盘但未计入会话"}
        _session["files"][rel] = delta
    return {"status": "success", "path": rel, "lines_changed": delta,
            "message": f"已写入 {rel}。尚未提交。"}


def commit_repair(message: str) -> dict:
    """只提交本会话写过的文件。分支前缀不符或策略关提交时不执行。不 push。"""
    text = (message or "").strip()
    if not text or len(text) > 500:
        return {"status": "error", "message": "提交说明不能为空，且不超过 500 字"}

    with _lock:
        session = dict(_session) if _session else None
        files = list(session["files"].keys()) if session else []
    if session is None:
        return {"status": "blocked", "message": "尚未开工。先调用 start_code_repair。"}
    if not files:
        return {"status": "blocked", "message": "本次会话没有写入文件，拒绝空提交"}

    root = session["repo_root"]
    head = _git(root, "rev-parse", "--abbrev-ref", "HEAD")
    branch = (head.stdout or "").strip()
    if branch != session["branch"]:
        return {"status": "blocked",
                "message": f"当前分支 {branch} 不是本次开工分支 {session['branch']}"}

    gate = check_commit_allowed(branch)
    if not gate.get("allowed"):
        return {"status": "blocked", "message": gate.get("reason", "策略拒绝提交")}

    # push 没有执行入口；这里再读一次，防止以后有人把 push 接到提交后面。
    push = check_push_allowed()
    if push.get("allowed"):
        return {"status": "blocked", "message": "push 闸门异常放行，提交已中止"}

    added = _git(root, "add", "--", *files)
    if added.returncode != 0:
        return {"status": "error", "message": (added.stderr or "git add 失败")[:400]}

    committed = _git(root, "commit", "-m", text)
    if committed.returncode != 0:
        return {"status": "error", "message": (committed.stderr or committed.stdout or "git commit 失败")[:800]}

    rev = _git(root, "rev-parse", "HEAD")
    return {
        "status": "success",
        "branch": branch,
        "commit": (rev.stdout or "").strip(),
        "files": files,
        "pushed": False,
        "message": "已提交到本地分支。未 push、未开 PR、未改工作项状态。",
    }


TOOL_MAP = {
    "start_code_repair": lambda args: start_code_repair(
        args["work_item_id"], args.get("repo_root")),
    "write_repair_file": lambda args: write_repair_file(
        args["path"], args["content"], args.get("repo_root")),
    "commit_repair": lambda args: commit_repair(args["message"]),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "start_code_repair",
            "description": (
                "自动修复开工：检查被测仓库工作区干净后，创建或切到 auto/wi-<工作项号>。"
                "这是改代码的唯一入口，内部已走策略闸门。不会 push。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {"type": "integer", "description": "ADO 工作项号"},
                    "repo_root": {"type": "string", "description": "被测仓库根，默认为 VEGA_SW_ROOT"},
                },
                "required": ["work_item_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "write_repair_file",
            "description": (
                "在当前修复分支上写一个仓库内相对路径的文件。"
                "受保护路径、超过文件数/行数上限、或尚未开工时拒绝，且不会落盘。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "path": {"type": "string", "description": "仓库相对路径，如 Vega.UI.Foo/Bar.cs"},
                    "content": {"type": "string", "description": "文件全文（UTF-8）"},
                    "repo_root": {"type": "string", "description": "须与开工时的仓库一致，可省略"},
                },
                "required": ["path", "content"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "commit_repair",
            "description": (
                "把本次开工后写过的文件提交到 auto/wi-* 分支。"
                "不 push、不开 PR、不改工作项状态。空提交和错误分支会被拒绝。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "message": {"type": "string", "description": "提交说明"},
                },
                "required": ["message"],
            },
        },
    },
]
