"""VegaAutoTest Runner 桥接：L0 编排层调用 L1 确定性验证（外挂化集成技术方案 §4.2）。

长任务必须异步——L2 UIA 用例 ~90s，带 --rebuild 要数分钟，同步调用必然超时。
采用 start / status 三件套 + list_cases：

  start_verification(work_item_id, rebuild, layers) -> job_id（后台跑 Runner CLI）
  get_verification_status(job_id)                   -> 轮询；done 时只回 verdict 摘要
  list_verifiable_work_items()                      -> registry.json 已登记清单

数据契约（result.json，schemaVersion 1.0）由 VegaAutoTest 仓库保证：
  verdict ∈ {PASS, FAIL, BLOCKED, ERROR}，退出码 {0,1,2,3}，stdout 末行 = result.json 路径。
判定唯一出处是 Runner 的 VerdictResolver——本模块只搬运，不修改、不解释判定。

Runner 位置：与本文件同属 VegaAutomation 工程，按目录相对定位
（code_indicator 的上一级就是 VegaAutoTest 的上一级），可用
VEGA_AUTOTEST_RUNNER 覆盖。被测主仓库根经环境变量 VEGA_SW_ROOT 下传
（Runner 与 spike 工程同一约定）。
"""

import json
import os
import subprocess
import threading
import uuid
from pathlib import Path

# tools -> vega-agent -> code_indicator -> VegaAutomation -> VegaAutoTest/runner/...
_DEFAULT_RUNNER = Path(__file__).resolve().parents[3] / "VegaAutoTest" / "runner" / "Vega.AutoTest.Runner"
RUNNER_DIR = os.environ.get("VEGA_AUTOTEST_RUNNER", str(_DEFAULT_RUNNER))
RUN_TIMEOUT = 2400  # 40min：uia 900s + logic 300s + 余量

# 被测主仓库根：显式环境变量 > config.json 的 vega_workspace_root。
# Runner 侧三级解析（--repo-root > VEGA_SW_ROOT > 上级目录）里，
# 这里同时下传两级，保证独立仓库布局下必能定位被测程序。
def _repo_root() -> str | None:
    from_env = os.environ.get("VEGA_SW_ROOT")
    if from_env:
        return from_env
    try:
        cfg_path = Path(__file__).resolve().parent.parent / "config.json"
        with open(cfg_path, encoding="utf-8") as f:
            root = json.load(f).get("vega_workspace_root")
        return root or None
    except Exception:  # noqa: BLE001 — config 缺失不是致命错，交给 Runner 兜底
        return None


_jobs: dict[str, dict] = {}
_lock = threading.Lock()


def _runner_cmd(args: list[str]) -> list[str]:
    cmd = ["dotnet", "run", "--project", RUNNER_DIR, "--no-build", "--", *args]
    repo_root = _repo_root()
    if repo_root and "--repo-root" not in args:
        cmd += ["--repo-root", repo_root]
    return cmd


def _runner_env() -> dict:
    env = {**os.environ}
    repo_root = _repo_root()
    if repo_root:
        env["VEGA_SW_ROOT"] = repo_root
    return env


def _run(job_id: str, args: list[str]):
    try:
        p = subprocess.run(_runner_cmd(args), capture_output=True, text=True,
                           encoding="utf-8", errors="replace", timeout=RUN_TIMEOUT,
                           env=_runner_env())
        lines = (p.stdout or "").strip().splitlines()
        result_path = lines[-1].strip() if lines else ""
        payload = None
        if result_path and Path(result_path).is_file():
            payload = json.loads(Path(result_path).read_text(encoding="utf-8"))
        with _lock:
            _jobs[job_id].update(state="done", exit_code=p.returncode,
                                 result=payload, stderr=(p.stderr or "")[-2000:])
    except Exception as e:  # noqa: BLE001 — 后台线程兜底，任何异常都要落到 job 状态
        with _lock:
            _jobs[job_id].update(state="error", message=str(e))


def run_verify_sync(work_item_id: int, rebuild: bool = False, layers: str = "logic,uia") -> dict:
    """同步跑一次 Runner。闭环用来先后各跑一遍。不解释 verdict。"""
    if not Path(RUNNER_DIR).is_dir():
        raise FileNotFoundError(f"Runner 工程不存在: {RUNNER_DIR}")
    args = ["verify", "--work-item", str(work_item_id), "--layers", layers]
    if rebuild:
        args.append("--rebuild")
    p = subprocess.run(_runner_cmd(args), capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=RUN_TIMEOUT,
                       env=_runner_env())
    lines = (p.stdout or "").strip().splitlines()
    result_path = lines[-1].strip() if lines else ""
    payload = None
    if result_path and Path(result_path).is_file():
        payload = json.loads(Path(result_path).read_text(encoding="utf-8"))
    return {"exit_code": p.returncode, "result": payload, "result_path": result_path,
            "stderr": (p.stderr or "")[-2000:]}


def start_verification(work_item_id: int, rebuild: bool = False,
                       layers: str = "logic,uia") -> dict:
    """启动某工作项的确定性验证，返回 job_id 供轮询。不阻塞。"""
    if not Path(RUNNER_DIR).is_dir():
        return {"status": "error",
                "message": f"Runner 工程不存在: {RUNNER_DIR}（可用 VEGA_AUTOTEST_RUNNER 指定）"}
    args = ["verify", "--work-item", str(work_item_id), "--layers", layers]
    if rebuild:
        args.append("--rebuild")
    job_id = uuid.uuid4().hex[:8]
    with _lock:
        _jobs[job_id] = {"state": "running", "work_item_id": work_item_id}
    threading.Thread(target=_run, args=(job_id, args), daemon=True).start()
    return {"status": "success", "job_id": job_id,
            "message": f"验证已启动（工作项 #{work_item_id}，layers={layers}）。"
                       f"请用 get_verification_status('{job_id}') 轮询，预计 2-5 分钟。"
                       f"注意：uia 层会拉起真实 Vega 界面，验证期间不要操作该机器的 UI。"}


def _report_path(evidence_dir: str | None) -> str | None:
    """result.json 同目录的可读报告；旧运行没有该文件时返回 None。"""
    if not evidence_dir:
        return None
    path = Path(evidence_dir) / "report.md"
    return str(path) if path.is_file() else None


def get_verification_status(job_id: str) -> dict:
    """轮询验证任务；done 时只回 verdict 摘要（明细按需再看 result.json）。"""
    with _lock:
        job = _jobs.get(job_id)
    if not job:
        return {"status": "error", "message": f"未知 job: {job_id}"}
    if job["state"] == "running":
        return {"status": "success", "state": "running",
                "message": f"工作项 #{job['work_item_id']} 仍在验证，请稍后再查"}
    if job["state"] == "error":
        return {"status": "error", "message": job.get("message", "Runner 执行异常")}

    r = job.get("result")
    if not r:
        return {"status": "error",
                "message": f"Runner 退出码 {job['exit_code']} 但未产出 result.json",
                "stderr": job.get("stderr")}

    # 只回摘要避免吃满上下文；failures 单列供报告引用
    return {
        "status": "success",
        "state": "done",
        "runId": r.get("runId"),
        "verdict": r.get("verdict"),
        "verdictCode": r.get("verdictCode"),
        "verdictReason": r.get("verdictReason"),
        "summary": r.get("summary"),
        "target": r.get("target"),
        "channels": r.get("channels"),
        "durationMs": r.get("durationMs"),
        "failedAssertions": [a for a in r.get("assertions", []) if a.get("result") == "FAIL"],
        "evidenceDir": (r.get("evidence") or {}).get("dir"),
        "reportPath": _report_path((r.get("evidence") or {}).get("dir")),
        "exitCode": job.get("exit_code"),
    }


def list_verifiable_work_items() -> dict:
    """列出 registry.json 已登记的可验证工作项（没登记的无法自动验证）。"""
    try:
        p = subprocess.run(_runner_cmd(["list-cases"]), capture_output=True,
                           text=True, encoding="utf-8", errors="replace", timeout=120)
        if p.returncode != 0:
            return {"status": "error", "message": (p.stderr or "list-cases 失败")[:2000]}
        return {"status": "success", "data": (p.stdout or "").strip()}
    except Exception as e:  # noqa: BLE001
        return {"status": "error", "message": str(e)}


TOOL_MAP = {
    "start_verification":         lambda args: start_verification(
        args["work_item_id"], args.get("rebuild", False), args.get("layers", "logic,uia")),
    "get_verification_status":    lambda args: get_verification_status(args["job_id"]),
    "list_verifiable_work_items": lambda args: list_verifiable_work_items(),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "start_verification",
            "description": (
                "启动 VegaAutoTest Runner 对某工作项做确定性验证（异步，返回 job_id 需轮询）。"
                "verdict 只能来自本工具，禁止凭代码阅读自行宣布'已修复/验证通过'。"
                "rebuild=True 会先用当前源码重编主程序（数分钟），结论才对当前源码有效；"
                "不 rebuild 时测的是已有 exe（旧二进制）。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {"type": "integer", "description": "ADO 工作项号，如 3478"},
                    "rebuild": {"type": "boolean", "description": "是否先重编被测主程序，默认 false"},
                    "layers": {
                        "type": "string",
                        "description": "跑哪些层，逗号分隔：logic（秒级冒烟）/ uia（拉真实界面，约 2-5 分钟），默认 logic,uia",
                    },
                },
                "required": ["work_item_id"],
            },
        }
    },
    {
        "type": "function",
        "function": {
            "name": "get_verification_status",
            "description": (
                "轮询验证任务结果。done 时返回 verdict（PASS/FAIL/BLOCKED/ERROR 四态必须原样转述，"
                "BLOCKED=环境问题无法判定，不是 Bug；引用结论必须带 runId）。"
                "reportPath 指向同目录 report.md，回复时给出该路径，不要等用户索要。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "job_id": {"type": "string", "description": "start_verification 返回的 job_id"},
                },
                "required": ["job_id"],
            },
        }
    },
    {
        "type": "function",
        "function": {
            "name": "list_verifiable_work_items",
            "description": "列出已登记自动化用例的可验证工作项。未登记的工作项无法自动验证（NO_CASE_REGISTERED），应如实告知需人工补用例",
            "parameters": {"type": "object", "properties": {}},
        }
    },
]
