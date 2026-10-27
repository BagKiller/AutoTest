"""Isolated scheduler contract tests; no remote, builds, Runner or daily cursor writes."""
import importlib.util
import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('daily_under_test', Path(__file__).with_name('daily.py'))
d = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = d
spec.loader.exec_module(d)


class IndividualTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name).resolve() / 'automation'
        self.root.mkdir()
        self.out = self.root / 'daily' / 'runs' / 'test'
        self.out.mkdir(parents=True)
        (self.root / 'probe').mkdir()
        (self.root / 'probe' / 'probe.csproj').write_text('<Project />')
        (self.root / 'probe' / 'Test.cs').write_text('// executable test fixture')
        root_patch = patch.object(d, 'ROOT', self.root)
        root_patch.start()
        self.addCleanup(root_patch.stop)
        self.registry = {'cases': [{'id': 'wi7', 'workItems': [7000], 'tags': [],
                                   'layers': {'logic': {'project': 'probe/probe.csproj'},
                                              'uia': {'project': 'probe/probe.csproj', 'filter': 'FullyQualifiedName~Ui'}}}]}
        self.registry['cases'][0]['layers']['logic']['filter'] = 'FullyQualifiedName~Logic'
        self.sched = {'tagCases': {}, 'baselineCases': [], 'headlessLayerModels': {},
                      'profileOrder': ['Vega300-HWv1'], 'caseProfiles': {'wi7': ['Vega300-HWv1']},
                      'defaultUiProfile': 'Vega300-HWv1', 'hardwareTags': {}}
        self.commits = [dict(sha=c*40, title=c, message='', workItems=[7000], groups=[],
                             files=['code.cs'], modules=[], unmappedFiles=[], noProductChange=False,
                             committedAt='2026-10-23T12:00:00+08:00') for c in ['b', 'a']]
        self.manifest = dict(batchId='test', head='b'*40, source='unused', commits=self.commits,
                             mode='example', remoteFresh=False, historyRewritten=False,
                             windowStart='2026-10-23T00:00:00+08:00', windowEnd='2026-10-23T23:30:00+08:00')

    def prepare(self):
        for c in self.commits:
            (self.out / (c['sha'][:8] + '.diff')).write_text('diff ' + c['sha'])
        return d.individual.prepare(d, self.out, self.manifest, self.registry, self.sched)

    def freeze(self, p, index=0, ui=True, case_ids=None, gap=None, source_state='AVAILABLE'):
        slot = p['commitSlots'][index]
        child = Path(slot['batch'])
        source = child / 'preparation' / 'workitems.json'
        d.save(source, dict(workItems=[dict(id=7000, state=source_state)]))
        requirement = dict(id='acceptance', workItems=[7000], steps=['Run target behavior'],
                           expected='Matches work item expectation', uiRequired=ui,
                           profiles=['Vega300-HWv1'] if ui else [], caseIds=['wi7'] if case_ids is None else case_ids)
        if gap:
            requirement['gap'] = gap
        review = dict(sha=slot['sha'], diffSha256=d.individual.coverage_contract.digest(self.out / (slot['sha'][:8] + '.diff')),
                      workItemsSha256=d.individual.coverage_contract.digest(source), requirements=[requirement])
        review_path = child / 'review-input.json'
        d.save(review_path, review)
        return d.individual.freeze_one(d, p, slot['sha'], self.registry, self.sched, review_path)

    def test_modes_and_legacy_resume(self):
        self.assertEqual(d.resolve_strategy(None, None), 'individual')
        self.assertEqual(d.resolve_strategy(None, {}), 'merged')
        self.assertEqual(d.resolve_strategy(None, {'strategy': 'individual'}), 'individual')
        with self.assertRaises(ValueError):
            d.resolve_strategy('individual', {})

    def test_same_case_has_independent_sha_plans_and_directories(self):
        p = self.prepare()
        self.assertEqual(len(p['commitSlots']), 2)
        targets = []
        for slot in p['commitSlots']:
            child = Path(slot['batch'])
            m = d.load(child/'manifest.json')
            cp = d.load(child/'preparation'/'plan.json')
            self.assertFalse((child/'plan.json').exists())
            self.assertEqual(len(m['commits']), 1)
            self.assertEqual(m['head'], slot['sha'])
            self.assertEqual(cp['headless'][0]['commits'], [slot['sha'][:8]])
            self.assertEqual(cp['uiSlots'][0]['commits'], [slot['sha'][:8]])
            targets.append(m['head'])
        self.assertEqual(set(targets), {'a'*40, 'b'*40})

    def test_previous_item_must_be_finished_and_failure_does_not_block_next(self):
        p = self.prepare()
        with self.assertRaises(ValueError):
            d.individual.run_one(d, self.out, p, p['commitSlots'][1]['sha'], self.registry, self.sched)
        self.freeze(p)
        with patch.object(d, 'execute', side_effect=RuntimeError('test build error')):
            d.individual.run_one(d, self.out, p, p['commitSlots'][0]['sha'], self.registry, self.sched)
        first = Path(p['commitSlots'][0]['batch'])
        self.assertEqual(d.load(first/'summary.json')['commits'][0]['status'], 'ERROR')
        d.individual.finish_one(d, self.out, p, p['commitSlots'][0]['sha'], 'Build failed; UI not executed')
        self.freeze(p, 1)
        with patch.object(d, 'execute') as execute:
            d.individual.run_one(d, self.out, p, p['commitSlots'][1]['sha'], self.registry, self.sched)
            self.assertEqual(execute.call_args.args[1]['head'], p['commitSlots'][1]['sha'])

    def test_merged_strategy_keeps_cross_commit_deduplication(self):
        p = d.plan(self.out, self.manifest, self.registry, self.sched)
        self.assertEqual(len(p['headless']), 1)
        self.assertEqual(len(p['headless'][0]['commits']), 2)

    def test_queue_is_not_acceptance_and_reports_missing_case(self):
        self.registry = {'cases': []}
        p = self.prepare()
        summary = d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        self.assertEqual(summary['acceptanceState'], 'INCOMPLETE')
        self.assertEqual(summary['statusCounts'], {'PENDING': 2})
        self.assertEqual(len(summary['pendingCommitValidations']), 2)
        self.assertEqual(summary['pendingUiSlots'], [])
        self.assertEqual(len(summary['pendingPreparations']), 2)
        self.assertEqual(summary['workflowState'], 'AWAITING_PREPARATION')

    def test_cannot_execute_or_finish_before_review_freeze(self):
        p = self.prepare()
        d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        with self.assertRaises(ValueError):
            d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
        with self.assertRaises(ValueError):
            d.individual.finish_one(d, self.out, p, 'a', 'Do not silently skip preparation')

    def test_required_ui_missing_cannot_be_accepted_despite_logic_pass(self):
        self.registry['cases'][0]['layers'].pop('uia')
        p = self.prepare()
        cp = self.freeze(p)
        cm = d.load(Path(p['commitSlots'][0]['batch']) / 'manifest.json')
        rows = d.evaluate(cm, cp, dict(runs=[dict(caseId='wi7', phase='headless', verdict='PASS', runId='logic-only')]), self.registry)
        self.assertEqual(rows[0]['status'], 'NEEDS_CASE')
        self.assertIn('Required UI case/profile missing', rows[0]['reason'])

    def test_real_case_reuse_and_ui_execution_are_still_required(self):
        p = self.prepare()
        cp = self.freeze(p)
        cm = d.load(Path(p['commitSlots'][0]['batch']) / 'manifest.json')
        runs = [dict(caseId='wi7', phase='headless', verdict='PASS', runId='logic')]
        self.assertEqual(d.evaluate(cm, cp, dict(runs=runs), self.registry)[0]['status'], 'PENDING')
        runs.append(dict(caseId='wi7', phase='ui-Vega300-HWv1', verdict='PASS', runId='actual-ui'))
        self.assertEqual(d.evaluate(cm, cp, dict(runs=runs), self.registry)[0]['status'], 'ACCEPTED')
        with self.assertRaises(ValueError):
            self.freeze(p)

    def test_explicit_gap_is_reported_but_other_tests_can_execute(self):
        p = self.prepare()
        cp = self.freeze(p, case_ids=[], gap='Input dataset unavailable')
        cm = d.load(Path(p['commitSlots'][0]['batch']) / 'manifest.json')
        self.assertEqual(d.evaluate(cm, cp, dict(runs=[]), self.registry)[0]['status'], 'NEEDS_CASE')
        with patch.object(d, 'execute') as execute:
            d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
            execute.assert_called_once()

    def test_source_or_assertion_changes_rejected_after_freeze(self):
        p = self.prepare()
        self.freeze(p)
        (self.root / 'probe' / 'Test.cs').write_text('// changed assertion')
        with patch.object(d, 'execute') as execute:
            d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
            execute.assert_not_called()
        first = Path(p['commitSlots'][0]['batch'])
        self.assertEqual(d.load(first / 'execution.json')['state'], 'ERROR')
        d.individual.finish_one(d, self.out, p, 'a', 'Frozen source changed; not executed; no session prepared')
        self.assertEqual(d.individual.select(d, p, 'b')['sha'], 'b'*40)

    def test_registry_added_after_collection_is_used_at_freeze(self):
        original = self.registry
        self.registry = dict(cases=[])
        p = self.prepare()
        self.registry = original
        cp = self.freeze(p)
        self.assertEqual(cp['headless'][0]['caseId'], 'wi7')
        self.assertTrue(cp['uiSlots'])

    def test_legacy_plans_remain_unchanged(self):
        legacy = dict(schemaVersion=3, strategy='individual', commitSlots=[], uiSlots=[])
        d.save(self.out / 'plan.json', legacy)
        self.assertEqual(self.prepare(), legacy)

    def test_empty_registry_mapping_is_not_executable_coverage(self):
        self.registry['cases'][0]['layers']['logic']['project'] = 'missing.csproj'
        p = self.prepare()
        with self.assertRaises(ValueError):
            self.freeze(p)

    def test_unavailable_work_item_source_forces_gap(self):
        p = self.prepare()
        cp = self.freeze(p, source_state='UNAVAILABLE')
        self.assertTrue(any('requirements unavailable' in gap for r in cp['coverageRequirements'] for gap in r['gaps']))
        # Coverage source retrieval itself has no side effects beyond saved evidence.
        child = Path(p['commitSlots'][1]['batch'])
        original_load = d.load
        def configured_load(path, default=None):
            if path.name == 'config.json':
                return dict(azure_devops=dict(org_url='http://example.invalid', project='test', pat='secret'))
            return original_load(path, default)
        with patch.object(d, 'load', side_effect=configured_load), patch('urllib.request.build_opener', side_effect=TimeoutError('secret must not appear')) as request:
            source = d.individual.coverage_contract.fetch_sources(d, child, d.load(child/'manifest.json'))
            request.assert_called_once()
        self.assertEqual(source['workItems'][0]['state'], 'UNAVAILABLE')
        self.assertNotIn('secret', str(source))

    def invoke_main(self, *args):
        stream = io.StringIO()
        with patch.object(sys, 'argv', ['daily.py', *map(str, args)]), contextlib.redirect_stdout(stream):
            d.main()
        import json
        return json.loads(stream.getvalue().splitlines()[-1])

    def test_cli_collection_is_explicitly_awaiting_preparation_not_execution(self):
        for c in self.commits:
            (self.out / (c['sha'][:8] + '.diff')).write_text('diff ' + c['sha'])
        with patch.object(d, 'collect', return_value=(self.out, self.manifest)), patch.object(d, 'execute') as execute:
            with patch.object(d, 'REGISTRY', self.root / 'registry.json'), patch.object(d, 'SCHEDULING', self.root / 'scheduling.json'):
                d.save(d.REGISTRY, self.registry)
                d.save(d.SCHEDULING, self.sched)
                result = self.invoke_main('--execute')
            execute.assert_not_called()
        self.assertEqual(result['workflowState'], 'AWAITING_PREPARATION')
        self.assertEqual(result['nextAction']['sha'], 'a'*40)
        self.assertEqual(result['pendingUiSlots'], [])

    def test_cli_child_cannot_replan_after_freeze(self):
        p = self.prepare()
        self.freeze(p)
        child = Path(p['commitSlots'][0]['batch'])
        with patch.object(d, 'REGISTRY', self.root / 'registry.json'), patch.object(d, 'SCHEDULING', self.root / 'scheduling.json'):
            d.save(d.REGISTRY, self.registry)
            d.save(d.SCHEDULING, self.sched)
            with self.assertRaisesRegex(ValueError, 'Frozen child plans'):
                self.invoke_main('--resume', child, '--plan-only')

    def test_diff_change_is_detected_before_execution(self):
        p = self.prepare()
        self.freeze(p)
        (self.out / ('a'*8 + '.diff')).write_text('changed')
        with patch.object(d, 'execute') as execute:
            d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
            execute.assert_not_called()
        self.assertEqual(d.load(Path(p['commitSlots'][0]['batch']) / 'execution.json')['state'], 'ERROR')

    def test_unrelated_new_registry_case_does_not_break_frozen_plan(self):
        p = self.prepare()
        self.freeze(p)
        self.registry['cases'].append(dict(id='unrelated', workItems=[9999], layers={}))
        with patch.object(d, 'execute') as execute:
            d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
            execute.assert_called_once()

    def test_cli_full_queue_advances_after_failure_and_requires_child_ui(self):
        p = self.prepare()
        self.manifest['strategy'] = 'individual'
        d.save(self.out/'manifest.json', self.manifest)
        self.freeze(p)
        with patch.object(d, 'REGISTRY', self.root / 'registry.json'), patch.object(d, 'SCHEDULING', self.root / 'scheduling.json'):
            d.save(d.REGISTRY, self.registry)
            d.save(d.SCHEDULING, self.sched)
            with patch.object(d, 'execute', side_effect=RuntimeError('isolated build failure')):
                result = self.invoke_main('--resume', self.out, '--execute', '--run-commit', 'a')
            self.assertEqual(len(result['pendingCommitValidations']), 2)
            self.invoke_main('--resume', self.out, '--finish-commit', 'a', '--completion-reason', 'Build failed; UI untested; no session prepared')
            self.freeze(p, 1)
            def headless(child, manifest, cp, outcomes, budget):
                d.save(child/'execution.json', dict(state='HEADLESS_DONE'))
                d.save(child/'outcomes.json', dict(runs=[dict(caseId='wi7', phase='headless', verdict='PASS', runId='mock-headless')]))
            with patch.object(d, 'execute', side_effect=headless):
                result = self.invoke_main('--resume', self.out, '--execute', '--run-commit', 'b')
            self.assertEqual(result['pendingUiSlots'][0]['batch'], p['commitSlots'][1]['batch'])
            def ui(child, manifest, cp, slot_id, session, outcomes):
                outcomes['runs'].append(dict(caseId='wi7', phase=slot_id, verdict='PASS', runId='mock-ui'))
                d.save(child/'outcomes.json', outcomes)
            with patch.object(d, 'run_slot', side_effect=ui):
                result = self.invoke_main('--resume', p['commitSlots'][1]['batch'], '--run-slot', 'ui-Vega300-HWv1')
            self.assertEqual(result['acceptanceState'], 'COMPLETE')
            result = self.invoke_main('--resume', self.out, '--finish-commit', 'b', '--completion-reason', 'Mock UI complete; mock session restored')
        self.assertEqual(result['pendingCommitValidations'], [])
        self.assertEqual(result['workflowState'], 'FINISHED')
        self.assertEqual(result['statusCounts'], {'ERROR': 1, 'ACCEPTED': 1})
        self.assertEqual(result['acceptanceState'], 'INCOMPLETE')


if __name__ == '__main__':
    unittest.main()
