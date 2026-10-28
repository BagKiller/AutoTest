"""Independent correction runs; original frozen baseline verdicts are not rewritten."""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import daily as d
import artifact_cleanup

root=Path(Path('C:/AutoTest/repair-campaign-path.txt').read_text().strip())
version=sys.argv[1] if len(sys.argv)>1 else '2'
plan=d.load(root/'plan.json');registry=d.load(d.REGISTRY);records=[]
with d.operation_lock.acquire(d.ROOT/'daily'/'.operation.lock'):
    slots=sorted(plan['commitSlots'],key=lambda slot:slot['sha']!=d.load(root/'manifest.json')['head'])
    for slot in slots:
        child=Path(slot['batch'])
        if not d.load(child/'completion.json'):raise RuntimeError('Complete original queue before supplements')
        if not (child/'build-reference.json').exists():
            records.append(dict(sha=slot['sha'],state='NOT_RUN_PRODUCT_BUILD_ERROR'))
            continue
        case_id='daily-recent10-v'+version if slot['sha']==d.load(root/'manifest.json')['head'] else 'daily-pmt-path-separators-v'+version
        case=next(c for c in registry['cases'] if c['id']==case_id)
        out=child/('fix-v'+version);out.mkdir(parents=True,exist_ok=True)
        repo=Path(d.load(child/'execution-target.json')['repo'])
        footprint=d.coverage_contract.case_fingerprints(d,registry,[case_id],transitive=True,linked_sources=True)
        proof=dict(sha=slot['sha'],case=case,footprint=footprint,frozenAt=d.now_cn().isoformat(),
            sourceBuild=d.load(child/'build-reference.json'),scope='Baseline engineering correction only; not complete product/UI acceptance',
            originalPlanSha256=d.coverage_contract.digest(child/'plan.json'))
        if (out/'plan.json').exists():
            if d.load(out/'plan.json')['footprint']!=footprint:raise RuntimeError('Frozen correction sources changed')
            saved=d.load(out/'outcomes.json',{}).get('runs',[])
            if saved:
                records.append(dict(sha=slot['sha'],**saved[-1]));continue
        else:d.save(out/'plan.json',proof)
        print('V2 '+slot['sha'][:8]+' '+case_id,flush=True)
        result=d.run_case(out,repo,out/'logs',case_id,list(case['layers']),'headless',
            sum(l['timeoutSeconds'] for l in case['layers'].values())+180,dict(runs=[]))
        if footprint!=d.coverage_contract.case_fingerprints(d,registry,[case_id],transitive=True,linked_sources=True):
            raise RuntimeError('Correction source changed during execution')
        records.append(dict(sha=slot['sha'],**result))
        d.save(root/('baseline-v'+version+'-results.json'),records)
        artifact_cleanup.reclaim(d,out)
    d.save(root/('baseline-v'+version+'-results.json'),records)
print('Independent baseline correction validation finished',flush=True)
