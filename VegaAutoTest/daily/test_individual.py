"""Isolated scheduler contract tests; no remote, builds, Runner or daily cursor writes."""
import importlib.util
import contextlib
import io
import sys
import tempfile
import unittest
import subprocess
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
        self.discovery_receipt(child, slot['sha'], requirement['caseIds'])
        if not requirement['caseIds'] or gap:
            log = child / 'preparation' / 'authoring-attempt.log'
            log.write_text('Fixture: attempted generation; dataset unavailable')
            requirement['attemptEvidence'] = [dict(path=str(log.relative_to(child)),
                sha256=d.coverage_contract.digest(log), action='Generate fixture', outcome='Dataset unavailable')]
            d.save(review_path, review)
        return d.individual.freeze_one(d, p, slot['sha'], self.registry, self.sched, review_path)

    def discovery_receipt(self, child, sha, ids):
        records = []
        for case in self.registry['cases']:
            if case['id'] not in ids:
                continue
            for layer in case['layers']:
                log = child / 'preparation' / (case['id'] + '-' + layer + '.log')
                log.write_text('Fixture discovered: ' + layer)
                records.append(dict(caseId=case['id'], layer=layer, exitCode=0, discoveredTests=[layer],
                    log=str(log.relative_to(child)), logSha256=d.coverage_contract.digest(log)))
        d.save(child / 'preparation' / 'case-check.json', dict(sha=sha, records=records,
            fingerprints=d.coverage_contract.case_fingerprints(d, self.registry, ids, transitive=True)))

    def test_modes_and_legacy_resume(self):
        self.assertEqual(d.resolve_strategy(None, None), 'individual')
        self.assertEqual(d.resolve_strategy(None, {}), 'merged')
        self.assertEqual(d.resolve_strategy(None, {'strategy': 'individual'}), 'individual')
        with self.assertRaises(ValueError):
            d.resolve_strategy('individual', {})

    def test_duplicate_operation_is_rejected_and_lock_releases(self):
        import operation_lock
        path = self.root / 'lock'
        with operation_lock.acquire(path):
            with self.assertRaisesRegex(RuntimeError, 'already running'):
                with operation_lock.acquire(path):
                    self.fail('second owner admitted')
        with operation_lock.acquire(path):
            pass

    def test_completed_headless_cannot_be_accidentally_relaunched(self):
        p = self.prepare()
        self.freeze(p)
        child = Path(p['commitSlots'][0]['batch'])
        d.save(child / 'execution.json', dict(state='HEADLESS_DONE'))
        with patch.object(d, 'execute') as execute:
            with self.assertRaisesRegex(ValueError, 'already completed'):
                d.individual.run_one(d, self.out, p, 'a', self.registry, self.sched)
            execute.assert_not_called()

    def test_unindexed_raw_result_is_preserved_in_aggregate(self):
        self.registry['cases'][0]['layers'].pop('uia')
        p = self.prepare()
        self.freeze(p, ui=False)
        child = Path(p['commitSlots'][0]['batch'])
        d.save(child/'runner'/'orphan'/'result.json', dict(runId='orphan', caseId='wi7',
            verdict='FAIL', verdictCode='ASSERTION_FAILED', target=dict(gitCommit='a'*40)))
        summary = d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        self.assertEqual(summary['runs'][0]['runId'], 'orphan')
        self.assertEqual(summary['runs'][0]['verdict'], 'FAIL')

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

    def test_completion_check_cannot_collect_or_advance_cursor(self):
        with patch.object(d, 'collect') as collect:
            with self.assertRaisesRegex(ValueError, 'never collect'):
                self.invoke_main('--check-complete')
            collect.assert_not_called()

    def test_revalidation_link_preserves_original_and_checks_evidence(self):
        p = self.prepare()
        old = d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        d.save(self.out/'summary-before-revalidation.json', old)
        campaign = self.out/'revalidation-test'
        manifest = dict(self.manifest, batchId='revalidation-test', revalidationOf=str(self.out))
        d.save(campaign/'manifest.json', manifest)
        cp = d.individual.prepare(d, campaign, manifest, self.registry, self.sched)
        d.save(campaign/'plan.json', cp)
        protected = self.out/'plan.json'
        digest = d.individual.coverage_contract.digest(protected)
        d.save(self.out/'revalidation-link.json', dict(campaign=str(campaign), linkedAt='test',
            originalEvidenceSha256={'plan.json':digest}))
        linked = d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        self.assertEqual(linked['batchId'], self.manifest['batchId'])
        self.assertEqual(linked['originalRun'], old)
        self.assertEqual(d.individual.coverage_contract.digest(protected), digest)
        self.assertEqual(len(linked['pendingCommitValidations']), 2)
        protected.write_text('{}')
        with self.assertRaisesRegex(ValueError, 'Original frozen evidence changed'):
            d.individual.aggregate(d, self.out, self.manifest, p, self.registry)

    def test_revalidation_link_rejects_different_commit_set(self):
        p = self.prepare()
        campaign = self.out/'revalidation-test'
        d.save(campaign/'manifest.json', dict(self.manifest, revalidationOf=str(self.out), commits=[]))
        d.save(self.out/'revalidation-link.json', dict(campaign=str(campaign), linkedAt='test', originalEvidenceSha256={}))
        with self.assertRaisesRegex(ValueError, 'same original commit set'):
            d.individual.aggregate(d, self.out, self.manifest, p, self.registry)

    def test_linked_historical_child_cannot_execute(self):
        p = self.prepare()
        d.save(self.out/'revalidation-link.json', dict(campaign='unused'))
        with patch.object(d, 'execute') as execute:
            with self.assertRaisesRegex(ValueError, 'Historical frozen batch'):
                self.invoke_main('--resume', p['commitSlots'][0]['batch'], '--execute')
            execute.assert_not_called()

    def test_completion_check_fails_until_queue_is_finished(self):
        self.prepare()
        self.manifest['strategy']='individual'
        d.save(self.out/'manifest.json',self.manifest)
        with patch.object(d,'REGISTRY',self.root/'registry.json'), patch.object(d,'SCHEDULING',self.root/'scheduling.json'):
            d.save(d.REGISTRY,self.registry)
            d.save(d.SCHEDULING,self.sched)
            with self.assertRaises(SystemExit) as error:
                self.invoke_main('--resume',self.out,'--check-complete')
            self.assertEqual(error.exception.code,2)

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

    def test_drain_runs_second_commit_after_first_build_failure(self):
        p = self.prepare()
        self.registry['cases'][0]['layers'].pop('uia')
        self.freeze(p, ui=False)
        self.freeze(p, index=1, ui=False)
        executed = []
        def execute(child, manifest, cp, outcomes, budget):
            executed.append(manifest['head'])
            if manifest['head'].startswith('a'):
                raise RuntimeError('Fixture isolated build error')
            d.save(child/'execution.json', dict(state='HEADLESS_DONE'))
            outcomes['runs'].append(dict(caseId='wi7', phase='headless', verdict='PASS', runId='fixture-second'))
            d.save(child/'outcomes.json', outcomes)
        with patch.object(d, 'execute', side_effect=execute):
            result = d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
        self.assertEqual(executed, ['a'*40, 'b'*40])
        self.assertEqual(result['workflowState'], 'FINISHED')
        self.assertEqual(result['statusCounts'], {'ERROR': 1, 'ACCEPTED': 1})
        # Restart is idempotent: completed SHA cannot rebuild accidentally.
        with patch.object(d, 'execute') as rerun:
            d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
            rerun.assert_not_called()

    def test_drain_preflights_whole_queue_before_starting_first_build(self):
        p = self.prepare()
        self.freeze(p)
        with patch.object(d, 'execute') as execute, self.assertRaisesRegex(ValueError, 'ALL commits'):
            d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
        execute.assert_not_called()

    def test_drain_does_not_finish_required_ui_without_execution(self):
        p = self.prepare()
        self.freeze(p)
        self.freeze(p, index=1)
        child = Path(p['commitSlots'][0]['batch'])
        d.save(child/'execution.json', dict(state='HEADLESS_DONE'))
        with patch.object(d, 'execute') as execute:
            result = d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
            execute.assert_not_called()
        self.assertEqual(len(result['pendingCommitValidations']), 2)
        self.assertTrue((child/'ui-request.json').exists())
        self.assertFalse((child/'completion.json').exists())

    def test_drain_accepts_verified_ui_blocker_and_continues_without_claiming_ui_pass(self):
        p = self.prepare()
        self.freeze(p)
        self.freeze(p, index=1)
        for slot in p['commitSlots']:
            child = Path(slot['batch'])
            d.save(child/'execution.json', dict(state='HEADLESS_DONE'))
            cp = d.load(child/'plan.json')
            for ui in cp['uiSlots']:
                log = child/'ui'/ui['id']/'attempt.log'
                log.parent.mkdir(parents=True, exist_ok=True)
                log.write_text('Fixture actual desktop request failed: desktop unavailable')
                d.save(log.with_name('blocker.json'), dict(reason='Desktop unavailable', attemptEvidence=[
                    dict(path=str(log.relative_to(child)), sha256=d.coverage_contract.digest(log))]))
        with patch.object(d, 'execute') as execute, patch.object(d, 'REGISTRY', self.root/'registry.json'):
            d.save(d.REGISTRY, self.registry)
            result = d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
            execute.assert_not_called()
        self.assertEqual(result['workflowState'], 'FINISHED')
        self.assertTrue(result['unverifiedUiSlots'])
        self.assertNotEqual(result['acceptanceState'], 'COMPLETE')

    def test_changed_or_empty_ui_blocker_does_not_finish_queue(self):
        p = self.prepare()
        self.freeze(p)
        self.freeze(p, index=1)
        child = Path(p['commitSlots'][0]['batch'])
        d.save(child/'execution.json', dict(state='HEADLESS_DONE'))
        ui = d.load(child/'plan.json')['uiSlots'][0]
        log = child/'ui'/ui['id']/'attempt.log'
        log.parent.mkdir(parents=True)
        log.write_bytes(b'')
        d.save(log.with_name('blocker.json'), dict(reason='Fixture', attemptEvidence=[
            dict(path=str(log.relative_to(child)), sha256=d.coverage_contract.digest(log))]))
        result = d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
        self.assertEqual(len(result['pendingCommitValidations']), 2)
        log.write_text('changed after blocker receipt')
        result = d.queue_driver.drain(d, self.out, self.manifest, p, self.registry, self.sched)
        self.assertEqual(len(result['pendingCommitValidations']), 2)

    def test_prepare_all_continues_after_one_authoring_failure(self):
        p = self.prepare()
        with patch.object(d.individual, 'prepare_one') as prepare, patch.object(
                d.case_authoring, 'generate', side_effect=[RuntimeError('unsupported source'), None]) as generate:
            d.queue_driver.prepare_all(d, p, self.registry, self.sched)
        self.assertEqual(prepare.call_count, 2)
        self.assertEqual(generate.call_count, 2)
        child = Path(p['commitSlots'][0]['batch'])
        self.assertEqual(d.load(child/'preparation'/'authoring.json')['state'], 'NEEDS_AUTHORING')
        self.assertFalse((child/'plan.json').exists())

    def test_deleted_product_test_is_not_retrieved_or_treated_as_executable(self):
        p = self.prepare()
        child = Path(p['commitSlots'][0]['batch'])
        cm = d.load(child/'manifest.json')
        cm['commits'][0]['files'] = ['A.Tests/Deleted.cs']
        with patch.object(d, 'git', return_value='') as git:
            result = d.case_authoring.generate(d, child, cm, self.registry)
        self.assertEqual(result['state'], 'NEEDS_AUTHORING')
        self.assertEqual(git.call_count, 1)
        self.assertEqual(git.call_args.args[0], 'ls-tree')

    def test_queue_actions_cannot_silently_run_on_merged_or_child_batches(self):
        for strategy in ('merged', 'individual-commit'):
            for action in ('--drain', '--prepare-all', '--check-complete'):
                d.save(self.out/'manifest.json', dict(self.manifest, strategy=strategy))
                with patch.object(sys, 'argv', ['daily.py', '--resume', str(self.out), action]):
                    with self.assertRaisesRegex(ValueError, 'individual root'):
                        d.main()
        self.assertFalse((self.out/'execution.json').exists())

    def test_completion_check_cannot_start_execution_and_revalidation_rejects_queue_flags(self):
        for flags in (['--resume', str(self.out), '--check-complete', '--execute'],
                      ['--revalidate-batch', str(self.out), '--drain']):
            with patch.object(sys, 'argv', ['daily.py', *flags]), self.assertRaises(ValueError):
                d.main()
        self.assertFalse((self.out/'manifest.json').exists())

    def test_gap_text_alone_is_not_an_authoring_attempt(self):
        p = self.prepare()
        slot = p['commitSlots'][0]
        child = Path(slot['batch'])
        with self.assertRaisesRegex(ValueError, 'actual authoring'):
            d.coverage_contract.validate_preparation(d, child, d.load(child/'manifest.json'), self.registry,
                [dict(id='remote', caseIds=[], gap='No registered cases')])

    def test_discovery_log_drift_rejected(self):
        p = self.prepare()
        child = Path(p['commitSlots'][0]['batch'])
        self.discovery_receipt(child, 'a'*40, ['wi7'])
        (child/'preparation'/'wi7-logic.log').write_text('Changed discovery output')
        with self.assertRaisesRegex(ValueError, 'discovery log'):
            d.coverage_contract.validate_preparation(d, child, d.load(child/'manifest.json'), self.registry,
                [dict(id='scenario', caseIds=['wi7'])])

    def test_shared_harness_change_invalidates_discovery(self):
        shared = self.root/'shared'
        shared.mkdir()
        (shared/'shared.csproj').write_text('<Project />')
        (shared/'Shared.cs').write_text('// fixture dependency')
        (self.root/'probe'/'probe.csproj').write_text('<Project><ItemGroup><ProjectReference Include="../shared/shared.csproj" /></ItemGroup></Project>')
        p = self.prepare()
        child = Path(p['commitSlots'][0]['batch'])
        self.discovery_receipt(child, 'a'*40, ['wi7'])
        (shared/'Shared.cs').write_text('// dependency drift')
        with self.assertRaisesRegex(ValueError, 'sources changed'):
            d.coverage_contract.validate_preparation(d, child, d.load(child/'manifest.json'), self.registry,
                [dict(id='scenario', caseIds=['wi7'])])

    def test_commit_scoped_case_never_leaks_to_other_sha(self):
        self.registry['cases'][0]['targetSha'] = 'b'*40
        p = self.prepare()
        first = d.load(Path(p['commitSlots'][0]['batch'])/'preparation'/'plan.json')
        second = d.load(Path(p['commitSlots'][1]['batch'])/'preparation'/'plan.json')
        self.assertFalse(first['headless'])
        self.assertTrue(second['headless'])

    def test_case_generator_records_need_to_author_without_fabricating_tests(self):
        p = self.prepare()
        child = Path(p['commitSlots'][0]['batch'])
        result = d.case_authoring.generate(d, child, d.load(child/'manifest.json'), self.registry)
        self.assertEqual(result['state'], 'NEEDS_AUTHORING')
        self.assertIsNone(result['caseId'])

    def test_generated_projects_preserve_friend_assembly_names_and_original_assertions(self):
        p = self.prepare()
        child = Path(p['commitSlots'][0]['batch'])
        cm = d.load(child/'manifest.json')
        cm['commits'][0]['files'] = ['A.Tests/One.cs', 'B.Tests/Two.cs']
        source = 'namespace Fixture; public class Checks { [Fact] public void Original() { Assert.Equal(1, 2); } }'
        def git(*args, **kwargs):
            return '\n'.join(cm['commits'][0]['files']) if args[0] == 'ls-tree' else source
        with patch.object(d, 'git', side_effect=git), patch.object(d, 'REGISTRY', self.root/'registry.json'):
            result = d.case_authoring.generate(d, child, cm, self.registry)
        case = next(c for c in self.registry['cases'] if c['id'] == result['caseId'])
        self.assertEqual(len(case['layers']), 2)
        self.assertIn('partial-acceptance', case['tags'])
        project = self.root/case['layers']['logic0']['project']
        self.assertIn('<AssemblyName>A.Tests</AssemblyName>', project.read_text())
        self.assertIn('Assert.Equal(1, 2)', (project.parent/'Original/00_One.cs').read_text())

    def test_hardcoded_external_source_rejected_before_freeze(self):
        (self.root/'probe'/'probe.csproj').write_text('<Project><ItemGroup><Compile Include="C:/retired-worktree/Old.cs" /></ItemGroup></Project>')
        with self.assertRaisesRegex(ValueError, 'Hardcoded external'):
            d.coverage_contract.case_fingerprints(d, self.registry, ['wi7'], transitive=True)

    def test_checked_build_cannot_be_finished_as_executed(self):
        p = self.prepare()
        self.freeze(p, ui=False)
        child = Path(p['commitSlots'][0]['batch'])
        d.save(child/'execution.json', dict(state='COVERAGE_CHECKED'))
        d.individual.refresh_child(d, child, self.registry)
        with self.assertRaisesRegex(ValueError, 'not test execution'):
            d.individual.finish_one(d, self.out, p, 'a', 'Discovery finished')

    def test_missing_required_ui_remains_visible_without_any_ui_slot(self):
        self.registry['cases'][0]['layers'].pop('uia')
        p = self.prepare()
        self.freeze(p)
        result = d.individual.aggregate(d, self.out, self.manifest, p, self.registry)
        self.assertFalse(result['pendingUiSlots'])
        self.assertEqual(result['unverifiedUiSlots'][0]['kind'], 'missing-ui-coverage')

    def test_failed_baseline_cannot_be_hidden_by_passing_work_item_case(self):
        p = self.prepare()
        cp = self.freeze(p, ui=False)
        cp['clusters'].append(dict(id='baseline',kind='baseline',label='baseline',commits=[],cases=['regression']))
        cp['casePhases']['regression']=['headless']
        cm=d.load(Path(p['commitSlots'][0]['batch'])/'manifest.json')
        for verdict in ['FAIL','ERROR','BLOCKED']:
            rows=d.evaluate(cm,cp,dict(runs=[dict(caseId='wi7',phase='headless',verdict='PASS',runId='fixture-work-item'),
                dict(caseId='regression',phase='headless',verdict=verdict,runId='fixture-regression')]),self.registry)
            self.assertEqual(rows[0]['status'],verdict)

    def test_preparation_build_can_freeze_but_changed_discovery_evidence_cannot_execute(self):
        p=self.prepare();child=Path(p['commitSlots'][0]['batch'])
        d.save(child/'execution.json',dict(state='COVERAGE_CHECKED'))
        self.freeze(p,ui=False)
        (child/'preparation'/'wi7-logic.log').write_text('changed after freeze')
        with patch.object(d,'execute') as execute:
            d.individual.run_one(d,self.out,p,'a',self.registry,self.sched)
            execute.assert_not_called()
        self.assertEqual(d.load(child/'execution.json')['state'],'ERROR')

    def test_linked_source_outside_project_directory_is_frozen(self):
        (self.root/'SharedStub.cs').write_text('// original shared logger')
        (self.root/'probe'/'probe.csproj').write_text('<Project><ItemGroup><Compile Include="../SharedStub.cs" /></ItemGroup></Project>')
        p=self.prepare();self.freeze(p,ui=False)
        (self.root/'SharedStub.cs').write_text('// changed shared logger')
        with patch.object(d,'execute') as execute:
            d.individual.run_one(d,self.out,p,'a',self.registry,self.sched)
            execute.assert_not_called()
        self.assertEqual(d.load(Path(p['commitSlots'][0]['batch'])/'execution.json')['state'],'ERROR')

    def test_atomic_json_write_does_not_extend_long_target_filename(self):
        padding = 230-len(str(self.root.resolve()))
        directory=self.root/('x'*padding)
        path=directory/'result.json'
        d.save(path,dict(value='retained'))
        self.assertEqual(d.load(path),dict(value='retained'))
        self.assertEqual([p.name for p in directory.iterdir()],['result.json'])

    def test_preparation_build_failure_is_not_rebuilt_and_next_sha_still_executes(self):
        p=self.prepare();self.registry['cases'][0]['layers'].pop('uia')
        self.freeze(p,ui=False);self.freeze(p,index=1,ui=False)
        first=Path(p['commitSlots'][0]['batch'])
        d.save(first/'execution.json',dict(state='ERROR',phase='coverage-build',reason='Fixture compiler error'))
        d.save(first/'preparation'/'case-check-error.json',dict(sha='a'*40,reason='Fixture compiler error'))
        def execute(child,manifest,cp,outcomes,budget):
            self.assertEqual(manifest['head'],'b'*40)
            d.save(child/'execution.json',dict(state='HEADLESS_DONE'))
            d.save(child/'outcomes.json',dict(runs=[dict(caseId='wi7',phase='headless',verdict='PASS',runId='fixture-second')]))
        with patch.object(d,'execute',side_effect=execute) as call:
            result=d.queue_driver.drain(d,self.out,self.manifest,p,self.registry,self.sched)
        self.assertEqual(call.call_count,1)
        self.assertEqual(result['workflowState'],'FINISHED')
        self.assertEqual(result['statusCounts'],{'ERROR':1,'ACCEPTED':1})

    def test_locked_scratch_cleanup_cannot_stop_remaining_commits(self):
        p=self.prepare();self.registry['cases'][0]['layers'].pop('uia')
        self.freeze(p,ui=False);self.freeze(p,index=1,ui=False)
        def execute(child,manifest,cp,outcomes,budget):
            d.save(child/'execution.json',dict(state='HEADLESS_DONE'))
            d.save(child/'outcomes.json',dict(runs=[dict(caseId='wi7',phase='headless',verdict='PASS',runId='fixture-'+manifest['head'][0])]))
        with patch.object(d,'execute',side_effect=execute), patch.object(d.queue_driver.artifact_cleanup,'reclaim',side_effect=PermissionError('Fixture scratch locked')):
            result=d.queue_driver.drain(d,self.out,self.manifest,p,self.registry,self.sched)
        self.assertEqual(result['workflowState'],'FINISHED')
        self.assertEqual(result['statusCounts'],{'ACCEPTED':2})

    def test_dependency_only_commit_reuses_executable_graph_case_without_manual_mapping(self):
        p=self.prepare();child=Path(p['commitSlots'][0]['batch']);cm=d.load(child/'manifest.json')
        cm['commits'][0]['files']=['packages.lock.json']
        graph=self.root/'probes'/'Vega.AutoTest.LockGraphV3'/'Vega.AutoTest.LockGraphV3.csproj'
        graph.parent.mkdir(parents=True);graph.write_text('<Project />')
        with patch.object(d,'REGISTRY',self.root/'registry.json'):
            result=d.case_authoring.generate(d,child,cm,self.registry)
        self.assertEqual(result['state'],'REUSED_REQUIRES_CHECK')
        case=next(c for c in self.registry['cases'] if c['id']==result['caseId'])
        self.assertIn('partial-acceptance',case['tags'])
        self.assertTrue(case['layers']['logic']['filter'])

    def ui_session_fixture(self):
        p = self.prepare()
        cp = self.freeze(p)
        child = Path(p['commitSlots'][0]['batch'])
        cm = d.load(child/'manifest.json')
        repo = self.root/'worktree'
        binary = repo/'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
        resources = binary/'Resources'
        resources.mkdir(parents=True)
        hashes = {}
        for name in ('ComponentConfig', 'HardwareProfiles'):
            config = resources/(name+'.json')
            config.write_text('{"fixture":true}')
            hashes[name] = d.coverage_contract.digest(config)
        d.save(child/'execution-target.json', dict(repo=str(repo)))
        d.save(child/'build-reference.json', dict(commit=cm['head']))
        session = self.root/'daily/sessions/fixture'
        slot = cp['uiSlots'][0]
        d.save(session/'session.json', dict(state='PREPARED', repo=str(repo), bin=str(binary),
            model=slot['model'], profile=slot['profile'], preparedHashes=hashes))
        return child, cm, cp, session

    def test_ui_rejects_missing_session_or_other_profile_before_running_cases(self):
        child, cm, cp, session = self.ui_session_fixture()
        with patch.object(d, 'git', return_value=cm['head']), patch.object(d, 'run_case') as run:
            with self.assertRaisesRegex(ValueError, 'requires --session'):
                d.run_slot(child, cm, cp, cp['uiSlots'][0]['id'], None, dict(runs=[]))
            state = d.load(session/'session.json')
            state['profile'] = 'Other-profile'
            d.save(session/'session.json', state)
            with self.assertRaisesRegex(ValueError, 'target/model/profile'):
                d.run_slot(child, cm, cp, cp['uiSlots'][0]['id'], session, dict(runs=[]))
            run.assert_not_called()

    def test_ui_rejects_changed_prepared_configuration(self):
        child, cm, cp, session = self.ui_session_fixture()
        state = d.load(session/'session.json')
        (Path(state['bin'])/'Resources/ComponentConfig.json').write_text('changed')
        with patch.object(d, 'git', return_value=cm['head']), patch.object(d, 'run_case') as run:
            with self.assertRaisesRegex(ValueError, 'configuration missing/changed'):
                d.run_slot(child, cm, cp, cp['uiSlots'][0]['id'], session, dict(runs=[]))
            run.assert_not_called()

    def test_valid_ui_session_restores_even_when_case_invocation_raises(self):
        child, cm, cp, session = self.ui_session_fixture()
        completed = subprocess.CompletedProcess([], 0, stdout='restored', stderr='')
        with patch.object(d, 'git', return_value=cm['head']), patch.object(
                d, 'run_case', side_effect=RuntimeError('fixture failure')), patch.object(
                d.subprocess, 'run', return_value=completed) as process:
            with self.assertRaisesRegex(RuntimeError, 'fixture failure'):
                d.run_slot(child, cm, cp, cp['uiSlots'][0]['id'], session, dict(runs=[]))
        self.assertEqual(process.call_count, 2)
        self.assertIn('Restore', process.call_args.args[0])
        self.assertTrue(list((child/'slots').glob('*/restore.json')))

    def test_cli_emits_utf8_help_for_windows_json_consumers(self):
        result=subprocess.run([sys.executable,'-B',str(Path(__file__).with_name('daily.py')),'--help'],capture_output=True,check=True)
        self.assertIn('新 individual',result.stdout.decode('utf-8'))


if __name__ == '__main__':
    unittest.main()
