"""策略层代码级强制（外挂化集成技术方案 §6）。

原则：安全约束不能只写进提示词——提示词能被模型忽略，代码不能。
本模块提供三个强制点：

1. check_write_allowed(paths, added, removed)
   任何写文件/改代码的动作前调用：校验 protectedPaths、maxFilesChanged、
   maxLinesChanged、allowCodeModification。
2. check_commit_allowed()  /  check_push_allowed()  /  check_pr_allowed()
   git 动作闸门。push / PR 在 policy 里硬关，代码层面直接拒绝。
3. check_work_item_state_change()
   ADO 状态回写闸门（allowWorkItemStateChange=false 时拦截）。

策略来源：vega-agent/policy.json（与 agent_core 同目录）。
policy.json 缺失/损坏时 fail-closed：一切危险动作拒绝。
"""

import fnmatch
import json
from pathlib import Path

_POLICY_PATH = Path(__file__).resolve().parent.parent / "policy.json"

# 永远为 false 的硬关项：即使 policy.json 被误改成 true 也拒绝。
# 硬件动作与 push/PR 不存在"放开"的场景。
_PERMANENTLY_DENIED = {"allowPush", "allowPullRequest", "allowHardwareCommands"}


def load_policy() -> dict:
    """读 policy.json；缺失或损坏时 fail-closed（全部闸门关闭）。"""
    try:
        with open(_POLICY_PATH, encoding="utf-8") as f:
            policy = json.load(f)
        if isinstance(policy, dict):
            for key in _PERMANENTLY_DENIED:
                policy[key] = False  # 硬关项强制覆盖
            return policy
    except Exception:
        pass
    return {
        "allowCodeModification": False,
        "branchPrefix": "auto/wi-",
        "requireCleanWorktreeBeforeStart": True,
        "allowCommit": False,
        "allowPush": False, "allowPullRequest": False,
        "allowWorkItemStateChange": False, "allowWorkItemAssign": False,
        "allowHardwareCommands": False,
        "maxFilesChanged": 0, "maxLinesChanged": 0,
        "protectedPaths": ["**/*"],
    }


def _match_protected(path: str, patterns: list[str]) -> bool:
    normalized = path.replace("\\", "/").lstrip("/")
    return any(fnmatch.fnmatch(normalized, p.replace("\\", "/")) for p in patterns)


def check_write_allowed(paths: list[str], lines_changed: int = 0) -> dict:
    """写文件前校验。paths 为将新建/修改的仓库相对路径列表。"""
    policy = load_policy()
    if not policy.get("allowCodeModification", False):
        return {"allowed": False, "reason": "策略禁止代码修改（allowCodeModification=false）"}

    protected_hits = [p for p in paths if _match_protected(p, policy.get("protectedPaths", []))]
    if protected_hits:
        return {"allowed": False,
                "reason": f"以下路径受保护，禁止修改: {protected_hits}"}

    if len(paths) > policy.get("maxFilesChanged", 0):
        return {"allowed": False,
                "reason": f"改动文件数 {len(paths)} 超过上限 {policy['maxFilesChanged']}"}

    if lines_changed > policy.get("maxLinesChanged", 0):
        return {"allowed": False,
                "reason": f"改动行数 {lines_changed} 超过上限 {policy['maxLinesChanged']}"}

    return {"allowed": True}


def check_commit_allowed(branch: str | None = None) -> dict:
    """git commit 前校验；指定分支时还校验分支前缀。"""
    policy = load_policy()
    if not policy.get("allowCommit", False):
        return {"allowed": False, "reason": "策略禁止自动提交（allowCommit=false）"}
    prefix = policy.get("branchPrefix", "auto/wi-")
    if branch and not branch.startswith(prefix):
        return {"allowed": False,
                "reason": f"自动提交只允许 {prefix}* 分支，当前分支 '{branch}' 不符"}
    return {"allowed": True}


def check_push_allowed() -> dict:
    return {"allowed": False, "reason": "push 永久禁止（allowPush=false，硬关）。请人工 push。"}


def check_pull_request_allowed() -> dict:
    return {"allowed": False, "reason": "创建 PR 永久禁止（allowPullRequest=false，硬关）。请人工操作。"}


def check_work_item_state_change() -> dict:
    policy = load_policy()
    if not policy.get("allowWorkItemStateChange", False):
        return {"allowed": False,
                "reason": "策略禁止自动改工作项状态（allowWorkItemStateChange=false）。请人工在 ADO 流转。"}
    return {"allowed": True}


def check_work_item_assign() -> dict:
    policy = load_policy()
    if not policy.get("allowWorkItemAssign", False):
        return {"allowed": False,
                "reason": "策略禁止自动指派工作项（allowWorkItemAssign=false）。"}
    return {"allowed": True}


# 这些文件变化不算"人的在途改动"：生成物/本机私有配置，git status 里出现属噪声。
# 与 VegaAutoTest README §9 约定一致（packages.lock.json 不提交）。
_NOISE_FILES = {
    "packages.lock.json",
    ".claude/settings.local.json",
}

# 已知本地修复豁免（path → 说明）。这些是本机为让主仓库可构建而做的有意识改动，
# 不是"人的未知在途工作"。新增豁免必须登记在此处并写明来源，禁止无登记跳过。
_KNOWN_LOCAL_FIXES = {
    "Vega.UI.Diagnostics/Vega.UI.Diagnostics.csproj":
        "09-16 构建修复：移除指向不存在目录的 SEPLATAPI ProjectReference"
        "（VegaAutoTest/工作进展与计划.md §-1 构建修复条目）",
}


def check_clean_worktree(repo_root: str) -> dict:
    """开工前工作区清洁检查（requireCleanWorktreeBeforeStart）。
    git status --porcelain 非空 = 有在途改动，拒绝启动会把人改动卷进去的流程。
    生成物噪声（_NOISE_FILES）不计入；已知本地修复（_KNOWN_LOCAL_FIXES）放行但回显说明。"""
    policy = load_policy()
    if not policy.get("requireCleanWorktreeBeforeStart", False):
        return {"allowed": True}
    try:
        import subprocess
        p = subprocess.run(["git", "status", "--porcelain"], cwd=repo_root,
                           capture_output=True, text=True, timeout=15,
                           encoding="utf-8", errors="replace")
        if p.returncode != 0:
            return {"allowed": False,
                    "reason": f"git status 失败（退出码 {p.returncode}）: {(p.stderr or '')[:200]}"}
        dirty = []
        known = []
        for ln in (p.stdout or "").splitlines():
            if not ln.strip():
                continue
            path = ln[3:].strip().replace("\\", "/")
            if path in _NOISE_FILES:
                continue
            if path in _KNOWN_LOCAL_FIXES:
                known.append(path)
                continue
            dirty.append(ln)
        if dirty:
            return {"allowed": False,
                    "reason": f"工作区不洁（{len(dirty)} 个在途改动）。开工前要求干净工作区，"
                              f"避免把人的在途改动卷进自动流程。文件: {dirty[:5]}"}
        if known:
            # 放行但让调用方知道带着哪些已知修复开工（写进日志/报告可追溯）
            return {"allowed": True,
                    "known_local_fixes": {k: _KNOWN_LOCAL_FIXES[k] for k in known}}
        return {"allowed": True}
    except Exception as e:  # noqa: BLE001 — fail-closed
        return {"allowed": False, "reason": f"工作区检查异常: {e}"}


TOOL_MAP = {
    "policy_check_write":            lambda args: check_write_allowed(
        args["paths"], args.get("lines_changed", 0)),
    "policy_check_commit":           lambda args: check_commit_allowed(args.get("branch")),
    "policy_check_push":             lambda args: check_push_allowed(),
    "policy_check_pull_request":     lambda args: check_pull_request_allowed(),
    "policy_check_work_item_state":  lambda args: check_work_item_state_change(),
    "policy_check_clean_worktree":   lambda args: check_clean_worktree(args["repo_root"]),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "policy_check_write",
            "description": "修改代码前必须先调用：校验目标路径是否受保护、改动量是否超限。返回 allowed=false 时不得执行写入",
            "parameters": {
                "type": "object",
                "properties": {
                    "paths": {"type": "array", "items": {"type": "string"},
                              "description": "将修改的仓库相对路径列表"},
                    "lines_changed": {"type": "integer", "description": "预计改动行数"},
                },
                "required": ["paths"],
            },
        }
    },
    {
        "type": "function",
        "function": {
            "name": "policy_check_commit",
            "description": "git commit 前必须先调用：校验分支前缀（auto/wi-*）与提交许可",
            "parameters": {
                "type": "object",
                "properties": {
                    "branch": {"type": "string", "description": "当前分支名"},
                },
            },
        }
    },
    {
        "type": "function",
        "function": {
            "name": "policy_check_push",
            "description": "push 闸门。当前策略下永远拒绝——push 只能人工执行",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "policy_check_pull_request",
            "description": "创建 PR 闸门。当前策略下永远拒绝——PR 只能人工创建",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "policy_check_work_item_state",
            "description": "修改工作项状态前必须先调用。当前策略下拒绝——ADO 状态流转只能人工操作",
            "parameters": {"type": "object", "properties": {}},
        }
    },
    {
        "type": "function",
        "function": {
            "name": "policy_check_clean_worktree",
            "description": "开工（自动修复/改代码/验证重编）前必须先调用：检查被测仓库工作区是否干净，有在途改动时拒绝启动",
            "parameters": {
                "type": "object",
                "properties": {
                    "repo_root": {"type": "string", "description": "被测仓库根路径"},
                },
                "required": ["repo_root"],
            },
        }
    },
]
