"""Summarize recent retained evidence without executing or changing any batch."""
import collections
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

DAILY = Path(__file__).resolve().parents[2]


def read(path, default=None):
    return json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else default


def inspect_batch(root):
    summary = read(root / 'summary.json', {})
    state = read(root / 'controller' / 'state.json', {})
    result = dict(batch=root.name, phase=state.get('phase'),
        collectedCommits=summary.get('collectedCommitCount'),
        acceptanceState=summary.get('acceptanceState'),
        statusCounts=summary.get('statusCounts'),
        commitResults=[dict(sha=c['sha'], status=c['status'], caseIds=c['caseIds'])
            for c in summary.get('commitResults', [])],
        officialRuns=[{key: row.get(key) for key in
            ('caseId', 'verdict', 'verdictCode', 'runId', 'resultPath')}
            for row in summary.get('runs', [])],
        unverifiedUiCount=len(summary.get('unverifiedUiSlots', [])), preparations=[])
    provenance = sorted((root / 'controller').glob('code-*.json'))
    if provenance:
        hashes = read(provenance[0])['sources']
        result['controllerSourcesChangedSinceRun'] = [name for name, digest in hashes.items()
            if hashlib.sha256((DAILY / name).read_bytes()).hexdigest() != digest]
    for path in root.glob('commits/*/preparation/case-check.json'):
        receipt = read(path)
        child = path.parents[1]
        records = receipt['records']
        counter = collections.Counter((r.get('runtime', {}).get('verdict'),
            r.get('runtime', {}).get('verdictCode')) for r in records)
        authored = read(path.parent / 'authored.json', {})
        plan = read(child / 'plan.json', {})
        item = dict(receipt=str(path), records=len(records),
            compileOrDiscoveryFailures=sum(r['exitCode'] != 0 for r in records),
            runtimeCounts=[dict(verdict=v, code=c, count=n) for (v, c), n in counter.items()],
            generatedCaseCount=len({cid for req in authored.get('requirements', [])
                for cid in req.get('caseIds', [])}),
            frozenCaseCount=len(plan.get('frozenCases', {}).get('cases', [])),
            candidates=[], scopes=[])
        for row in records:
            log = child / row['log']
            errors = list(dict.fromkeys(re.findall(r'error (CS\d+): (.*?)(?: \[|$)',
                log.read_text(encoding='utf-8', errors='replace'), re.M)))
            runtime = row.get('runtime', {})
            candidate = dict(caseId=row['caseId'], layer=row['layer'],
                compileExitCode=row['exitCode'], discoveryLog=str(log),
                compileErrors=errors, runtimeVerdict=runtime.get('verdict'),
                runtimeCode=runtime.get('verdictCode'), runtimeResult=runtime.get('resultPath'))
            if runtime.get('resultPath'):
                messages = []
                for trx in Path(runtime['resultPath']).parent.glob('*/trx/*.trx'):
                    tree = ET.parse(trx)
                    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
                    for error in tree.findall('.//t:ErrorInfo/t:Message', ns):
                        messages.append((error.text or '')[:1600])
                candidate['testErrorMessages'] = list(dict.fromkeys(messages))
            item['candidates'].append(candidate)
        for scope in sorted((path.parent / 'scopes').glob('*')):
            requests = []
            for request in sorted(scope.glob('preparation/model/*/request.txt')):
                payload = json.loads(request.read_text(encoding='utf-8').split('\nINPUT DATA:\n', 1)[1])
                requests.append(dict(folder=request.parent.name,
                    remainingSourceRequests=payload.get('remainingSourceRequests'),
                    compilerRepair=payload.get('compilerRepair', False),
                    sourcesCount=len(payload.get('sources', {})),
                    sourceCharacters=sum(map(len, payload.get('sources', {}).values())),
                    uiFindContractIncluded='uiFind' in payload or any('UiFind.cs' in key
                        for key in payload.get('sources', {}))))
            item['scopes'].append(dict(sha=scope.name,
                budget=read(scope / 'preparation' / 'author-budget.json', {}), requests=requests))
        result['preparations'].append(item)
    return result


if __name__ == '__main__':
    results = [inspect_batch(root) for root in sorted((DAILY / 'runs').glob('202611*T*'))
        if '20261107' <= root.name[:8] <= '20261110']
    output = Path(__file__).with_name('evidence.json')
    output.write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps([dict(batch=r['batch'], collected=r['collectedCommits'],
        acceptance=r['acceptanceState'], officialRuns=len(r['officialRuns']),
        preparations=[dict(records=p['records'], compileFailures=p['compileOrDiscoveryFailures'],
            runtime=p['runtimeCounts']) for p in r['preparations']])
        for r in results], ensure_ascii=False, indent=2))
