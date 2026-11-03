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
