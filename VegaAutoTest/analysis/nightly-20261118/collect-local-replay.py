"""Explicit user-authorized replay; no network sync or daily cursor changes."""
import sys
from pathlib import Path
from datetime import datetime
import shutil
sys.path.insert(0, r'C:\AutoTest\AutoTest\VegaAutoTest\daily')
import daily as d
import model_daily as model

out = d.ROOT / 'daily/runs/manual-model-20261118'
start = datetime.fromisoformat('2026-11-18T00:00:00+08:00')
end = datetime.fromisoformat('2026-11-18T23:30:00+08:00')
with d.operation_lock.acquire(d.ROOT / 'daily/.operation.lock'):
    if (out / 'manifest.json').exists():
        print(out)
        raise SystemExit(0)
    head = d.git('rev-parse', 'master', repo=d.MIRROR)
    log = d.git('log', 'master', f'--format={d.LOG_FORMAT}', repo=d.MIRROR)
    commits = []
    for row in log.split('\x1e'):
        row = row.strip('\n')
        if row and start <= datetime.fromisoformat(row.split('\x1f')[1]) <= end:
            commits.append(d.describe(row, d.MIRROR, start, end, 'window'))
    manifest = dict(schemaVersion=2, batchId=out.name, mode='manual-replay',
        collectedAt=d.now_cn().isoformat(), timezone='Asia/Shanghai',
        source=str(d.MIRROR), head=head, remoteFresh=False,
        windowStart=start.isoformat(), windowEnd=end.isoformat(),
        selection='User-authorized Nov18 replay from local master; remote completeness unverified',
        replayAuthorization='User requested simulating last night and explicitly using local mirror and PAT snapshots',
        commits=commits)
    out.mkdir(parents=True)
    for commit in commits:
        (out / (commit['sha'][:8] + '.diff')).write_text(d.git('show', '--format=fuller',
            '--first-parent', '--no-ext-diff', commit['sha'], repo=d.MIRROR), encoding='utf-8')
    model.initialize(out, manifest)
    cached = d.ROOT / 'analysis/manual-20261118/preparation/workitems.json'
    (out / 'preparation').mkdir()
    shutil.copy2(cached, out / 'preparation/workitems.json')
    d.save(out / 'preparation/source-provenance.json', dict(source=str(cached),
        copiedAt=d.now_cn().isoformat(), note='Existing PAT-fetched snapshot; original retrievedAt/revisions preserved'))
    print(out)
