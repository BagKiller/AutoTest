"""未提交工作项的自动闭环（技术方案 §5 + 设计文档 §4.5）。

顺序固定，不能跳：
  1. start_loop_baseline  用当前二进制复现，并把界面指纹存成基线
  2. （模型）仅当基线 verdict=FAIL 时，走 start_code_repair / write_repair_file / commit_repair
  3. finish_loop          --rebuild 重验，对比两份 result.json 和两份界面指纹

本模块不改产品代码、不 push、不开 PR、不改工作项状态。
界面对比只认带 AutomationId 的控件：新增/消失视为可能误改；位移超过 16px 记入报告供人工看。
"""

import json
import shutil
import threading
import uuid
from pathlib import Path

from tools.autotest import _report_path, run_verify_sync

_AUTOTEST = Path(__file__).resolve().parents[3] / "VegaAutoTest"
_BASELINES = _AUTOTEST / "baselines"
_LOOPS = _AUTOTEST / "loops"
_MOVE_TOLERANCE = 16

_jobs: dict[str, dict] = {}
_lock = threading.Lock()


def diff_snapshots(before: dict, after: dict, tolerance: int = _MOVE_TOLERANCE) -> dict:
    """对比两份 ui-snapshot.json。只把有 AutomationId 的节点当作组件身份。"""
    def index(doc: dict) -> dict[str, dict]:
        found: dict[str, dict] = {}
        for node in doc.get("nodes") or []:
            aid = (node.get("automationId") or "").strip()
            if aid and aid not in found:
                found[aid] = node
        return found

    left, right = index(before), index(after)
    removed = sorted(set(left) - set(right))
    added = sorted(set(right) - set(left))
    moved = []
    for aid in sorted(set(left) & set(right)):
        a, b = left[aid], right[aid]
        if max(abs(int(a.get("x", 0)) - int(b.get("x", 0))),
               abs(int(a.get("y", 0)) - int(b.get("y", 0))),
               abs(int(a.get("w", 0)) - int(b.get("w", 0))),
               abs(int(a.get("h", 0)) - int(b.get("h", 0)))) > tolerance:
            moved.append({
                "automationId": aid,
                "before": {"x": a.get("x"), "y": a.get("y"), "w": a.get("w"), "h": a.get("h")},
                "after": {"x": b.get("x"), "y": b.get("y"), "w": b.get("w"), "h": b.get("h")},
            })
    return {
        "added": added,
        "removed": removed,
        "moved": moved,
        "clean": not added and not removed and not moved,
    }


def _state_path(work_item_id: int) -> Path:
    return _LOOPS / f"wi{work_item_id}" / "state.json"


def _load_state(work_item_id: int) -> dict | None:
    path = _state_path(work_item_id)
    if not path.is_file():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def _save_state(work_item_id: int, state: dict) -> None:
    path = _state_path(work_item_id)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")


def _snapshot_in(run_dir: str | None) -> Path | None:
    if not run_dir:
        return None
    candidate = Path(run_dir) / "uia" / "ui-snapshot.json"
    return candidate if candidate.is_file() else None


def _archive_snapshot(work_item_id: int, commit: str, phase: str, run_dir: str | None) -> str | None:
    src = _snapshot_in(run_dir)
    if src is None:
        return None
    safe_commit = (commit or "unknown")[:12]
    dest_dir = _BASELINES / f"wi{work_item_id}" / safe_commit / phase
    dest_dir.mkdir(parents=True, exist_ok=True)
    dest = dest_dir / "ui-snapshot.json"
    shutil.copy2(src, dest)
    png = src.with_suffix(".png")
    if png.is_file():
        shutil.copy2(png, dest_dir / "ui-snapshot.png")
    return str(dest)


def _summarize(result: dict | None) -> dict:
    if not result:
        return {"verdict": None, "verdictCode": None, "runId": None, "commit": None, "builtFromCurrentSource": None}
    target = result.get("target") or {}
    return {
        "verdict": result.get("verdict"),
        "verdictCode": result.get("verdictCode"),
        "verdictReason": result.get("verdictReason"),
        "runId": result.get("runId"),
        "commit": target.get("gitCommit"),
        "builtFromCurrentSource": target.get("builtFromCurrentSource"),
        "evidenceDir": (result.get("evidence") or {}).get("dir"),
        "reportPath": _report_path((result.get("evidence") or {}).get("dir")),
    }


def _run_job(job_id: str, fn):
    try:
        payload = fn()
        with _lock:
            _jobs[job_id].update(state="done", **payload)
    except Exception as e:  # noqa: BLE001 — 后台线程必须把异常写进 job
        with _lock:
            _jobs[job_id].update(state="error", message=str(e))


def start_loop_baseline(work_item_id: int) -> dict:
    """复现当前未改二进制，并记住界面。不重编。"""
    job_id = uuid.uuid4().hex[:8]
    with _lock:
        _jobs[job_id] = {"state": "running", "phase": "baseline", "work_item_id": work_item_id}

    def work():
        result = run_verify_sync(work_item_id, rebuild=False, layers="uia")
        summary = _summarize(result.get("result"))
        snapshot = _archive_snapshot(
            work_item_id, summary.get("commit") or "", "before", summary.get("evidenceDir"))
        state = {
            "workItemId": work_item_id,
            "phase": "baseline_done",
            "baseline": {**summary, "snapshot": snapshot},
            "after": None,
            "report": None,
        }
        _save_state(work_item_id, state)
        verdict = summary.get("verdict")
        if verdict == "FAIL":
            nxt = "基线已复现（FAIL）。下一步只能走修复闸门改代码，然后再 finish_loop。"
        elif verdict == "PASS":
            nxt = "当前二进制已经符合预期，不要再改代码。"
        else:
            nxt = "基线不是 FAIL/PASS，先处理环境或用例，不要进入修复。"
        return {"status": "success", "phase": "baseline_done", "baseline": state["baseline"],
                "uiRemembered": snapshot is not None, "next": nxt}

    threading.Thread(target=_run_job, args=(job_id, work), daemon=True).start()
    return {
        "status": "success",
        "job_id": job_id,
        "message": f"已启动工作项 #{work_item_id} 的复现基线（不重编，并记住界面）。"
                   f"用 get_loop_status('{job_id}') 轮询。期间不要操作这台机器的界面。",
    }


def finish_loop(work_item_id: int) -> dict:
    """修复后重编重验，并与基线界面对比。要求基线已经跑完。"""
    state = _load_state(work_item_id)
    if not state or not (state.get("baseline") or {}).get("runId"):
        return {"status": "error", "message": "还没有复现基线。先 start_loop_baseline。"}
    if state["baseline"].get("verdict") != "FAIL":
        return {
            "status": "blocked",
            "message": f"基线 verdict={state['baseline'].get('verdict')}，不是失败复现。"
                       "没有红过的绿不可信，finish_loop 不会重编。",
        }

    job_id = uuid.uuid4().hex[:8]
    with _lock:
        _jobs[job_id] = {"state": "running", "phase": "reverify", "work_item_id": work_item_id}

    def work():
        result = run_verify_sync(work_item_id, rebuild=True, layers="uia")
        summary = _summarize(result.get("result"))
        snapshot = _archive_snapshot(
            work_item_id, summary.get("commit") or "", "after", summary.get("evidenceDir"))
        ui = {"compared": False, "reason": "缺少修复前或修复后的界面指纹"}
        before_path = state["baseline"].get("snapshot")
        if before_path and snapshot and Path(before_path).is_file() and Path(snapshot).is_file():
            before = json.loads(Path(before_path).read_text(encoding="utf-8"))
            after = json.loads(Path(snapshot).read_text(encoding="utf-8"))
            ui = {"compared": True, **diff_snapshots(before, after)}

        before_verdict = state["baseline"].get("verdict")
        after_verdict = summary.get("verdict")
        if before_verdict == "FAIL" and after_verdict == "PASS" and ui.get("clean"):
            functional = "FAIL_TO_PASS"
            loop_verdict = "FIXED_CLEAN"
        elif before_verdict == "FAIL" and after_verdict == "PASS":
            functional = "FAIL_TO_PASS"
            loop_verdict = "FIXED_UI_DRIFT"
        elif after_verdict == "FAIL":
            functional = "STILL_FAIL"
            loop_verdict = "NOT_FIXED"
        else:
            functional = f"{before_verdict}_TO_{after_verdict}"
            loop_verdict = "INCONCLUSIVE"

        report = {
            "workItemId": work_item_id,
            "loopVerdict": loop_verdict,
            "functional": functional,
            "baseline": state["baseline"],
            "after": {**summary, "snapshot": snapshot},
            "uiDrift": ui,
            "humanGate": "stop",
            "pushed": False,
            "pullRequest": False,
            "workItemStateChanged": False,
        }
        report_path = _LOOPS / f"wi{work_item_id}" / "loop-report.json"
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        state["phase"] = "reported"
        state["after"] = report["after"]
        state["report"] = str(report_path)
        _save_state(work_item_id, state)
        return {
            "status": "success",
            "loopVerdict": loop_verdict,
            "functional": functional,
            "uiDrift": {
                "compared": ui.get("compared"),
                "clean": ui.get("clean"),
                "added": ui.get("added", [])[:20],
                "removed": ui.get("removed", [])[:20],
                "movedCount": len(ui.get("moved") or []),
            },
            "baselineRunId": state["baseline"].get("runId"),
            "afterRunId": summary.get("runId"),
            "report": str(report_path),
            "message": "闭环已停在人工闸门：未 push、未开 PR、未改工作项状态。界面差异需人工确认。",
        }

    threading.Thread(target=_run_job, args=(job_id, work), daemon=True).start()
    return {
        "status": "success",
        "job_id": job_id,
        "message": f"已启动工作项 #{work_item_id} 的修复后重验（会重编）。"
                   f"用 get_loop_status('{job_id}') 轮询。",
    }


def get_loop_status(job_id: str) -> dict:
    with _lock:
        job = _jobs.get(job_id)
    if not job:
        return {"status": "error", "message": f"未知 job: {job_id}"}
    if job["state"] == "running":
        return {"status": "success", "state": "running",
                "message": f"工作项 #{job['work_item_id']} 的{job['phase']}仍在进行"}
    if job["state"] == "error":
        return {"status": "error", "message": job.get("message", "闭环执行异常")}
    done = {k: v for k, v in job.items() if k != "state"}
    return {"status": "success", "state": "done", **done}


TOOL_MAP = {
    "start_loop_baseline": lambda args: start_loop_baseline(args["work_item_id"]),
    "finish_loop": lambda args: finish_loop(args["work_item_id"]),
    "get_loop_status": lambda args: get_loop_status(args["job_id"]),
}

TOOLS_SCHEMA = [
    {
        "type": "function",
        "function": {
            "name": "start_loop_baseline",
            "description": (
                "未提交工作项闭环的第一步：用当前未重编的 Vega 复现，并记住界面指纹。"
                "必须先于任何代码修改。返回 job_id，用 get_loop_status 轮询。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {"type": "integer", "description": "ADO 工作项号"},
                },
                "required": ["work_item_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "finish_loop",
            "description": (
                "闭环的收尾：基线必须已是 FAIL。重编后重验，对比修复前后的 result.json 和界面指纹。"
                "停在人工闸门，不 push、不开 PR、不改工作项状态。"
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "work_item_id": {"type": "integer", "description": "须与基线是同一个工作项"},
                },
                "required": ["work_item_id"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "get_loop_status",
            "description": "轮询 start_loop_baseline 或 finish_loop 的任务。",
            "parameters": {
                "type": "object",
                "properties": {
                    "job_id": {"type": "string"},
                },
                "required": ["job_id"],
            },
        },
    },
]
