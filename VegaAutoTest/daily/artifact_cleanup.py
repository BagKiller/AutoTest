"""Reclaim only proven per-layer MSBuild scratch directories, never raw evidence."""
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import os
import subprocess


def reclaim(d, child, artifact_root=None):
    root = (artifact_root or (d.WORKSPACES.parent / 'test-artifacts')).resolve()
    records = []
    # These mappings are created by Runner in result layers. Compilation output
    # is reproducible scratch; result.json/TRX/assertions/screenshots stay put.
    for mapping in child.rglob('test-artifacts.json'):
        layer = mapping.parent.resolve()
        info = json.loads(mapping.read_text(encoding='utf-8-sig'))
        target = Path(info['artifacts']).resolve()
        expected = hashlib.sha256(str(layer).encode()).hexdigest().upper()[:24]
        if target.parent != root or target.name.upper() != expected:
            records.append(dict(mapping=str(mapping), state='SKIPPED', reason='Artifact ownership/path fingerprint mismatch'))
            continue
        receipt = mapping.with_name('test-artifacts-cleanup.json')
        if not target.exists():
            continue
        size = sum(p.stat().st_size for p in target.rglob('*') if p.is_file())
        # The resolved absolute path has now been verified against the fixed
        # workspace scratch root and Runner's exact layer path fingerprint.
        shutil.rmtree(target)
        record = dict(artifacts=str(target), reclaimedBytes=size, state='REMOVED_BUILD_SCRATCH',
                      rawEvidencePreserved=True, at=d.now_cn().isoformat())
        d.save(receipt, record)
        records.append(record)
    return records


def reclaim_workspace(d, child, compress_below_gib=30):
    """Retain source and deployable binaries; drop only untracked build intermediates.

    Completed, controller-owned worktrees only. Historical reports, Git objects,
    the entire Vega/bin deployment and tracked source files are never removed.
    Low-space NTFS compression changes storage metadata, not any file bytes.
    """
    manifest = d.load(child / 'manifest.json', {})
    target = d.load(child / 'execution-target.json', {})
    if not d.load(child / 'completion.json') or not target.get('repo'):
        return dict(state='SKIPPED', reason='Not a completed owned worktree')
    repo = Path(target['repo']).resolve()
    workspace_root = d.WORKSPACES.resolve()
    if (not manifest.get('batchId') or not manifest.get('head') or repo.parent != workspace_root
            or not repo.name.startswith(manifest['batchId'] + '_')):
        return dict(state='SKIPPED', reason='Worktree ownership mismatch')
    if not repo.exists() or d.git('rev-parse', 'HEAD', repo=repo) != manifest.get('head'):
        return dict(state='SKIPPED', reason='Worktree SHA unavailable/mismatched')
    if d.git('status', '--porcelain', '--untracked-files=no', repo=repo):
        return dict(state='SKIPPED', reason='Tracked source changed')
    deployment = (repo / 'Vega' / 'bin').resolve()
    tracked_directories = set()
    for name in d.git('ls-files', '-z', repo=repo).split('\0'):
        if not name:
            continue
        parent = PurePosixPath(name).parent
        while parent != PurePosixPath('.'):
            tracked_directories.add(parent.as_posix().casefold())
            parent = parent.parent
    project_dirs = {p.parent for p in repo.rglob('*.csproj')
                    if not {'bin', 'obj', '.git'}.intersection(p.relative_to(repo).parts)}
    removed, size = [], 0
    for project in sorted(project_dirs):
        for name in ('obj', 'bin'):
            candidate = (project / name).resolve()
            if not candidate.exists() or candidate == repo or not candidate.is_relative_to(repo) or candidate.is_relative_to(deployment):
                continue
            relative = candidate.relative_to(repo).as_posix()
            if relative.casefold() in tracked_directories:
                continue
            # Both containment and Git's absence of tracked inputs are checked
            # before recursive removal. Extended paths handle WPF obj filenames.
            io_path = Path('\\\\?\\' + str(candidate)) if os.name == 'nt' else candidate
            size += sum(p.stat().st_size for p in io_path.rglob('*') if p.is_file())
            shutil.rmtree(io_path)
            removed.append(relative)
    result = dict(state='RECLAIMED_INTERMEDIATES', repo=str(repo), reclaimedBytes=size,
                  removed=removed, sourceAndDeploymentPreserved=True)
    if os.name == 'nt' and shutil.disk_usage(repo).free < compress_below_gib * 1024**3:
        log = child / 'workspace-compression.log'
        with log.open('w', encoding='utf-8') as stream:
            compressed = d.run_bounded(['compact.exe', '/C', '/I', '/Q', '/S:' + str(deployment)],
                stdout=stream, stderr=subprocess.STDOUT, timeout=180)
        result['compressionExitCode'] = compressed.returncode
        result['compressionLog'] = str(log)
    d.save(child / 'workspace-cleanup.json', result)
    return result
