"""Completion, interruption and generated-code boundaries without network/product builds."""
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

import autopilot as a
import model_author as m


class ControllerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.child = self.root / 'daily' / 'runs' / 'batch' / 'commits' / ('a' * 40)
        self.child.mkdir(parents=True)
        self.manifest = dict(head='a' * 40, commits=[dict(workItems=[7000], noProductChange=False)])
        self.registry = dict(cases=[])
        self.sched = dict(profileOrder=['Vega100-PIC'], caseProfiles={})
        for name, value in [('ROOT', self.root), ('REGISTRY', self.root / 'registry.json'),
                            ('SCHEDULING', self.root / 'scheduling.json'), ('ACTIVE', self.root / 'active.json')]:
            target = a if name == 'ACTIVE' else a.d
            p = patch.object(target, name, value)
            p.start()
            self.addCleanup(p.stop)

    def response(self):
        return dict(neededSources=[], files=[dict(path='Behavior.cs', content='// generated test')],
            cases=[dict(key='bounds', title='Actual bounds behavior', className='Vega.AutoTest.Authored.Bounds',
                layer='logic', profiles=[], minAssertions=1)],
            requirements=[dict(id='bounds', workItems=[7000], steps=['Call product'], expected='Reject invalid bound',
                oracle='Requirement 7000', uiRequired=False, profiles=[], caseKeys=['bounds'], existingCaseIds=[], gap='')])

    def test_generated_files_cannot_escape_probe_root(self):
        for name in ['../../../../daily/daily.py', 'C:/outside.cs', '../escape.cs', 'build.targets', 'a.csproj']:
            response = self.response()
            response['files'][0]['path'] = name
            with self.subTest(name=name), self.assertRaises(ValueError):
                m.install(a.d, self.child, self.manifest, response, self.registry, self.sched, 'v1')
        self.assertFalse((self.root / 'registry.json').exists())

    def test_generated_probe_version_cannot_be_overwritten(self):
        req = m.install(a.d, self.child, self.manifest, self.response(), self.registry, self.sched, 'v1')
        source = self.root / 'probes/authored/v1' / ('a' * 12) / 'Behavior.cs'
        before = source.read_bytes()
        self.assertEqual(req[0]['caseIds'], ['authored-v1-' + 'a' * 12 + '-bounds'])
        with self.assertRaises(ValueError):
            m.install(a.d, self.child, self.manifest, self.response(), self.registry, self.sched, 'v1')
        self.assertEqual(source.read_bytes(), before)

    def test_ui_case_without_profile_is_rejected(self):
        response = self.response()
        response['cases'][0]['layer'] = 'uia'
        with self.assertRaisesRegex(ValueError, 'profile'):
            m.install(a.d, self.child, self.manifest, response, self.registry, self.sched, 'v1')

    def test_model_has_no_shell_or_user_configuration(self):
        command = m.command('codex', self.root, self.root / 'schema.json', self.root / 'response.json')
        self.assertIn('--ignore-user-config', command)
        self.assertIn('read-only', command)
        self.assertIn('shell_tool', command)
        self.assertIn('multi_agent', command)
        self.assertNotIn('--dangerously-bypass-approvals-and-sandbox', command)

    def test_model_tool_execution_is_rejected_even_with_valid_response(self):
        def transport(command, **kwargs):
            a.d.save(self.root / 'inference/response.json', self.response())
            kwargs['stdout'].write(json.dumps(dict(item=dict(type='command_execution'))))
            return subprocess.CompletedProcess(command, 0)
        with patch.object(a.d, 'run_bounded', side_effect=transport):
            with self.assertRaisesRegex(RuntimeError, 'tool use'):
                m.infer(a.d, self.root / 'inference', {}, executable='codex')

    def test_timeout_terminates_owned_child_and_persists_failure(self):
        supervisor = a.Supervisor(self.root)
        with self.assertRaisesRegex(RuntimeError, 'exceeded'):
            supervisor.run([sys.executable, '-c', 'import time;time.sleep(30)'], timeout=.15,
                           stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        records = [a.d.load(p) for p in (self.root / 'controller/processes').glob('*.json')]
        self.assertEqual(len(records), 1)
        self.assertEqual(records[0]['state'], 'TIMEOUT')
        self.assertNotEqual(a.identity(records[0]['pid']), records[0]['identity'])

    def test_recovery_never_kills_reused_pid(self):
        path = self.root / 'controller/processes/old.json'
        a.d.save(path, dict(state='RUNNING', pid=1234, identity='old', deadline=0))
        with patch.object(a, 'identity', return_value='new'), patch.object(a.Supervisor, 'stop') as stop:
            a.Supervisor(self.root).recover()
        stop.assert_not_called()
        self.assertEqual(a.d.load(path)['state'], 'RECOVERED_AFTER_CONTROLLER_EXIT')

    def test_pending_active_batch_is_resumed_without_collecting(self):
        batch = self.root / 'daily/runs/pending'
        a.d.save(a.ACTIVE, dict(batch=str(batch), phase='ERROR'))
        with patch.object(sys, 'argv', ['autopilot.py', '--daily']), patch.object(a, 'run') as run, patch.object(a.d, 'collect') as collect:
            a.main()
        run.assert_called_once_with(batch)
        collect.assert_not_called()

    def test_complete_gate_requires_all_preparations_and_execution(self):
        root = self.child.parents[1]
        children = [self.child, root / 'commits' / ('b' * 40)]
        slots = [dict(sha=c.name, batch=str(c)) for c in children]
        a.d.save(root / 'manifest.json', dict(strategy='individual'))
        a.d.save(a.d.REGISTRY, dict(cases=[]))
        a.d.save(a.d.SCHEDULING, self.sched)
        finished = dict(pendingCommitValidations=[], workflowState='FINISHED', acceptanceState='INCOMPLETE', statusCounts={'NEEDS_CASE': 1})
        with patch.object(a.d.individual, 'prepare', return_value=dict(commitSlots=slots)), \
             patch.object(a.d.queue_driver, 'prepare_all'), patch.object(a, 'prepare_commit') as prepare, \
             patch.object(a, 'restore_sessions'), patch.object(a.d, 'build_runner'), \
             patch.object(a, 'preflight'), \
             patch.object(a.d, 'main'), \
             patch.object(a.d.queue_driver, 'drain', return_value=finished), \
             patch.object(a.d.individual, 'aggregate', return_value=finished):
            a.run(root)
        self.assertEqual([call.args[1]['sha'] for call in prepare.call_args_list], ['a' * 40, 'b' * 40])
        self.assertEqual(a.d.load(root / 'controller/completion-check.json')['exitCode'], 0)

    def test_model_capacity_failure_keeps_commit_unfrozen_for_retry(self):
        a.d.save(self.child / 'manifest.json', self.manifest)
        slot = dict(sha='a' * 40, batch=str(self.child))
        with patch.object(a, 'generate', side_effect=m.ModelCapacityUnavailable('usage limit')), \
             patch.object(a.d, 'prepare_target') as build, \
             patch.object(a.d.individual, 'freeze_one') as freeze:
            with self.assertRaises(m.ModelCapacityUnavailable):
                a.prepare_commit(self.child.parents[1], slot, {}, self.registry, self.sched)
        build.assert_not_called()
        freeze.assert_not_called()
        self.assertFalse((self.child / 'preparation/controller-review.json').exists())

    def test_capacity_exit_preserves_same_batch_with_waiting_state(self):
        root = self.root / 'daily/runs/waiting'
        a.d.save(a.ACTIVE, dict(batch=str(root), phase='AUTHORING'))
        with patch.object(sys, 'argv', ['autopilot.py', '--daily']), \
             patch.object(a, 'run', side_effect=m.ModelCapacityUnavailable('usage limit')), \
             patch.object(a.d, 'collect') as collect:
            with self.assertRaises(SystemExit) as exit_state:
                a.main()
        self.assertEqual(exit_state.exception.code, 75)
        state = a.d.load(a.ACTIVE)
        self.assertEqual(state['batch'], str(root))
        self.assertEqual(state['phase'], 'WAITING_MODEL')
        collect.assert_not_called()

    def test_transport_detects_capacity_error_and_omits_private_inventory(self):
        def transport(command, **kwargs):
            kwargs['stdout'].write(json.dumps(dict(error="You've hit your usage limit")))
            return subprocess.CompletedProcess(command, 1)
        folder = self.root / 'inference'
        with patch.object(a.d, 'run_bounded', side_effect=transport):
            with self.assertRaises(m.ModelCapacityUnavailable):
                m.infer(a.d, folder, dict(sha='a' * 40, _sourceInventory=['private-marker.cs']), executable='codex')
        self.assertNotIn('private-marker', (folder / 'request.txt').read_text(encoding='utf-8'))
        self.assertTrue((folder / 'events.jsonl').is_file())

    def test_model_failure_becomes_evidenced_gap_and_does_not_abort_preparation(self):
        a.d.save(self.child / 'manifest.json', self.manifest)
        diff = self.child / 'input.diff'
        diff.write_text('actual diff', encoding='utf-8')
        a.d.save(self.child / 'scope.json', dict(diff=str(diff)))
        a.d.save(self.child / 'preparation/workitems.json', dict(workItems=[]))
        slot = dict(sha='a' * 40, batch=str(self.child))
        with patch.object(a, 'generate', side_effect=RuntimeError('Actual model transport failure')), \
             patch.object(a.d, 'prepare_target', return_value=(self.root / 'worktree', self.root / 'evidence')), \
             patch.object(a.d.queue_driver, 'check_cases'), patch.object(a.d.individual, 'freeze_one') as freeze:
            a.prepare_commit(self.child.parents[1], slot, {}, self.registry, self.sched)
        freeze.assert_called_once()
        review = a.d.load(self.child / 'preparation/controller-review.json')
        req = review['requirements'][0]
        self.assertIn('transport failure', req['gap'])
        evidence = req['attemptEvidence'][0]
        self.assertEqual(a.d.coverage_contract.digest(self.child / evidence['path']), evidence['sha256'])
        self.assertFalse(req['caseIds'])

    def test_resume_pointer_is_written_before_collection_can_advance_cursor(self):
        root = self.root / 'daily/runs/new-batch'
        manifest = dict(batchId='new-batch')
        a.collected(root, manifest)
        self.assertEqual(a.d.load(a.ACTIVE)['batch'], str(root))
        self.assertEqual(a.d.load(root / 'manifest.json')['strategy'], 'individual')

    def test_restart_reuses_only_raw_results_from_same_campaign_sha_and_case(self):
        raw = self.child / 'runner/run/result.json'
        a.d.save(raw, dict(target=dict(gitCommit='a' * 40), caseId='behavior', runId='run', verdict='FAIL'))
        outcomes = dict(runs=[dict(caseId='behavior', phase='headless', resultPath=str(raw), runId='run',
                                  sha256=a.d.coverage_contract.digest(raw))])
        self.assertTrue(a.d.completed_headless(self.child, 'a' * 40, dict(caseId='behavior'), outcomes))
        self.assertFalse(a.d.completed_headless(self.child, 'b' * 40, dict(caseId='behavior'), outcomes))
        raw.write_text('{}')
        self.assertFalse(a.d.completed_headless(self.child, 'a' * 40, dict(caseId='behavior'), outcomes))

    def test_budget_placeholder_is_not_a_completed_test(self):
        outcomes = dict(runs=[dict(caseId='behavior', phase='headless', verdictCode='NOT_RUN_BUDGET')])
        self.assertFalse(a.d.completed_headless(self.child, 'a' * 40, dict(caseId='behavior'), outcomes))

    def test_compiler_repair_cannot_remove_cases_or_change_oracles(self):
        original = self.response()
        response = dict(files=[dict(path='Behavior.cs', content='// corrected API usage')], cases=[], requirements=[])
        repaired = m.compiler_repair(original, response)
        self.assertEqual(repaired['cases'], original['cases'])
        self.assertEqual(repaired['requirements'], original['requirements'])
        self.assertEqual(repaired['files'][0]['content'], '// corrected API usage')
        self.assertEqual(original['files'][0]['content'], '// generated test')

    def test_required_ui_without_executable_case_cannot_be_claimed_covered(self):
        response = self.response()
        response['requirements'][0].update(uiRequired=True, profiles=['Vega100-PIC'])
        with self.assertRaisesRegex(ValueError, 'executable UI'):
            m.install(a.d, self.child, self.manifest, response, self.registry, self.sched, 'v1')

    def test_restoration_failure_is_quarantined_and_does_not_abort_other_commits(self):
        repo = str(self.root / 'worktree')
        a.d.save(self.child / 'execution-target.json', dict(repo=repo))
        a.d.save(self.child / 'plan.json', dict())
        a.d.save(self.root / 'daily/sessions/session/session.json', dict(repo=repo, state='PREPARED', profile='Vega100-PIC'))
        with patch.object(a, 'restore_sessions', side_effect=RuntimeError('Actual restore failure')):
            self.assertFalse(a.restore_or_quarantine(self.child.parents[1], self.child))
        q = a.d.load(self.child.parents[1] / 'controller/quarantined-profiles.json')
        self.assertIn('Vega100-PIC', q)
        self.assertEqual(a.d.load(self.child / 'execution.json')['state'], 'ERROR')

    def test_bad_source_request_is_feedback_without_reading_untrusted_path(self):
        payload = dict(sourceInventory=['Product/Behavior.cs'], sources={})
        manifest = dict(head='a' * 40, source=str(self.root))
        with patch.object(a.d, 'git', return_value='actual source') as git:
            m.add_sources(a.d, payload, manifest, ['../../private.json', 'Product/Behavior.cs'])
        self.assertEqual(git.call_count, 1)
        self.assertEqual(payload['sources'], {'Product/Behavior.cs': 'actual source'})
        self.assertEqual(payload['sourceRequestErrors'][0]['path'], '../../private.json')

    def test_recovery_installs_completed_model_response_without_calling_model_again(self):
        folder = self.child / 'preparation/model/attempt'
        a.d.save(folder / 'response.json', self.response())
        (folder / 'request.txt').write_text('policy\nINPUT DATA:\n' + json.dumps(dict(sha='a' * 40)), encoding='utf-8')
        (folder / 'events.jsonl').write_text(json.dumps(dict(type='turn.completed')), encoding='utf-8')
        with patch.object(m, 'infer') as infer:
            receipt = a.recover_authored(self.child, self.manifest, self.registry, self.sched)
        infer.assert_not_called()
        self.assertTrue(receipt['recoveredFromModelResponse'])
        self.assertEqual(len(receipt['requirements'][0]['caseIds']), 1)

    def test_scheduler_can_resolve_native_cli_without_desktop_path(self):
        executable = self.root / 'OpenAI/Codex/bin/version/codex.exe'
        executable.parent.mkdir(parents=True)
        executable.write_bytes(b'installed binary')
        with patch.dict(os.environ, {'LOCALAPPDATA': str(self.root), 'VEGA_CODEX_EXECUTABLE': ''}), \
             patch.object(m.shutil, 'which', return_value=None):
            self.assertEqual(Path(m.find_transport()), executable)

    def test_finishing_yesterdays_active_batch_still_collects_todays_commits(self):
        old = self.root / 'daily/runs/old'
        new = self.root / 'daily/runs/new'
        a.d.save(a.ACTIVE, dict(batch=str(old), phase='ERROR'))
        a.d.save(old / 'manifest.json', dict(windowStart='2026-10-28T00:00:00+08:00'))
        from datetime import datetime
        with patch.object(sys, 'argv', ['autopilot.py', '--daily']), patch.object(a, 'run') as run, \
             patch.object(a.d, 'now_cn', return_value=datetime(2026, 10, 29, tzinfo=a.d.CN)), \
             patch.object(a.d, 'collect', return_value=(new, {})) as collect:
            a.main()
        self.assertEqual([call.args[0] for call in run.call_args_list], [old, new])
        collect.assert_called_once_with(0, False, on_collected=a.collected)

    def test_long_generated_case_id_can_actually_open_runner_log_on_windows(self):
        cid = 'authored-' + 'very-long-business-scenario-' * 8
        log_dir = self.child / 'attempts' / ('a' * 30)
        log_dir.mkdir(parents=True)
        result_path = self.child / 'runner/run/result.json'
        a.d.save(result_path, dict(caseId=cid, target=dict(gitCommit='a' * 40), runId='run',
            verdict='PASS', verdictCode='OK'))
        outcomes = dict(runs=[])
        def runner(command, **kwargs):
            self.assertLess(len(Path(kwargs['stdout'].name).name), 25)
            kwargs['stdout'].write(str(result_path) + '\n')
            return subprocess.CompletedProcess(command, 0)
        with patch.object(a.d, 'run_bounded', side_effect=runner), patch.object(a.d, 'runner_binary', return_value=self.root / 'runner.dll'), \
             patch.object(a.d, 'git', return_value='a' * 40):
            result = a.d.run_case(self.child, self.root, log_dir, cid, ['logic'], 'headless', 60, outcomes)
        self.assertEqual(result['runId'], 'run')
        self.assertEqual(result['caseId'], cid)
        self.assertTrue(Path(result['log']).is_file())


if __name__ == '__main__':
    unittest.main()
