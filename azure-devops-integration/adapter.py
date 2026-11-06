"""Offline Azure DevOps integration boundary. Python 3.10+, standard library only.

Runner owns verdict; this module validates and preserves it. No HTTP writes.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent
AUTOTEST = ROOT.parent / "VegaAutoTest"
CODES = {"PASS": 0, "FAIL": 1, "BLOCKED": 2, "ERROR": 3}
OUTCOMES = {"PASS": "Passed", "FAIL": "Failed", "BLOCKED": "Inconclusive", "ERROR": "Inconclusive"}


class IntegrationError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise IntegrationError(message)


def load(path):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=pairs,
                      parse_constant=lambda _: (_ for _ in ()).throw(IntegrationError("Non-finite JSON")))


def save(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.NamedTemporaryFile(mode="w", encoding="utf-8", dir=path.parent, delete=False) as f:
        json.dump(value, f, ensure_ascii=False, indent=2)
        name = f.name
    os.replace(name, path)


def inside(path, root):
    path, root = Path(path).resolve(), Path(root).resolve()
    require(path != root and path.is_relative_to(root), "Path escapes allowed root")
    return path


def nonempty(value):
    return isinstance(value, str) and bool(value.strip())


def validate_result(path, runs_root, registry, work_item, case_id, process_exit):
    path = inside(path, runs_root)
    require(path.name == "result.json" and path.is_file(), "Missing Runner result.json")
    result = load(path)
    require(result.get("schemaVersion") == "1.0", "Unsupported result schema")
    run_id = result.get("runId")
    require(nonempty(run_id) and re.fullmatch(r"[A-Za-z0-9_.-]+", run_id) is not None
            and path.parent.name == run_id, "Invalid runId/directory")
    require(type(work_item) is int and work_item > 0, "Work item must be positive")
    require(result.get("workItemId") == work_item and result.get("caseId") == case_id,
            "Requested identity differs from Runner")
    cases = [c for c in registry["cases"] if c["id"] == case_id and work_item in c["workItems"]]
    require(len(cases) == 1, "Runner case must match exactly one registry entry")
    verdict = result.get("verdict")
    require(verdict in CODES and type(result.get("exitCode")) is int and
            type(process_exit) is int and result["exitCode"] == process_exit == CODES[verdict],
            "Runner verdict/JSON/process exit conflict")
    for field in ("verdictCode", "verdictReason", "startedAt", "finishedAt"):
        require(nonempty(result.get(field)), "Missing " + field)
    started = datetime.fromisoformat(result["startedAt"].replace("Z", "+00:00"))
    finished = datetime.fromisoformat(result["finishedAt"].replace("Z", "+00:00"))
    require(started.tzinfo is not None and finished.tzinfo is not None and finished >= started,
            "Invalid Runner timestamps")
    require(result["verdictCode"] != "CONTRACT_VIOLATION", "Runner reports CONTRACT_VIOLATION")
    require(type(result.get("durationMs")) is int and result["durationMs"] > 0, "Invalid duration")
    target = result["target"]
    for field in ("gitCommit", "vegaExe", "repoRoot", "gitBranch"):
        require(nonempty(target.get(field)), "Missing target." + field)
    for field in ("gitDirty", "rebuilt", "builtFromCurrentSource"):
        require(type(target.get(field)) is bool, "Invalid target." + field)
    require(not target["builtFromCurrentSource"] or target["rebuilt"], "Rebuild contract conflict")
    assertions, summary = result["assertions"], result["summary"]
    require(isinstance(assertions, list) and isinstance(result["tests"], list), "Invalid result lists")
    require(all(isinstance(a, dict) and a.get("result") in ("PASS", "FAIL", "SKIP") for a in assertions),
            "Invalid assertion structure")
    counts = {"total": len(assertions), "passed": sum(a["result"] == "PASS" for a in assertions),
              "failed": sum(a["result"] == "FAIL" for a in assertions),
              "skipped": sum(a["result"] == "SKIP" for a in assertions)}
    require(all(type(summary.get(k)) is int and summary[k] == v for k, v in counts.items()),
            "Assertion/summary contract conflict")
    channels = result["channels"]
    for name in ("l1Api", "l2Uia", "assertions"):
        require(type(channels[name].get("available")) is bool, "Invalid channel availability")
    require(channels["assertions"]["available"] or nonempty(channels["assertions"].get("reason")),
            "Missing unavailable assertions reason")
    if channels["l1Api"]["available"] and assertions:
        require(any(a.get("kind") in ("oracle-crosscheck", "l1-query") and a["result"] != "SKIP"
                    for a in assertions), "L1 availability lacks consumed oracle")
    require(not (not channels["l2Uia"]["available"] and verdict in ("FAIL", "ERROR") and
                 summary["passed"] > 0 and any(t.get("outcome") == "Failed" for t in result["tests"])),
            "Runner channel contract conflict")
    evidence_dir = Path(result["evidence"]["dir"])
    if not evidence_dir.is_absolute():
        evidence_dir = Path(runs_root).resolve().parent / evidence_dir
    require(inside(evidence_dir, runs_root) == path.parent, "Evidence directory differs from run")
    require(isinstance(result["evidence"]["files"], list), "Invalid evidence files")
    for name in result["evidence"]["files"]:
        require(nonempty(name) and not Path(name).is_absolute(), "Evidence names must be relative")
        require(inside(path.parent / name, path.parent).is_file(), "Missing declared evidence")
    require((path.parent / "report.md").is_file(), "Missing Runner report.md")
    return result


def select_mapping(config, registry, work_item, case_id):
    require(config.get("schemaVersion") == "1.0", "Unsupported mapping schema")
    seen, points = set(), set()
    for item in config["mappings"]:
        key = (item["workItemId"], item["runnerCaseId"])
        require(key not in seen, "Duplicate Runner mapping; configuration fan-out is unsupported")
        seen.add(key)
        matches = [c for c in registry["cases"] if c["id"] == key[1] and key[0] in c["workItems"]]
        require(len(matches) == 1, "Mapping absent from registry")
        require(isinstance(item.get("layers"), list) and item["layers"] and
                len(set(item["layers"])) == len(item["layers"]) and
                set(item["layers"]) <= set(matches[0]["layers"]), "Mapping has invalid layers")
        for field in ("adoTestCaseId", "planId", "suiteId", "configurationId", "testPointId"):
            require(item.get(field) is None or type(item[field]) is int and item[field] > 0,
                    "Invalid ADO identifier: " + field)
        if item.get("testPointId") is not None:
            point = (item.get("planId"), item["testPointId"])
            require(point not in points, "Duplicate Test Point mapping")
            points.add(point)
    matches = [m for m in config["mappings"] if (m["workItemId"], m["runnerCaseId"]) == (work_item, case_id)]
    require(len(matches) == 1, "Missing unique mapping")
    return matches[0]


def resolve_point(mapping, points):
    fields = ("adoTestCaseId", "planId", "suiteId", "configurationId", "testPointId")
    require(all(type(mapping.get(k)) is int and mapping[k] > 0 for k in fields), "Incomplete ADO mapping")
    # Input is a normalized read-only snapshot, explicitly scoped to this plan/suite.
    require(points.get("planId") == mapping["planId"] and points.get("suiteId") == mapping["suiteId"]
            and points.get("complete") is True, "Point snapshot scope/pagination unconfirmed")
    found = [p for p in points["points"] if p.get("testCaseId") == mapping["adoTestCaseId"]
             and p.get("configurationId") == mapping["configurationId"]]
    require(len(found) == 1 and found[0].get("id") == mapping["testPointId"], "No unique matching Test Point")
    return found[0]


def archive(run_dir, output):
    """Snapshot each file once: hashes refer to exact ZIP bytes, originals untouched."""
    run_dir, output = Path(run_dir).resolve(), Path(output).resolve()
    require(not output.is_relative_to(run_dir), "Output cannot be inside Runner evidence")
    files = []
    for path in sorted(run_dir.rglob("*")):
        # Resolve even directories to reject junctions before packaging.
        inside(path, run_dir)
        if path.is_file():
            files.append(path)
    output.mkdir(parents=True, exist_ok=True)
    index = {}
    with tempfile.NamedTemporaryFile(dir=output, suffix=".zip", delete=False) as f:
        temporary = Path(f.name)
    try:
        with zipfile.ZipFile(temporary, "w", zipfile.ZIP_DEFLATED) as zipped:
            for path in files:
                data = path.read_bytes()
                name = path.relative_to(run_dir).as_posix()
                zipped.writestr(name, data)
                index[name] = {"sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
        os.replace(temporary, output / "evidence.zip")
    finally:
        temporary.unlink(missing_ok=True)
    save(output / "artifact-sha256-manifest.json", index)
    return index


def prepare(path, runs_root, registry, mapping_config, work_item, case_id, process_exit, out, points=None):
    result = validate_result(path, runs_root, registry, work_item, case_id, process_exit)
    mapping = select_mapping(mapping_config, registry, work_item, case_id)
    blockers = ["REMOTE_PUBLISH_NOT_IMPLEMENTED_PENDING_COMPATIBILITY"]
    try:
        require(points is not None, "POINT_SNAPSHOT_MISSING")
        resolve_point(mapping, points)
    except IntegrationError as exc:
        blockers.append(str(exc))
    output = Path(out) / result["runId"]
    require(not output.exists(), "Output run already exists; history is immutable, choose a new output root")
    index = archive(Path(path).parent, output)
    target = result["target"]
    warnings = []
    if not target["builtFromCurrentSource"]:
        warnings.append("未使用当前源码重编，结论仅对旧二进制有效")
    if not result["channels"]["l1Api"]["available"]:
        warnings.append("未进行 L1 交叉验证")
    manifest = {"schemaVersion": "1.0", "runnerRunId": result["runId"],
                "runnerResultPath": str(Path(path).resolve()), "runnerVerdict": result["verdict"],
                "runnerVerdictCode": result["verdictCode"], "runnerVerdictReason": result["verdictReason"],
                "runnerExitCode": process_exit, "target": target, "mapping": mapping,
                "warnings": warnings, "summary": result["summary"],
                "ado": {"publicationMode": "dry-run", "publishStatus": "BLOCKED",
                        "testRunId": None, "testResultId": None, "blockers": blockers,
                        "outcomePreview": OUTCOMES[result["verdict"]]},
                "evidence": {"archive": "evidence.zip", "sha256Manifest": "artifact-sha256-manifest.json",
                             "resultSha256": index["result.json"]["sha256"]}}
    save(output / "publication-manifest.json", manifest)
    return manifest


def invoke(work_item, case_id, layers, repo_root, timeout, registry, rebuild=False):
    matches = [c for c in registry["cases"] if c["id"] == case_id and work_item in c["workItems"]]
    require(len(matches) == 1 and layers and set(layers) <= set(matches[0]["layers"]), "Invalid requested case/layers")
    require(timeout > 0, "Timeout must be positive")
    cmd = ["dotnet", "run", "--project", str(AUTOTEST / "runner/Vega.AutoTest.Runner"),
           "--no-build", "--", "verify", "--work-item", str(work_item), "--case", case_id,
           "--layers", ",".join(layers), "--repo-root", str(Path(repo_root).resolve()),
           "--runs-dir", str(AUTOTEST / "runs")]
    if rebuild:
        cmd.append("--rebuild")
    with subprocess.Popen(cmd, cwd=AUTOTEST, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                          text=True, encoding="utf-8", errors="replace") as process:
        try:
            stdout, _ = process.communicate(timeout=timeout)
        except subprocess.TimeoutExpired:
            if os.name == "nt":
                subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"], capture_output=True)
            process.kill()
            process.communicate()
            raise IntegrationError("Runner timeout; no result published; retain runs for diagnostics")
    lines = stdout.strip().splitlines()
    require(bool(lines), "Runner stdout lacks result path")
    path = inside(lines[-1].strip(), AUTOTEST / "runs")
    return path, process.returncode


def aggregate(spec, registry, runs_root):
    """Release policy is deliberately distinct from Runner verdict."""
    require(nonempty(spec.get("releaseId")) and nonempty(spec.get("suiteName")), "Missing release identity")
    require(isinstance(spec.get("items"), list) and spec["items"], "Empty release suite")
    baseline = spec.get("baseline", {})
    incomplete, blockers, rows, seen = [], [], [], set()
    for item in spec["items"]:
        key = (item["workItemId"], item["caseId"])
        require(key not in seen, "Duplicate release work item/case")
        seen.add(key)
        require(type(item.get("required")) is bool, "Explicit required flag needed")
        if not item["required"]:
            continue
        try:
            require(item.get("releaseId") == spec["releaseId"], "Result not bound to requested release")
            result = validate_result(item["resultPath"], runs_root, registry, *key, item["processExitCode"])
            capture = load(item["executionReceipt"])
            require(capture.get("runId") == result["runId"] and capture.get("releaseId") == spec["releaseId"]
                    and capture.get("resultSha256") == hashlib.sha256(Path(item["resultPath"]).read_bytes()).hexdigest(),
                    "Execution receipt is not bound to result/release")
            case = next(c for c in registry["cases"] if c["id"] == key[1])
            require(isinstance(item.get("layers"), list) and item["layers"] and
                    set(item["layers"]) <= set(case["layers"]) and
                    isinstance(capture.get("layers"), list) and set(capture["layers"]) == set(item["layers"]),
                    "Required layer coverage lacks execution receipt")
            for field in ("gitCommit", "exeSha256", "environmentId", "automationCommit", "buildId"):
                require(nonempty(baseline.get(field)) and capture.get(field) == baseline[field],
                        "Missing/mismatched baseline: " + field)
            require(result["target"]["gitCommit"] == baseline["gitCommit"], "Target commit differs from release")
            rows.append({"workItemId": key[0], "caseId": key[1], "runId": result["runId"],
                         "verdict": result["verdict"], "verdictCode": result["verdictCode"],
                         "target": result["target"], "summary": result["summary"]})
            if result["verdict"] != "PASS" or result["verdictCode"] == "OK_STALE_BUILD" or not result["target"]["builtFromCurrentSource"] or result["target"]["gitDirty"]:
                blockers.append(f"{key[0]}: verdict/build policy not satisfied")
            require(type(item.get("requiresL1Oracle")) is bool, "Explicit oracle policy needed")
            if item["requiresL1Oracle"] and not result["channels"]["l1Api"]["available"]:
                blockers.append(f"{key[0]}: required L1 oracle unavailable")
        except (IntegrationError, OSError, ValueError, KeyError, TypeError) as exc:
            incomplete.append(f"{key[0]}: {exc}")
    require(any(i["required"] for i in spec["items"]), "Release has no required items")
    approvals = spec.get("approvals", {})
    pending = [name for name in ("manualTests", "criticalBugs", "deviceSafety", "stationConfiguration")
               if not isinstance(approvals.get(name), dict) or approvals[name].get("approved") is not True
               or not nonempty(approvals[name].get("evidence")) or not nonempty(approvals[name].get("approvedBy"))]
    decision = ("INCOMPLETE_EVIDENCE" if incomplete else "NOT_READY" if blockers else
                "NEEDS_HUMAN_REVIEW" if pending else "READY_FOR_APPROVAL")
    return {"releaseId": spec["releaseId"], "suiteName": spec["suiteName"], "releaseDecision": decision,
            "workItems": rows, "blockerReasons": incomplete + blockers, "pendingApprovals": pending,
            "finalReleaseApprovalRequired": True}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--registry", type=Path, default=AUTOTEST / "cases/registry.json")
    commands = parser.add_subparsers(dest="command", required=True)
    commands.add_parser("discover", help="Print offline compatibility status; performs no network access")
    for name in ("publish", "verify"):
        p = commands.add_parser(name)
        p.add_argument("--mapping", type=Path, default=ROOT / "config/test-case-map.sample.json")
        p.add_argument("--work-item", type=int, required=True)
        p.add_argument("--case", required=True)
        p.add_argument("--out", type=Path, default=ROOT / "out")
        p.add_argument("--points", type=Path)
        if name == "publish":
            p.add_argument("--result", type=Path, required=True)
            p.add_argument("--process-exit-code", type=int, required=True)
            p.add_argument("--runs-root", type=Path, default=AUTOTEST / "runs")
            p.add_argument("--execute", action="store_true")
        else:
            p.add_argument("--repo-root", type=Path, required=True)
            p.add_argument("--layers", required=True)
            p.add_argument("--timeout", type=int, default=2400)
            p.add_argument("--rebuild", action="store_true", help="Explicit opt-in; only approved isolated checkout")
    p = commands.add_parser("aggregate")
    p.add_argument("--spec", type=Path, required=True)
    p.add_argument("--runs-root", type=Path, default=AUTOTEST / "runs")
    p.add_argument("--out", type=Path, required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == "discover":
            print(json.dumps(load(ROOT / "config/compatibility.json"), ensure_ascii=False, indent=2))
            return 2
        registry = load(args.registry)
        if args.command == "aggregate":
            report = aggregate(load(args.spec), registry, args.runs_root)
            require(not args.out.exists(), "Release report directory exists; choose new output")
            save(args.out / "release-gate.json", report)
            (args.out / "release-report.md").write_text(
                "# Release " + report["releaseId"] + "\n\n" + report["releaseDecision"] + "\n\n" +
                "\n".join(f"- {r['workItemId']}: {r['verdict']} / {r['verdictCode']} / {r['runId']}" for r in report["workItems"]) +
                "\n\n" + "\n".join(report["blockerReasons"] + report["pendingApprovals"]) +
                "\n\n最终发布仍需负责人批准。\n", encoding="utf-8")
            print(report["releaseDecision"])
            return 0 if report["releaseDecision"] == "READY_FOR_APPROVAL" else 2
        require(not getattr(args, "execute", False), "Execute disabled: target API and authorization unverified")
        mapping = load(args.mapping)
        selected = select_mapping(mapping, registry, args.work_item, args.case)
        if args.command == "verify":
            layers = args.layers.split(",")
            require(set(layers) == set(selected["layers"]), "Requested layers differ from mapped coverage")
            path, code = invoke(args.work_item, args.case, layers, args.repo_root, args.timeout, registry, args.rebuild)
            runs = AUTOTEST / "runs"
        else:
            path, code, runs = args.result, args.process_exit_code, args.runs_root
        manifest = prepare(path, runs, registry, mapping, args.work_item, args.case, code,
                           args.out, load(args.points) if args.points else None)
        print(json.dumps(manifest, ensure_ascii=False, indent=2))
        # Never return green for failed Runner or unverified external publication.
        return code if code else 2
    except (IntegrationError, OSError, ValueError, KeyError, TypeError, AttributeError) as exc:
        print("INTEGRATION_FAILURE: " + str(exc), file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
