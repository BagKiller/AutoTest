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
        api = SimpleNamespace(WORKSPACES=workspaces, ROOT=self.root, load=d.load, save=d.save, git=Mock())
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
