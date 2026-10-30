"""Durable single-process daily validation, independent of chat turn lifetime."""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid

import daily as d
import model_author


ACTIVE = d.ROOT / 'daily' / 'controller-active.json'


def identity(pid):
    """PID plus kernel creation time prevents killing/reusing an unrelated process."""
    if os.name != 'nt':
        try:
            return Path('/proc/' + str(pid) + '/stat').read_text().rsplit(')', 1)[1].split()[19]
        except OSError:
            return None
    import ctypes
    from ctypes import wintypes
    kernel = ctypes.WinDLL('kernel32', use_last_error=True)
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.GetProcessTimes.argtypes = [wintypes.HANDLE] + [ctypes.POINTER(wintypes.FILETIME)] * 4
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        return None
    try:
        values = [wintypes.FILETIME() for _ in range(4)]
        if not kernel.GetProcessTimes(handle, *(ctypes.byref(v) for v in values)):
            return None
        return str((values[0].dwHighDateTime << 32) | values[0].dwLowDateTime)
    finally:
        kernel.CloseHandle(handle)


class Supervisor:
    def __init__(self, root):
        self.root = root

    def run(self, command, *, timeout, **kwargs):
        record_path = self.root / 'controller' / 'processes' / (uuid.uuid4().hex[:12] + '.json')
        process = subprocess.Popen(command, **kwargs)
        record = dict(pid=process.pid, identity=identity(process.pid), state='RUNNING',
            deadline=time.time() + timeout, executable=str(command[0]), startedAt=d.now_cn().isoformat())
        d.save(record_path, record)
        try:
            code = process.wait(timeout=timeout)
            record.update(state='EXITED', exitCode=code)
            return subprocess.CompletedProcess(command, code)
        except subprocess.TimeoutExpired:
            self.stop(record)
            process.wait(timeout=15)
            record.update(state='TIMEOUT')
            raise RuntimeError('Owned stage exceeded ' + str(timeout) + 's; see ' + str(record_path)) from None
        finally:
            record['finishedAt'] = d.now_cn().isoformat()
            d.save(record_path, record)

    @staticmethod
    def stop(record):
        if record.get('identity') and identity(record['pid']) == record['identity']:
            if os.name == 'nt':
                subprocess.run(['taskkill', '/PID', str(record['pid']), '/T', '/F'],
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=20)
            else:
                os.kill(record['pid'], 15)

    def recover(self):
        # The outer operation lock establishes that the old controller is gone.
        # Never start a second build while its surviving owned child still runs.
        for path in (self.root / 'controller' / 'processes').glob('*.json'):
            record = d.load(path)
            if record.get('state') != 'RUNNING':
                continue
            while record.get('identity') and identity(record['pid']) == record['identity']:
                if time.time() >= record['deadline']:
                    self.stop(record)
                    break
                time.sleep(2)
            record.update(state='RECOVERED_AFTER_CONTROLLER_EXIT', recoveredAt=d.now_cn().isoformat())
            d.save(path, record)


def progress(root, phase, **values):
    state = dict(batch=str(root), pid=os.getpid(), identity=identity(os.getpid()), phase=phase,
                 updatedAt=d.now_cn().isoformat(), **values)
    d.save(root / 'controller' / 'state.json', state)
    d.save(ACTIVE, state)
    print(json.dumps(state, ensure_ascii=False), flush=True)


def attempt(child, path, action, outcome):
    return dict(path=str(path.relative_to(child)), sha256=d.coverage_contract.digest(path),
                action=action, outcome=outcome)


def gap_requirements(manifest, reason):
    return [dict(id='preparation-unavailable', workItems=manifest['commits'][0].get('workItems', []),
        steps=['Generate behavior tests from this exact commit and requirement snapshot'],
        expected='Requirements could not be fully evaluated; no product acceptance claimed',
        uiRequired=True, profiles=[], caseIds=[], gap=reason)]


def generate(d, child, manifest, registry, sched, root, repair=None):
    payload = model_author.context(d, child, manifest, registry, sched)
    context_path = child / 'preparation' / 'source-context.json'
    saved = d.load(context_path, {})
    if saved.get('sha') == manifest['head']:
        for name, source in saved.get('sources', {}).items():
            payload['sources'][name] = model_author.source_excerpt(source)
        while sum(map(len, payload['sources'].values())) > 140000:
            payload['sources'].pop(next(iter(payload['sources'])))
    if repair:
        payload['previousGeneration'] = repair['response']
        payload['compilerDiagnostics'] = repair['diagnostics']
        payload['compilerRepair'] = True
    token = uuid.uuid4().hex[:10]
    budget_path = child / 'preparation' / 'author-budget.json'
    budget = d.load(budget_path, dict(usedSeconds=0))
    if budget.get('activeStarted'):
        budget['usedSeconds'] += time.time() - budget.pop('activeStarted')
    validation_retry = False
    for index in range(5):
        remaining = 1200 - budget['usedSeconds']
        if remaining <= 0:
            raise RuntimeError('Per-commit 20 minute model authoring budget exhausted')
        payload['remainingSourceRequests'] = max(0, 3 - index)
        folder = child / 'preparation' / 'model' / (token + '-' + str(index))
        progress(root, 'AUTHORING', sha=manifest['head'], inference=str(folder))
        budget['activeStarted'] = time.time()
        d.save(budget_path, budget)
        try:
            # The per-commit total is the hard limit. An arbitrary shorter call
            # timeout used to discard a nearly finished large response despite
            # more than half the authoring budget still being available.
            response = model_author.infer(d, folder, payload, timeout=remaining)
        finally:
            budget['usedSeconds'] += time.time() - budget.pop('activeStarted')
            d.save(budget_path, budget)
        if response.get('neededSources'):
            if index < 3:
                model_author.add_sources(d, payload, manifest, response['neededSources'])
                d.save(context_path, dict(sha=manifest['head'], sources=payload['sources']))
                continue
            payload['validationError'] = 'Source request rounds exhausted. Emit tests and concrete external gaps now.'
            continue
        if repair:
            response = model_author.compiler_repair(repair['response'], response)
            d.save(folder / 'effective-response.json', response)
        version = 'v' + token
        try:
            requirements = model_author.install(d, child, manifest, response, registry, sched, version)
        except (ValueError, KeyError, TypeError) as error:
            if validation_retry:
                raise
            validation_retry = True
            payload['validationError'] = str(error)
            payload['previousInvalidResponse'] = response
            # Every retry has its own immutable path, including a partially
            # installed rejected response; no frozen source is ever rewritten.
            token = uuid.uuid4().hex[:10]
            continue
        receipt = dict(requirements=requirements, response=response,
                       responsePath=str(folder / ('effective-response.json' if repair else 'response.json')),
                       generatedAt=d.now_cn().isoformat(), version=version)
        d.save(child / 'preparation' / 'authored.json', receipt)
        return receipt
    raise RuntimeError('Model exhausted its bounded exact-SHA source requests')


def recover_authored(child, manifest, registry, sched):
    """Use a finished model response after parent death, without another call."""
    folders = sorted((child / 'preparation' / 'model').glob('*'), key=lambda p: p.stat().st_mtime, reverse=True)
    for folder in folders:
        response_path, request_path = folder / 'response.json', folder / 'request.txt'
        if not response_path.is_file() or not request_path.is_file():
            continue
        try:
            payload = json.loads(request_path.read_text(encoding='utf-8').split('\nINPUT DATA:\n', 1)[1])
            if payload.get('sha') != manifest['head']:
                continue
            response = d.load(response_path)
            if response.get('neededSources'):
                continue
            for line in (folder / 'events.jsonl').read_text(encoding='utf-8', errors='replace').splitlines():
                try:
                    event = json.loads(line)
                except ValueError:
                    continue
                if event.get('item', {}).get('type') in ('command_execution', 'file_change', 'mcp_tool_call', 'web_search'):
                    raise ValueError('Recovered inference attempted forbidden tools')
            if payload.get('compilerRepair'):
                response = model_author.compiler_repair(payload['previousGeneration'], response)
                response_path = folder / 'recovered-effective-response.json'
                d.save(response_path, response)
            if not response.get('requirements') and not manifest['commits'][0].get('noProductChange'):
                continue
            version = 'v' + uuid.uuid4().hex[:10]
            requirements = model_author.install(d, child, manifest, response, registry, sched, version)
            receipt = dict(requirements=requirements, response=response, responsePath=str(response_path),
                version=version, recoveredFromModelResponse=True, generatedAt=d.now_cn().isoformat())
            d.save(child / 'preparation' / 'authored.json', receipt)
            return receipt
        except (ValueError, KeyError, TypeError):
            continue
    return None


def prepare_commit(root, slot, plan, registry, sched):
    child = Path(slot['batch'])
    if (child / 'plan.json').exists():
        d.coverage_contract.verify_frozen(d, child, d.load(child / 'plan.json'), registry, sched)
        return
    manifest = d.load(child / 'manifest.json')
    journal = child / 'preparation' / 'controller-attempt.json'
    receipt = d.load(child / 'preparation' / 'authored.json')
    try:
        if receipt is None:
            receipt = recover_authored(child, manifest, registry, sched) or generate(d, child, manifest, registry, sched, root)
        requirements = receipt['requirements']
        author_evidence = Path(receipt['responsePath'])
    except model_author.ModelCapacityUnavailable:
        raise
    except Exception as error:
        d.save(journal, dict(sha=slot['sha'], stage='model-generation', reason=str(error)))
        requirements = gap_requirements(manifest, str(error))
        author_evidence = journal
    progress(root, 'BUILD_DISCOVERY', sha=slot['sha'])
    repo = None
    try:
        # Reuse an existing independent build only after its original provenance
        # has been verified by normal execution. The fresh path is the default.
        target = d.load(child / 'execution-target.json', {})
        if target and (child / 'build-reference.json').exists():
            repo = Path(target['repo'])
            if d.git('rev-parse', 'HEAD', repo=repo) != slot['sha']:
                raise RuntimeError('Prepared build SHA mismatch')
        else:
            repo = d.prepare_target(child, manifest)[0]
        for repair_index in range(2):
            ids = sorted({cid for req in requirements for cid in req['caseIds']})
            try:
                d.queue_driver.check_cases(d, child, manifest, registry, ids, repo)
                break
            except Exception:
                diagnostics = '\n'.join(p.read_text(encoding='utf-8', errors='replace')[-10000:]
                    for p in (child / 'preparation' / 'checks').glob('*.log'))
                if repair_index or receipt is None:
                    raise
                receipt = generate(d, child, manifest, registry, sched, root,
                    repair=dict(response=receipt['response'], diagnostics=diagnostics))
                requirements = receipt['requirements']
                author_evidence = Path(receipt['responsePath'])
    except model_author.ModelCapacityUnavailable:
        raise
    except Exception as error:
        failure_path = child / 'preparation' / 'case-check-error.json'
        d.save(failure_path, dict(sha=slot['sha'], phase='coverage-build', reason=str(error), at=d.now_cn().isoformat()))
        if repo is None or not (child / 'build-reference.json').exists():
            state = d.load(child / 'execution.json', {})
            d.save(child / 'execution.json', dict(state, state='ERROR', phase='coverage-build', reason=str(error)))
        else:
            # A generated test compile failure must not suppress the baselines.
            # Keep discovered valid candidates; retain failures as coverage gaps.
            checked = d.load(child / 'preparation' / 'case-check.json', {})
            records = checked.get('records', [])
            valid = {c['id'] for c in checked.get('fingerprints', {}).get('cases', []) if
                all(any(r['caseId'] == c['id'] and r['layer'] == layer and r['exitCode'] == 0 and
                        r.get('discoveredTests') for r in records) for layer in c['layers'])}
            for req in requirements:
                invalid = set(req['caseIds']) - valid
                if invalid:
                    req['caseIds'] = [cid for cid in req['caseIds'] if cid in valid]
                    req['gap'] = (req.get('gap', '') + '; executable test preparation failed: ' + str(error)).strip('; ')
            d.save(child / 'execution.json', dict(state='COVERAGE_CHECKED', reason='Test preparation incomplete; baselines still execute'))
        if repo is None or not (child / 'build-reference.json').exists():
            for req in requirements:
                req['caseIds'] = []
                req['gap'] = (req.get('gap', '') + '; independent product build failed: ' + str(error)).strip('; ')
        author_evidence = failure_path
    for req in requirements:
        if req.get('gap') or not req['caseIds']:
            req['attemptEvidence'] = [attempt(child, author_evidence, 'Generate/compile exact-SHA behavioral coverage',
                req.get('gap', 'No executable acceptance scenario could be prepared'))]
    review_path = child / 'preparation' / 'controller-review.json'
    d.save(review_path, dict(sha=slot['sha'],
        diffSha256=d.coverage_contract.digest(Path(d.load(child / 'scope.json')['diff'])),
        workItemsSha256=d.coverage_contract.digest(child / 'preparation' / 'workitems.json'), requirements=requirements))
    try:
        d.individual.freeze_one(d, plan, slot['sha'], registry, sched, review_path)
    except Exception as error:
        # Invalid generated review/code is an item failure, not permission to
        # abandon the other commits or fabricate acceptance. Keep its actual
        # rejection and execute this item's available baselines.
        rejected = child / 'preparation' / 'review-rejected.json'
        d.save(rejected, dict(reason=str(error), review=d.load(review_path)))
        requirements = gap_requirements(manifest, 'Generated coverage review rejected: ' + str(error))
        requirements[0]['attemptEvidence'] = [attempt(child, rejected,
            'Validate generated test coverage and exact-SHA discovery', str(error))]
        review = d.load(review_path)
        review['requirements'] = requirements
        d.save(child / 'preparation' / 'controller-review-fallback.json', review)
        d.individual.freeze_one(d, plan, slot['sha'], registry, sched,
            child / 'preparation' / 'controller-review-fallback.json')
    progress(root, 'PREPARED', sha=slot['sha'])


def ps(action, slot, repo, session, log):
    args = [d.SHELL, '-NoProfile', '-File', str(d.ROOT / 'daily' / 'Start-ModelValidation.ps1'),
        '-Model', slot['model'], '-Profile', slot['profileArg'], '-RepoRoot', str(repo), '-Action', action]
    if session:
        args += ['-SessionPath', str(session)]
    with log.open('w', encoding='utf-8') as output:
        result = d.run_bounded(args, timeout=120, stdout=output, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError(action + ' failed; see ' + str(log))
    return log.read_text(encoding='utf-8', errors='replace')


def restore_sessions(child):
    target = d.load(child / 'execution-target.json', {})
    if not target:
        return
    repo = Path(target['repo']).resolve()
    # Also covers interruption immediately after Prepare but before session path
    # persistence: session.json itself is the durable configuration receipt.
    for path in (d.ROOT / 'daily' / 'sessions').glob('*/session.json'):
        state = d.load(path)
        if state.get('state') == 'RESTORED' or Path(state.get('repo', '')).resolve() != repo:
            continue
        session = path.parent
        slot = dict(model=state['model'], profileArg=state['profile'].split('-', 1)[1] if '-' in state['profile'] else 'Default')
        logs = child / 'controller-restoration'
        logs.mkdir(exist_ok=True)
        ps('Stop', slot, repo, session, logs / (session.name + '-stop.log'))
        ps('Restore', slot, repo, session, logs / (session.name + '-restore.log'))


def recover_build(child):
    """Adopt a completed build after parent death only with all output hashes."""
    if (child / 'build-reference.json').exists():
        return
    target = d.load(child / 'execution-target.json', {})
    if not target.get('repo') or not target.get('evidence'):
        return
    repo = Path(target['repo']).resolve()
    manifest = d.load(child / 'manifest.json')
    path = Path(target['evidence']) / 'build' / 'build-provenance.json'
    provenance = d.load(path, {})
    if not provenance:
        return
    if (repo.parent != d.WORKSPACES.resolve() or provenance.get('commit') != manifest['head'] or
            Path(provenance.get('repo', '')).resolve() != repo or
            d.git('rev-parse', 'HEAD', repo=repo) != manifest['head'] or
            d.git('status', '--porcelain', '--untracked-files=no', repo=repo)):
        raise RuntimeError('Interrupted build provenance/source mismatch')
    binary = repo / 'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
    for name, key in [('Vega.dll', 'vegaDllSha256'), ('RecipeBatchEditor/RecipeBatchEditor.dll', 'batchEditorDllSha256'),
                      ('ComponentConfigEditor/ComponentConfigEditor.dll', 'componentEditorDllSha256')]:
        value = provenance.get(key)
        if (not value and key != 'componentEditorDllSha256') or (value and
                d.coverage_contract.digest(binary / name).lower() != value.lower()):
            raise RuntimeError('Interrupted build output hash mismatch: ' + name)
    d.save(child / 'build-reference.json', dict(path=str(path), **provenance))
    d.save(child / 'controller' / 'recovered-build.json', dict(path=str(path),
        verifiedAt=d.now_cn().isoformat(), allOutputFingerprintsVerified=True))


def restore_or_quarantine(root, child):
    try:
        restore_sessions(child)
        return True
    except Exception as error:
        path = child / 'controller-restoration' / 'failure.json'
        d.save(path, dict(reason=str(error), at=d.now_cn().isoformat(), restored=False))
        target = d.load(child / 'execution-target.json', {})
        quarantine_path = d.ROOT / 'daily' / 'controller-quarantined-profiles.json'
        quarantine = d.load(quarantine_path, {})
        for session in (d.ROOT / 'daily' / 'sessions').glob('*/session.json'):
            state = d.load(session)
            if state.get('repo') == target.get('repo') and state.get('state') != 'RESTORED':
                quarantine[state['profile']] = dict(reason=str(error), evidence=str(path),
                    sha256=d.coverage_contract.digest(path), session=str(session.parent))
        d.save(root / 'controller' / 'quarantined-profiles.json', quarantine)
        d.save(quarantine_path, quarantine)
        # Keep headless work and other model profiles independent. This is a
        # controller/configuration error, never a changed raw Runner verdict.
        if (child / 'plan.json').exists():
            d.save(child / 'execution.json', dict(state='ERROR', reason='Configuration restoration failed: ' + str(error)))
        return False


def ui(root, child):
    cp = d.load(child / 'plan.json')
    repo = Path(d.load(child / 'execution-target.json')['repo'])
    _, summary = d.individual.refresh_child(d, child, d.load(d.REGISTRY))
    for slot in cp['uiSlots']:
        if slot['id'] not in summary['pendingUiSlots']:
            continue
        evidence = child / 'ui' / slot['id'] / 'controller'
        evidence.mkdir(parents=True, exist_ok=True)
        quarantine = d.load(d.ROOT / 'daily' / 'controller-quarantined-profiles.json', {}).get(slot['profile'])
        if quarantine:
            source = Path(quarantine['evidence'])
            if not source.is_file() or d.coverage_contract.digest(source) != quarantine['sha256']:
                raise RuntimeError('Configuration quarantine evidence changed; refusing unsafe profile reuse')
            path = evidence / 'quarantine.json'
            d.save(path, quarantine)
            d.save(evidence.parent / 'blocker.json', dict(reason='Shared model profile has an unresolved restoration error',
                attemptEvidence=[attempt(child, path, 'Check restoration safety before preparing this model', quarantine['reason'])]))
            continue
        progress(root, 'UI', sha=d.load(child / 'manifest.json')['head'], slot=slot['id'])
        session = None
        error = None
        try:
            output = ps('Prepare', slot, repo, None, evidence / 'prepare.log')
            match = re.search(r'SessionPath=(.+)', output)
            if not match:
                raise RuntimeError('Prepare did not return its session path')
            session = Path(match.group(1).strip())
            d.save(evidence / 'session.json', dict(path=str(session)))
            # Generated UI tests use the actual application and existing official
            # authentication/login path; expired/missing authorization is a real
            # failure, never substituted with a manufactured key or PASS.
            outcomes = d.load(child / 'outcomes.json', dict(runs=[]))
            d.run_slot(child, d.load(child / 'manifest.json'), cp, slot['id'], session, outcomes)
        except Exception as exc:
            error = str(exc)
        finally:
            restore_or_quarantine(root, child)
        _, result = d.individual.refresh_child(d, child, d.load(d.REGISTRY))
        d.save(evidence / 'attempt.json', dict(session=str(session) if session else None,
            reason=error, state='ATTEMPTED', at=d.now_cn().isoformat()))
        if slot['id'] in result['pendingUiSlots']:
            d.save(evidence.parent / 'blocker.json', dict(reason=error or 'UI attempt did not produce a complete Runner result',
                attemptEvidence=[attempt(child, evidence / 'attempt.json', 'Prepare/run actual UI and restore', error or 'UI incomplete')]))


def preflight(root):
    progress(root, 'PREFLIGHT')
    runtime_check()
    for path in (Path(d.SHELL), d.DOTNET, Path('C:/AutoTest/runtime/dotnet10/dotnet.exe')):
        if not path.is_file():
            raise RuntimeError('Required local tool missing: ' + str(path))
    log_path = root / 'controller' / 'database-preflight.log'
    with log_path.open('w', encoding='utf-8') as log:
        result = d.run_bounded([d.SHELL, '-NoProfile', '-File', str(d.ROOT / 'daily' / 'Ensure-DailyDatabase.ps1')],
            timeout=60, stdout=log, stderr=subprocess.STDOUT)
    if result.returncode:
        raise RuntimeError('Existing local database unavailable; see ' + str(log_path))
    quarantine_path = d.ROOT / 'daily' / 'controller-quarantined-profiles.json'
    quarantine = d.load(quarantine_path, {})
    for profile, entry in list(quarantine.items()):
        session = Path(entry['session']).resolve()
        if not session.is_relative_to((d.ROOT / 'daily' / 'sessions').resolve()):
            raise RuntimeError('Invalid persisted model quarantine session')
        state = d.load(session / 'session.json')
        repo = Path(state['repo']).resolve()
        if repo.parent != d.WORKSPACES.resolve():
            raise RuntimeError('Model quarantine is outside managed workspaces')
        slot = dict(model=state['model'], profileArg=profile.split('-', 1)[1] if '-' in profile else 'Default')
        try:
            ps('Stop', slot, repo, session, root / 'controller' / (profile + '-recovery-stop.log'))
            ps('Restore', slot, repo, session, root / 'controller' / (profile + '-recovery-restore.log'))
        except Exception:
            continue # This profile remains blocked; other commits/profiles continue.
        del quarantine[profile]
    d.save(quarantine_path, quarantine)


def runtime_check():
    executable = model_author.find_transport()
    result = subprocess.run([executable, 'exec', '--help'], capture_output=True, encoding='utf-8', errors='replace', timeout=15)
    if result.returncode or any(flag not in result.stdout for flag in ('--ignore-user-config', '--output-schema', '--json')):
        raise RuntimeError('Installed Codex CLI lacks required structured inference capabilities')
    authentication = subprocess.run([executable, 'login', 'status'], capture_output=True,
        encoding='utf-8', errors='replace', timeout=15)
    if authentication.returncode:
        raise RuntimeError('Existing Codex model login is unavailable in this execution environment')
    receipt = dict(codexExecutable=executable, pythonExecutable=sys.executable,
        cliStructuredInferenceAvailable=True, existingModelLoginAvailable=True, checkedAt=d.now_cn().isoformat(),
        pid=os.getpid(), sessionName=os.environ.get('SESSIONNAME'),
        inheritedPathHasCodex=bool(__import__('shutil').which('codex')))
    d.save(d.ROOT / 'daily' / 'controller-runtime.json', receipt)
    return receipt


def run(root):
    manifest = d.load(root / 'manifest.json')
    if manifest.get('strategy') != 'individual':
        raise ValueError('Controller only accepts individual queue roots')
    provenance = root / 'controller' / ('code-' + d.now_cn().strftime('%Y%m%dT%H%M%S') + '.json')
    d.save(provenance, dict(sources={p.name: d.coverage_contract.digest(p) for p in Path(__file__).parent.glob('*.py')
                                   if not p.name.startswith('test_')}, recordedAt=d.now_cn().isoformat()))
    supervisor = Supervisor(root)
    supervisor.recover()
    original_run = d.run_bounded
    d.run_bounded = supervisor.run
    try:
        registry, sched = d.load(d.REGISTRY), d.load(d.SCHEDULING)
        plan = d.individual.prepare(d, root, manifest, registry, sched)
        for slot in plan['commitSlots']:
            restore_or_quarantine(root, Path(slot['batch']))
            recover_build(Path(slot['batch']))
            execution = d.load(Path(slot['batch']) / 'execution.json', {})
            if execution.get('state') in ('PREPARING', 'BUILDING', 'HEADLESS'):
                child = Path(slot['batch'])
                d.save(child / 'controller' / 'interrupted-execution.json', execution)
                # Recover raw results before another attempt. Existing prepared
                # targets remain SHA/hash verified by normal execution.
                d.individual.refresh_child(d, child, registry)
                d.save(child / 'execution.json', dict(state='RECOVERED', reason='Previous controller exited; owned children inspected'))
        preflight(root)
        progress(root, 'PREPARE_ALL')
        d.queue_driver.prepare_all(d, plan, registry, sched)
        for slot in plan['commitSlots']:
            prepare_commit(root, slot, plan, registry, sched)
        if plan['commitSlots']:
            d.build_runner(root)
        while True:
            progress(root, 'EXECUTING')
            data = d.queue_driver.drain(d, root, manifest, plan, registry, sched)
            if not data['pendingCommitValidations']:
                break
            before = json.dumps(data['pendingCommitValidations'], sort_keys=True)
            child = Path(data['pendingCommitValidations'][0]['batch'])
            ui(root, child)
            after = d.individual.aggregate(d, root, manifest, plan, registry)
            # The next drain consumes raw UI results/blockers and finishes this
            # item. Per-item failure never exits the batch execution loop.
            if not (child / 'ui-request.json').exists() and before == json.dumps(after['pendingCommitValidations'], sort_keys=True):
                raise RuntimeError('Execution made no progress without a concrete UI request')
        data = d.individual.aggregate(d, root, manifest, plan, registry)
        if data['pendingCommitValidations']:
            raise RuntimeError('Completion gate rejected unfinished commits')
        # Run the real CLI completion implementation, inside the already-held
        # operation lock; this is not a second process or a forged exit marker.
        import contextlib
        argv = sys.argv
        try:
            sys.argv = ['daily.py', '--resume', str(root), '--check-complete']
            with (root / 'controller' / 'completion-check-cli.log').open('w', encoding='utf-8') as log:
                with contextlib.redirect_stdout(log):
                    d.main()
        finally:
            sys.argv = argv
        d.save(root / 'controller' / 'completion-check.json', dict(exitCode=0, pendingCommitValidations=[],
            workflowState=data['workflowState'], acceptanceState=data['acceptanceState'], checkedAt=d.now_cn().isoformat()))
        progress(root, 'FINISHED', report=str(root / 'report.md'), statusCounts=data['statusCounts'],
                 acceptanceState=data['acceptanceState'])
        return data
    finally:
        d.run_bounded = original_run


def resolve_resume(path):
    root = path.resolve()
    if not root.is_relative_to((d.ROOT / 'daily' / 'runs').resolve()):
        raise ValueError('Controller batch must be inside daily/runs')
    return root


def collected(root, manifest):
    manifest['strategy'] = 'individual'
    d.save(root / 'manifest.json', manifest)
    progress(root, 'COLLECTED')


def main():
    parser = argparse.ArgumentParser()
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument('--daily', action='store_true', help='Resume the active controller batch, otherwise collect new commits')
    group.add_argument('--resume', type=Path)
    group.add_argument('--status', action='store_true')
    group.add_argument('--runtime-check', action='store_true', help='Validate the real scheduler environment without model calls or collection')
    args = parser.parse_args()
    if args.runtime_check:
        print(json.dumps(runtime_check(), ensure_ascii=False))
        return
    if args.status:
        state = d.load(ACTIVE, dict(phase='NOT_STARTED'))
        state['processAlive'] = bool(state.get('identity') and identity(state.get('pid', 0)) == state['identity'])
        print(json.dumps(state, ensure_ascii=False))
        return
    root = None
    with d.operation_lock.acquire(d.ROOT / 'daily' / '.controller.lock'):
        with d.operation_lock.acquire(d.ROOT / 'daily' / '.operation.lock'):
            try:
                active = d.load(ACTIVE, {})
                if args.resume:
                    root = resolve_resume(args.resume)
                elif active.get('batch') and active.get('phase') != 'FINISHED':
                    root = resolve_resume(Path(active['batch']))
                else:
                    out, manifest = d.collect(0, False, on_collected=collected)
                    manifest['strategy'] = 'individual'
                    d.save(out / 'manifest.json', manifest)
                    root = out
                progress(root, 'STARTING')
                run(root)
                previous = d.load(root / 'manifest.json', {})
                if (args.daily and active.get('phase') != 'FINISHED' and active.get('batch') and
                        previous.get('windowStart') and previous['windowStart'][:10] < d.now_cn().date().isoformat()):
                    # Finishing yesterday's interrupted active batch must not
                    # consume tonight's trigger and silently omit today's commits.
                    root, current = d.collect(0, False, on_collected=collected)
                    current['strategy'] = 'individual'
                    d.save(root / 'manifest.json', current)
                    progress(root, 'STARTING')
                    run(root)
            except model_author.ModelCapacityUnavailable as exc:
                if root:
                    progress(root, 'WAITING_MODEL', reason=str(exc), report=str(root / 'report.md'))
                raise SystemExit(75)
            except Exception as exc:
                if root:
                    progress(root, 'ERROR', reason=str(exc), report=str(root / 'report.md'))
                raise


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stderr.reconfigure(encoding='utf-8')
    main()
