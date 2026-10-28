"""Deterministic queue execution after AI has prepared ALL review files.

No LLM is invoked here. A failed SHA cannot terminate the remaining queue.
Missing UI session receipts are requests for actual work, never fabricated PASS.
"""
from pathlib import Path
import subprocess
import artifact_cleanup


def prepare_all(d, plan, registry, sched):
    """Keep one unsupported/deleted test from skipping preparation of later SHAs."""
    for slot in plan['commitSlots']:
        child = Path(slot['batch'])
        if (child / 'plan.json').exists():
            continue
        try:
            d.individual.prepare_one(d, plan, slot['sha'], registry, sched)
            d.case_authoring.generate(d, child, d.load(child / 'manifest.json'), registry)
        except Exception as exc:
            d.save(child / 'preparation' / 'authoring.json', dict(
                sha=slot['sha'], state='NEEDS_AUTHORING', caseId=None,
                reason='Automatic preparation failed: ' + str(exc),
                boundary='AI must inspect the failure and prepare real coverage before freezing.'))


def check_cases(d, child, manifest, registry, ids, repo):
    if d.git('rev-parse', 'HEAD', repo=repo) != manifest['head']:
        raise ValueError('Case discovery must use the exact target SHA')
    by_id = {c['id']: c for c in registry['cases']}
    evidence = child / 'preparation' / 'checks'
    evidence.mkdir(parents=True, exist_ok=True)
    records = []
    for cid in ids:
        for name, layer in by_id[cid]['layers'].items():
            project = (d.ROOT / layer['project']).resolve()
            log_path = evidence / (cid + '-' + name + '.log')
            artifacts = d.WORKSPACES.parent / 'test-artifacts' / ('check-' + manifest['head'][:12] + '-' + name)
            environment = d.runner_env(repo)
            discovery_dir = evidence / 'discovery' / cid / name
            discovery_dir.mkdir(parents=True, exist_ok=True)
            environment.update(VEGA_AUTOTEST_RUNDIR=str(discovery_dir), VEGA_AUTOTEST_OUTDIR=str(discovery_dir))
            with log_path.open('w', encoding='utf-8') as log:
                result = d.run_bounded([str(d.DOTNET), 'test', str(project), '--list-tests',
                    '--filter', layer['filter'], '--artifacts-path', str(artifacts),
                    '-p:UseSharedCompilation=false'], env=environment, cwd=d.ROOT,
                    stdout=log, stderr=subprocess.STDOUT, timeout=layer.get('timeoutSeconds', 600))
            output = log_path.read_text(encoding='utf-8', errors='replace')
            # VSTest list-tests emits an indented name for each discovered test.
            listing = output.split('The following Tests are available:', 1)
            discovered = [s.strip() for s in listing[-1].splitlines() if len(listing) == 2
                          and s.startswith('    ') and s.strip() and not s.strip().startswith(('at ', 'Failed ', 'Error '))]
            records.append(dict(caseId=cid, layer=name, exitCode=result.returncode,
                discoveredTests=discovered, log=str(log_path.relative_to(child)),
                logSha256=d.coverage_contract.digest(log_path)))
    receipt = dict(sha=manifest['head'], repo=str(repo), checkedAt=d.now_cn().isoformat(), records=records, fingerprintVersion=3,
                   fingerprints=d.coverage_contract.case_fingerprints(d, registry, ids, transitive=True, linked_sources=True))
    d.save(child / 'preparation' / 'case-check.json', receipt)
    target = d.load(child / 'execution-target.json', {})
    if target.get('repo') == str(repo):
        d.save(child / 'execution.json', dict(state='COVERAGE_CHECKED', evidence=target['evidence']))
    if any(r['exitCode'] != 0 or not r['discoveredTests'] for r in records):
        raise RuntimeError('Case build/discovery failed; inspect preparation/checks and repair before freezing')
    return receipt


def drain(d, out, manifest, plan, registry, sched):
    """Preflight every plan before any build, then execute every SHA in order."""
    missing = [s for s in plan['commitSlots'] if not (Path(s['batch']) / 'plan.json').exists()]
    if missing:
        raise ValueError('Prepare and freeze ALL commits before --drain; missing: ' + ', '.join(s['sha'][:8] for s in missing))
    for slot in plan['commitSlots']:
        child = Path(slot['batch'])
        if d.load(child / 'completion.json'):
            continue
        execution = d.load(child / 'execution.json', {})
        if execution.get('state') in ('PREPARING', 'BUILDING', 'HEADLESS'):
            raise RuntimeError('Interrupted/running execution must be inspected before drain: ' + slot['sha'])
        known_preparation_failure = (execution.get('state') == 'ERROR' and execution.get('phase') == 'coverage-build'
                                     and (child / 'preparation' / 'case-check-error.json').exists())
        if execution.get('state') != 'HEADLESS_DONE' and not known_preparation_failure:
            d.individual.run_one(d, out, plan, slot['sha'], registry, sched)
        _, summary = d.individual.refresh_child(d, child, registry)
        execution = d.load(child / 'execution.json', {})
        # A genuine build failure already establishes that this SHA's UI cannot
        # execute. Keep the error, finish this SHA, and proceed to the next.
        unresolved_ui = [sid for sid in summary['pendingUiSlots']
                         if d.individual.ui_blocker_error(d, child, sid)]
        if unresolved_ui and execution.get('state') not in ('ERROR', 'BLOCKED'):
            d.save(child / 'ui-request.json', dict(sha=slot['sha'], slots=summary['pendingUiSlots'],
                action='Prepare official Simulator session, execute actual UI, run-slot, restore; or record a real blocker with evidence; resume --drain.',
                target=d.load(child / 'execution-target.json')))
            return d.individual.aggregate(d, out, manifest, plan, registry)
        runs = summary['runs']
        reason = 'Deterministic queue: ' + '; '.join(
            f"{r['caseId']} {r.get('verdict')} {r.get('runId')}" for r in runs)
        if execution.get('state') in ('ERROR', 'BLOCKED'):
            reason += '; execution: ' + execution.get('reason', execution['state']) + '; required UI not executed'
        if not runs:
            reason += '; no Runner result; see coverage/execution evidence'
        reason += '; coverage gaps retained in frozen review; no UI session created by drain'
        if summary['pendingUiSlots']:
            reason += '; UI not executed: see ui/<slot>/blocker.json and attempt evidence'
        d.individual.finish_one(d, out, plan, slot['sha'], reason)
        try:
            cleanup = artifact_cleanup.reclaim(d, child)
        except Exception as error:
            # Locked scratch or malformed mappings do not invalidate raw
            # verdicts and must not stop the remaining commit queue.
            cleanup = [dict(state='ERROR', reason=str(error), rawEvidencePreserved=True)]
        d.save(child / 'artifact-cleanup.json', cleanup)
        try:
            artifact_cleanup.reclaim_workspace(d, child)
        except Exception as error:
            d.save(child / 'workspace-cleanup-error.json', dict(reason=str(error), sourceAndDeploymentPreserved=True))
        d.individual.aggregate(d, out, manifest, plan, registry)
    return d.individual.aggregate(d, out, manifest, plan, registry)
