"""Check work-item mappings against the local registry. ADO ids stay null until discovery fills them."""

from __future__ import annotations

import json
from pathlib import Path

from .contract import ContractError


def load_registry(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    cases = data.get("cases")
    if not isinstance(cases, list):
        raise ContractError("CONTRACT_VIOLATION", "registry.cases must be a list")
    return data


def case_by_id(registry: dict, case_id: str) -> dict | None:
    for case in registry["cases"]:
        if case.get("id") == case_id:
            return case
    return None


def validate_map(map_data: dict, registry: dict) -> list[dict]:
    if map_data.get("schemaVersion") != "1.0":
        raise ContractError("CONTRACT_VIOLATION", "map schemaVersion must be 1.0")
    mappings = map_data.get("mappings")
    if not isinstance(mappings, list) or not mappings:
        raise ContractError("CONTRACT_VIOLATION", "mappings must be a non-empty list")
    seen_points = {}
    reports = []
    for item in mappings:
        case_id = item.get("runnerCaseId")
        case = case_by_id(registry, case_id)
        problems = []
        if case is None:
            problems.append("runner case is not in registry")
        else:
            work_items = case.get("workItems") or []
            if item.get("workItemId") not in work_items:
                problems.append("workItemId is not on that registry case")
            layers = case.get("layers") or {}
            for layer in item.get("layers") or []:
                if layer not in layers:
                    problems.append(f"layer {layer} is not on the registry case")
        point_key = (
            item.get("planId"),
            item.get("suiteId"),
            item.get("adoTestCaseId"),
            item.get("configurationId"),
        )
        if all(value is not None for value in point_key):
            if point_key in seen_points:
                problems.append("duplicate plan/suite/case/configuration")
            seen_points[point_key] = case_id
        publishable = all(
            item.get(key) is not None
            for key in ("adoTestCaseId", "planId", "suiteId", "configurationId", "testPointId")
        ) and not problems
        reports.append({
            "workItemId": item.get("workItemId"),
            "runnerCaseId": case_id,
            "publishable": publishable,
            "problems": problems,
        })
    if any(row["problems"] for row in reports):
        raise ContractError("MAP_INVALID", json.dumps(reports, ensure_ascii=False))
    return reports


def resolve_point(mappings: list[dict], work_item_id: int, plan_id, suite_id, configuration_id) -> dict:
    matches = [
        item for item in mappings
        if item.get("workItemId") == work_item_id
        and item.get("planId") == plan_id
        and item.get("suiteId") == suite_id
        and item.get("configurationId") == configuration_id
        and item.get("testPointId") is not None
    ]
    if len(matches) != 1:
        raise ContractError("POINT_NOT_UNIQUE", f"matched {len(matches)} points")
    return matches[0]
