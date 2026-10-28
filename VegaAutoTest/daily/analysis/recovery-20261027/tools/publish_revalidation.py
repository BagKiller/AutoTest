"""Attach a completed, explicitly authorized revalidation without rewriting history."""
import argparse
from pathlib import Path
import shutil
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
import daily as d
import coverage_preparation as coverage

p=argparse.ArgumentParser()
p.add_argument('original',type=Path)
p.add_argument('campaign',type=Path)
a=p.parse_args()
original,campaign=a.original.resolve(),a.campaign.resolve()
with d.operation_lock.acquire(d.ROOT/'daily/.operation.lock'):
    source=d.load(original/'manifest.json'); manifest=d.load(campaign/'manifest.json')
    if not campaign.is_relative_to(original) or campaign==original or manifest.get('revalidationOf')!=str(original):
        raise ValueError('Not an explicitly derived child campaign')
    if {c['sha'] for c in source['commits']}!={c['sha'] for c in manifest['commits']}:
        raise ValueError('Commit sets differ')
    registry=d.load(d.REGISTRY)
    summary=d.individual.aggregate(d,campaign,manifest,d.load(campaign/'plan.json'),registry)
    if summary['pendingCommitValidations']:
        raise ValueError('Cannot publish an unfinished workflow')
    for name in ('summary.json','report.md'):
        dest=original/(name.replace('.', '-before-revalidation.',1))
        if not dest.exists():shutil.copy2(original/name,dest)
    paths=[original/'manifest.json',original/'plan.json',*original.glob('*.diff')]
    for child in (original/'commits').iterdir():
        if not child.is_dir():continue
        paths.extend(child.glob('manifest.json'))
        paths.extend(child.glob('scope.json'))
        paths.extend(child.glob('execution-target.json'))
        paths.extend(child.glob('attempts/*/build/build-provenance.json'))
        paths.extend(child.glob('plan.json'))
        paths.extend(child.glob('preparation/*.json'))
        paths.extend(child.glob('runner/*/result.json'))
        paths.extend(child.glob('runner/*/report.md'))
    hashes={p.relative_to(original).as_posix():coverage.digest(p) for p in paths}
    d.save(original/'revalidation-link.json',dict(campaign=str(campaign),linkedAt=d.now_cn().isoformat(),originalEvidenceSha256=hashes))
    for commit in summary['commits']:
        child=original/'commits'/commit['sha']
        report=child/'report.md'
        backup=child/'report-before-revalidation.md'
        if not backup.exists():shutil.copy2(report,backup)
        current=Path(commit['report'])
        relative=Path('../../')/campaign.name/'commits'/commit['sha']/'report.md'
        heading=(f"# 2026-10-27 用户授权专项复验\n\n当前流程已收尾；验收状态：{commit['status']}。\n\n"
                 f"本 SHA 专项报告：[report]({relative.as_posix()})。原始报告：[历史报告](report-before-revalidation.md)。\n\n"
                 "原始冻结计划和 Runner 结果保持原样。后续结果来自独立构建及本次实际 UI，不把流程完成视作完整验收。\n\n")
        report.write_text(heading+backup.read_text(encoding='utf-8'),encoding='utf-8')
        historical=d.load(child/'summary.json')
        historical['revalidation']=dict(status=commit['status'],workflowFinished=True,report=str(current),campaign=str(campaign),completion=commit['completion'])
        d.save(child/'summary.json',historical)
    result=d.individual.aggregate(d,original,source,d.load(original/'plan.json'),registry)
    print(d.json.dumps(dict(workflowState=result['workflowState'],acceptanceState=result['acceptanceState'],statusCounts=result['statusCounts'],pendingCommitValidations=result['pendingCommitValidations'],report=result['report']),ensure_ascii=False))
