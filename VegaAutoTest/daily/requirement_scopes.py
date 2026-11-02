"""Analyze original changes separately while testing one immutable product HEAD."""
from pathlib import Path
import copy


def generate(d, child, manifest, registry, sched, root, generate_one, repair, gap_factory):
    campaign = Path(manifest['parentBatch'])
    commits = d.load(campaign / 'requirement-scopes.json')
    if commits is None:
        raise ValueError('Latest HEAD batch lacks original requirement scopes; create a new batch')
    combined, entries = [], []
    previous = {s['sourceCommit']: s for s in (repair or {}).get('response', {}).get('scopes', [])}
    for commit in commits:
        if commit.get('noProductChange'):
            continue
        scope = child / 'preparation' / 'scopes' / commit['sha']
        cm = dict(manifest, commits=[commit], analysisSourceCommit=commit['sha'])
        cm.pop('testingPolicy', None)
        d.save(scope / 'manifest.json', cm)
        d.save(scope / 'scope.json', dict(sha=manifest['head'],
            diff=str(campaign / 'collected-diffs' / (commit['sha'][:8] + '.diff'))))
        sources = d.load(child / 'preparation' / 'workitems.json', {})
        d.save(scope / 'preparation' / 'workitems.json', dict(sources,
            workItems=[x for x in sources.get('workItems', []) if x['id'] in commit.get('workItems', [])]))
        d.save(scope / 'preparation' / 'attachments' / 'index.json',
            d.load(child / 'preparation' / 'attachments' / 'index.json', {}))
        try:
            old = previous.get(commit['sha'])
            receipt = d.load(scope / 'preparation' / 'authored.json')
            if repair and old and old.get('response'):
                receipt = generate_one(d, scope, cm, registry, sched, root,
                    dict(response=old['response'], diagnostics=repair['diagnostics']))
            elif receipt is None:
                receipt = generate_one(d, scope, cm, registry, sched, root)
            requirements = copy.deepcopy(receipt['requirements'])
            entry = dict(sourceCommit=commit['sha'], response=receipt['response'],
                         responsePath=receipt['responsePath'], state='GENERATED')
        except Exception as error:
            # Capacity is resumable; do not consume the batch as permanent gaps.
            from model_author import ModelCapacityUnavailable
            if isinstance(error, ModelCapacityUnavailable):
                raise
            if repair and old and old.get('response'):
                # Preserve the previous candidate for per-case discovery and
                # failure isolation; never delete other scopes on repair failure.
                saved = d.load(scope / 'preparation' / 'authored.json')
                if saved:
                    requirements = copy.deepcopy(saved['requirements'])
                    entry = dict(old, state='REPAIR_FAILED', reason=str(error))
                else:
                    requirements = gap_factory(cm, str(error), d.load(scope / 'preparation' / 'candidate-response.json'))
                    entry = dict(sourceCommit=commit['sha'], state='FAILED', reason=str(error))
            else:
                requirements = gap_factory(cm, str(error), d.load(scope / 'preparation' / 'candidate-response.json'))
                entry = dict(sourceCommit=commit['sha'], state='FAILED', reason=str(error))
        for req in requirements:
            req['id'] = commit['sha'][:12] + '-' + req['id']
            req['sourceCommit'] = commit['sha']
        combined.extend(requirements)
        entries.append(entry)
        # Durable progress after EACH scope. Restart retains completed work.
        d.save(child / 'preparation' / 'scope-progress.json', entries)
    response = dict(scopes=entries)
    path = child / 'preparation' / ('scoped-response-repair.json' if repair else 'scoped-response.json')
    d.save(path, response)
    receipt = dict(requirements=combined, response=response, responsePath=str(path),
                   generatedAt=d.now_cn().isoformat(), version='scoped')
    d.save(child / 'preparation' / 'authored.json', receipt)
    return receipt
