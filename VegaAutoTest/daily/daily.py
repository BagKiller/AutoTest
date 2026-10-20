"""Collect immutable commit manifests, plan model batches, invoke the existing Runner.

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
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
REPO = Path(os.environ.get("VEGA_SW_ROOT", r"C:\Vega\Vega_SW"))
MIRROR = ROOT / "daily" / "cache.git"
STATE = ROOT / "daily" / "state.json"
CN = timezone(timedelta(hours=8))
REMOTE = "http://win-devops:8080/DefaultCollection/Vega_SW/_git/Vega_SW"


def save(path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, ensure_ascii=False, indent=2), encoding="utf-8")


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
    return p.stdout.strip()


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


def collect(last, offline):
    now = datetime.now(CN)
    start = now.replace(hour=0, minute=0, second=0, microsecond=0)
    source = REPO if offline else sync()
    head = git("rev-parse", "master", repo=source)
    old = json.loads(STATE.read_text(encoding="utf-8")) if STATE.exists() else {}
    previous_head = old.get("collectedHead")
    history_changed = False
    if previous_head:
        try:
            git("merge-base", "--is-ancestor", previous_head, head, repo=source)
        except RuntimeError:
            history_changed = True
    rows = git("log", "master", "--format=%H%x09%cI%x09%aI%x09%s", *([f"-{last}"] if last else []), repo=source).splitlines()
    unseen = set(git("rev-list", head, "^" + previous_head, repo=source).splitlines()) if previous_head and not history_changed else set()
    commits = []
    for row in rows:
        sha, committed, authored, title = row.split("\t", 3)
        is_today = start <= datetime.fromisoformat(committed) <= now
        # Select today's commits plus every newly reachable commit since last collection,
        # including backdated commits and submissions after yesterday's cutoff.
        if not last and not (is_today or sha in unseen):
            continue
        files = git("diff-tree", "--root", "--no-commit-id", "--name-only", "-r", "--first-parent", sha, repo=source).splitlines()
        groups, unknown = classify(files)
        commits.append(dict(sha=sha, committedAt=committed, authoredAt=authored, title=title,
                            carryover=not is_today, files=files, groups=groups, unmappedFiles=unknown))
    batch_id = now.strftime("%Y%m%dT%H%M%S%z") + "_" + uuid.uuid4().hex[:6]
    out = ROOT / "daily" / "runs" / batch_id
    out.mkdir(parents=True)
    manifest = dict(schemaVersion=1, batchId=batch_id, collectedAt=now.isoformat(), timezone="Asia/Shanghai",
                    mode="recent-example" if last else "daily", source=str(source), remoteFresh=not offline,
                    selection="all reachable DAG commits; committer date today plus newly reachable carryover",
                    windowStart=start.isoformat(), windowEnd=now.isoformat(), head=head,
                    previousCollectedHead=previous_head, historyRewritten=history_changed,
                    localHead=git("rev-parse", "HEAD"), localDirty=bool(git("status", "--porcelain")),
                    pendingFromEarlierCollections=old.get("pendingCommits", []), commits=commits)
    for commit in commits:
        diff = git("show", "--format=fuller", "--first-parent", "--no-ext-diff", commit["sha"], repo=source)
        (out / (commit["sha"][:8] + ".diff")).write_text(diff, encoding="utf-8")
    save(out / "manifest.json", manifest)
    if not last and not offline and not history_changed:
        # Collection checkpoint is separate from acceptance: blocked items remain pending.
        pending = list(dict.fromkeys(old.get("pendingCommits", []) + [c["sha"] for c in commits]))
        save(STATE, dict(collectedHead=head, collectedAt=now.isoformat(), pendingCommits=pending,
                         previousBatch=str(out), note="Collection is not acceptance. Pending commits require verified closure."))
    return out, manifest


def plan(out, manifest):
    groups = sorted({g for c in manifest["commits"] for g in c["groups"]})
    data = dict(targetHead=manifest["head"], groups=groups, configurationPolicy="backup/hash/restore; change model only while stopped; one start per model; Gen2 + Simulator + GemSim; never reuse one model verdict for another",
                batches=[
                    dict(order=0, id="preflight", models=[], startupCount=0, tasks=["remote freshness, source/binary fingerprints", "review all diffs and unclassified files", "lockfile restore/build gate, installer generation" ]),
                    dict(order=1, id="headless", models=["Vega100", "Vega300", "Vega500"], startupCount=0,
                         tasks=["Runner daily-recent10 logic100,logic300,logic300-hwv2,logic500", "Runner daily-supplement logic,logic-channels100: Binning/delivery/cancellation/timeout/fence/asset/LPD save", "size/BFP defaults, PMT routing, AutoOptics role predicate, DN fallback, GEMSim bool/single-call, run cache key, IO mapping, XY Notch visibility"]),
                    dict(order=2, id="ui100", models=["Vega100"], startupCount=1, tasks=["200mm XY/XYAuto/ECAM/Uniformity/Beam/BFP", "AutoOptics hidden for su", "PMT model table/OD0.5/laser output/PowerLevel hidden", "notch simulated data source and LoadPort interaction"]),
                    dict(order=3, id="ui300", models=["Vega300"], startupCount=2, tasks=["HWv1 and HWv2 are separate configurations: one start per hardware profile", "300mm defaults/BFP, IO Slit/W1FF mapping and capability-filtered display", "AutoOptics role regression, shared PMT conditions, LoadPort/GEMSim interaction"]),
                    dict(order=4, id="ui500", models=["Vega500"], startupCount=1, tasks=["300mm defaults/BFP", "AutoOptics role regression", "shared PMT conditions", "GEM lifecycle/abort simulation and config restoration"]),
                    dict(order=5, id="hardware", models=["Vega100", "Vega300", "Vega500"], startupCount=None, tasks=["NI external-stop error codes and real acquisition race", "SEPLAT/E87 Host loading handshake/race repeated stress", "real Slit/W1FF wiring and MS motor timeout response", "real alignment channel signal quality; failed-scan rerun data integrity"])
                ], automationScope="headless Runner only; scheduled agent reviews and executes UI when license/session/config prerequisites allow; hardware never automated")
    save(out / "plan.json", data)
    return data


def execute(out, manifest):
    if manifest["historyRewritten"]:
        save(out / "execution.json", dict(state="BLOCKED", reason="remote history rewritten; retained for review"))
        return None
    attempt = datetime.now(CN).strftime("%Y%m%dT%H%M%S") + "_" + uuid.uuid4().hex[:6]
    evidence = out / "attempts" / attempt
    evidence.mkdir(parents=True)
    save(out / "execution.json", dict(state="PREPARING", attempt=attempt, evidence=str(evidence)))
    repo = Path(r"C:\AutoTest\workspaces") / (manifest["batchId"] + "_" + attempt)
    shell = r"C:\Users\Administrator\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"
    # Unattended recovery: owned Simulator processes may be stopped; never reuse
    # an output directory with a live session or reset the user's checkout.
    with (evidence / "cleanup.log").open("w", encoding="utf-8") as log:
        cleanup = run_bounded([shell, "-NoProfile", "-File", str(ROOT / "daily" / "Stop-ManagedVega.ps1"),
                                  "-AuditPath", str(evidence / "cleanup.json")], stdout=log, stderr=subprocess.STDOUT, timeout=45)
    save(evidence / "cleanup-status.json", dict(exitCode=cleanup.returncode,
         fallback="Fresh worktree isolates build even if an unrelated session cannot be stopped"))
    git("worktree", "add", "--detach", str(repo), manifest["head"], repo=Path(manifest["source"]))
    current_head = git("rev-parse", "HEAD", repo=repo)
    if current_head != manifest["head"] or git("status", "--porcelain", repo=repo):
        raise RuntimeError("Fresh worktree fingerprint mismatch")
    save(out / "execution-target.json", dict(repo=str(repo), head=current_head, dirty=False, evidence=str(evidence)))
    save(out / "execution.json", dict(state="BUILDING", attempt=attempt, evidence=str(evidence)))
    with (evidence / "build-driver.log").open("w", encoding="utf-8") as log:
        build = run_bounded([shell, "-NoProfile", "-File", str(ROOT / "daily" / "Build-DailyVega.ps1"),
                                "-RepoRoot", str(repo), "-EvidenceRoot", str(evidence / "build")],
                               stdout=log, stderr=subprocess.STDOUT, timeout=1800)
    if build.returncode:
        raise RuntimeError(f"Target build failed; see {evidence / 'build'}")
    provenance = json.loads((evidence / "build" / "build-provenance.json").read_text(encoding="utf-8-sig"))
    dll = repo / "Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.dll"
    if provenance["commit"] != manifest["head"] or hashlib.sha256(dll.read_bytes()).hexdigest().upper() != provenance["vegaDllSha256"].upper():
        raise RuntimeError("Build provenance mismatch")
    save(out / "build-reference.json", dict(path=str(evidence / "build" / "build-provenance.json"), **provenance))
    env = os.environ.copy()
    dotnet = Path(r"C:\AutoTest\runtime\dotnet\dotnet.exe")
    env.update(VEGA_SW_ROOT=str(repo), DOTNET_ROOT=str(dotnet.parent),
               NUGET_PACKAGES=r"C:\Users\Administrator\.nuget\packages",
               GIT_CONFIG_COUNT="1", GIT_CONFIG_KEY_0="safe.directory", GIT_CONFIG_VALUE_0=repo.as_posix())
    env["PATH"] = str(dotnet.parent) + os.pathsep + env["PATH"]
    runner = ROOT / "runner" / "Vega.AutoTest.Runner" / "bin" / "Debug" / "net8.0-windows" / "Vega.AutoTest.Runner.dll"
    # Locate by file name, to support the Runner project's target framework suffix.
    if not runner.exists():
        candidates = list((ROOT / "runner" / "Vega.AutoTest.Runner" / "bin" / "Debug").glob("*/Vega.AutoTest.Runner.dll"))
        if len(candidates) != 1:
            raise RuntimeError("Runner binary is missing/ambiguous; build the Runner first")
        runner = candidates[0]
    before = set((out / "runner").glob("*/result.json"))
    log_path = out / ("runner-" + datetime.now(CN).strftime("%H%M%S") + ".log")
    with log_path.open("w", encoding="utf-8") as log:
        p = run_bounded([str(dotnet), str(runner), "verify", "--case", "daily-recent10", "--layers", "logic100,logic300,logic300-hwv2,logic500", "--repo-root", str(repo), "--runs-dir", str(out / "runner")],
                           env=env, stdout=log, stderr=subprocess.STDOUT, timeout=1600, cwd=ROOT)
    paths = list(set((out / "runner").glob("*/result.json")) - before)
    save(out / "execution.json", dict(state="EXECUTED", exitCode=p.returncode, resultPaths=[str(p) for p in paths]))
    if len(paths) != 1:
        raise RuntimeError("Runner did not produce exactly one new result.json; inspect runner timestamped log")
    result = json.loads(paths[0].read_text(encoding="utf-8-sig"))
    save(out / "runner-reference.json", dict(path=str(paths[0]), sha256=hashlib.sha256(paths[0].read_bytes()).hexdigest(), runId=result["runId"]))
    supplement_dir = out / "supplement-runner"
    before_supplement = set(supplement_dir.glob("*/result.json"))
    try:
        with (evidence / "supplement-runner.log").open("w", encoding="utf-8") as log:
            supplemental = run_bounded([str(dotnet), str(runner), "verify", "--case", "daily-supplement",
                "--layers", "logic,logic-channels100", "--repo-root", str(repo), "--runs-dir", str(supplement_dir)],
                env=env, stdout=log, stderr=subprocess.STDOUT, timeout=700, cwd=ROOT)
        new_supplement = list(set(supplement_dir.glob("*/result.json")) - before_supplement)
        if len(new_supplement) != 1:
            raise RuntimeError("Supplement Runner did not produce exactly one result")
        supplemental_result = json.loads(new_supplement[0].read_text(encoding="utf-8-sig"))
        save(out / "supplement-reference.json", dict(path=str(new_supplement[0]),
            sha256=hashlib.sha256(new_supplement[0].read_bytes()).hexdigest(),
            runId=supplemental_result["runId"], verdict=supplemental_result["verdict"],
            verdictCode=supplemental_result["verdictCode"], exitCode=supplemental.returncode))
    except Exception as exc:
        save(out / "supplement-reference.json", dict(state="ERROR", reason=str(exc)))
    return result


def report(out, manifest, plan_data, result):
    counts = {}
    if result:
        for a in result.get("assertions", []):
            counts[a["result"]] = counts.get(a["result"], 0) + 1
    data = dict(batchId=manifest["batchId"], acceptanceState="INCOMPLETE" if manifest["commits"] else "NO_COMMITS",
                commitCount=len(manifest["commits"]), target=manifest["head"], remoteFresh=manifest["remoteFresh"],
                runnerVerdict=result["verdict"] if result else None, runnerCode=result["verdictCode"] if result else None,
                runId=result["runId"] if result else None, assertionCounts=counts,
                builtFromCurrentSource=result["target"]["builtFromCurrentSource"] if result else False,
                note="Acceptance completeness is separate from the unchanged Runner verdict. No commit is accepted solely from static inspection or partial probes.")
    if (out / "supplement-reference.json").exists():
        data["supplement"] = json.loads((out / "supplement-reference.json").read_text(encoding="utf-8"))
    if (out / "supplement-report.md").exists():
        data["uiSupplementReport"] = str(out / "supplement-report.md")
    save(out / "summary.json", data)
    lines = ["# Vega 提交批次总测试报告", "", f"批次 `{manifest['batchId']}`，目标 `{manifest['head']}`。", "",
             f"收集 {len(manifest['commits'])} 个提交；远端刷新：{manifest['remoteFresh']}。总体验收：**{data['acceptanceState']}**。",
             "", "## 正式执行结果", "",
             f"Runner：{data['runnerVerdict']} / {data['runnerCode']}；runId `{data['runId']}`；断言 {counts}。",
             "Runner 自身未重编产品（builtFromCurrentSource=false），正式结论仅对所记录 DLL 有效；若另行独立构建，以附录构建证据说明，不能改写 Runner 字段。链接的源文件另行标注。",
             "四组为独立配置的无界面测试宿主（V100、V300 HWv1/HWv2、V500），不代表 Vega.exe 已启动验收。HWv2 探针使用显式能力 token，尚未验证完整 HardwareProfileProvider 装配。", "",
             "## 提交覆盖清单", "", "| 提交 | 改动 | 合并批次 | 验收状态 |", "| --- | --- | --- | --- |"]
    for c in reversed(manifest["commits"]):
        lines.append(f"| {c['sha'][:8]} | {c['title'].replace('|','/')} | {', '.join(c['groups']) or '待分析'} | 未完整验收；见下述边界 |")
    if "supplement" in data:
        s = data["supplement"]
        lines += ["", "## 补充正式 Runner", "",
                  f"`{s.get('verdict', s.get('state'))}` / `{s.get('verdictCode')}`；runId `{s.get('runId')}`。原始结果：`{s.get('path', s.get('reason'))}`。",
                  "覆盖 Binning 原始行为用例、Windows 交付计数/取消/超时/会话栅栏、V100 资源部署完整性与独立进程中的 LPD 通道过滤/裸键保存。不是 Linux 真机或完整桌面检测流程验收。"]
    if "uiSupplementReport" in data:
        lines += ["", "本批已有独立桌面补测记录：[完整补测矩阵与边界](supplement-report.md)。该记录是此前实际观察，不应被当成本次重跑的UI结果。"]
    target_path = out / "execution-target.json"
    if target_path.exists():
        target = json.loads(target_path.read_text(encoding="utf-8"))
        lines += ["", "## 无人值守构建与恢复证据", "",
                  f"实际目录：`{target['repo']}`；目标：`{target['head']}`。",
                  f"本次独立证据目录：`{target.get('evidence', '未记录')}`。cleanup.json 记录自动进程清理，build/build-provenance.json 仅在构建成功后生成；未使用 locked restore，NI 原生项目被 Simulator 构建排除。"]
    lines += ["", "## 执行顺序", "", "在同一目标 HEAD 验证累积效果，不逐个 checkout。先处理依赖与安装配置，再运行所有无界面逻辑；依次按 Vega100 → Vega300 → Vega500，每机型最多启动一次并完成其所有界面检查。若已有目标机型会话，先复用可减少重启，最后恢复原配置。", "",
              "| 顺序 | 批次 | 计划启动 | 内容 |", "| --- | --- | --- | --- |"]
    for b in plan_data["batches"]:
        lines.append(f"| {b['order']} | {b['id']} | {b['startupCount']} | {'；'.join(b['tasks'])} |")
    lines += ["", "## Simulator 边界与未完成项", "",
              ("以下探针范围仅针对本次 Runner 原始结果；不是完整提交验收。" if result else "本次未取得 Runner 结果，以下为已有探针的设计范围与待验边界，均不得视为本次已执行。"), "",
              "- #3644：尺寸/转换/BFP辅助查询/空队列防御有核心探针；真实校准与完整调用链未覆盖。",
              "- DN 优先：运行仓库原有回退行为测试；真实信号质量和对位完成需要设备数据。",
              "- LoadPort：运行原有 GEMBase safe-default 和 GEMSim true/false 单调用透传测试；界面警告、连点与 SEPLAT/E87 仍待验证。",
              "- AutoOptics：只验证真实二进制的机型+角色可见性谓词；未把它算作实际工具栏界面验收。",
              "- BFP：验证配置工厂初始尺寸；视图历史配置恢复、实际光斑测量未覆盖。",
              "- Run 隔离 #3660：检查真实 DLL 的不同 ID 缓存键隔离、集合编号分离、ID=0 同实例并发首访稳定性；失败取消链、跨会话栅栏、超时/短读记账与真实数据完整性尚未覆盖。",
              "- IO #3655：检查 V300/V500 内置 Slit/W1FF 位号与能力 token 门控；JSON/用户旧副本的加载迁移、完整硬件 profile 装配与真实接线另行验证。",
              "- AFS #3657：仅核对 MS 震荡余量常量 30 秒；AFS 采集中关页、UI 不死锁与残留帧仍需 Simulator 交互测试；MS 实际运动响应需真机。",
              "- XY #3650：验证机型/扫描方向与 VM Notch 区域可见性谓词；旧配置自愈、Sim 注入三偏移恒零与真实 UI 仍需进一步执行。",
              "- PMT 配置路径/条件表作为共享回归探针，不属于本次十提交的直接变更验收。",
              "- NI 采集卡外部停止：Simulator 不使用真实 NI 驱动，硬件错误码与竞态不得计为通过。",
              "- GEM Load TCS 握手：并发成功/失败/取消/超时/订阅先后顺序尚未执行；可新增 Simulator 压力用例，真实 Host/E87 另需联机。",
              "- 若后续提交涉及 NuGet 锁文件或安装器，必须另执行 locked restore/build 或 Inno 真实生成校验；不得以本批探针代替。",
              "- 三机型完整界面切换需匹配的启动授权和独立配置；自动官方认证恢复失败后该组保持 BLOCKED，继续其他组，不等待人工回复，不绕过登录/授权。", "",
              "新增/未映射改动必须逐项分析，不自动沿用上述探针结论。manifest.json 保存全量文件，*.diff 保存原始改动，plan.json 保存批次，Runner 原始 result.json/report.md 保持不变。",
              "", "## 每日收集规则", "",
              "北京时间每天 23:30 执行。范围为当天 00:00 到执行时，按提交时间（committer time）收集，同时补收上次收集后新增的可达提交，覆盖昨日 23:30 后的提交与回填日期提交。集合按 SHA 去重；同日重跑展示当天完整集合。",
              "收集游标与待验收队列分开：收集成功不会清除未通过/阻塞项目；历史重写报警，首次运行不追溯全部历史；本次最近十次为示例，不推进每日游标。远端获取失败不得宣称当天无提交。", ""]
    (out / "report.md").write_text("\n".join(lines), encoding="utf-8")
    return data


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--last", type=int, default=0)
    parser.add_argument("--offline", action="store_true")
    parser.add_argument("--execute", action="store_true")
    parser.add_argument("--resume", type=Path, help="Execute an existing immutable manifest after preparing its exact target")
    args = parser.parse_args()
    if args.resume:
        out = args.resume.resolve()
        if not out.is_relative_to((ROOT / "daily" / "runs").resolve()):
            raise ValueError("Resume path must be inside daily/runs")
        manifest = json.loads((out / "manifest.json").read_text(encoding="utf-8"))
    else:
        out, manifest = collect(args.last, args.offline)
    plan_data = plan(out, manifest)
    if (out / "report.md").exists():
        stamp = datetime.now(CN).strftime("%Y%m%dT%H%M%S")
        (out / ("report-before-" + stamp + ".md")).write_bytes((out / "report.md").read_bytes())
    result = None
    error = None
    try:
        result = execute(out, manifest) if args.execute and manifest["commits"] else None
    except Exception as exc:
        error = str(exc)
        save(out / "execution.json", dict(state="ERROR", reason=error, at=datetime.now(CN).isoformat()))
    data = report(out, manifest, plan_data, result)
    if error:
        with (out / "report.md").open("a", encoding="utf-8") as stream:
            stream.write("\n## 执行失败（未等待人工）\n\n" + error + "\n")
    print(json.dumps(dict(report=str(out / "report.md"), **data), ensure_ascii=False))
    if error:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
