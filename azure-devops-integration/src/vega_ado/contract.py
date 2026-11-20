"""Validate a Runner result.json without recomputing its verdict."""

from __future__ import annotations

import json
from pathlib import Path

VERDICTS = ("PASS", "FAIL", "BLOCKED", "ERROR")
EXIT_BY_VERDICT = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}


class ContractError(Exception):
    def __init__(self, code: str, message: str):
        self.code = code
        super().__init__(message)


def load_json(path: Path) -> dict:
    try:
        text = path.read_text(encoding="utf-8")
    except OSError as error:
        raise ContractError("RESULT_MISSING", str(error)) from error
    try:
        data = json.loads(text)
    except json.JSONDecodeError as error:
        raise ContractError("INVALID_JSON", str(error)) from error
    if not isinstance(data, dict):
        raise ContractError("INVALID_JSON", "result root must be an object")
    return data


def _inside(path: Path, root: Path) -> bool:
    try:
        path.resolve().relative_to(root.resolve())
        return True
    except ValueError:
        return False


def evidence_dir(result_path: Path, data: dict, runs_root: Path) -> Path:
    raw = ((data.get("evidence") or {}).get("dir")) or ""
    if not isinstance(raw, str) or not raw.strip():
        raise ContractError("CONTRACT_VIOLATION", "evidence.dir is required")
    candidate = Path(raw)
    if not candidate.is_absolute():
        candidate = result_path.parent / candidate
    resolved = candidate.resolve()
    root = runs_root.resolve()
    if not _inside(resolved, root):
        raise ContractError("PATH_TRAVERSAL", f"evidence.dir escapes {root}")
    return resolved


def validate_result(
    data: dict,
    *,
    result_path: Path,
    runs_root: Path,
    expected_work_item: int | None = None,
    expected_case_id: str | None = None,
    process_exit: int | None = None,
) -> dict:
    if data.get("schemaVersion") != "1.0":
        raise ContractError("CONTRACT_VIOLATION", "schemaVersion must be 1.0")
    run_id = data.get("runId")
    if not isinstance(run_id, str) or not run_id.strip():
        raise ContractError("CONTRACT_VIOLATION", "runId is required")
    verdict = data.get("verdict")
    if verdict not in VERDICTS:
        raise ContractError("CONTRACT_VIOLATION", "verdict is not one of the four Runner states")
    exit_code = data.get("exitCode")
    if exit_code != EXIT_BY_VERDICT[verdict]:
        raise ContractError("CONTRACT_CONFLICT", "exitCode does not match verdict")
    if process_exit is not None and process_exit != exit_code:
        raise ContractError("CONTRACT_CONFLICT", "process exit code does not match result.json")
    if expected_work_item is not None and data.get("workItemId") != expected_work_item:
        raise ContractError("CONTRACT_VIOLATION", "workItemId does not match the request")
    if expected_case_id is not None and data.get("caseId") != expected_case_id:
        raise ContractError("CONTRACT_VIOLATION", "caseId does not match the request")
    target = data.get("target")
    if not isinstance(target, dict):
        raise ContractError("CONTRACT_VIOLATION", "target is required")
    for key in ("gitCommit", "gitDirty", "builtFromCurrentSource", "vegaExe"):
        if key not in target:
            raise ContractError("CONTRACT_VIOLATION", f"target.{key} is required")
    if data.get("verdictCode") == "ZERO_ASSERTIONS_MATCHED" and verdict != "BLOCKED":
        raise ContractError("CONTRACT_CONFLICT", "ZERO_ASSERTIONS_MATCHED must stay BLOCKED")
    directory = evidence_dir(result_path, data, runs_root)
    if not _inside(result_path.resolve(), runs_root.resolve()):
        raise ContractError("PATH_TRAVERSAL", "result.json is outside the runs root")
    return {
        "runId": run_id,
        "verdict": verdict,
        "verdictCode": data.get("verdictCode"),
        "verdictReason": data.get("verdictReason"),
        "workItemId": data.get("workItemId"),
        "caseId": data.get("caseId"),
        "exitCode": exit_code,
        "builtFromCurrentSource": bool(target.get("builtFromCurrentSource")),
        "gitCommit": target.get("gitCommit"),
        "gitDirty": target.get("gitDirty"),
        "evidenceDir": str(directory),
        "l1Available": ((data.get("channels") or {}).get("l1Api") or {}).get("available"),
        "summary": data.get("summary"),
    }


def notes_for(checked: dict) -> list[str]:
    notes = []
    if checked["verdict"] == "PASS" and not checked["builtFromCurrentSource"]:
        notes.append("旧二进制 PASS：结论只对这次记录的 EXE 有效，不能当作当前源码验收。")
    if checked.get("verdictCode") == "OK_STALE_BUILD":
        notes.append("OK_STALE_BUILD 不能满足正式发布门禁。")
    if checked.get("l1Available") is False:
        notes.append("未进行 L1 交叉验证。")
    if checked.get("verdictCode") == "ZERO_ASSERTIONS_MATCHED" and checked["verdict"] != "BLOCKED":
        notes.append("ZERO_ASSERTIONS_MATCHED 必须保持 BLOCKED。")
    summary = checked.get("summary") or {}
    if summary.get("skipped"):
        notes.append("summary.passed 少于 total 时先看 skipped，不能直接当成 FAIL。")
    return notes
