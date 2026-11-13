"""Read-only controller reproduction; all writes are isolated temporary fixtures.

No model, network, product build, Runner execution, or historical batch changes.
"""
import copy
import json
from pathlib import Path
import subprocess
import sys
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
from test_individual import IndividualTests, d


def reproduce(runtime_verdict):
    fixture = IndividualTests()
    fixture.setUp()
    try:
        plan = fixture.prepare()
        child = Path(plan['commitSlots'][0]['batch'])
        manifest = d.load(child / 'manifest.json')
        good = copy.deepcopy(fixture.registry['cases'][0])
        good.update(id='runtime-case', layers={'logic': good['layers']['logic']})
        good['layers']['logic']['filter'] = 'FullyQualifiedName~RuntimeCase'
        bad = copy.deepcopy(good)
        bad['id'] = 'compile-case'
        bad['layers']['logic']['filter'] = 'FullyQualifiedName~CompileCase'
        registry = {'cases': [bad, good]}

        def discovery(command, **kwargs):
            failed = 'FullyQualifiedName~CompileCase' in command
            kwargs['stdout'].write('error CS7036: missing argument' if failed else
                'The following Tests are available:\n    RuntimeCase.Test\n')
            return subprocess.CompletedProcess(command, int(failed))

        def trial(*args):
            args[-1]['runs'].append(dict(runId='isolated-fixture', verdict=runtime_verdict,
                verdictCode='OK' if runtime_verdict == 'PASS' else 'HARNESS_EXCEPTION',
                verdictReason='fixture constructor type mismatch'))

        with patch.object(d, 'WORKSPACES', fixture.root / 'workspaces'), \
             patch.object(d, 'git', return_value=manifest['head']), \
             patch.object(d, 'run_bounded', side_effect=discovery), \
             patch.object(d, 'build_runner'), patch.object(d, 'run_case', side_effect=trial):
            try:
                d.queue_driver.check_cases(d, child, manifest, registry,
                    ['compile-case', 'runtime-case'], fixture.root / 'worktree')
            except RuntimeError as error:
                records = d.load(child / 'preparation' / 'case-check.json')['records']
                return dict(runtimeVerdict=runtime_verdict,
                    compileFailures=sum(r['exitCode'] != 0 for r in records),
                    raised=type(error).__name__, message=str(error),
                    compilerRepairBranchReached=not isinstance(error,
                        d.queue_driver.CaseRuntimePreflightError))
        raise AssertionError('Fixture unexpectedly passed')
    finally:
        fixture.doCleanups()


if __name__ == '__main__':
    results = [reproduce(verdict) for verdict in ['PASS', 'ERROR']]
    assert results[0]['compilerRepairBranchReached'] is True
    # After the 2026-11-11 correction, the mixed case must take the repair path too.
    assert results[1]['compilerRepairBranchReached'] is True
    print(json.dumps(results, ensure_ascii=False, indent=2))
