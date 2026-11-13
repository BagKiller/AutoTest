import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import Mock
import daily as d
import latest_head as latest


class LatestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)

    def test_combines_all_requirements_at_one_head_preserving_original_diffs(self):
        commits = [dict(sha=c * 40, title=c + ' #' + str(n), files=[c + '.cs'], workItems=[n], noProductChange=False)
                   for c, n in [('b', 1002), ('a', 1001)]]
        for c in commits:
            (self.root / (c['sha'][:8] + '.diff')).write_text(c['title'])
        manifest = dict(commits=commits, head='b' * 40)
        latest.configure(d, self.root, manifest)
        self.assertEqual(len(manifest['commits']), 1)
        self.assertEqual(manifest['commits'][0]['workItems'], [1001, 1002])
        self.assertEqual(manifest['commits'][0]['files'], ['a.cs', 'b.cs'])
        self.assertEqual(manifest['commits'][0]['sha'], 'b' * 40)
        self.assertEqual(d.load(self.root / 'collected-commits.json'), commits)
        self.assertEqual((self.root / 'collected-diffs/bbbbbbbb.diff').read_text(), 'b #1002')
        self.assertIn('Collected commit ' + 'a' * 40, (self.root / 'bbbbbbbb.diff').read_text())
        latest.configure(d, self.root, manifest)
        self.assertEqual(manifest['collectedCommitCount'], 2)

    def test_empty_batch_has_no_test_target(self):
        m = dict(commits=[], head='a' * 40)
        latest.configure(d, self.root, m)
        self.assertEqual(m['commits'], [])

    def test_existing_plan_cannot_be_migrated(self):
        (self.root / 'plan.json').write_text('{}')
        with self.assertRaises(ValueError):
            latest.configure(d, self.root, dict(commits=[]))

    def test_recycle_requires_completion_and_clean_owned_repository(self):
        workspaces = self.root / 'workspaces'
        (workspaces / 'daily-latest').mkdir(parents=True)
        (workspaces / 'daily-latest' / '.git').write_text('gitdir: fixture')
        api = SimpleNamespace(WORKSPACES=workspaces, ROOT=self.root, load=d.load, save=d.save, git=Mock(), now_cn=d.now_cn)
        owner = self.root / 'daily/latest-workspace-owner.json'
        manifest = dict(source=str(self.root / 'cache.git'), head='b' * 40)
        with self.assertRaises(RuntimeError):
            latest.workspace(api, self.root / 'next', manifest)
        previous = self.root / 'previous'
        d.save(owner, dict(batch=str(previous)))
        d.save(previous / 'completion.json', dict(done=True))
        api.git.side_effect = [manifest['source'], ' M source.cs']
        with self.assertRaises(RuntimeError):
            latest.workspace(api, self.root / 'next', manifest)
        api.git.side_effect = [manifest['source'], '', '']
        result = latest.workspace(api, self.root / 'next', manifest)
        self.assertEqual(result, workspaces / 'daily-latest')
        self.assertEqual(api.git.call_args.args[:2], ('worktree', 'remove'))
        self.assertEqual(d.load(owner)['head'], manifest['head'])

    # ── shared-workspace reclaim rules ──

    def campaign(self, name, mode, phase=None):
        root = self.root / 'runs' / name
        child = root / 'commits' / ('a' * 40)
        d.save(root / 'manifest.json', dict(mode=mode))
        d.save(child / 'manifest.json', dict(parentBatch=str(root)))
        if phase:
            d.save(root / 'controller' / 'state.json', dict(phase=phase))
        return child

    def reclaim(self, owner_child, requester_child):
        workspaces = self.root / 'workspaces'
        (workspaces / 'daily-latest').mkdir(parents=True, exist_ok=True)
        (workspaces / 'daily-latest' / '.git').write_text('gitdir: fixture')
        api = SimpleNamespace(WORKSPACES=workspaces, ROOT=self.root, load=d.load, save=d.save,
                              git=Mock(side_effect=[str(self.root / 'cache.git'), '', '']), now_cn=d.now_cn)
        d.save(self.root / 'daily/latest-workspace-owner.json', dict(batch=str(owner_child)))
        result = latest.workspace(api, requester_child, dict(source=str(self.root / 'cache.git'), head='b' * 40))
        return api, result

    def test_same_campaign_retry_after_failed_build_recreates_workspace(self):
        child = self.campaign('tonight', 'daily')
        api, _ = self.reclaim(child, child)
        self.assertEqual(api.git.call_args.args[:2], ('worktree', 'remove'))
        self.assertEqual(d.load(child / 'workspace-reclaim.json')['reason'], 'same-campaign-retry')

    def test_superseded_owner_does_not_block_next_night(self):
        old = self.campaign('yesterday', 'daily', phase='SUPERSEDED')
        new = self.campaign('tonight', 'daily')
        self.reclaim(old, new)
        self.assertEqual(d.load(new / 'workspace-reclaim.json')['reason'], 'previous-owner-superseded')

    def test_nightly_has_priority_over_unfinished_revalidation(self):
        old = self.campaign('reval', 'targeted-revalidation')
        new = self.campaign('tonight', 'daily')
        self.reclaim(old, new)
        self.assertTrue(d.load(new / 'workspace-reclaim.json')['reason'].startswith('nightly-priority'))

    def test_unfinished_active_nightly_owner_is_preserved(self):
        old = self.campaign('yesterday', 'daily', phase='EXECUTING')
        new = self.campaign('reval', 'targeted-revalidation')
        with self.assertRaises(RuntimeError):
            self.reclaim(old, new)

    # ── per-commit results on the single HEAD build ──

    def test_commit_results_are_reported_per_original_commit(self):
        root = self.root / 'batch'
        child = root / 'commits' / ('h' * 40)
        commits = [dict(sha='c' * 40, title='docs', workItems=[], noProductChange=True),
                   dict(sha='b' * 40, title='fail', workItems=[2]),
                   dict(sha='a' * 40, title='pass', workItems=[1])]
        d.save(root / 'requirement-scopes.json', commits)
        d.save(child / 'plan.json', dict(coverageRequirements=[
            dict(id='a-req', sourceCommit='a' * 40, caseIds=['ca'], gaps=[]),
            dict(id='b-req', sourceCommit='b' * 40, caseIds=['cb'], gaps=[])]))
        runs = [dict(caseId='ca', phase='headless', runId='r1', verdict='PASS'),
                dict(caseId='cb', phase='headless', runId='r2', verdict='FAIL')]
        results = latest.commit_results(d, root, child, runs)
        self.assertEqual([r['sha'][0] for r in results], ['a', 'b', 'c'])
        self.assertEqual([r['status'] for r in results], ['PASSED_AT_HEAD', 'FAIL', 'NOT_APPLICABLE'])

    def test_unrun_or_gapped_commit_never_passes(self):
        root = self.root / 'batch'
        child = root / 'commits' / ('h' * 40)
        d.save(root / 'requirement-scopes.json', [dict(sha='a' * 40, title='x', workItems=[1]),
                                                  dict(sha='b' * 40, title='y', workItems=[2])])
        d.save(child / 'plan.json', dict(coverageRequirements=[
            dict(id='a-req', sourceCommit='a' * 40, caseIds=['ca'], gaps=[]),
            dict(id='b-req', sourceCommit='b' * 40, caseIds=[], gaps=['No executable case'])]))
        runs = [dict(caseId='ca', phase='headless', runId=None, verdict=None)]
        status = {r['sha'][0]: r['status'] for r in latest.commit_results(d, root, child, runs)}
        self.assertEqual(status, dict(b='NEEDS_CASE', a='PENDING'))

    def test_insufficient_info_commit_is_tagged_not_needs_case(self):
        # 2026-11-13 policy: work items without repro steps/acceptance criteria are
        # tagged as insufficient info, distinct from authoring shortfalls.
        root = self.root / 'batch'
        child = root / 'commits' / ('h' * 40)
        d.save(root / 'requirement-scopes.json', [dict(sha='a' * 40, title='tagged', workItems=[1]),
                                                  dict(sha='b' * 40, title='mixed', workItems=[2])])
        d.save(child / 'plan.json', dict(coverageRequirements=[
            dict(id='a-req', sourceCommit='a' * 40, caseIds=[], gaps=['Work item provides title only'],
                 gapKind='INSUFFICIENT_INFO'),
            # Mixed commit: one unguessable scenario plus one executable one that passed.
            dict(id='b-tag', sourceCommit='b' * 40, caseIds=[], gaps=['Work item provides title only'],
                 gapKind='INSUFFICIENT_INFO'),
            dict(id='b-exec', sourceCommit='b' * 40, caseIds=['cb'], gaps=[])]))
        runs = [dict(caseId='cb', phase='headless', runId='r1', verdict='PASS')]
        results = {r['sha'][0]: r for r in latest.commit_results(d, root, child, runs)}
        self.assertEqual(results['a']['status'], 'INSUFFICIENT_INFO')
        # A mixed commit keeps the honest uncovered-scenario status; its reason
        # still names the insufficient-info gap.
        self.assertEqual(results['b']['status'], 'NEEDS_CASE')
        self.assertIn('Work item provides title only', results['b']['reason'])

    # ── half-removed worktree recovery (2026-11-04 nightly failure) ──

    def broken_workspace(self, registered=True, recorded=False):
        workspaces = self.root / 'workspaces'
        repo = workspaces / 'daily-latest'
        (repo / 'Vega' / 'bin').mkdir(parents=True, exist_ok=True)
        (repo / 'Vega' / 'bin' / 'leftover.dll').write_text('locked once')  # no .git: half-removed
        source = self.root / 'cache.git'
        listing = ('worktree ' + str(source) + '\nbare\n\n' +
                   ('worktree ' + repo.as_posix() + '\nHEAD ' + 'a' * 40 + '\ndetached\n' if registered else ''))
        git = Mock(side_effect=lambda *args, repo=None: listing if args[:2] == ('worktree', 'list') else '')
        api = SimpleNamespace(WORKSPACES=workspaces, ROOT=self.root, load=d.load, save=d.save, git=git,
                              now_cn=d.now_cn, release_workspace=Mock())
        tonight = self.campaign('tonight', 'daily')
        owner = dict(batch=str(self.campaign('yesterday', 'daily', phase='SUPERSEDED')))
        if recorded:
            owner['repo'] = str(repo)
        d.save(self.root / 'daily/latest-workspace-owner.json', owner)
        return api, repo, source, tonight

    def test_half_removed_registered_worktree_is_cleaned_and_pruned(self):
        api, repo, source, tonight = self.broken_workspace(registered=True)
        result = latest.workspace(api, tonight, dict(source=str(source), head='b' * 40), self.root / 'evidence')
        self.assertEqual(result, repo)
        self.assertFalse(repo.exists())
        calls = [c.args for c in api.git.call_args_list]
        self.assertIn(('worktree', 'prune'), calls)
        self.assertNotIn(('rev-parse', '--git-common-dir'), calls)
        api.release_workspace.assert_called_once()
        self.assertIn('half-removed-worktree-cleaned', d.load(tonight / 'workspace-reclaim.json')['reason'])

    def test_half_removed_recorded_owner_is_cleaned_even_after_manual_prune(self):
        api, repo, source, tonight = self.broken_workspace(registered=False, recorded=True)
        latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertFalse(repo.exists())

    def test_link_to_pruned_admin_entry_in_our_mirror_is_cleaned(self):
        api, repo, source, tonight = self.broken_workspace(registered=False, recorded=False)
        (repo / '.git').write_text('gitdir: ' + str(source / 'worktrees' / 'daily-latest'))
        latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertFalse(repo.exists())

    def test_full_repository_in_place_is_never_deleted(self):
        api, repo, source, tonight = self.broken_workspace(registered=True)
        (repo / '.git').mkdir()
        api.git = Mock(side_effect=lambda *args, repo=None: 'C:/other/.git' if args[0] == 'rev-parse' else '')
        with self.assertRaisesRegex(RuntimeError, 'different repository'):
            latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertTrue((repo / 'Vega' / 'bin' / 'leftover.dll').exists())

    def test_unproven_directory_without_git_is_preserved(self):
        api, repo, source, tonight = self.broken_workspace(registered=False, recorded=False)
        with self.assertRaisesRegex(RuntimeError, 'preserved for manual review'):
            latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertTrue((repo / 'Vega' / 'bin' / 'leftover.dll').exists())

    def test_remove_failing_half_way_is_completed(self):
        api, repo, source, tonight = self.broken_workspace(registered=True)
        (repo / '.git').write_text('gitdir: fixture')
        listing = 'worktree ' + repo.as_posix() + '\n'

        def git(*args, repo=None):
            if args[:2] == ('worktree', 'remove'):
                (self.root / 'workspaces' / 'daily-latest' / '.git').unlink()  # Git deleted .git, then hit a lock
                raise RuntimeError('Git operation worktree failed (exit 255): Permission denied')
            if args[:2] == ('worktree', 'list'):
                return listing
            return str(source) if args[0] == 'rev-parse' else ''
        api.git = Mock(side_effect=git)
        latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertFalse(repo.exists())
        self.assertIn('remove-failed-then-half-removed', d.load(tonight / 'workspace-reclaim.json')['reason'])

    def test_remove_failure_before_git_file_deleted_keeps_original_error(self):
        api, repo, source, tonight = self.broken_workspace(registered=True)
        (repo / '.git').write_text('gitdir: fixture')

        def git(*args, repo=None):
            if args[:2] == ('worktree', 'remove'):
                raise RuntimeError('locked')
            return str(source) if args[0] == 'rev-parse' else ''
        api.git = Mock(side_effect=git)
        with self.assertRaisesRegex(RuntimeError, 'locked'):
            latest.workspace(api, tonight, dict(source=str(source), head='b' * 40))
        self.assertTrue(repo.exists())

    # ── revalidation never uses the nightly workspace ──

    def test_only_nightly_and_sample_campaigns_share_daily_latest(self):
        policy = dict(testingPolicy='latest-head')
        self.assertTrue(latest.uses_shared_workspace(d, self.campaign('tonight', 'daily'), policy))
        self.assertTrue(latest.uses_shared_workspace(d, self.campaign('sample', 'recent-example'), policy))
        self.assertFalse(latest.uses_shared_workspace(d, self.campaign('reval', 'targeted-revalidation'), policy))
        self.assertFalse(latest.uses_shared_workspace(d, self.campaign('legacy', 'daily'), {}))


class GitErrorTests(unittest.TestCase):
    def test_stderr_reason_is_kept_and_credentials_redacted(self):
        header = 'Authorization: Basic ' + 'QUJDREVGR0hJSktMTU5PUA=='
        stderr = ('fatal: unable to access http://user:secret@win-devops:8080/x: ' + header + '\n'
                  'fatal: not a git repository (or any of the parent directories): .git\n')
        detail = d.git_error_detail(stderr, dict(GIT_CONFIG_VALUE_0=header))
        self.assertIn('not a git repository', detail)
        self.assertNotIn('QUJDREVGR0hJSktMTU5PUA', detail)
        self.assertNotIn('secret', detail)
        self.assertLessEqual(len(detail), 400)
