"""Validate new adapters before freezing, with originals and failures retained."""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import daily as d

root=Path(Path('C:/AutoTest/repair-campaign-path.txt').read_text().strip())
registry=d.load(d.REGISTRY)
status=[]
with d.operation_lock.acquire(d.ROOT/'daily'/'.operation.lock'):
    d.build_runner(root)
    for slot in d.load(root/'plan.json')['commitSlots']:
        child=Path(slot['batch']);cm=d.load(child/'manifest.json')
        if not (child/'build-reference.json').exists():
            failure=d.load(child/'preparation'/'build-check-error.json',{})
            target=d.load(child/'execution-target.json',{})
            log=Path(target['evidence'])/'build'/'build.log'
            errors=[line.split(': error ',1)[1].split(' [',1)[0] for line in log.read_text(encoding='utf-8',errors='replace').splitlines() if ': error ' in line]
            failure.update(sha=slot['sha'],phase='coverage-build',state='ERROR',reason='; '.join(dict.fromkeys(errors)),
                           evidence=str(log),evidenceSha256=d.coverage_contract.digest(log))
            d.save(child/'preparation'/'case-check-error.json',failure)
            d.save(child/'execution.json',failure)
            item=dict(sha=slot['sha'],state='PRODUCT_BUILD_ERROR',reason=failure['reason'])
        else:
            a=d.load(child/'preparation'/'authoring.json');cid=a['caseId'];repo=Path(d.load(child/'execution-target.json')['repo'])
            print('CHECK '+slot['sha'][:8],flush=True)
            try:
                d.queue_driver.check_cases(d,child,cm,registry,[cid],repo)
                case=next(c for c in registry['cases'] if c['id']==cid)
                trial=child/'preparation'/'trial';outcomes=dict(runs=[])
                entry=d.run_case(trial,repo,trial/'logs',cid,list(case['layers']),'headless',
                                 sum(l['timeoutSeconds'] for l in case['layers'].values())+180,outcomes)
                item=dict(sha=slot['sha'],state='TRIAL_'+entry['verdict'],runId=entry.get('runId'),reason=entry.get('verdictReason'))
            except Exception as error:
                item=dict(sha=slot['sha'],state='CHECK_ERROR',reason=str(error))
        status.append(item);d.save(root/'trial-progress.json',status)
        print(str(item),flush=True)
