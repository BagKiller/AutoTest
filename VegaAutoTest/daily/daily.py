"""Collect immutable commit manifests, cluster commits, plan one run per case, invoke the existing Runner.

No product checkout/configuration edits, no hardware commands, no fabricated verdicts.
The scheduler supplies an agent for reviewing diffs and completing authorized UI work.
"""
import argparse
import base64
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import time
import uuid
import individual
import coverage_preparation as coverage_contract
import operation_lock
import shutil
import sys
import case_authoring
import queue_driver

ROOT = Path(__file__).resolve().parents[1]
REPO = Path(os.environ.get("VEGA_SW_ROOT", r"C:\Vega\Vega_SW"))
MIRROR = ROOT / "daily" / "cache.git"
STATE = ROOT / "daily" / "state.json"
SCHEDULING = ROOT / "daily" / "scheduling.json"
REGISTRY = ROOT / "cases" / "registry.json"
CN = timezone(timedelta(hours=8))
REMOTE = "http://win-devops:8080/DefaultCollection/Vega_SW/_git/Vega_SW"
SHELL = r"C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
DOTNET = Path(r"C:\AutoTest\runtime\dotnet\dotnet.exe")
WORKSPACES = Path(r"C:\AutoTest\workspaces")
LOG_FORMAT = "%H%x1f%cI%x1f%aI%x1f%s%x1f%b%x1e"
PR_REF = re.compile(r"(?i)(?:merged\s+)?(?:PR|pull\s*request)\s*#?\s*\d+")
WI_REFS = [
    re.compile(r"(?<![\w#])(?:AB)?#(\d{3,6})(?!\d)"),
    re.compile(r"(?i)(?:工作项|需求|任务|缺陷|(?<![A-Za-z])(?:work\s*item|WI|bug|task|pbi))\s*[#:：]?\s*(\d{3,6})(?!\d)"),
    re.compile(r"[【\[]\s*(\d{4,6})\s*[】\]]"),
]
VERDICT_RANK = {"FAIL": 0, "ERROR": 1, "BLOCKED": 2, None: 3, "PASS": 4}
CLOSED = ("ACCEPTED", "NOT_APPLICABLE", "ACKNOWLEDGED")


def save(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    # Nested campaign/SHA/run evidence paths are already close to MAX_PATH.
    # Appending a full UUID to the destination name made otherwise valid paths
    # fail with FileNotFoundError during atomic writes.
    temporary = path.with_name('.' + uuid.uuid4().hex[:12] + '.tmp')
    try:
        temporary.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")
        os.replace(temporary, path)
    finally:
        temporary.unlink(missing_ok=True)


def load(path, default=None):
    return json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else default


def now_cn():
    return datetime.now(CN)


def run_bounded(command, *, timeout, **kwargs):
    """A deadline also terminates this invocation's child tree, not just its shell."""
    process = subprocess.Popen(command, **kwargs)
    try:
        return subprocess.CompletedProcess(command, process.wait(timeout=timeout))
    except subprocess.TimeoutExpired:
        subprocess.run(["taskkill", "/PID", str(process.pid), "/T", "/F"],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=15)
        process.wait(timeout=10)
        raise RuntimeError(f"Stage exceeded {timeout}s; terminated owned process tree PID {process.pid}") from None


def git(*args, repo=REPO, env=None):
    p = subprocess.run(["git", "-c", f"safe.directory={repo.as_posix()}", "-C", str(repo), *args],
                       capture_output=True, encoding="utf-8", errors="replace", env=env, timeout=180)
    if p.returncode:
        # Never expose authentication headers or Git's echoed command in diagnostics.
        raise RuntimeError(f"Git operation {args[0]} failed (exit {p.returncode}); inspect connectivity/permissions")
    return p.stdout if '-z' in args else p.stdout.strip()


def sync():
    if not MIRROR.exists():
        MIRROR.parent.mkdir(parents=True, exist_ok=True)
        git("clone", "--bare", "--shared", str(REPO), str(MIRROR))
        git("remote", "set-url", "origin", REMOTE, repo=MIRROR)
    env = os.environ.copy()
    pat = env.get("AZURE_DEVOPS_PAT", "")
    if not pat:
        config = ROOT.parent / "code_indicator" / "vega-agent" / "config.json"
        pat = json.loads(config.read_text(encoding="utf-8-sig"))["azure_devops"]["pat"]
    env.update(GIT_TERMINAL_PROMPT="0", GCM_INTERACTIVE="Never", GIT_CONFIG_COUNT="3",
               GIT_CONFIG_KEY_0="http.http://win-devops:8080/.extraHeader",
               GIT_CONFIG_VALUE_0="Authorization: Basic " + base64.b64encode((":" + pat).encode()).decode(),
               GIT_CONFIG_KEY_1="credential.helper", GIT_CONFIG_VALUE_1="",
               GIT_CONFIG_KEY_2="http.followRedirects", GIT_CONFIG_VALUE_2="false")
    git("fetch", "--quiet", "origin", "+refs/heads/master:refs/heads/master", repo=MIRROR, env=env)
    return MIRROR


# ────────────────────────────── 提交解析 ──────────────────────────────

def classify(files):
    """Conservative coverage hints. Unmapped changes explicitly require agent triage."""
    rules = {
        "sizes": lambda p: any(s in p for s in ["WaferSize", "XYCali", "XYAuto", "EcamCalibration", "Uniformity", "BeamStability", "RecipeBatch", "ValueConverters", "KTStyle"]),
        "notch": lambda p: "NotchChannelResolver" in p or "WaferRunSequenceBase" in p,
        "loadport": lambda p: any(s in p for s in ["GEMService", "IGEMCommandChannel", "SubstrateInLPStatus", "Gem300Parameters"]),
        "autooptics": lambda p: "HorizontalToolBarViewModel" in p,
        "ni-abort": lambda p: any(s in p for s in ["NICardDevice", "DataCollectorBase", "QT1426Proxy"]),
        "gem-handshake": lambda p: any(s in p for s in ["IWaferRunService", "WaferRunService.cs", "WaferRunScanOnly", "WaferRunInfo.cs", "JobLifecycleManager", "WaferRunEngine", "WaferRunSequenceBase"]),
        "bfp": lambda p: "BFPCali.cs" in p or "BeamFocusProfileViewModel" in p,
        "dependencies": lambda p: p.endswith("packages.lock.json"),
        "pmt": lambda p: any(s in p for s in ["PMT", "CaliCommonDataDefinition", "OpticsGear", "CaliApplyHelper", "OpticsConfig", "OpticsLiteVega100"]),
        "installer": lambda p: p.endswith("Vega_Inno.iss"),
        "run-isolation": lambda p: any(s in p for s in ["WaferRunDeviceControlHelper", "WaferRunInfo", "HazeMappingRingScan", "WaferRunXYAutoCalSequence", "WaferRunXYCalSequence", "DataCollectorBase", "QT1426Proxy"]),
        "io-hwv2": lambda p: any(s in p for s in ["IOBitMapping", "CylinderConfigViewModel", "CommonEnums"]),
        "afs-close": lambda p: "AFSDiagnosticsViewModel" in p or "MSRotatingMotorDevice" in p,
        "xy-notch-gate": lambda p: "XYCalibrationExecutor" in p or "XYCalibrationView" in p,
    }
    groups = [key for key, pred in rules.items() if any(pred(p) for p in files)]
    ignored = lambda p: p.startswith("ve-docs/") or p.endswith((".resx", "Resources.Designer.cs", ".sln", ".csproj")) or p == "version.props"
    unknown = [p for p in files if not ignored(p) and not any(pred(p) for pred in rules.values())]
    return groups, unknown


def work_items(text):
    text = PR_REF.sub(" ", text or "")
    found = []
    for pattern in WI_REFS:
        found += [int(m) for m in pattern.findall(text)]
    return sorted(set(found))


def module_of(path):
    parts = path.split("/")
    if len(parts) == 1:
        return "(root)"
    if parts[0] in ("Vega", "src", "Source") and len(parts) > 2:
        return parts[0] + "/" + parts[1]
    return parts[0]


def no_product_change(files):
    return not files or all(p.startswith("ve-docs/") or p.lower().endswith(".md") for p in files)


def describe(row, source, start, end, origin):
    sha, committed, authored, title, body = (row.split("\x1f") + [""] * 5)[:5]
    files = git("diff-tree", "--root", "--no-commit-id", "--name-only", "-r", "--first-parent", sha, repo=source).splitlines()
    groups, unknown = classify(files)
    is_today = start <= datetime.fromisoformat(committed) <= end
    return dict(sha=sha, committedAt=committed, authoredAt=authored, title=title, message=body.strip(),
                origin=origin if origin != "window" else ("today" if is_today else "carryover"),
                carryover=not is_today, workItems=work_items(title + "\n" + body), files=files,
                groups=groups, unmappedFiles=unknown, modules=sorted({module_of(p) for p in unknown}),
                noProductChange=no_product_change(files))


def collect(last, offline, on_collected=None):
    now = now_cn()
    start = now.replace(hour=0, minute=0, second=0, microsecond=0)
    source = REPO if offline else sync()
    head = git("rev-parse", "master", repo=source)
    old = load(STATE, {})
    previous_head = old.get("collectedHead")
    history_changed = False
    if previous_head:
        try:
            git("merge-base", "--is-ancestor", previous_head, head, repo=source)
        except RuntimeError:
            history_changed = True
    log = git("log", "master", f"--format={LOG_FORMAT}", *([f"-{last}"] if last else []), repo=source)
    rows = [r.strip("\n") for r in log.split("\x1e") if r.strip()]
    unseen = set(git("rev-list", head, "^" + previous_head, repo=source).splitlines()) if previous_head and not history_changed else set()
    commits = []
    for row in rows:
        sha, committed = row.split("\x1f", 2)[:2]
        is_today = start <= datetime.fromisoformat(committed) <= now
        # Select today's commits plus every newly reachable commit since last collection,
        # including backdated commits and submissions after yesterday's cutoff.
        if not last and not (is_today or sha in unseen):
            continue
        commits.append(describe(row, source, start, now, "window"))
    # A daily batch validates only new commits selected for this collection. Historical
    # NEEDS_CASE/ERROR records remain in their original reports and are never re-planned.
    retired_pending = old.get("pendingCommits", []) if not last else []
    batch_id = now.strftime("%Y%m%dT%H%M%S%z") + "_" + uuid.uuid4().hex[:6]
    out = ROOT / "daily" / "runs" / batch_id
    out.mkdir(parents=True)
    manifest = dict(schemaVersion=2, batchId=batch_id, collectedAt=now.isoformat(), timezone="Asia/Shanghai",
                    mode="recent-example" if last else "daily", source=str(source), remoteFresh=not offline,
                    selection="all reachable DAG commits; committer date today plus newly reachable carryover; no historical pending replay",
                    windowStart=start.isoformat(), windowEnd=now.isoformat(), head=head,
                    previousCollectedHead=previous_head, historyRewritten=history_changed,
                    localHead=git("rev-parse", "HEAD"), localDirty=bool(git("status", "--porcelain")),
                    retiredPendingFromEarlierCollections=retired_pending, unreachablePending=[],
                    commits=commits)
    for commit in commits:
        diff = git("show", "--format=fuller", "--first-parent", "--no-ext-diff", commit["sha"], repo=source)
        (out / (commit["sha"][:8] + ".diff")).write_text(diff, encoding="utf-8")
    save(out / "manifest.json", manifest)
    if on_collected:
        # The independent controller persists its resume pointer before the
        # collection cursor advances. A chat/process crash cannot lose the batch.
        on_collected(out, manifest)
    if not last and not offline and not history_changed:
        # Do not retain an acceptance backlog. Reports preserve unresolved evidence,
        # while later daily runs are scoped only to newly collected commits.
        retired = old.get("retiredPendingCommits", []) + retired_pending
        save(STATE, dict(old, collectedHead=head, collectedAt=now.isoformat(), pendingCommits=[],
                         retiredPendingCommits=list(dict.fromkeys(retired))[-500:], previousBatch=str(out),
                         note="Daily collection has no replay queue; unresolved results remain in their original batch reports."))
    return out, manifest


# ────────────────────────────── 归簇与编排 ──────────────────────────────

def is_ui_layer(name, layer):
    return name in ("uia", "l1") or layer["project"].replace("\\", "/").startswith("l2/")


def profile_args(profile):
    """Vega100-PIC → (-Model Vega100, -Profile PIC); Vega500 → (-Model Vega500, -Profile Default)."""
    model, _, variant = profile.partition("-")
    return dict(model=model, profileArg=variant or "Default")


def plan(out, manifest, registry, sched):
    cases = {c["id"]: c for c in registry["cases"] if not c.get('targetSha') or c['targetSha'] == manifest['head']}
    by_wi = {}
    for case in cases.values():
        if manifest.get('coverageSelectionExplicit') and case['id'] not in manifest.get('reviewCaseIds', []):
            continue
        for wi in case.get("workItems", []):
            by_wi.setdefault(wi, []).append(case["id"])
    partial = {cid for cid, c in cases.items() if "partial-acceptance" in c.get("tags", [])}

    clusters = {}

    def add(cid, kind, label, sha, case_ids):
        entry = clusters.setdefault(cid, dict(id=cid, kind=kind, label=label, commits=[], cases=[]))
        if sha not in entry["commits"]:
            entry["commits"].append(sha)
        entry["cases"] = sorted(set(entry["cases"]) | {c for c in case_ids if c in cases})

    for c in manifest["commits"]:
        if c.get("noProductChange"):
            continue
        wis = c.get("workItems") or work_items(c["title"] + "\n" + c.get("message", ""))
        for wi in wis:
            add(f"wi:{wi}", "workItem", f"工作项 #{wi}", c["sha"], by_wi.get(wi, []))
        if not wis and manifest.get('reviewCaseIds'):
            add('review:' + c['sha'][:8], 'workItem', '经 diff 需求审核的提交用例', c['sha'], manifest['reviewCaseIds'])
        for tag in c.get("groups", []):
            add(f"tag:{tag}", "pathTag", f"路径候选 {tag}", c["sha"], sched["tagCases"].get(tag, []))
        if not wis:
            for module in c.get("modules") or sorted({module_of(p) for p in c.get("unmappedFiles", [])}):
                add(f"module:{module}", "unmapped", f"未映射模块 {module}", c["sha"], [])

    product_commits = [c for c in manifest["commits"] if not c.get("noProductChange")]
    if product_commits:
        for cid in sched["baselineCases"]:
            entry = clusters.setdefault("baseline", dict(id="baseline", kind="baseline", label="每日回归底线（与具体提交无关）", commits=[], cases=[]))
            entry["cases"] = sorted(set(entry["cases"]) | ({cid} & cases.keys()))

    # 每个用例只跑一次：被多少簇/提交引用都合并到同一次 Runner 调用。
    case_refs = {}
    for cl in clusters.values():
        for cid in cl["cases"]:
            ref = case_refs.setdefault(cid, dict(clusters=set(), commits=set()))
            ref["clusters"].add(cl["id"])
            ref["commits"].update(cl["commits"])

    def priority(cid):
        kinds = {clusters[k]["kind"] for k in case_refs[cid]["clusters"]}
        rank = 0 if "workItem" in kinds else 1 if "pathTag" in kinds else 2
        return (rank, -len(case_refs[cid]["commits"]), cid)

    headless, ui_needs, case_phases = [], {}, {}
    for cid in sorted(case_refs, key=priority):
        layers = cases[cid]["layers"]
        quiet = [n for n, l in layers.items() if not is_ui_layer(n, l)]
        loud = [n for n, l in layers.items() if is_ui_layer(n, l)]
        refs = case_refs[cid]
        if quiet:
            headless.append(dict(order=len(headless) + 1, caseId=cid, layers=quiet,
                                 models=[sched["headlessLayerModels"].get(n, n) for n in quiet],
                                 clusters=sorted(refs["clusters"]), commits=[s[:8] for s in sorted(refs["commits"])],
                                 partial=cid in partial, timeoutSeconds=sum(layers[n].get("timeoutSeconds", 600) for n in quiet) + 180))
            case_phases.setdefault(cid, []).append("headless")
        if loud:
            ui_needs[cid] = loud

    order = sched["profileOrder"]
    assigned = {cid: sched["caseProfiles"].get(cid) for cid in ui_needs}
    declared = sorted({p for ps in assigned.values() if ps for p in ps}, key=lambda p: order.index(p) if p in order else len(order))
    host = declared[0] if declared else sched["defaultUiProfile"]
    slots = {}
    for cid, loud in ui_needs.items():
        profiles, assumed = (assigned[cid], False) if assigned[cid] else ([host], True)
        for profile in profiles:
            slot = slots.setdefault(profile, dict(id="ui-" + profile, profile=profile, **profile_args(profile), cases=[], commits=set()))
            slot["cases"].append(dict(caseId=cid, layers=loud, profileAssumed=assumed, partial=cid in partial,
                                      timeoutSeconds=sum(cases[cid]["layers"][n].get("timeoutSeconds", 900) for n in loud) + 180))
            slot["commits"].update(case_refs[cid]["commits"])
            case_phases.setdefault(cid, []).append(slot["id"])
    ui_slots = []
    for profile in sorted(slots, key=lambda p: order.index(p) if p in order else len(order)):
        slot = slots[profile]
        slot.update(order=len(ui_slots) + 1, commits=[s[:8] for s in sorted(slot["commits"])], configurationSwitches=1)
        ui_slots.append(slot)

    hardware = []
    for tag, note in sched["hardwareTags"].items():
        shas = clusters.get(f"tag:{tag}", {}).get("commits", [])
        if shas:
            hardware.append(dict(tag=tag, note=note, commits=[s[:8] for s in shas]))

    cluster_list = []
    for cl in sorted(clusters.values(), key=lambda x: (["workItem", "pathTag", "unmapped", "baseline"].index(x["kind"]), x["id"])):
        cluster_list.append(dict(cl, commits=[s[:8] for s in cl["commits"]], hasCase=bool(cl["cases"]),
                                 partial=bool(cl["cases"]) and all(c in partial for c in cl["cases"])))

    data = dict(schemaVersion=2, targetHead=manifest["head"], generatedAt=now_cn().isoformat(),
                configurationPolicy="backup/hash/restore; change model only while stopped; one configuration switch per profile; never reuse one model verdict for another",
                clusters=cluster_list, headless=headless, uiSlots=ui_slots, hardware=hardware, casePhases=case_phases,
                needsCase=[cl for cl in cluster_list if cl["kind"] == "workItem" and not cl["hasCase"]],
                unmapped=[cl for cl in cluster_list if cl["kind"] == "unmapped"],
                automationScope="--execute runs headless layers; each uiSlot runs via --run-slot after the agent prepares and authenticates that profile; hardware never automated")
    save(out / "plan.json", data)
    return data


# ────────────────────────────── 执行 ──────────────────────────────

def runner_binary():
    runner = ROOT / "runner" / "Vega.AutoTest.Runner" / "bin" / "Debug" / "net8.0-windows" / "Vega.AutoTest.Runner.dll"
    if runner.exists():
        return runner
    # Locate by file name, to support the Runner project's target framework suffix.
    candidates = list((ROOT / "runner" / "Vega.AutoTest.Runner" / "bin" / "Debug").glob("*/Vega.AutoTest.Runner.dll"))
    if len(candidates) != 1:
        raise RuntimeError("Runner binary is missing/ambiguous; build the Runner first")
    return candidates[0]


def runner_env(repo):
    env = os.environ.copy()
    env.update(VEGA_SW_ROOT=str(repo), DOTNET_ROOT=str(DOTNET.parent),
               DOTNET_CLI_UI_LANGUAGE='en',
               VEGA_AUTOTEST_ARTIFACTS_ROOT=r'C:\AutoTest\test-artifacts',
               NUGET_PACKAGES=r"C:\Users\Administrator\.nuget\packages",
               GIT_CONFIG_COUNT="1", GIT_CONFIG_KEY_0="safe.directory", GIT_CONFIG_VALUE_0=Path(repo).as_posix())
    env["PATH"] = str(DOTNET.parent) + os.pathsep + env["PATH"]
    return env


def build_runner(out):
    """The controller must execute the Runner we just tested, not a stale DLL."""
    project = ROOT / 'runner' / 'Vega.AutoTest.Runner' / 'Vega.AutoTest.Runner.csproj'
    env = os.environ.copy()
    env.update(DOTNET_ROOT=str(DOTNET.parent), DOTNET_CLI_UI_LANGUAGE='en')
    env['PATH'] = str(DOTNET.parent) + os.pathsep + env['PATH']
    log_path = out / 'runner-build.log'
    with log_path.open('w', encoding='utf-8') as log:
        result = run_bounded([str(DOTNET), 'build', str(project), '-p:UseSharedCompilation=false'],
            env=env, cwd=ROOT, stdout=log, stderr=subprocess.STDOUT, timeout=180)
    if result.returncode:
        raise RuntimeError('Runner build failed; see ' + str(log_path))
    binary = runner_binary()
    save(out / 'runner-reference.json', dict(binary=str(binary), sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
        builtAt=now_cn().isoformat(), sources={str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in project.parent.glob('*') if p.suffix in ('.cs', '.csproj')}))


def prepare_target(out, manifest):
    """Fresh worktree at the manifest HEAD + Simulator build + DLL fingerprint check."""
    attempt = now_cn().strftime("%Y%m%dT%H%M%S") + "_" + uuid.uuid4().hex[:6]
    evidence = out / "attempts" / attempt
    evidence.mkdir(parents=True)
    save(out / "execution.json", dict(state="PREPARING", attempt=attempt, evidence=str(evidence)))
    repo = WORKSPACES / (manifest["batchId"] + "_" + attempt)
    # Unattended recovery: owned Simulator processes may be stopped; never reuse
    # an output directory with a live session or reset the user's checkout.
    with (evidence / "cleanup.log").open("w", encoding="utf-8") as log:
        cleanup = run_bounded([SHELL, "-NoProfile", "-File", str(ROOT / "daily" / "Stop-ManagedVega.ps1"),
                               "-AuditPath", str(evidence / "cleanup.json")], stdout=log, stderr=subprocess.STDOUT, timeout=45)
    save(evidence / "cleanup-status.json", dict(exitCode=cleanup.returncode,
         fallback="Fresh worktree isolates build even if an unrelated session cannot be stopped"))
    git("worktree", "add", "--detach", str(repo), manifest["head"], repo=Path(manifest["source"]))
    current_head = git("rev-parse", "HEAD", repo=repo)
    if current_head != manifest["head"] or git("status", "--porcelain", repo=repo):
        raise RuntimeError("Fresh worktree fingerprint mismatch")
    save(out / "execution-target.json", dict(repo=str(repo), head=current_head, dirty=False, evidence=str(evidence), attempt=attempt))
    save(out / "execution.json", dict(state="BUILDING", attempt=attempt, evidence=str(evidence)))
    with (evidence / "build-driver.log").open("w", encoding="utf-8") as log:
        build = run_bounded([SHELL, "-NoProfile", "-File", str(ROOT / "daily" / "Build-DailyVega.ps1"),
                             "-RepoRoot", str(repo), "-EvidenceRoot", str(evidence / "build")],
                            stdout=log, stderr=subprocess.STDOUT, timeout=1800)
    if build.returncode:
        log_path = evidence / 'build' / 'build.log'
        errors = []
        if log_path.exists():
            errors = list(dict.fromkeys(line.split(': error ', 1)[1].split(' [', 1)[0]
                for line in log_path.read_text(encoding='utf-8', errors='replace').splitlines() if ': error ' in line))[:5]
        save(evidence / 'build-failure.json', dict(exitCode=build.returncode, errors=errors, log=str(log_path)))
        raise RuntimeError(f"Target build failed: {'; '.join(errors) or 'inspect restore/build logs'}; see {evidence / 'build'}")
    provenance = load(evidence / "build" / "build-provenance.json")
    dll = repo / "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.dll"
    if provenance["commit"] != manifest["head"] or hashlib.sha256(dll.read_bytes()).hexdigest().upper() != provenance["vegaDllSha256"].upper():
        raise RuntimeError("Build provenance mismatch")
    save(out / "build-reference.json", dict(path=str(evidence / "build" / "build-provenance.json"), **provenance))
    return repo, evidence


def run_case(out, repo, log_dir, case_id, layers, phase, timeout, outcomes):
    """One Runner verify call. The verdict is copied from result.json, never derived here."""
    log_dir.mkdir(parents=True, exist_ok=True)
    # Generated IDs can be long, while nested campaign/SHA/slot directories
    # already approach MAX_PATH. Full case/phase/timestamps remain in outcomes;
    # the filesystem basename must not repeat them.
    log_path = log_dir / ('r-' + uuid.uuid4().hex[:12] + '.log')
    entry = dict(caseId=case_id, phase=phase, layers=layers, startedAt=now_cn().isoformat(), log=str(log_path))
    try:
        env = runner_env(repo)
        if phase != 'headless':
            env['VEGA_DAILY_UI_EVIDENCE'] = str(out / 'ui' / phase / 'evidence.json')
        with log_path.open("w", encoding="utf-8") as log:
            p = run_bounded([str(DOTNET), str(runner_binary()), "verify", "--case", case_id, "--layers", ",".join(layers),
                             "--repo-root", str(repo), "--runs-dir", str(out / "runner")],
                            env=env, stdout=log, stderr=subprocess.STDOUT, timeout=timeout, cwd=ROOT)
        entry["exitCode"] = p.returncode
        lines = [l.strip() for l in log_path.read_text(encoding="utf-8", errors="replace").splitlines() if l.strip()]
        result_path = Path(lines[-1]) if lines else None
        if not result_path or result_path.name != "result.json" or not result_path.is_file():
            raise RuntimeError("Runner 末行不是 result.json 路径")
        result = load(result_path)
        if result.get('caseId') != case_id or result.get('target', {}).get('gitCommit') != git('rev-parse', 'HEAD', repo=repo):
            raise RuntimeError('Runner result case/target does not match this invocation')
        if not result_path.resolve().is_relative_to((out / 'runner').resolve()):
            raise RuntimeError('Runner result is outside this commit evidence directory')
        entry.update(runId=result["runId"], verdict=result["verdict"], verdictCode=result["verdictCode"],
                     verdictReason=result.get("verdictReason"), summary=result.get("summary"),
                     resultPath=str(result_path), reportPath=str(result_path.with_name("report.md")),
                     sha256=hashlib.sha256(result_path.read_bytes()).hexdigest())
    except Exception as exc:  # noqa: BLE001 — 记成工具异常，继续下一个用例
        entry.update(verdict="ERROR", verdictCode="DAILY_RUNNER_INVOCATION", verdictReason=str(exc),
                     exceptionType=type(exc).__name__, runId=None)
    entry["finishedAt"] = now_cn().isoformat()
    outcomes["runs"].append(entry)
    save(out / "outcomes.json", outcomes)
    print(f"[{phase}] {case_id}: {entry['verdict']} / {entry['verdictCode']} runId={entry.get('runId')}", flush=True)
    return entry


def execute(out, manifest, plan_data, outcomes, budget_minutes):
    if manifest["historyRewritten"]:
        save(out / "execution.json", dict(state="BLOCKED", reason="remote history rewritten; retained for review"))
        return
    prepared = plan_data.get('preparedTarget')
    if prepared:
        repo, evidence = Path(prepared['repo']), Path(prepared['evidence'])
        # Reuse only THIS commit's own preparation build. Discovery and execution
        # consume identical binaries; no DLL/runId sharing between SHAs.
        if load(out / 'execution-target.json') != prepared or git('rev-parse', 'HEAD', repo=repo) != manifest['head']:
            raise RuntimeError('Prepared worktree target changed')
        provenance = load(out / 'build-reference.json', {})
        binary_root = repo / 'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
        for name, key in [('Vega.dll', 'vegaDllSha256'), ('RecipeBatchEditor/RecipeBatchEditor.dll', 'batchEditorDllSha256'),
                          ('ComponentConfigEditor/ComponentConfigEditor.dll', 'componentEditorDllSha256')]:
            if provenance.get(key) and hashlib.sha256((binary_root / name).read_bytes()).hexdigest().upper() != provenance[key].upper():
                raise RuntimeError('Prepared binary changed: ' + name)
        if provenance.get('commit') != manifest['head'] or git('status', '--porcelain', '--untracked-files=no', repo=repo):
            raise RuntimeError('Prepared source/provenance changed')
    else:
        repo, evidence = prepare_target(out, manifest)
    save(out / "execution.json", dict(state="HEADLESS", evidence=str(evidence)))
    deadline = time.monotonic() + budget_minutes * 60
    for item in plan_data["headless"]:
        # Restarting an interrupted controller must consume this campaign's
        # verified raw results rather than repeat completed tests. An invocation
        # error or budget placeholder has no raw result and may be retried.
        if completed_headless(out, manifest['head'], item, outcomes):
            continue
        if time.monotonic() + item["timeoutSeconds"] > deadline:
            outcomes["runs"].append(dict(caseId=item["caseId"], phase="headless", layers=item["layers"], runId=None,
                                         verdict=None, verdictCode="NOT_RUN_BUDGET",
                                         verdictReason=f"超出夜间无界面预算 {budget_minutes} 分钟，未执行",
                                         finishedAt=now_cn().isoformat()))
            save(out / "outcomes.json", outcomes)
            continue
        run_case(out, repo, evidence, item["caseId"], item["layers"], "headless", item["timeoutSeconds"], outcomes)
    save(out / "execution.json", dict(state="HEADLESS_DONE", evidence=str(evidence),
                                      pendingUiSlots=[s["id"] for s in plan_data["uiSlots"]]))


def completed_headless(out, sha, item, outcomes):
    for entry in reversed(outcomes['runs']):
        if entry.get('caseId') != item['caseId'] or entry.get('phase') != 'headless':
            continue
        if not entry.get('resultPath'):
            return False
        path = Path(entry['resultPath']).resolve()
        if not path.is_relative_to((out / 'runner').resolve()) or not path.is_file():
            return False
        raw = load(path)
        return (raw.get('target', {}).get('gitCommit') == sha and raw.get('caseId') == item['caseId']
                and raw.get('runId') == entry.get('runId')
                and raw.get('verdict') in ('PASS', 'FAIL', 'ERROR', 'BLOCKED')
                and (not entry.get('sha256') or coverage_contract.digest(path) == entry['sha256']))
    return False


def run_slot(out, manifest, plan_data, slot_id, session, outcomes):
    slot = next((s for s in plan_data["uiSlots"] if s["id"] == slot_id), None)
    if slot is None:
        raise RuntimeError(f"plan.json 没有界面会话 {slot_id}；可选: {[s['id'] for s in plan_data['uiSlots']]}")
    target = load(out / "execution-target.json")
    if not target or not (out / "build-reference.json").exists():
        raise RuntimeError("本批次尚未完成目标构建；先运行 --resume <batch> --execute")
    repo = Path(target["repo"])
    if git("rev-parse", "HEAD", repo=repo) != manifest["head"]:
        raise RuntimeError("worktree HEAD 与 manifest 不一致")
    if not session:
        raise ValueError('UI execution requires --session from Start-ModelValidation.ps1 Prepare')
    session = Path(session).resolve()
    if not session.is_relative_to((ROOT / 'daily' / 'sessions').resolve()):
        raise ValueError('UI session must be inside daily/sessions')
    state = load(session / 'session.json', {})
    binary_root = repo / 'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
    if (state.get('state') != 'PREPARED' or Path(state.get('repo', '')).resolve() != repo.resolve()
            or Path(state.get('bin', '')).resolve() != binary_root.resolve()
            or state.get('model') != slot['model'] or state.get('profile') != slot['profile']):
        raise ValueError('UI session target/model/profile does not match this planned slot')
    for name in ('ComponentConfig', 'HardwareProfiles'):
        config = binary_root / 'Resources' / (name + '.json')
        expected = state.get('preparedHashes', {}).get(name)
        if not expected or not config.is_file() or coverage_contract.digest(config).upper() != expected.upper():
            raise ValueError('Prepared UI configuration missing/changed: ' + name)
    log_dir = out / "slots" / f"{slot_id}_{now_cn():%Y%m%dT%H%M%S}"
    log_dir.mkdir(parents=True, exist_ok=True)
    try:
        for item in slot["cases"]:
            run_case(out, repo, log_dir, item["caseId"], item["layers"], slot_id, item["timeoutSeconds"], outcomes)
    finally:
        try:
            subprocess.run([SHELL, "-NoProfile", "-File", str(ROOT / "daily" / "Stop-ManagedVega.ps1"),
                            "-AuditPath", str(log_dir / "cleanup.json")], timeout=60)
        except Exception as exc:  # noqa: BLE001 — 清理失败不能挡住配置恢复
            save(log_dir / "cleanup-error.json", dict(reason=str(exc)))
        if session:
            restore = subprocess.run([SHELL, "-NoProfile", "-File", str(ROOT / "daily" / "Start-ModelValidation.ps1"),
                                      "-Model", slot["model"], "-Profile", slot["profileArg"], "-RepoRoot", str(repo),
                                      "-SessionPath", str(session), "-Action", "Restore"],
                                     capture_output=True, encoding="utf-8", errors="replace", timeout=120)
            save(log_dir / "restore.json", dict(exitCode=restore.returncode, output=(restore.stdout + restore.stderr)[-2000:]))
            if restore.returncode:
                raise RuntimeError(f"配置恢复失败，见 {log_dir / 'restore.json'}")


# ────────────────────────────── 逐提交判定 ──────────────────────────────

def latest_runs(outcomes):
    latest = {}
    for entry in outcomes["runs"]:
        latest[(entry["caseId"], entry["phase"])] = entry
    return latest


def case_state(cid, plan_data, latest):
    entries = [latest.get((cid, ph)) for ph in plan_data["casePhases"].get(cid, [])]
    verdicts = [e.get("verdict") if e else None for e in entries]
    worst = min(verdicts, key=lambda v: VERDICT_RANK.get(v, 3)) if verdicts else None
    return worst, [e for e in entries if e]


def evaluate(manifest, plan_data, outcomes, registry):
    latest = latest_runs(outcomes)
    partial = {c["id"] for c in registry["cases"] if "partial-acceptance" in c.get("tags", [])}
    clusters = {c["id"]: c for c in plan_data["clusters"]}
    baseline_cases = sorted({cid for cl in clusters.values() if cl['kind'] == 'baseline' for cid in cl['cases']})
    phases = {}
    for s in plan_data["uiSlots"]:
        for item in s["cases"]:
            phases.setdefault(item["caseId"], []).append(s["id"])
    rows = []
    for c in manifest["commits"]:
        sha8 = c["sha"][:8]
        mine = [cl for cl in clusters.values() if sha8 in cl["commits"]]
        wi_clusters = [cl for cl in mine if cl["kind"] == "workItem"]
        tag_cases = sorted({cid for cl in mine if cl["kind"] == "pathTag" for cid in cl["cases"]})
        wi_cases = sorted({cid for cl in wi_clusters for cid in cl["cases"]})
        own_cases = sorted(set(wi_cases + tag_cases))
        cells = {cid: case_state(cid, plan_data, latest) for cid in sorted(set(own_cases + baseline_cases))}
        def failure_details(verdict, case_ids):
            return '; '.join(dict.fromkeys(
                f"{cid} [{e.get('phase', '?')}] {e.get('verdictCode', verdict)}: {e.get('verdictReason', '见原始结果')}"
                for cid in case_ids for e in cells[cid][1] if e.get('verdict') == verdict))
        evidence = [dict(caseId=cid, verdict=cells[cid][0], runIds=[e.get("runId") for e in cells[cid][1]],
                         codes=[e.get("verdictCode") for e in cells[cid][1]], partial=cid in partial)
                    for cid in sorted(cells)]
        baseline_evidence = []
        for cid in baseline_cases:
            verdict, entries = cells[cid]
            baseline_evidence.append(dict(caseId=cid, verdict=verdict,
                verdictCode=entries[-1].get("verdictCode") if entries else None,
                reason=entries[-1].get("verdictReason") if entries else None,
                runIds=[e.get("runId") for e in entries]))
        present = [item["verdict"] for item in baseline_evidence]
        baseline_verdict = None if not present or any(item is None for item in present) else min(
            present, key=lambda verdict: VERDICT_RANK.get(verdict, 3))
        if c.get("noProductChange"):
            status, reason = "NOT_APPLICABLE", "仅文档改动"
        elif any(cells[cid][0] == "FAIL" for cid in own_cases):
            status, reason = "FAIL", '用例失败: ' + failure_details('FAIL', own_cases)
        elif any(cells[cid][0] == "ERROR" for cid in own_cases):
            status, reason = "ERROR", '验证工具异常: ' + failure_details('ERROR', own_cases)
        elif any(cells[cid][0] == "BLOCKED" for cid in own_cases):
            status, reason = "BLOCKED", '前置/环境不满足: ' + failure_details('BLOCKED', own_cases)
        elif any(r.get('gaps') for r in plan_data.get('coverageRequirements', [])):
            status, reason = 'NEEDS_CASE', '需求覆盖未齐：' + '; '.join(
                gap for r in plan_data['coverageRequirements'] for gap in r.get('gaps', []))
        elif not wi_clusters:
            status = "PARTIAL" if tag_cases else "NEEDS_CASE"
            reason = ("提交说明无工作项号，只有路径候选探针（部分验收）" if tag_cases
                      else "提交说明无工作项号，且无可映射用例；需人工/Agent 分析 diff 并补用例")
        elif any(not cl["cases"] for cl in wi_clusters):
            missing = [cl["label"] for cl in wi_clusters if not cl["cases"]]
            status, reason = "NEEDS_CASE", f"{', '.join(missing)} 未在 registry.json 登记用例"
        elif any(cells[cid][0] is None for cid in own_cases):
            waiting = sorted({ph for cid in own_cases if cells[cid][0] is None for ph in phases.get(cid, ["headless"])})
            status, reason = "PENDING", f"等待执行: {', '.join(waiting)}"
        elif any(cid in partial for cid in wi_cases):
            status, reason = "PARTIAL", "登记用例全部 PASS，但标记为 partial-acceptance"
        else:
            status, reason = "ACCEPTED", "工作项登记用例在目标 HEAD 的独立构建上全部 PASS"
        if status in ("ACCEPTED", "PARTIAL") and any(cells[cid][0] in ("ERROR", "BLOCKED") for cid in tag_cases):
            reason += "；关联路径探针未得出 PASS"
        if status in ("ACCEPTED", "PARTIAL") and baseline_cases and any(item["verdict"] is None for item in baseline_evidence):
            waiting = [item["caseId"] for item in baseline_evidence if item["verdict"] is None]
            status, reason = "PENDING", "等待公共基线执行: " + ", ".join(waiting)
        elif baseline_verdict in ("FAIL", "ERROR", "BLOCKED"):
            detail = failure_details(baseline_verdict, baseline_cases)
            reason += f"；公共基线未通过（{baseline_verdict}）：{detail}。该结果在批次报告只计一次"
        rows.append(dict(sha=c["sha"], sha8=sha8, title=c["title"], origin=c.get("origin", "carryover" if c.get("carryover") else "today"),
                         workItems=c.get("workItems", []), clusters=[cl["id"] for cl in mine], status=status,
                         reason=reason, evidence=evidence, baselineVerdict=baseline_verdict,
                         baselineEvidence=baseline_evidence, unmappedFiles=c.get("unmappedFiles", [])))
    return rows


def baseline_open(row):
    """A shared baseline that is missing or not PASS still blocks calling the commit accepted."""
    return bool(row.get("baselineEvidence")) and row.get("baselineVerdict") != "PASS"


def acceptance_state(rows):
    if not rows:
        return "NO_COMMITS"
    if any(r["status"] == "FAIL" or r.get("baselineVerdict") == "FAIL" for r in rows):
        return "HAS_FAIL"
    if any(r.get("baselineVerdict") in ("ERROR", "BLOCKED") for r in rows):
        return "INCOMPLETE"
    if all(r["status"] in CLOSED and not baseline_open(r) for r in rows):
        return "COMPLETE"
    return "INCOMPLETE"


def baseline_lines(rows):
    """One row per distinct failing baseline, listing every affected commit."""
    groups = {}
    for row in rows:
        for item in row.get("baselineEvidence") or []:
            if item.get("verdict") in (None, "PASS"):
                continue
            key = (item.get("caseId"), item.get("verdict"), item.get("verdictCode"))
            bucket = groups.setdefault(key, [])
            if row.get("sha8") not in bucket:
                bucket.append(row.get("sha8"))
    if not groups:
        return []
    lines = ["", "## 公共基线（每个失败只计一次）", "",
             "下列结论来自每日回归底线。同一失败列出受影响提交，不把每个提交再记成一次独立失败。", "",
             "| 用例 | 结论 | 受影响提交 |", "| --- | --- | --- |"]
    for (case_id, verdict, code), shas in groups.items():
        lines.append(f"| {case_id} | {verdict} / {code or '—'} | {', '.join(shas)} |")
    lines.append("")
    return lines


def update_pending(manifest, rows, out):
    if manifest.get("mode") != "daily" or not STATE.exists():
        return
    state = load(STATE)
    closed = {r["sha"]: r for r in rows if r["status"] in CLOSED and not baseline_open(r)}
    if not closed:
        return
    state["pendingCommits"] = [s for s in state.get("pendingCommits", []) if s not in closed]
    history = state.get("closedCommits", [])
    history += [dict(sha=s, status=r["status"], batch=out.name, at=now_cn().isoformat(),
                     runIds=[rid for e in r["evidence"] for rid in e["runIds"] if rid]) for s, r in closed.items()]
    state["closedCommits"] = history[-500:]
    save(STATE, state)


def acknowledge(sha, reason):
    if not reason:
        raise ValueError("--ack 需要 --ack-reason 说明人工关闭理由")
    state = load(STATE, {})
    pending = state.get("pendingCommits", [])
    matched = [s for s in pending if s.startswith(sha)]
    if len(matched) != 1:
        raise ValueError(f"pendingCommits 中匹配 {sha} 的提交数为 {len(matched)}，需唯一")
    state["pendingCommits"] = [s for s in pending if s != matched[0]]
    state.setdefault("closedCommits", []).append(dict(sha=matched[0], status="ACKNOWLEDGED", reason=reason, at=now_cn().isoformat()))
    save(STATE, state)
    print(json.dumps(dict(acknowledged=matched[0], reason=reason), ensure_ascii=False))


# ────────────────────────────── 报告 ──────────────────────────────

STATUS_TEXT = {"ACCEPTED": "已验收", "PARTIAL": "部分验收", "PENDING": "待执行", "NEEDS_CASE": "缺用例",
               "FAIL": "失败", "ERROR": "工具异常", "BLOCKED": "无法判定", "NOT_APPLICABLE": "无需验证"}


def md(text):
    return str(text).replace("|", "/").replace("\n", " ")


def report(out, manifest, plan_data, outcomes, rows):
    latest = latest_runs(outcomes)
    state = acceptance_state(rows)
    counts = {}
    for r in rows:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    build = load(out / "build-reference.json")
    data = dict(batchId=manifest["batchId"], acceptanceState=state, commitCount=len(rows), statusCounts=counts,
                target=manifest["head"], remoteFresh=manifest["remoteFresh"], built=bool(build),
                runs=[dict(caseId=e["caseId"], phase=e["phase"], runId=e.get("runId"), verdict=e.get("verdict"),
                           verdictCode=e.get("verdictCode"), verdictReason=e.get('verdictReason'),
                           log=e.get('log'), resultPath=e.get('resultPath')) for e in latest.values()],
                pendingUiSlots=[s["id"] for s in plan_data["uiSlots"]
                                if any((i["caseId"], s["id"]) not in latest for i in s["cases"])],
                unverifiedUiSlots=[s["id"] for s in plan_data["uiSlots"]
                                   if any(latest.get((i["caseId"], s["id"]), {}).get('verdict') in (None, 'BLOCKED', 'ERROR')
                                          for i in s['cases'])],
                commits=[dict(sha=r["sha"], status=r["status"], reason=r["reason"]) for r in rows],
                note="acceptanceState 表示验收完整性，不是第二套 verdict；正式 verdict 只来自各 Runner result.json。")
    save(out / "summary.json", data)

    L = ["# Vega 每日提交批次报告", "",
         f"批次 `{manifest['batchId']}`；目标 HEAD `{manifest['head']}`；收集窗口 {manifest['windowStart'][:16]} ~ {manifest['windowEnd'][:16]}；远端刷新：{manifest['remoteFresh']}。",
         "", f"总体验收：**{state}**。逐提交：" + ("、".join(f"{STATUS_TEXT.get(k, k)} {v}" for k, v in counts.items()) or "无提交") + "。", ""]
    if manifest.get("historyRewritten"):
        L += ["> 远端历史被改写，本批未执行，需人工审查。", ""]
    if build:
        L += [f"构建：独立 worktree，提交 `{build.get('commit')}`，Vega.dll SHA256 `{build.get('vegaDllSha256')}`。"
              "Runner 未自行重编，`builtFromCurrentSource=false` 保持原样；本节构建证据说明这些 DLL 来自目标 HEAD。", ""]
    else:
        L += ["构建：本批尚无成功的目标构建，所有用例未执行。", ""]

    L += ["## 逐提交结论", "", "| 提交 | 来源 | 标题 | 工作项 | 簇 | 用例 → 结论（runId） | 状态 |", "| --- | --- | --- | --- | --- | --- | --- |"]
    origin_text = {"today": "当天", "carryover": "补收", "pending": "前批待验"}
    for r in rows:
        ev = "<br>".join(f"{e['caseId']}{'（部分）' if e['partial'] else ''} → {e['verdict'] or '未执行'}"
                         f"（{', '.join(x for x in e['runIds'] if x) or '无'}）" for e in r["evidence"]) or "—"
        L.append(f"| `{r['sha8']}` | {origin_text.get(r['origin'], r['origin'])} | {md(r['title'])} | "
                 f"{', '.join('#' + str(w) for w in r['workItems']) or '—'} | {', '.join(r['clusters']) or '—'} | {ev} | "
                 f"**{STATUS_TEXT.get(r['status'], r['status'])}**：{md(r['reason'])} |")

    L += ["", "## 合并编排", "",
          "同一目标 HEAD 只构建一次；同一用例被多个提交引用时只跑一次。以下顺序即执行顺序。", "",
          "### 无界面（--execute 自动执行）", "", "| 顺序 | 用例 | 层（机型） | 覆盖提交 | 来源簇 | 结论 |", "| --- | --- | --- | --- | --- | --- |"]
    for h in plan_data["headless"]:
        e = latest.get((h["caseId"], "headless"))
        verdict = f"{e.get('verdict') or '未执行'} / {e.get('verdictCode')} `{e.get('runId')}`" if e else "未执行"
        L.append(f"| {h['order']} | {h['caseId']}{'（部分）' if h['partial'] else ''} | "
                 f"{', '.join(f'{n}（{m}）' for n, m in zip(h['layers'], h['models']))} | {', '.join(h['commits']) or '底线'} | "
                 f"{', '.join(h['clusters'])} | {verdict} |")
    if not plan_data["headless"]:
        L.append("| — | 无 | — | — | — | — |")

    L += ["", "### 界面会话（按机型配置，一次配置切换覆盖同组全部用例）", ""]
    if not plan_data["uiSlots"]:
        L += ["当前计划未关联界面用例；不能据此推断无需 UI。必需 UI 缺口见需求覆盖审核，未覆盖不得完整验收。"]
    target = load(out / "execution-target.json") or {}
    for s in plan_data["uiSlots"]:
        L += [f"**{s['order']}. `{s['id']}`**（{s['profile']}；覆盖提交 {', '.join(s['commits'])}）", ""]
        for i in s["cases"]:
            e = latest.get((i["caseId"], s["id"]))
            note = "；未声明机型，挂在本会话执行，结论只代表该机型" if i["profileAssumed"] else ""
            L.append(f"- {i['caseId']} [{', '.join(i['layers'])}] → "
                     f"{(e.get('verdict') or '未执行') + ' / ' + str(e.get('verdictCode')) + ' `' + str(e.get('runId')) + '`' if e else '未执行'}{note}")
        L += ["", "```powershell",
              f"& .\\daily\\Start-ModelValidation.ps1 -Model {s['model']} -Profile {s['profileArg']} -RepoRoot {target.get('repo', '<worktree>')} -Action Prepare",
              "# 官方认证器按 AGENTS.md 配置并确认能登录后：",
              f"& $python .\\daily\\daily.py --resume {out} --run-slot {s['id']} --session <Prepare 输出的 SessionPath>",
              "```", ""]
    L += ["界面用例每条仍会自行拉起并关闭 Vega（L2TestBase 约定）；会话合并省的是机型配置切换与授权，不是进程启动次数。", ""]

    if plan_data["needsCase"] or plan_data["unmapped"]:
        L += ["## 需补用例 / 需分析", ""]
        for cl in plan_data["needsCase"]:
            L.append(f"- {cl['label']}：提交 {', '.join(cl['commits'])}，registry.json 无对应用例。")
        for cl in plan_data["unmapped"]:
            L.append(f"- {cl['label']}：提交 {', '.join(cl['commits'])}，路径规则未覆盖，需读 diff 判断影响面。")
        L.append("")
    if plan_data.get('coverageRequirements'):
        L += ['## 冻结前需求覆盖审核', '', '| 场景 | 必需 UI | 用例 | 未覆盖原因 |', '| --- | --- | --- | --- |']
        for requirement in plan_data['coverageRequirements']:
            L.append(f"| {md(requirement['id'])} | {requirement['uiRequired']} | {md(', '.join(requirement.get('caseIds', [])))} | {md('; '.join(requirement.get('gaps', []))) or '无已声明缺口，仍须实跑'} |")
        L += ['', '审核/登记不是测试通过证明；正式结论仍须本提交 Runner 原始结果。', '']
    if plan_data["hardware"]:
        L += ["## 需真机验证", ""] + [f"- {h['tag']}（提交 {', '.join(h['commits'])}）：{h['note']}" for h in plan_data["hardware"]] + [""]
    L += baseline_lines(rows)
    failed = [r for r in rows if r["status"] == "FAIL"]
    if failed:
        L += ["## 失败隔离建议", "", "多提交合并验证无法直接定位引入者。按失败用例所在簇的提交，从早到晚逐个在独立 worktree 上复跑同一用例。", ""]
        for r in failed:
            L.append(f"- `{r['sha8']}`：{md(r['reason'])}")
        L.append("")
    if manifest.get("unreachablePending"):
        L += ["## 不可达的待验提交", "", "以下提交已不在当前 master 历史中，保留在待验队列，需人工确认：", ""]
        L += [f"- `{s}`" for s in manifest["unreachablePending"]] + [""]
    L += ["## 规则", "",
          "- 收集：北京时间当天 00:00 至执行时刻按 committer time 取 master 全部可达提交，补收上次收集后新出现的提交；历史未验收提交只留在原报告，不重新编入或重放。",
          "- 归簇：提交说明里的工作项号优先，同一工作项的提交合为一簇；无工作项号时用路径规则给出候选探针；未覆盖文件按模块成簇待分析。",
          "- 验收：只有工作项登记用例（非 partial-acceptance）在目标 HEAD 上全部 PASS 才记“已验收”并移出待验队列；路径探针只算部分验收。公共基线失败在批次报告只计一次，并挡住整批完整验收。",
          "- 判定只来自 Runner result.json；本报告不改写任何 verdict。", ""]
    (out / "report.md").write_text("\n".join(L), encoding="utf-8")
    return data


# ────────────────────────────── 入口 ──────────────────────────────

def resolve_strategy(requested, existing_manifest):
    # Existing manifests without a mode marker are legacy merged batches.
    stored = existing_manifest.get("strategy", "merged") if existing_manifest is not None else None
    if requested and stored and requested != stored:
        raise ValueError("Cannot switch strategy for an existing batch; create a new batch")
    return requested or stored or "individual"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--last", type=int, default=0, help="示例模式：最近 N 个提交，不推进每日游标")
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--execute", action="store_true", help="新 individual 批次仅收集候选队列；配合 --run-commit 才构建/执行该 SHA；merged 执行无界面")
    parser.add_argument('--check-complete', action='store_true', help='Exit 2 while any commit workflow is unfinished; never treat a background launch as completion')
    parser.add_argument('--prepare-all', action='store_true', help='Retrieve ALL commit requirements and generate isolated adapters for changed product tests before execution')
    parser.add_argument('--author-cases', help='Generate isolated executable adapters from changed product regression tests at this SHA')
    parser.add_argument('--check-cases', help='Build/discover reviewed case IDs at this exact SHA before freeze')
    parser.add_argument('--case-ids', help='Comma-separated reviewed case IDs for --check-cases')
    parser.add_argument('--repo-root', type=Path, help='Already built exact-SHA worktree for --check-cases; omitted creates a fresh target build')
    parser.add_argument('--drain', action='store_true', help='Execute the entire frozen queue deterministically; exit 2 only when actual UI work remains')
    parser.add_argument('--revalidate-batch', type=Path, help='Explicit targeted simulation of an existing manifest in a NEW child campaign; leaves original plans/results and daily cursor intact')
    parser.add_argument("--strategy", choices=("individual", "merged"), help="新批默认逐提交；merged 保留原总体编排，历史批次沿用原模式")
    parser.add_argument("--run-commit", help="逐提交队列中执行一项的独立构建及无界面测试（需 --resume --execute）")
    parser.add_argument("--finish-commit", help="记录一项流程已收尾（含失败/未验范围和恢复证据），再允许下一项")
    parser.add_argument("--completion-reason")
    parser.add_argument('--prepare-commit', help='读取工作项并准备候选覆盖；此时允许补写用例，尚未冻结')
    parser.add_argument('--freeze-commit', help='审核真实用例/必需 UI 后冻结一个提交计划（需 --review-file）')
    parser.add_argument('--review-file', type=Path, help='逐场景的需求覆盖审核 JSON，见 daily/README.md')
    parser.add_argument("--resume", type=Path, help="Execute an existing immutable manifest after preparing its exact target")
    parser.add_argument("--run-slot", help="执行 plan.json 中的一个界面会话（需 --resume，且该机型已准备并认证）")
    parser.add_argument("--session", type=Path, help="Start-ModelValidation 的 SessionPath；会话结束后自动 Restore")
    parser.add_argument("--plan-only", action="store_true", help="只重算计划与报告，不执行")
    parser.add_argument("--ack", help="人工关闭一个待验提交（SHA 前缀）")
    parser.add_argument("--ack-reason")
    args = parser.parse_args()
    if args.check_complete and not args.resume:
        raise ValueError('--check-complete requires --resume; it must never collect or advance the cursor')
    queue_action = any((args.prepare_all, args.author_cases, args.check_cases, args.drain))
    if args.check_complete and any((args.execute, args.last, args.offline, args.prepare_commit,
            args.freeze_commit, args.run_commit, args.finish_commit, args.run_slot, args.plan_only,
            args.ack, args.revalidate_batch, queue_action)):
        raise ValueError('--check-complete cannot be combined with preparation/execution actions')
    if args.revalidate_batch:
        if any((args.resume, args.execute, args.last, args.offline, args.prepare_commit,
                args.freeze_commit, args.run_commit, args.finish_commit, args.run_slot, args.plan_only,
                args.ack, queue_action, args.case_ids, args.repo_root, args.review_file)):
            raise ValueError('--revalidate-batch creates only a new candidate campaign')
        source = args.revalidate_batch.resolve()
        if not source.is_relative_to((ROOT / 'daily' / 'runs').resolve()):
            raise ValueError('Source batch must be inside daily/runs')
        original = load(source / 'manifest.json')
        out = source / ('revalidation-' + now_cn().strftime('%Y%m%dT%H%M%S') + '_' + uuid.uuid4().hex[:6])
        manifest = dict(original, batchId=out.name, mode='targeted-revalidation', strategy='individual',
                        revalidationOf=str(source), revalidationRequestedAt=now_cn().isoformat())
        save(out / 'manifest.json', manifest)
        for commit in original['commits']:
            shutil.copy2(source / (commit['sha'][:8] + '.diff'), out / (commit['sha'][:8] + '.diff'))
        registry, sched = load(REGISTRY), load(SCHEDULING)
        queue = individual.prepare(__import__(__name__), out, manifest, registry, sched)
        print(json.dumps(individual.aggregate(__import__(__name__), out, manifest, queue, registry), ensure_ascii=False))
        return
    actions = [args.run_commit, args.finish_commit, args.prepare_commit, args.freeze_commit, args.run_slot, args.plan_only,
               args.prepare_all, args.author_cases, args.check_cases, args.drain]
    if sum(bool(x) for x in actions) > 1:
        raise ValueError('Select only one preparation/execution/completion action per invocation')
    if (args.prepare_commit or args.freeze_commit) and not args.resume:
        raise ValueError('Preparation actions require --resume <individual root batch>')
    if bool(args.review_file) != bool(args.freeze_commit):
        raise ValueError('--review-file is required only with --freeze-commit')
    if (args.prepare_all or args.author_cases or args.check_cases or args.drain) and not args.resume:
        raise ValueError('Queue preparation/drain requires --resume <root>')
    if bool(args.case_ids) != bool(args.check_cases) or (args.repo_root and not args.check_cases):
        raise ValueError('--check-cases requires --case-ids; --repo-root is only for discovery')
    if args.ack:
        acknowledge(args.ack, args.ack_reason)
        return
    if args.run_slot and not args.resume:
        raise ValueError("--run-slot 需要 --resume <batch>")
    if (args.run_commit or args.finish_commit) and not args.resume:
        raise ValueError("--run-commit/--finish-commit require --resume <individual batch>")
    if args.run_commit and (not args.execute or args.finish_commit or args.run_slot or args.plan_only):
        raise ValueError("--run-commit requires --execute and cannot be combined with other actions")
    if args.resume:
        out = args.resume.resolve()
        if not out.is_relative_to((ROOT / "daily" / "runs").resolve()):
            raise ValueError("Resume path must be inside daily/runs")
        manifest = load(out / "manifest.json")
        strategy = resolve_strategy(args.strategy, manifest)
        historical_root = Path(manifest['parentBatch']) if manifest.get('parentBatch') else out
        if (historical_root / 'revalidation-link.json').exists() and (any(actions) or args.execute):
            raise ValueError('Historical frozen batch is linked to revalidation; operate on the separate campaign')
    else:
        strategy = resolve_strategy(args.strategy, None)
        out, manifest = collect(args.last, args.offline)
        manifest["strategy"] = strategy
        save(out / "manifest.json", manifest)
    if (queue_action or args.check_complete) and strategy != 'individual':
        raise ValueError('Queue preparation/drain/completion checks require an individual root batch')
    registry = load(REGISTRY)
    sched = load(SCHEDULING)
    if strategy == "individual":
        if (out / 'revalidation-link.json').exists() and any(actions):
            raise ValueError('Historical batch is linked to explicit revalidation; operate on the linked campaign, preserve the original frozen batch')
        if args.run_slot:
            raise ValueError("Resume the commit child directory to run a UI slot, not the queue root")
        plan_data = individual.prepare(__import__(__name__), out, manifest, registry, sched)
        if args.prepare_all:
            queue_driver.prepare_all(__import__(__name__), plan_data, registry, sched)
        if args.author_cases or args.check_cases:
            slot = individual.select(__import__(__name__), plan_data, args.author_cases or args.check_cases, require_previous=False)
            child = Path(slot['batch'])
            if (child / 'plan.json').exists():
                raise ValueError('Authoring/discovery must happen before plan freeze')
            cm = load(child / 'manifest.json')
            if args.author_cases:
                case_authoring.generate(__import__(__name__), child, cm, registry)
            else:
                try:
                    repo = args.repo_root.resolve() if args.repo_root else prepare_target(child, cm)[0]
                    queue_driver.check_cases(__import__(__name__), child, cm, registry, args.case_ids.split(','), repo)
                except Exception as exc:
                    failure = dict(sha=cm['head'], reason=str(exc), phase='coverage-build', at=now_cn().isoformat())
                    save(child / 'preparation' / 'case-check-error.json', failure)
                    if not (child / 'build-reference.json').exists():
                        previous = load(child / 'execution.json', {})
                        save(child / 'execution.json', dict(previous, state='ERROR', **failure))
                    raise
        if args.prepare_commit:
            individual.prepare_one(__import__(__name__), plan_data, args.prepare_commit, registry, sched)
        if args.freeze_commit:
            individual.freeze_one(__import__(__name__), plan_data, args.freeze_commit, registry, sched, args.review_file)
        if args.run_commit:
            individual.run_one(__import__(__name__), out, plan_data, args.run_commit, registry, sched)
        if args.finish_commit:
            individual.finish_one(__import__(__name__), out, plan_data, args.finish_commit, args.completion_reason)
        if args.drain:
            build_runner(out)
        data = (queue_driver.drain(__import__(__name__), out, manifest, plan_data, registry, sched) if args.drain else
                individual.aggregate(__import__(__name__), out, manifest, plan_data, registry))
        print(json.dumps(data, ensure_ascii=False))
        if (args.check_complete or args.drain or args.prepare_all) and data['pendingCommitValidations']:
            raise SystemExit(2)
        return
    if args.run_commit or args.finish_commit or args.prepare_commit or args.freeze_commit:
        raise ValueError("Commit queue actions require an individual root batch")
    # A resumed execution uses its frozen plan; explicit --plan-only prepares a new plan.
    if strategy == 'individual-commit':
        if args.plan_only:
            raise ValueError('Frozen child plans cannot be recalculated; prepare a separate targeted campaign')
        frozen = load(out / 'plan.json')
        if frozen is None:
            raise ValueError('Freeze reviewed coverage via the parent queue before executing a child')
        if frozen.get('coverageReviewed'):
            coverage_contract.verify_frozen(__import__(__name__), out, frozen, registry, sched)
    plan_data = load(out / "plan.json") if args.resume and not args.plan_only else None
    plan_data = plan_data or plan(out, manifest, registry, sched)
    if (out / "report.md").exists():
        stamp = now_cn().strftime("%Y%m%dT%H%M%S")
        (out / ("report-before-" + stamp + ".md")).write_bytes((out / "report.md").read_bytes())
    outcomes = load(out / "outcomes.json", dict(runs=[]))
    error = None
    try:
        if args.plan_only:
            pass
        elif args.run_slot:
            run_slot(out, manifest, plan_data, args.run_slot, args.session, outcomes)
        elif args.execute and manifest["commits"]:
            execute(out, manifest, plan_data, outcomes, sched.get("headlessBudgetMinutes", 150))
    except Exception as exc:
        error = str(exc)
        save(out / "execution.json", dict(state="ERROR", reason=error, at=now_cn().isoformat()))
    evaluation_registry = dict(cases=plan_data['frozenCases']['cases']) if 'frozenCases' in plan_data else registry
    rows = evaluate(manifest, plan_data, outcomes, evaluation_registry)
    data = report(out, manifest, plan_data, outcomes, rows)
    update_pending(manifest, rows, out)
    if error:
        with (out / "report.md").open("a", encoding="utf-8") as stream:
            stream.write("\n## 执行失败（未等待人工）\n\n" + error + "\n")
    print(json.dumps(dict(report=str(out / "report.md"), **{k: data[k] for k in ("batchId", "acceptanceState", "statusCounts", "pendingUiSlots")}),
                     ensure_ascii=False))
    if error:
        raise SystemExit(1)


if __name__ == "__main__":
    # PowerShell/tool consumers decode JSON as UTF-8, while Windows Python may
    # otherwise emit CP936 and silently corrupt Chinese requirement text.
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    if '--help' in sys.argv[1:] or '-h' in sys.argv[1:]:
        main()  # argparse help is read-only and must work during a long queue.
    else:
        with operation_lock.acquire(ROOT / 'daily' / '.operation.lock'):
            main()
