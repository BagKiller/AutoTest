"""One HEAD build per nightly batch; every collected commit is analysed and reported separately."""
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
        target.update(sha=manifest['head'], title='最新 HEAD 构建，逐提交测试（%d 个新增提交）' % len(original),
                      message='\n'.join(c['sha'] + ' ' + c['title'] for c in original),
                      files=files, groups=groups, unmappedFiles=unknown,
                      modules=sorted({d.module_of(p) for p in unknown}),
                      workItems=sorted({w for c in original for w in c.get('workItems', [])}),
                      noProductChange=all(c.get('noProductChange') for c in original))
        (out / (manifest['head'][:8] + '.diff')).write_text('\n\n'.join(chunks), encoding='utf-8')
        manifest['commits'] = [target]
    manifest.update(strategy='individual', testingPolicy='latest-head', collectedCommitCount=len(original))
    d.save(out / 'manifest.json', manifest)


def _campaign(d, batch):
    """Root campaign directory and manifest of a queue child (or root) path."""
    batch = Path(batch)
    child_manifest = d.load(batch / 'manifest.json', {}) or {}
    root = Path(child_manifest['parentBatch']) if child_manifest.get('parentBatch') else batch
    return root.resolve(), d.load(root / 'manifest.json', {}) or {}


def reclaim_reason(d, out, manifest, owner):
    """Why the shared workspace may be recreated for `out`, or None to fail closed.

    All callers hold the daily operation lock, so no other daily command is using
    the workspace while this runs. Live Simulator sessions were already stopped and
    their configuration restored by Stop-ManagedVega before recycling.
    """
    previous = Path(owner.get('batch', ''))
    if not owner.get('batch') or not previous.exists():
        return None
    if (previous / 'completion.json').is_file():
        return 'previous-owner-completed'
    owner_root, owner_manifest = _campaign(d, previous)
    requester_root, requester_manifest = _campaign(d, out)
    if owner_root == requester_root:
        # Retry inside the same campaign (failed/interrupted build). The old
        # attempt has no verified build-reference, so a fresh build is required.
        return 'same-campaign-retry'
    state = d.load(owner_root / 'controller' / 'state.json', {}) or {}
    if state.get('phase') == 'SUPERSEDED':
        return 'previous-owner-superseded'
    if requester_manifest.get('mode') == 'daily' and owner_manifest.get('mode') != 'daily':
        # The 23:30 nightly run has priority over sample/revalidation campaigns.
        # Their reports and raw results stay; resuming them rebuilds the workspace.
        return 'nightly-priority-over-' + str(owner_manifest.get('mode') or 'unknown')
    return None


SHARED_MODES = ('daily', 'recent-example')


def uses_shared_workspace(d, out, manifest):
    """Only nightly and --last sample campaigns use daily-latest.

    Targeted revalidation keeps the latest-head analysis policy but builds in its
    own worktree, so a daytime rerun can never break or evict the nightly workspace.
    """
    if manifest.get('testingPolicy') != 'latest-head':
        return False
    _, root_manifest = _campaign(d, out)
    return root_manifest.get('mode', manifest.get('mode')) in SHARED_MODES


def _same(a, b):
    import os
    return os.path.normcase(str(Path(a).resolve())) == os.path.normcase(str(Path(b).resolve()))


def _registered(d, source, repo):
    """Is `repo` still listed as a worktree of the mirror (admin entry present)?"""
    listing = d.git('worktree', 'list', '--porcelain', repo=Path(source))
    return any(line.startswith('worktree ') and _same(line[len('worktree '):], repo)
               for line in listing.splitlines())


def _remove_tree(path):
    import os
    import shutil
    import stat

    def writable(function, name, _):
        os.chmod(name, stat.S_IWRITE)
        function(name)
    target = Path('\\\\?\\' + str(path.resolve())) if os.name == 'nt' else path
    shutil.rmtree(target, onerror=writable)


def _recover_half_removed(d, repo, manifest, owner):
    """A failed `git worktree remove` deletes files but leaves the directory without .git.

    Ownership is proven by the mirror's worktree registration or by the owner
    record naming this exact path; a directory that is itself another repository
    (.git directory) is never touched. Returns a reclaim reason, or None.
    """
    git_entry = repo / '.git'
    source = Path(manifest['source']).resolve()
    if git_entry.is_dir():
        return None  # A full repository: never ours to delete.
    if git_entry.is_file():
        # Worktree link whose admin entry in our mirror was pruned/deleted:
        # also unusable, and provably ours (it points into our mirror).
        text = git_entry.read_text(encoding='utf-8', errors='replace').strip()
        if not text.startswith('gitdir:'):
            return None
        admin = Path(text[len('gitdir:'):].strip())
        if not admin.is_absolute():
            admin = repo / admin
        if admin.exists() or not _same(admin.parent, source / 'worktrees'):
            return None
    registered = _registered(d, manifest['source'], repo)
    recorded = bool(owner.get('repo')) and _same(owner['repo'], repo)
    if not (registered or recorded or git_entry.is_file()):
        raise RuntimeError('Latest workspace has no .git and is neither registered in the mirror nor recorded '
                           'as ours; preserved for manual review: ' + str(repo))
    _remove_tree(repo)
    d.git('worktree', 'prune', repo=Path(manifest['source']))
    return 'half-removed-worktree-cleaned'


def workspace(d, out, manifest, evidence=None):
    """Single shared worktree. Clean, owned, same-repository paths only; dirty/foreign paths fail closed."""
    repo = d.WORKSPACES / 'daily-latest'
    owner_file = d.ROOT / 'daily' / 'latest-workspace-owner.json'
    if repo.is_symlink() or repo.parent.resolve() != d.WORKSPACES.resolve():
        raise RuntimeError('Unsafe latest workspace path')
    if repo.exists():
        owner = d.load(owner_file, {}) or {}
        reason = reclaim_reason(d, out, manifest, owner)
        if reason is None:
            raise RuntimeError('Latest workspace cannot be recycled: owner unknown/missing, or an unfinished '
                               'nightly batch that was not superseded; resume it first: ' + str(owner.get('batch')))
        if evidence is not None and hasattr(d, 'release_workspace'):
            # Free file locks first; otherwise worktree removal can fail half-way.
            d.release_workspace(repo, evidence)
        recovered = _recover_half_removed(d, repo, manifest, owner)
        if recovered:
            reason = reason + '; ' + recovered
        else:
            common = Path(d.git('rev-parse', '--git-common-dir', repo=repo)).resolve()
            if common != Path(manifest['source']).resolve():
                raise RuntimeError('Latest workspace belongs to a different repository')
            if d.git('status', '--porcelain', '--untracked-files=all', repo=repo):
                raise RuntimeError('Latest workspace has unsaved changes; preserved')
            # Normal Git removal refuses dirty/locked worktrees. No forced reset of
            # user files. Recreating at the same path prevents stale deployment DLLs.
            try:
                d.git('worktree', 'remove', '--', str(repo), repo=common)
            except RuntimeError as error:
                # A lock can still abort removal half-way; finish only if Git
                # already deleted the .git file, otherwise keep the original error.
                if not repo.exists():
                    raise
                recovered = _recover_half_removed(d, repo, manifest, owner)
                if not recovered:
                    raise
                reason = reason + '; remove-failed-then-' + recovered + ': ' + str(error)
        d.save(out / 'workspace-reclaim.json', dict(reason=reason, previousOwner=owner,
                                                   repo=str(repo), at=d.now_cn().isoformat()))
    d.save(owner_file, dict(batch=str(out.resolve()), head=manifest['head'], repo=str(repo)))
    return repo


_ORDER = ['FAIL', 'ERROR', 'BLOCKED', 'NEEDS_CASE', 'PENDING', 'NOT_RUN', 'PASSED_AT_HEAD']


def commit_results(d, root, child, runs):
    """Per-collected-commit status derived from the single HEAD child's frozen plan and raw runs."""
    commits = d.load(root / 'requirement-scopes.json') or []
    plan = d.load(child / 'plan.json')
    requirements = (plan or {}).get('coverageRequirements', [])
    finished = (child / 'completion.json').is_file()
    by_case = {}
    for run in runs:
        by_case.setdefault(run['caseId'], []).append(run)
    results = []
    for commit in reversed(commits):  # ancestor first, matching the queue order
        sha = commit['sha']
        wis = {str(w) for w in commit.get('workItems', [])}
        own = [r for r in requirements if r.get('sourceCommit') == sha or
               (not r.get('sourceCommit') and str(r.get('id', '')).startswith('source-')
                and str(r['id'])[len('source-'):] in wis)]
        cases = sorted({cid for r in own for cid in r.get('caseIds', [])})
        gaps = [g for r in own for g in r.get('gaps', [])]
        verdicts = {cid: [x.get('verdict') for x in by_case.get(cid, [])] for cid in cases}
        if commit.get('noProductChange'):
            status, reason = 'NOT_APPLICABLE', '仅文档改动'
        elif plan is None:
            status, reason = 'PENDING', '需求分析/测试准备尚未冻结'
        else:
            flat = [v for vs in verdicts.values() for v in vs]
            # A budget/invocation placeholder has no PASS verdict and never counts as run.
            missing = [cid for cid, vs in verdicts.items() if not vs or any(v != 'PASS' for v in vs)]
            if 'FAIL' in flat:
                status = 'FAIL'
            elif 'ERROR' in flat:
                status = 'ERROR'
            elif 'BLOCKED' in flat:
                status = 'BLOCKED'
            elif gaps or not own or not cases:
                status = 'NEEDS_CASE'
            elif missing:
                status = 'NOT_RUN' if finished else 'PENDING'
            else:
                status = 'PASSED_AT_HEAD'
            reason = '; '.join(gaps) if gaps else ('未运行: ' + ', '.join(missing) if missing else '')
        results.append(dict(sha=sha, title=commit.get('title', ''), workItems=commit.get('workItems', []),
                            status=status, reason=reason, requirementIds=[r['id'] for r in own],
                            caseIds=cases, runs=[dict(caseId=cid, phase=x.get('phase'), runId=x.get('runId'),
                                                      verdict=x.get('verdict'))
                                                 for cid in cases for x in by_case.get(cid, [])]))
    return results


def commit_lines(d, results, head):
    lines = ['', '## 逐提交测试结果（同一 HEAD 构建）', '',
             f'全部用例在 `{head[:12]}` 的一次构建上执行。PASSED_AT_HEAD 表示该提交的需求在最新 HEAD 上通过，'
             '不代表该提交在自己的 SHA 上独立验收（perCommitAcceptance=false）。', '',
             '| 提交 | 标题 | 工作项 | 状态 | 用例 | runId | 说明 |', '| --- | --- | --- | --- | --- | --- | --- |']
    for r in results:
        run_ids = ', '.join(sorted({x['runId'] for x in r['runs'] if x.get('runId')}))
        lines.append(f"| `{r['sha'][:8]}` | {d.md(r['title'])} | {d.md(', '.join(map(str, r['workItems'])))} | "
                     f"{r['status']} | {d.md(', '.join(r['caseIds']))} | {d.md(run_ids)} | {d.md(r['reason'])} |")
    return lines + ['']
