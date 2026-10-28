"""Read-only inventory of automation source, registry and all available daily runs."""
import collections
import hashlib
import json
from pathlib import Path

root=Path(__file__).resolve().parents[3]
output=Path(__file__).resolve().parent
inventory=[]
for directory in [root/name for name in ['daily','runner','harness','l2','probes']] + [root.parent/'code_indicator']:
    for p in directory.rglob('*'):
        if p.is_file() and p.name not in ('config.json','marked.min.js') and p.suffix in ('.cs','.csproj','.py','.ps1','.md','.json','.ts','.js') and not {
            'bin','obj','runs','analysis','workitems','cache.git','__pycache__','sessions','node_modules','dist'}.intersection(p.relative_to(directory).parts):
            content=p.read_bytes()
            inventory.append(dict(path=str(p.relative_to(root.parent)),bytes=len(content),sha256=hashlib.sha256(content).hexdigest()))
batches=[]; protected={}
for p in sorted((root/'daily'/'runs').glob('*/summary.json')):
    j=json.loads(p.read_text(encoding='utf-8-sig'));codes=collections.Counter();reasons=collections.Counter()
    # Original raw runs, separately from explicitly authorized revalidation.
    paths=list((p.parent/'runner').glob('*/result.json'))+list((p.parent/'commits').glob('*/runner/*/result.json'))
    for r in paths:
        value=json.loads(r.read_text(encoding='utf-8-sig'));codes[value['verdict']+'/'+value['verdictCode']]+=1
        if value['verdict']!='PASS':reasons[value.get('verdictReason','')]+=1
    batches.append(dict(batch=p.parent.name,acceptance=j.get('acceptanceState'),workflow=j.get('workflowState'),
        status=j.get('statusCounts'),pending=len(j.get('pendingCommitValidations',[])),originalRuns=len(paths),
        verdictCodes=dict(codes),originalFailureReasons=dict(reasons)))
    files=[p,p.parent/'report.md',p.parent/'manifest.json',p.parent/'plan.json']+paths+list((p.parent/'commits').glob('*/plan.json'))
    for f in files:
        if f.is_file():protected[str(f.relative_to(root))]=hashlib.sha256(f.read_bytes()).hexdigest()
registry=json.loads((root/'cases'/'registry.json').read_text(encoding='utf-8-sig'))
issues=[]
for case in registry['cases']:
    for name,layer in case.get('layers',{}).items():
        project=root/layer['project']
        if not project.exists():issues.append(dict(caseId=case['id'],layer=name,issue='missing project',path=layer['project']))
        if not layer.get('filter'):issues.append(dict(caseId=case['id'],layer=name,issue='missing filter'))
        if project.exists() and ('C:\\AutoTest\\workspaces\\' in project.read_text(encoding='utf-8-sig')):
            issues.append(dict(caseId=case['id'],layer=name,issue='hardcoded temporary product source',path=layer['project']))
(output/'source-inventory.json').write_text(json.dumps(inventory,ensure_ascii=False,indent=2),encoding='utf-8')
(output/'daily-audit.json').write_text(json.dumps(dict(batches=batches,registryIssues=issues),ensure_ascii=False,indent=2),encoding='utf-8')
receipt=output/'original-evidence-hashes.json'
if not receipt.exists():receipt.write_text(json.dumps(protected,ensure_ascii=False,indent=2),encoding='utf-8')
else:
    old=json.loads(receipt.read_text(encoding='utf-8'));drift=[name for name,sha in old.items() if hashlib.sha256((root/name).read_bytes()).hexdigest()!=sha]
    if drift:raise RuntimeError('Original evidence drift: '+str(drift))
print(json.dumps(dict(sourceFiles=len(inventory),dailyBatches=len(batches),registryIssues=len(issues),originalEvidencePreserved=True)))
