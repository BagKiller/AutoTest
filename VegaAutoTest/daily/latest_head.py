"""One integrated HEAD validation per new daily batch; immutable historical inputs."""
from pathlib import Path


def configure(d, out, manifest):
    if manifest.get('testingPolicy') == 'latest-head':
        return
    if (out / 'plan.json').exists():
        raise ValueError('Cannot change an existing batch plan')
    original = manifest['commits']
    d.save(out / 'collected-commits.json', original)
    # Keep raw collection evidence; normalize only the new analysis inputs.
    original = [dict(c, workItems=d.work_items(c['title'] + '\n' + c.get('message', '')))
                for c in original]
    d.save(out / 'requirement-scopes.json', original)
    if original:
        # Keep every original diff, including the HEAD diff before replacing its
        # conventional filename with the combined review input.
        diff_dir = out / 'collected-diffs'
        diff_dir.mkdir(exist_ok=True)
        chunks = []
        for commit in original:
            path = out / (commit['sha'][:8] + '.diff')
            content = path.read_text(encoding='utf-8')
            (diff_dir / path.name).write_text(content, encoding='utf-8')
            chunks.append('Collected commit ' + commit['sha'] + '\n' + content)
        target = dict(next((c for c in original if c['sha'] == manifest['head']), original[0]))
        files = sorted({p for c in original for p in c['files']})
        groups, unknown = d.classify(files)
        target.update(sha=manifest['head'], title='最新 HEAD 整合回归（%d 个新增提交）' % len(original),
                      message='\n'.join(c['sha'] + ' ' + c['title'] for c in original),
                      files=files, groups=groups, unmappedFiles=unknown,
                      modules=sorted({d.module_of(p) for p in unknown}),
                      workItems=sorted({w for c in original for w in c.get('workItems', [])}),
                      noProductChange=all(c.get('noProductChange') for c in original))
        (out / (manifest['head'][:8] + '.diff')).write_text('\n\n'.join(chunks), encoding='utf-8')
        manifest['commits'] = [target]
    manifest.update(strategy='individual', testingPolicy='latest-head', collectedCommitCount=len(original))
    d.save(out / 'manifest.json', manifest)


def workspace(d, out, manifest):
    """Recycle only our completed workspace; active/dirty/foreign paths fail closed."""
    repo = d.WORKSPACES / 'daily-latest'
    owner_file = d.ROOT / 'daily' / 'latest-workspace-owner.json'
    if repo.exists():
        owner = d.load(owner_file, {})
        previous = Path(owner.get('batch', ''))
        if not owner.get('batch') or not (previous / 'completion.json').is_file():
            raise RuntimeError('Latest workspace owner has not completed; resume its batch first')
        common = Path(d.git('rev-parse', '--git-common-dir', repo=repo)).resolve()
        if common != Path(manifest['source']).resolve():
            raise RuntimeError('Latest workspace belongs to a different repository')
        if d.git('status', '--porcelain', '--untracked-files=all', repo=repo):
            raise RuntimeError('Latest workspace has unsaved changes; preserved')
        if repo.is_symlink() or repo.resolve().parent != d.WORKSPACES.resolve():
            raise RuntimeError('Unsafe latest workspace path')
        # Normal Git removal refuses dirty/locked worktrees. No forced reset of
        # user files. Recreating at the same path prevents stale deployment DLLs.
        d.git('worktree', 'remove', '--', str(repo), repo=common)
    d.save(owner_file, dict(batch=str(out.resolve()), head=manifest['head'], repo=str(repo)))
    return repo
