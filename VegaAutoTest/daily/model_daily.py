"""Collect/build support for the Cursor CLI nightly check.

This module never generates tests, drives the UI, or decides acceptance.
The scheduled Cursor CLI agent owns those actions and observes the actual screen.
"""
import argparse
import hashlib
import json
from collections import Counter
from pathlib import Path

import daily as d
import latest_head

MODE = 'model-interactive'


def initialize(root, manifest):
    """Persist the recoverable batch before the collection cursor advances."""
    manifest.update(validationMode=MODE, perCommitAcceptance=False)
    d.save(root / 'manifest.json', manifest)
    latest_head.configure(d, root, manifest)
    queue = root / 'model-units.json'
    if not queue.exists():
        d.save(queue, dict(units=units(d.load(root / 'requirement-scopes.json', []))))
    state = root / 'model-state.json'
    if not state.exists():
        d.save(state, dict(phase='AWAITING_MODEL', updatedAt=d.now_cn().isoformat()))


def units(commits):
    rows = []
    for commit in commits:
        sha = commit['sha']
        ids = d.work_items(commit.get('title', '') + '\n' + commit.get('message', ''))
        files = commit.get('files') or []
        lock_only = bool(files) and all(Path(p).name == 'packages.lock.json' for p in files)
        if lock_only or not ids:
            rows.append(dict(sha=sha, workItem=None, status='NOT_APPLICABLE',
                             reason='仅自动更新 NuGet 锁文件，按每日规则忽略' if lock_only
                             else '提交说明未匹配到绑定工作项，不做校验，跳过'))
        else:
            rows.extend(dict(sha=sha, workItem=wi, status='PENDING') for wi in ids)
    return rows


def begin(day):
    roots = []
    for path in (d.ROOT / 'daily' / 'runs').glob('*/manifest.json'):
        manifest = d.load(path, {})
        if manifest.get('mode') == 'daily' and manifest.get('windowStart', '')[:10] == day:
            roots.append((path.parent, manifest))
    if roots:
        root, manifest = max(roots, key=lambda item: item[1].get('collectedAt', ''))
        if manifest.get('validationMode') != MODE:
            raise ValueError('This window already has a legacy batch; preserve it, do not duplicate')
        initialize(root, manifest)
        return root
    # Existing target windows may resume after midnight, but new collection may not.
    if day != d.now_cn().date().isoformat():
        raise ValueError('Target day differs from current Beijing date; do not create another window')
    root, manifest = d.collect(0, False, on_collected=initialize)
    return root


def requirements(root):
    import coverage_preparation
    import requirement_assets
    manifest = d.load(root / 'manifest.json', {})
    if manifest.get('validationMode') != MODE:
        raise ValueError('Only model-interactive batches are accepted')
    # Fetch only matched units, never PR numbers or unrelated HEAD-wide references.
    ids = sorted({row['workItem'] for row in d.load(root / 'model-units.json')['units']
                  if row.get('workItem') is not None})
    sources = coverage_preparation.fetch_sources(d, root, {'commits': [{'workItems': ids}]})
    requirement_assets.fetch(d, root)
    return dict(workitems=str(root / 'preparation/workitems.json'),
                states=[dict(id=row['id'], state=row['state']) for row in sources['workItems']])


def build(root):
    manifest = d.load(root / 'manifest.json', {})
    if manifest.get('validationMode') != MODE:
        raise ValueError('Only new model-interactive batches are accepted')
    reference = d.load(root / 'build-reference.json', {})
    target = d.load(root / 'execution-target.json', {})
    dll = Path(target.get('repo', '')) / 'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.dll'
    if reference:
        repo = Path(target.get('repo', ''))
        if (target.get('head') != manifest['head'] or reference.get('commit') != manifest['head'] or not dll.is_file()
                or hashlib.sha256(dll.read_bytes()).hexdigest().upper() != reference.get('vegaDllSha256', '').upper()):
            raise ValueError('Existing build fingerprint mismatch; do not rebuild silently')
        if d.git('rev-parse', 'HEAD', repo=repo) != manifest['head'] or d.git('status', '--porcelain', repo=repo):
            raise ValueError('Existing product workspace changed; preserve evidence')
        for key, name in [('batchEditorDllSha256', 'RecipeBatchEditor/RecipeBatchEditor.dll'),
                          ('componentEditorDllSha256', 'ComponentConfigEditor/ComponentConfigEditor.dll')]:
            expected = reference.get(key)
            if expected:
                path = dll.parent / name
                if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest().upper() != expected.upper():
                    raise ValueError('Existing build dependency fingerprint mismatch: ' + name)
        ensure_runner(root)
        return target
    if (root / 'execution.json').exists():
        raise ValueError('A build was already attempted; inspect evidence instead of rebuilding')
    if not any(row['status'] == 'PENDING' for row in d.load(root / 'model-units.json')['units']):
        return dict(skipped=True, reason='No bound work-item units to validate')
    ensure_runner(root)
    repo, evidence = d.prepare_target(root, manifest)
    return dict(repo=str(repo), evidence=str(evidence), head=manifest['head'])


def ensure_runner(root):
    """An old Runner DLL may lack --registry even when product build is current."""
    ref = d.load(root / 'runner-reference.json', {})
    if ref:
        binary = Path(ref['binary'])
        if not binary.is_file() or hashlib.sha256(binary.read_bytes()).hexdigest() != ref['sha256']:
            raise ValueError('Runner changed after its build; do not use a stale executable')
        for name, digest in ref.get('sources', {}).items():
            path = d.ROOT / name
            if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
                raise ValueError('Runner source changed after build')
    else:
        d.build_runner(root)


def accept_sparse_skip(reason):
    """Empty repro or acceptance fields are not, by themselves, a reason to stop."""
    text = reason or ''
    if '缺少关键校验信息' not in text:
        raise ValueError('Skip must state 缺少关键校验信息 and name what is missing')
    cites_empty_field = any(word in text for word in ('复现步骤', '验收标准', 'ReproSteps', 'AcceptanceCriteria'))
    cites_real_content = any(word in text for word in ('描述', 'diff', '预期', '标题'))
    refuses_to_try = any(word in text for word in ('不启动', '不编造', '不能编造', '禁止'))
    if cites_empty_field and cites_real_content and refuses_to_try:
        raise ValueError('Other information already supports a UI check; do not skip only because a field is empty')


def require_open_vega(root, row):
    """A bound unit is not finished until Vega was actually opened and restored."""
    sessions = row.get('sessionPaths') or []
    if not sessions:
        raise ValueError('Bound unit did not open Vega')
    target = d.load(root / 'execution-target.json', {})
    for session in sessions:
        state = d.load(Path(session) / 'session.json', {})
        repo = state.get('repo')
        expected_repo = target.get('repo')
        if (state.get('state') != 'RESTORED' or not repo or not expected_repo
                or Path(repo).resolve() != Path(expected_repo).resolve()):
            raise ValueError('Session not restored')
        for key, name in [('ComponentConfig', 'ComponentConfig.json'), ('HardwareProfiles', 'HardwareProfiles.json')]:
            path = Path(state['bin']) / 'Resources' / name
            if hashlib.sha256(path.read_bytes()).hexdigest().upper() != state['originalHashes'][key].upper():
                raise ValueError('Restored configuration hash mismatch')
        machine = state.get('machineConfiguration')
        if machine and state.get('machineOriginalSha256'):
            if hashlib.sha256(Path(machine).read_bytes()).hexdigest().upper() != state['machineOriginalSha256'].upper():
                raise ValueError('Machine configuration hash mismatch')
        elif machine and state.get('machineExisted') is False and Path(machine).exists():
            raise ValueError('New machine configuration was not removed on restore')


def finish(root):
    """Check model-authored accounting against immutable Runner/session evidence."""
    manifest = d.load(root / 'manifest.json', {})
    if manifest.get('validationMode') != MODE:
        raise ValueError('Only model-interactive batches are accepted')
    expected = {(x['sha'], x['workItem']): x for x in units(d.load(root / 'requirement-scopes.json', []))}
    rows = d.load(root / 'model-units.json')['units']
    actual = [(x['sha'], x.get('workItem')) for x in rows]
    if len(actual) != len(set(actual)) or set(actual) != set(expected):
        raise ValueError('Unit pairing changed or some collected commits were omitted')
    runs = set()
    for row in rows:
        initial = expected[(row['sha'], row.get('workItem'))]
        status = row.get('status')
        if initial['status'] == 'NOT_APPLICABLE':
            if status != 'NOT_APPLICABLE':
                raise ValueError('Skipped commit status changed')
            continue
        if status not in ('PASSED_AT_HEAD', 'FAIL', 'ERROR', 'BLOCKED', 'INSUFFICIENT_INFO', 'NEEDS_CASE'):
            raise ValueError('Bound work-item remains pending or was incorrectly skipped')
        if row.get('resultPath'):
            path = Path(row['resultPath']).resolve()
            if not path.is_relative_to(root.resolve()):
                raise ValueError('Result must belong to this batch')
            result = d.load(path)
            mapping = {'PASS': 'PASSED_AT_HEAD', 'FAIL': 'FAIL', 'ERROR': 'ERROR', 'BLOCKED': 'BLOCKED'}
            if (mapping.get(result.get('verdict')) != status or result.get('workItemId') != row['workItem']
                    or result.get('target', {}).get('gitCommit') != manifest['head']
                    or not result.get('runId')):
                raise ValueError('Unit status/target/work item does not match Runner result')
            runs.add(result['runId'])
            row.update(verdict=result['verdict'], verdictCode=result.get('verdictCode'), runId=result['runId'])
            require_open_vega(root, row)
        elif status in ('PASSED_AT_HEAD', 'FAIL') or not row.get('reason') or not row.get('attemptEvidence'):
            raise ValueError('Missing Runner result or concrete gap evidence')
        else:
            accept_sparse_skip(row.get('reason'))
            for item in row['attemptEvidence']:
                if not isinstance(item, dict) or not item.get('path'):
                    raise ValueError('Gap evidence must reference a real file')
                path = Path(item['path']).resolve()
                if not path.is_relative_to(root.resolve()) or not path.is_file() or not path.stat().st_size:
                    raise ValueError('Gap evidence is missing or outside this batch')
    if not (root / 'report.md').is_file():
        raise ValueError('Model report is missing')
    counts = dict(Counter(row['status'] for row in rows))
    bound = sum(row.get('workItem') is not None for row in rows)
    acceptance = ('NO_COMMITS' if not rows else 'ALL_SKIPPED' if not bound else
                  'HAS_FAIL' if counts.get('FAIL') else 'PASSED_AT_HEAD' if counts.get('PASSED_AT_HEAD') == bound else 'INCOMPLETE')
    summary = dict(testingPolicy='latest-head', perCommitAcceptance=False, testedHead=manifest['head'],
                   collectedCommitCount=manifest['collectedCommitCount'], boundUnitCount=bound,
                   runnerRunCount=len(runs), statusCounts=counts, statusCountsScope='commit-work-item-units',
                   acceptanceState=acceptance, unitResults=rows)
    d.save(root / 'summary.json', summary)
    state = dict(phase='FINISHED' if acceptance in ('NO_COMMITS', 'ALL_SKIPPED', 'PASSED_AT_HEAD') else 'FINISHED_WITH_GAPS',
                 acceptanceState=acceptance, updatedAt=d.now_cn().isoformat())
    d.save(root / 'model-state.json', state)
    d.save(root / 'completion.json', dict(state, evidence=str(root / 'summary.json')))
    return summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument('--begin', metavar='BEIJING_DATE')
    action.add_argument('--build', type=Path)
    action.add_argument('--requirements', type=Path)
    action.add_argument('--finish', type=Path)
    args = parser.parse_args()
    with d.operation_lock.acquire(d.ROOT / 'daily' / '.operation.lock'):
        if args.begin:
            print(json.dumps(dict(batch=str(begin(args.begin))), ensure_ascii=False))
        else:
            root = (args.build or args.requirements or args.finish).resolve()
            if root.parent != (d.ROOT / 'daily' / 'runs').resolve():
                raise ValueError('Expected a direct daily root batch')
            print(json.dumps(finish(root) if args.finish else requirements(root) if args.requirements else build(root), ensure_ascii=False))


if __name__ == '__main__':
    main()
