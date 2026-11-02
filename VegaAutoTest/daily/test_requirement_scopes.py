import tempfile
import unittest
from pathlib import Path
from unittest.mock import Mock
import daily as d
import requirement_scopes as scopes
import autopilot as a


class ScopeTests(unittest.TestCase):
    def test_failed_scope_keeps_other_scenarios_and_uses_complete_individual_diff(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            child = root / 'commits/head'
            commits = [dict(sha=c * 40, workItems=[wi], noProductChange=False) for c, wi in [('a', 1001), ('b', 1002)]]
            d.save(root / 'requirement-scopes.json', commits)
            d.save(child / 'preparation/workitems.json', dict(workItems=[dict(id=1001), dict(id=1002)]))
            (root / 'collected-diffs').mkdir()
            for c in commits:
                (root / 'collected-diffs' / (c['sha'][:8] + '.diff')).write_text('x' * 160000 + c['sha'])
            manifest = dict(head='c' * 40, parentBatch=str(root), testingPolicy='latest-head')
            def generate(api, scope, cm, registry, sched, campaign, *args):
                self.assertEqual(cm['head'], 'c' * 40)
                self.assertEqual(len(cm['commits']), 1)
                source = Path(d.load(scope / 'scope.json')['diff']).read_text()
                self.assertTrue(source.endswith(cm['commits'][0]['sha']))
                if cm['analysisSourceCommit'] == 'b' * 40:
                    raise ValueError('Invalid second scenario')
                req = dict(id='acceptance', workItems=[1001], caseIds=['valid-case'], uiRequired=False)
                receipt = dict(requirements=[req], response=dict(files=[], cases=[], requirements=[]), responsePath=str(scope / 'response.json'))
                d.save(scope / 'preparation/authored.json', receipt)
                return receipt
            generate_mock = Mock(side_effect=generate)
            receipt = scopes.generate(d, child, manifest, {}, {}, root, generate_mock, None, a.gap_requirements)
            self.assertEqual(receipt['requirements'][0]['caseIds'], ['valid-case'])
            self.assertEqual(receipt['requirements'][1]['uiAssessment'], 'UNKNOWN')
            self.assertEqual([x['state'] for x in receipt['response']['scopes']], ['GENERATED', 'FAILED'])
            # A restart reuses the successful scope rather than regenerating it.
            scopes.generate(d, child, manifest, {}, {}, root, generate_mock, None, a.gap_requirements)
            self.assertEqual(generate_mock.call_count, 3)
