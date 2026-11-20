"""Dry-run publication. Writing to Azure DevOps stays off unless every gate is explicit."""

from __future__ import annotations

import json
from pathlib import Path

from .contract import ContractError, load_json, notes_for, validate_result


def manifest_for(checked: dict, mapping: dict | None, evidence_index: str | None) -> dict:
    mapping = mapping or {}
    publishable = all(
        mapping.get(key) is not None
        for key in ("adoTestCaseId", "planId", "suiteId", "configurationId", "testPointId")
    )
    return {
        "schemaVersion": "1.0",
        "runnerRunId": checked["runId"],
        "runnerVerdict": checked["verdict"],
        "runnerVerdictCode": checked["verdictCode"],
        "builtFromCurrentSource": checked["builtFromCurrentSource"],
        "sourceGitCommit": checked["gitCommit"],
        "notes": notes_for(checked),
        "mapping": {
            "workItemId": mapping.get("workItemId", checked.get("workItemId")),
            "runnerCaseId": mapping.get("runnerCaseId", checked.get("caseId")),
            "adoTestCaseId": mapping.get("adoTestCaseId"),
            "planId": mapping.get("planId"),
            "suiteId": mapping.get("suiteId"),
            "configurationId": mapping.get("configurationId"),
            "testPointId": mapping.get("testPointId"),
        },
        "ado": {
            "publicationMode": "dry-run",
            "testRunId": None,
            "testResultId": None,
            "publishStatus": "NOT_ATTEMPTED" if not publishable else "DRY_RUN",
        },
        "evidence": {"sha256Manifest": evidence_index},
    }


def publication_key(manifest: dict) -> str:
    mapping = manifest["mapping"]
    return "|".join(str(mapping.get(key)) for key in ("planId", "testPointId")) + "|" + manifest["runnerRunId"]


def publish_dry_run(
    result_path: Path,
    *,
    runs_root: Path,
    mapping: dict | None,
    out_dir: Path,
    log_path: Path,
    execute: bool = False,
    write_enabled: bool = False,
    approved: bool = False,
) -> dict:
    if execute or write_enabled:
        if not approved or not write_enabled:
            raise ContractError("NOT_AUTHORIZED", "ADO write is disabled; only dry-run is available")
        raise ContractError("NOT_AUTHORIZED", "execute is not enabled in this offline adapter")
    data = load_json(result_path)
    checked = validate_result(data, result_path=result_path, runs_root=runs_root)
    manifest = manifest_for(checked, mapping, None)
    key = publication_key(manifest)
    history = []
    if log_path.exists():
        history = json.loads(log_path.read_text(encoding="utf-8"))
    prior = [row for row in history if row.get("key") == key and row.get("publishStatus") == "PUBLISHED"]
    if prior:
        manifest["ado"]["publishStatus"] = "ALREADY_PUBLISHED"
        manifest["ado"]["testRunId"] = prior[-1].get("testRunId")
    out_dir.mkdir(parents=True, exist_ok=True)
    destination = out_dir / f"{checked['runId']}.publication-manifest.json"
    destination.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    if not any(row.get("key") == key and row.get("mode") == "dry-run" for row in history):
        history.append({"key": key, "mode": "dry-run", "publishStatus": manifest["ado"]["publishStatus"], "testRunId": None})
        log_path.parent.mkdir(parents=True, exist_ok=True)
        log_path.write_text(json.dumps(history, ensure_ascii=False, indent=2), encoding="utf-8")
    manifest["manifestPath"] = str(destination)
    return manifest
