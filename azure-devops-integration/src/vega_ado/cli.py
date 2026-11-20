"""Command line for the offline adapter."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .contract import ContractError, validate_result
from .discover import probe_collection, write_discovery
from .gate import decide
from .mapping import load_registry, validate_map
from .publish import publish_dry_run

ROOT = Path(__file__).resolve().parents[2]


def _settings() -> dict:
    path = ROOT / "config" / "ado-settings.sample.json"
    return json.loads(path.read_text(encoding="utf-8"))


def cmd_discover(_args) -> int:
    settings = _settings()
    probe = probe_collection("http://win-devops:8080/DefaultCollection")
    registry = Path(settings["registryPath"])
    case_count = len(load_registry(registry)["cases"]) if registry.is_file() else None
    compatibility = {
        "productGuess": "Azure DevOps Server or TFS",
        "collectionBaseUrl": "http://win-devops:8080/DefaultCollection",
        "project": "Vega_SW",
        "apiVersionNegotiated": None,
        "anonymousProbe": probe,
        "writeTestResultsEnabled": False,
        "https": False,
        "httpsException": "现场集合地址是内网 HTTP。适配层不改成公网 HTTPS，也不跳过证书校验。",
        "registryCases": case_count,
        "sampleWorkItemsInRegistry": ["wi3478", "wi3547", "wi3549"],
        "nightlyEntry": "VegaAutoTest-NightlyAgent",
        "adapterDoesNotReplaceNightly": True,
        "credentials": "not read; AZURE_DEVOPS_PAT is not required for this offline stage",
    }
    report = """# P0 发现报告

适配层只做离线契约。没有读取 PAT，没有创建 Test Plan，没有 POST/PATCH。

## 和方案文档不一致、已按现场改正的地方

- 自动化根是 `C:\\AutoTest\\AutoTest\\VegaAutoTest`，不是文档里的 `D:\\Code\\Vega\\VegaAutomation\\VegaAutoTest`。
- 被测工作区在 `C:\\AutoTest\\workspaces`，不写产品仓库。
- `cases/registry.json` 里 `wi3478`、`wi3547`、`wi3549` 都在。用例 id 不总是 `wi` 加数字，映射必须用 registry 的真实 id。
- 夜间检查仍是计划任务 `VegaAutoTest-NightlyAgent`。这个适配层不替换它，也不调用 `autopilot.py --daily`。
- 集合地址来自本机非秘密配置，是 `http://win-devops:8080/DefaultCollection`，项目 `Vega_SW`。内网 HTTP 作为现场例外保留，写入开关保持关闭。
- 样例映射里的 ADO Test Case / Plan / Point 仍是 null。在实查到唯一 Point 之前不能发布。

## 未做

- 未用凭据调用 Test Plans。
- 未在这台桌面上启动 Vega 做新的 UIA verify。
- 未注册 Pipeline。
"""
    out = ROOT / "out"
    write_discovery(out, compatibility, report)
    print(out / "discovery-report.md")
    return 0


def cmd_validate_map(args) -> int:
    registry = load_registry(Path(args.registry))
    mapping = json.loads(Path(args.map).read_text(encoding="utf-8"))
    try:
        rows = validate_map(mapping, registry)
    except ContractError as error:
        print(error.code + ": " + str(error), file=sys.stderr)
        return 1
    print(json.dumps(rows, ensure_ascii=False, indent=2))
    return 0


def cmd_publish(args) -> int:
    settings = _settings()
    mapping = None
    if args.map:
        data = json.loads(Path(args.map).read_text(encoding="utf-8"))
        wanted = [row for row in data["mappings"] if row.get("workItemId") == args.work_item] if args.work_item else data["mappings"][:1]
        mapping = wanted[0] if wanted else None
    try:
        manifest = publish_dry_run(
            Path(args.result),
            runs_root=Path(args.runs_root or settings["runsRoot"]),
            mapping=mapping,
            out_dir=Path(args.out),
            log_path=Path(args.out) / "publication-log.json",
            execute=args.execute,
            write_enabled=bool(settings.get("writeTestResultsEnabled")),
            approved=bool(args.approval),
        )
    except ContractError as error:
        print(error.code + ": " + str(error), file=sys.stderr)
        return 1
    print(manifest["manifestPath"])
    return 0


def cmd_aggregate(args) -> int:
    suite = json.loads(Path(args.suite).read_text(encoding="utf-8"))
    results_dir = Path(args.results_dir)
    items = []
    for entry in suite["items"]:
        matches = list(results_dir.glob(f"*wi{entry['workItemId']}*/result.json"))
        if not matches:
            items.append({**entry, "runId": None, "runnerVerdict": None, "builtFromCurrentSource": False, "l1Available": None})
            continue
        path = matches[-1]
        data = json.loads(path.read_text(encoding="utf-8"))
        checked = validate_result(data, result_path=path, runs_root=Path(args.runs_root))
        items.append({
            **entry,
            "runId": checked["runId"],
            "runnerVerdict": checked["verdict"],
            "verdictCode": checked["verdictCode"],
            "builtFromCurrentSource": checked["builtFromCurrentSource"],
            "l1Available": checked["l1Available"],
        })
    decision = decide(items)
    destination = Path(args.out) / "release-gate.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps({"suite": suite.get("suiteName"), **decision, "items": items}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(destination)
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="vega-ado")
    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("discover")
    validate = sub.add_parser("validate-map")
    validate.add_argument("--map", required=True)
    validate.add_argument("--registry", required=True)
    publish = sub.add_parser("publish")
    publish.add_argument("--result", required=True)
    publish.add_argument("--map")
    publish.add_argument("--work-item", type=int)
    publish.add_argument("--runs-root")
    publish.add_argument("--out", required=True)
    publish.add_argument("--execute", action="store_true")
    publish.add_argument("--approval")
    aggregate = sub.add_parser("aggregate")
    aggregate.add_argument("--suite", required=True)
    aggregate.add_argument("--results-dir", required=True)
    aggregate.add_argument("--runs-root", required=True)
    aggregate.add_argument("--out", required=True)
    args = parser.parse_args(argv)
    commands = {
        "discover": cmd_discover,
        "validate-map": cmd_validate_map,
        "publish": cmd_publish,
        "aggregate": cmd_aggregate,
    }
    return commands[args.command](args)


if __name__ == "__main__":
    raise SystemExit(main())
