"""Regression coverage for the 2026-11-09/10 nightly preparation failures."""
import copy
import json
import hashlib
from pathlib import Path
import subprocess
import unittest
from unittest.mock import Mock, patch
import xml.etree.ElementTree as ET

import autopilot as a
import daily as d
import model_author as m
import requirement_scopes as scopes
import test_autopilot as controller_fixtures
import test_individual as queue_fixtures


class ReliabilityTests(unittest.TestCase):
    def controller(self):
        fixture = controller_fixtures.ControllerTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        return fixture

    def test_mixed_compile_and_runtime_failures_keep_both_and_allow_compile_repair(self):
        f = queue_fixtures.IndividualTests()
        f.setUp()
        self.addCleanup(f.doCleanups)
        api = queue_fixtures.d
        child = Path(f.prepare()['commitSlots'][0]['batch'])
        manifest = api.load(child / 'manifest.json')
        cases = []
        for name in ['compile', 'runtime']:
            case = copy.deepcopy(f.registry['cases'][0])
            case.update(id=name, layers={'logic': dict(case['layers']['logic'], filter=name)})
            cases.append(case)
        def discover(command, **kwargs):
            failed = 'compile' in command
            kwargs['stdout'].write('error CS7036: compile marker' if failed else
                'The following Tests are available:\n    RuntimeCase.Test\n')
            return subprocess.CompletedProcess(command, int(failed))
        def trial(*args):
            args[-1]['runs'].append(dict(runId='raw-runtime', verdict='ERROR',
                verdictCode='HARNESS_EXCEPTION', verdictReason='runtime marker'))
        with patch.object(api, 'WORKSPACES', f.root / 'workspaces'), \
             patch.object(api, 'git', return_value=manifest['head']), \
             patch.object(api, 'run_bounded', side_effect=discover), \
             patch.object(api, 'build_runner'), patch.object(api, 'run_case', side_effect=trial):
            with self.assertRaises(api.queue_driver.CaseDiscoveryError) as error:
                api.queue_driver.check_cases(api, child, manifest, {'cases': cases},
                    ['compile', 'runtime'], f.root / 'worktree')
        self.assertEqual(error.exception.case_ids, ['compile'])
        records = api.load(child / 'preparation/case-check.json')['records']
        self.assertEqual(records[1]['runtime']['verdictCode'], 'HARNESS_EXCEPTION')
        self.assertFalse(records[1]['runtimeUsable'])

    def test_controller_repairs_only_compiler_errors_and_archives_first_receipt(self):
        f = self.controller()
        d.save(f.child / 'manifest.json', f.manifest)
        diff = f.child / 'scope.diff'
        diff.write_text('immutable diff')
        d.save(f.child / 'scope.json', dict(diff=str(diff)))
        d.save(f.child / 'preparation/workitems.json', dict(workItems=[]))
        d.save(f.child / 'execution-target.json', dict(repo=str(f.root / 'repo')))
        d.save(f.child / 'build-reference.json', {})
        reqs = [dict(id=name, caseIds=[name], steps=['observe'], expected='contract',
            uiRequired=False, workItems=[]) for name in ['compile', 'runtime']]
        response_path = f.child / 'candidate.json'
        d.save(response_path, {})
        receipt = dict(requirements=reqs, response=dict(files=[], cases=[], requirements=[]),
                       responsePath=str(response_path))
        d.save(f.child / 'preparation/authored.json', receipt)
        def check(*args):
            records = []
            for name in ['compile', 'runtime']:
                log = f.child / 'preparation/checks' / (name + '.log')
                log.parent.mkdir(parents=True, exist_ok=True)
                log.write_text(name + ' marker')
                records.append(dict(caseId=name, layer='logic', exitCode=int(name == 'compile'),
                    discoveredTests=[] if name == 'compile' else ['Runtime.Test'],
                    log=str(log.relative_to(f.child)), runtimeUsable=False))
            d.save(f.child / 'preparation/case-check.json', dict(records=records, fingerprints={'cases': []}))
            raise d.queue_driver.CaseDiscoveryError(records)
        with patch.object(d, 'git', return_value=f.manifest['head']), \
             patch.object(d.queue_driver, 'check_cases', side_effect=check) as checks, \
             patch.object(a, 'generate', return_value=copy.deepcopy(receipt)) as generate, \
             patch.object(d.individual, 'freeze_one'):
            a.prepare_commit(f.child.parents[1], dict(sha=f.manifest['head'], batch=str(f.child)),
                             {}, f.registry, f.sched)
        self.assertEqual(checks.call_count, 2)
        generate.assert_called_once()
        repair = generate.call_args.kwargs['repair']
        self.assertEqual(repair['caseIds'], ['compile'])
        self.assertIn('compile marker', repair['diagnostics'])
        self.assertNotIn('runtime marker', repair['diagnostics'])
        saved = f.child / 'preparation/before-compiler-repair/preparation/checks/runtime.log'
        self.assertEqual(saved.read_text(), 'runtime marker')

    def test_discovery_timeout_keeps_checking_other_layers(self):
        f = queue_fixtures.IndividualTests()
        f.setUp()
        self.addCleanup(f.doCleanups)
        api = queue_fixtures.d
        child = Path(f.prepare()['commitSlots'][0]['batch'])
        manifest = api.load(child / 'manifest.json')
        count = 0
        def discover(command, **kwargs):
            nonlocal count
            count += 1
            if count == 1:
                raise RuntimeError('Owned discovery process exceeded its deadline')
            kwargs['stdout'].write('The following Tests are available:\n    UiCase.Test\n')
            return subprocess.CompletedProcess(command, 0)
        with patch.object(api, 'WORKSPACES', f.root / 'workspaces'), \
             patch.object(api, 'git', return_value=manifest['head']), \
             patch.object(api, 'run_bounded', side_effect=discover):
            with self.assertRaises(api.queue_driver.CaseDiscoveryError):
                api.queue_driver.check_cases(api, child, manifest, f.registry, ['wi7'], f.root / 'repo')
        records = api.load(child / 'preparation/case-check.json')['records']
        self.assertEqual(count, 2)
        self.assertEqual(records[1]['runtimeState'], 'REQUIRES_PREPARED_UI_SESSION')
        self.assertIn('deadline', (child / records[0]['log']).read_text())

    def test_scope_repair_does_not_regenerate_unaffected_runtime_scope(self):
        f = self.controller()
        root = f.child.parents[1]
        commits = [dict(sha=c * 40, workItems=[], noProductChange=False) for c in ['a', 'b']]
        d.save(root / 'requirement-scopes.json', commits)
        d.save(f.child / 'preparation/workitems.json', dict(workItems=[]))
        previous = []
        for commit, name in zip(commits, ['runtime', 'compile']):
            response = dict(files=[dict(path=name + '.cs', content='// unchanged')],
                cases=[dict(key=name, className='Vega.AutoTest.Authored.' + name,
                            sourceFiles=[name + '.cs'])], requirements=[])
            receipt = dict(requirements=[dict(id=name, caseIds=[name])], response=response,
                           responsePath=str(root / (name + '.json')))
            d.save(f.child / 'preparation/scopes' / commit['sha'] / 'preparation/authored.json', receipt)
            previous.append(dict(sourceCommit=commit['sha'], response=response))
            f.registry['cases'].append(dict(id=name, layers={'logic': dict(
                filter='FullyQualifiedName~Vega.AutoTest.Authored.' + name + '.')}))
        def repaired(*args):
            self.assertEqual(args[2]['analysisSourceCommit'], 'b' * 40)
            self.assertEqual(args[-1]['files'], ['compile.cs'])
            self.assertEqual(args[-1]['diagnostics'], 'own compiler diagnostic')
            return d.load(args[1] / 'preparation/authored.json')
        generate = Mock(side_effect=repaired)
        receipt = scopes.generate(d, f.child, dict(head='c' * 40, parentBatch=str(root)),
            f.registry, f.sched, root, generate,
            dict(response={'scopes': previous}, diagnostics='compiler only', caseIds=['compile'],
                 diagnosticsByCase={'compile': 'own compiler diagnostic', 'unrelated': 'other scope diagnostic'}),
            a.gap_requirements)
        generate.assert_called_once()
        self.assertEqual([r['caseIds'] for r in receipt['requirements']], [['runtime'], ['compile']])

    def test_generated_projects_isolate_cases_and_ui_exception_policy(self):
        f = self.controller()
        response = f.response()
        response['files'] = [dict(path='Logic.cs', content='void T(){ try {} catch(Exception e){} }'),
            dict(path='Ui.cs', content='// UI without catch-all'), dict(path='Shared.cs', content='// helper')]
        response['cases'][0]['sourceFiles'] = ['Logic.cs', 'Shared.cs']
        response['cases'].append(dict(key='ui', title='UI', className='Vega.AutoTest.Authored.Ui',
            layer='uia', profiles=['Vega100-PIC'], minAssertions=1, sourceFiles=['Ui.cs', 'Shared.cs']))
        m.install(d, f.child, f.manifest, response, f.registry, f.sched, 'visolated')
        paths = [d.ROOT / case['layers'][next(iter(case['layers']))]['project'] for case in f.registry['cases']]
        self.assertNotEqual(paths[0], paths[1])
        for path, expected, excluded in zip(paths, ['Logic.cs', 'Ui.cs'], ['Ui.cs', 'Logic.cs']):
            project = ET.parse(path).getroot()
            self.assertEqual(project.find('.//EnableDefaultCompileItems').text, 'false')
            includes = [e.get('Include') for e in project.iter('Compile')]
            self.assertIn(expected, includes)
            self.assertIn('Shared.cs', includes)
            self.assertNotIn(excluded, includes)

    def test_shared_ui_helper_cannot_swallow_prerequisites(self):
        f = self.controller()
        response = f.response()
        response['cases'][0].update(layer='uia', profiles=['Vega100-PIC'], sourceFiles=['Behavior.cs'])
        response['files'][0]['content'] = 'void Helper(){ try {} catch(Exception error) {} }'
        with self.assertRaisesRegex(ValueError, 'must not catch Exception'):
            m.install(d, f.child, f.manifest, response, f.registry, f.sched, 'vunsafe')

    def test_source_ownership_rejects_missing_or_msbuild_expressions(self):
        f = self.controller()
        for names in [[], ['Missing.cs'], ['$(Injected).cs'], ['Behavior.cs', 'behavior.cs']]:
            response = f.response()
            response['cases'][0]['sourceFiles'] = names
            with self.assertRaises(ValueError):
                m.install(d, f.child, f.manifest, response, f.registry, f.sched, 'vinvalid')

    def test_compiler_patch_cannot_touch_another_cases_file_or_metadata(self):
        f = self.controller()
        response = f.response()
        response['files'].append(dict(path='Other.cs', content='original assertion'))
        patch_response = dict(files=[dict(path='Other.cs', content='weakened')], cases=[], requirements=[])
        with self.assertRaisesRegex(ValueError, 'outside the failed cases'):
            m.compiler_repair(response, patch_response, ['Behavior.cs'])
        patch_response['files'] = [dict(path='Behavior.cs', content='compiler fix')]
        result = m.compiler_repair(response, patch_response, ['Behavior.cs'])
        self.assertEqual(result['cases'], response['cases'])
        self.assertEqual(result['requirements'], response['requirements'])
        self.assertEqual(result['files'][1]['content'], 'original assertion')

    def test_compiler_patch_cannot_touch_helpers_shared_with_a_runtime_case(self):
        response = dict(files=[], cases=[dict(className='Vega.AutoTest.Authored.Bad',
            sourceFiles=['Bad.cs', 'Checks.cs']), dict(className='Vega.AutoTest.Authored.Runtime',
            sourceFiles=['Runtime.cs', 'Checks.cs'])])
        registry = dict(cases=[dict(id='bad', layers={'logic': dict(
            filter='FullyQualifiedName~Vega.AutoTest.Authored.Bad.')})])
        self.assertEqual(m.repair_files(response, ['bad'], registry), ['Bad.cs'])

    def test_source_requests_continue_past_three_rounds_and_keep_saved_line_numbers(self):
        f = self.controller()
        saved = 'SOURCE EXCERPT:\n' + ('321: original method body\n' * 1100)
        d.save(f.child / 'preparation/source-context.json', dict(sha=f.manifest['head'], sources={'large.cs#M': saved}))
        calls = []
        def infer(api, folder, payload, **kwargs):
            calls.append(copy.deepcopy(payload))
            value = dict(neededSources=['?TypeName'], files=[], cases=[], requirements=[]) if len(calls) <= 4 else f.response()
            api.save(folder / 'response.json', value)
            return value
        with patch.object(m, 'context', return_value=dict(sources={}, sha=f.manifest['head'])), \
             patch.object(m, 'infer', side_effect=infer), patch.object(m, 'add_sources') as add:
            receipt = a.generate_one(d, f.child, f.manifest, f.registry, f.sched, f.child.parents[1])
        self.assertEqual(add.call_count, 4)
        self.assertGreater(calls[3]['remainingSourceRequests'], 0)
        self.assertEqual(calls[0]['sources']['large.cs#M'], saved)
        self.assertEqual(len(receipt['requirements']), 1)

    def test_context_supplies_real_harness_contracts_separately_from_product_inventory(self):
        actual = Path(__file__).resolve().parents[1]
        f = self.controller()
        names = ['harness/UiFind.cs', 'harness/Wpf/WpfTestHost.cs', 'harness/AssertionRecorder.cs',
                 'harness/L2TestBase.cs', 'daily/Start-ModelValidation.ps1']
        for name in names:
            target = f.root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes((actual / name).read_bytes())
        diff = f.child / 'input.diff'
        diff.write_text('production change')
        d.save(f.child / 'scope.json', dict(diff=str(diff)))
        manifest = dict(f.manifest, source=str(f.root), commits=[dict(files=['Product.cs'], workItems=[])])
        with patch.object(d, 'git', side_effect=lambda *args, **kwargs:
                          'Product.cs' if args[0] == 'ls-tree' else 'public class Product {}'):
            payload = m.context(d, f.child, manifest, f.registry, f.sched)
        contract = payload['harnessContracts']['UiFind.cs']
        self.assertEqual(contract['sha256'], hashlib.sha256((actual / 'harness/UiFind.cs').read_bytes()).hexdigest())
        self.assertIn('TimeSpan timeout', contract['source'])
        self.assertNotIn('UiFind.cs', payload['sourceInventory'])

    def test_validation_metadata_fix_cannot_change_oracle_or_minimum_assertions(self):
        f = self.controller()
        original = f.response()
        response = copy.deepcopy(original)
        response['cases'][0]['profiles'] = ['Vega100-PIC']
        self.assertEqual(m.validation_repair(original, response)['cases'][0]['profiles'], ['Vega100-PIC'])
        response['cases'][0]['minAssertions'] = 0
        with self.assertRaisesRegex(ValueError, 'assertion thresholds'):
            m.validation_repair(original, response)
        response = copy.deepcopy(original)
        response['requirements'][0]['expected'] = 'weakened acceptance'
        with self.assertRaisesRegex(ValueError, 'acceptance'):
            m.validation_repair(original, response)

    def test_runtime_check_rejects_missing_shared_framework_before_model_login(self):
        f = self.controller()
        for name in ['daily/daily.py', 'harness/UiFind.cs', 'harness/Wpf/WpfTestHost.cs']:
            path = f.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('// runtime fixture')
        d.save(d.REGISTRY, f.registry)
        d.save(d.SCHEDULING, f.sched)
        # Tool paths are host-specific; this contract must hold on any machine.
        fake_dotnet = f.root / 'dotnet.exe'
        fake_dotnet.write_bytes(b'#')
        fake_shell = f.root / 'pwsh.exe'
        fake_shell.write_bytes(b'#')
        with patch.object(d, 'DOTNET', fake_dotnet), patch.object(d, 'SHELL', str(fake_shell)), \
             patch.object(a.subprocess, 'run', return_value=subprocess.CompletedProcess(
                 ['dotnet'], 0,
                 stdout='Microsoft.NETCore.App 8.0.32\nMicrosoft.WindowsDesktop.App 8.0.32')) as run:
            with self.assertRaisesRegex(RuntimeError, 'AspNetCore'):
                a.runtime_check()
        # The shared-framework gate fires before any Codex CLI probe or login.
        run.assert_called_once()


if __name__ == '__main__':
    unittest.main()
