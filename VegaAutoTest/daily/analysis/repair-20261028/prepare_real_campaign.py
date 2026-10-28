"""One-off regression of the repaired pipeline on the 2026-10-27 commit set.
No original frozen artifact is changed. Each build is from its own exact SHA.
"""
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import daily as d

campaign = Path(Path('C:/AutoTest/repair-campaign-path.txt').read_text().strip())
manifest = d.load(campaign / 'manifest.json')
plan = d.load(campaign / 'plan.json')
status = []
with d.operation_lock.acquire(d.ROOT / 'daily' / '.operation.lock'):
    for slot in plan['commitSlots']:
        child = Path(slot['batch'])
        cm = d.load(child / 'manifest.json')
        print('BUILD ' + slot['sha'][:8], flush=True)
        try:
            target = d.load(child / 'execution-target.json', {})
            if not target:
                repo, _ = d.prepare_target(child, cm)
            else:
                repo = Path(target['repo'])
            registry = d.load(d.REGISTRY)
            authored = d.load(child / 'preparation' / 'authoring.json', {})
            ids = [authored['caseId']] if authored.get('caseId') else []
            if ids:
                d.queue_driver.check_cases(d, child, cm, registry, ids, repo)
                item = dict(sha=slot['sha'], state='DISCOVERED', cases=ids)
            else:
                d.save(child / 'execution.json', dict(state='COVERAGE_PREPARATION', evidence=target.get('evidence')))
                item = dict(sha=slot['sha'], state='BUILT_NEEDS_AUTHORED_CASE')
        except Exception as exc:
            item = dict(sha=slot['sha'], state='PREPARATION_ERROR', reason=str(exc))
            # A bounded build failure is no longer an active build.
            d.save(child / 'preparation' / 'build-check-error.json', item)
            d.save(child / 'execution.json', dict(state='PREPARATION_ERROR', reason=str(exc)))
        status.append(item)
        d.save(campaign / 'preparation-progress.json', status)
        print(json.dumps(item), flush=True)
