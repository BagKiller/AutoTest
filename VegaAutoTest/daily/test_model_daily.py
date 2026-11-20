import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import model_daily as m


class ModelDailyContracts(unittest.TestCase):
    def test_units_keep_commit_pairing_and_skip_unbound(self):
        rows = m.units([
            dict(sha='a', title='fix #3774 #3820', files=['a.cs']),
            dict(sha='b', title='fix #3774', files=['b.cs']),
            dict(sha='c', title='Merged PR 5152', files=['c.cs']),
            dict(sha='d', title='fix #3774', files=['x/packages.lock.json']),
        ])
        self.assertEqual([(r['sha'], r['workItem']) for r in rows],
                         [('a', 3774), ('a', 3820), ('b', 3774), ('c', None), ('d', None)])
        self.assertEqual([r['status'] for r in rows], ['PENDING'] * 3 + ['NOT_APPLICABLE'] * 2)

    def test_delayed_run_cannot_collect_another_day(self):
        with patch.object(m.d, 'collect') as collect:
            with self.assertRaisesRegex(ValueError, 'Target day'):
                m.begin('2000-01-01')
            collect.assert_not_called()

    def test_interrupted_build_cannot_silently_repeat(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            m.d.save(root / 'manifest.json', dict(validationMode=m.MODE, head='abc'))
            m.d.save(root / 'execution.json', dict(state='BUILDING'))
            with patch.object(m.d, 'prepare_target') as prepare:
                with self.assertRaisesRegex(ValueError, 'already attempted'):
                    m.build(root)
                prepare.assert_not_called()

    def test_same_window_reuses_existing_model_batch(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            batch = root / 'daily/runs/existing'
            day = m.d.now_cn().date().isoformat()
            m.d.save(batch / 'manifest.json', dict(mode='daily', validationMode=m.MODE,
                     commits=[], head='abc', windowStart=day + 'T00:00:00+08:00', collectedAt=day + 'T23:30:00+08:00'))
            with patch.object(m.d, 'ROOT', root), patch.object(m.d, 'collect') as collect:
                self.assertEqual(m.begin(day), batch)
                collect.assert_not_called()

    def test_partial_collection_recovers_units_without_overwriting_results(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            manifest = dict(commits=[], head='abc', validationMode=m.MODE)
            m.initialize(root, manifest)
            m.d.save(root / 'model-units.json', {'units': [{'status': 'BLOCKED', 'sha': 'a'}]})
            m.initialize(root, manifest)
            self.assertEqual(m.d.load(root / 'model-units.json')['units'][0]['status'], 'BLOCKED')

    def test_matching_batch_can_resume_after_midnight(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            batch = root / 'daily/runs/yesterday'
            m.d.save(batch / 'manifest.json', dict(mode='daily', validationMode=m.MODE,
                     commits=[], head='abc', windowStart='2000-01-01T00:00:00+08:00'))
            with patch.object(m.d, 'ROOT', root), patch.object(m.d, 'collect') as collect:
                self.assertEqual(m.begin('2000-01-01'), batch)
                collect.assert_not_called()

    def test_same_window_legacy_batch_prevents_duplicate(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            day = m.d.now_cn().date().isoformat()
            m.d.save(root / 'daily/runs/legacy/manifest.json', dict(mode='daily',
                     windowStart=day + 'T00:00:00+08:00'))
            with patch.object(m.d, 'ROOT', root), patch.object(m.d, 'collect') as collect:
                with self.assertRaisesRegex(ValueError, 'legacy batch'):
                    m.begin(day)
                collect.assert_not_called()

    def test_finish_rejects_pass_without_runner(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'abc.diff').write_text('synthetic diff')
            m.initialize(root, dict(head='abc', commits=[dict(sha='abc', title='fix #3774', message='', files=['a.cs'])]))
            rows = m.d.load(root / 'model-units.json')
            rows['units'][0]['status'] = 'PASSED_AT_HEAD'
            m.d.save(root / 'model-units.json', rows)
            with self.assertRaisesRegex(ValueError, 'Missing Runner'):
                m.finish(root)
            self.assertFalse((root / 'completion.json').exists())

    def test_finish_rejects_omitted_pair(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            m.d.save(root / 'manifest.json', dict(validationMode=m.MODE, head='abc'))
            m.d.save(root / 'requirement-scopes.json', [dict(sha='abc', title='fix #3774', files=['a.cs'])])
            m.d.save(root / 'model-units.json', {'units': []})
            with self.assertRaisesRegex(ValueError, 'omitted'):
                m.finish(root)

    def test_finish_rejects_skip_that_only_cites_an_empty_field(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'abc.diff').write_text('synthetic diff')
            m.initialize(root, dict(head='abc', commits=[dict(sha='abc', title='fix #3822', message='', files=['a.cs'])]))
            note = root / 'note.txt'
            note.write_text('description already states the behavior')
            rows = m.d.load(root / 'model-units.json')
            rows['units'][0].update(
                status='INSUFFICIENT_INFO',
                reason='缺少关键校验信息：描述里已有预期，但复现步骤为空，不能编造，所以不启动',
                attemptEvidence=[dict(path=str(note))])
            m.d.save(root / 'model-units.json', rows)
            (root / 'report.md').write_text('skipped before launch')
            with self.assertRaisesRegex(ValueError, 'field is empty'):
                m.finish(root)

    def test_finish_accepts_skip_when_key_facts_are_missing(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'abc.diff').write_text('synthetic diff')
            m.initialize(root, dict(head='abc', commits=[dict(sha='abc', title='fix #3822', message='', files=['a.cs'])]))
            note = root / 'note.txt'
            note.write_text('title only')
            rows = m.d.load(root / 'model-units.json')
            rows['units'][0].update(
                status='INSUFFICIENT_INFO',
                reason='缺少关键校验信息：描述为空，diff 只有注释，没有要打开的页面、没有输入、看不出怎样算对',
                attemptEvidence=[dict(path=str(note))])
            m.d.save(root / 'model-units.json', rows)
            (root / 'report.md').write_text('skipped: key facts missing')
            result = m.finish(root)
            self.assertEqual(result['statusCounts']['INSUFFICIENT_INFO'], 1)

    def test_finish_distinguishes_all_skipped_from_acceptance(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / 'abc.diff').write_text('synthetic diff')
            m.initialize(root, dict(head='abc', commits=[dict(sha='abc', title='no work item', message='', files=['a.cs'])]))
            (root / 'report.md').write_text('Skipped: no bound work item')
            result = m.finish(root)
            self.assertEqual(result['acceptanceState'], 'ALL_SKIPPED')
            self.assertEqual(result['runnerRunCount'], 0)


if __name__ == '__main__':
    unittest.main()
