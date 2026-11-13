"""Real SDK integration: an invalid UI candidate must not break logic/API contracts.

Uses a tiny synthetic product DLL and a private temporary automation tree. Does
not touch the live registry, product checkout, nightly cursor, or frozen probes.
"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import uuid
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'daily'))
import daily as d
import model_author as m


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--evidence', type=Path, required=True)
    parser.add_argument('--model-smoke', action='store_true', help='Exercise real bounded JSON generation instead of deterministic compile fixtures')
    parser.add_argument('--response', type=Path, help='Compile an unchanged retained model response, without another inference')
    args = parser.parse_args()
    args.evidence.mkdir(parents=True, exist_ok=True)
    results = []
    with tempfile.TemporaryDirectory(prefix='vega-authored-contract-') as temporary:
        root = Path(temporary).resolve()
        automation = root / 'automation'
        shutil.copytree(ROOT / 'harness', automation / 'harness',
                        ignore=shutil.ignore_patterns('bin', 'obj', '__pycache__'))
        product = root / 'product'
        project = product / 'Vega' / 'Vega.csproj'
        project.parent.mkdir(parents=True)
        project.write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            '<TargetFramework>net8.0</TargetFramework><NuGetAudit>false</NuGetAudit>'
            '</PropertyGroup></Project>', encoding='utf-8')
        project.with_name('Calculation.cs').write_text('namespace ProductFixture; '
            'public static class Calculation { public static int Double(int value) => value * 2; }', encoding='utf-8')
        product_bin = product / 'Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64'
        environment = dict(os.environ, VEGA_SW_ROOT=str(product), MSBUILDDISABLENODEREUSE='1',
                           VEGA_AUTOTEST_RUNDIR=str(root / 'assertions'), VEGA_AUTOTEST_OUTDIR=str(root / 'assertions'))

        def run(label, command, expected=0, marker=None):
            result = subprocess.run([str(d.DOTNET)] + command, env=environment, cwd=automation,
                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=180)
            output = result.stdout.decode('utf-8', errors='replace')
            (args.evidence / (label + '.log')).write_text(output, encoding='utf-8')
            passed = (result.returncode == 0) if expected == 0 else (result.returncode != 0)
            if marker:
                passed = passed and marker in output
            results.append(dict(check=label, exitCode=result.returncode, passed=passed))
            if not passed:
                raise RuntimeError(label + ' failed; see ' + str(args.evidence / (label + '.log')))

        run('synthetic-product-build', ['build', str(project), '-o', str(product_bin),
            '--artifacts-path', str(root / 'scratch/product'), '-p:UseSharedCompilation=false', '-nodeReuse:false'])
        (product_bin / 'Galaxy_ComponentConfig.json').write_text('{}', encoding='utf-8')
        (product_bin / 'Vega.runtimeconfig.json').write_text(json.dumps(dict(runtimeOptions=dict(
            frameworks=[dict(name='Microsoft.AspNetCore.App', version='8.0.0')]))), encoding='utf-8')
        files = [dict(path='Logic.cs', content='''using Xunit;
namespace Vega.AutoTest.Authored;
public class GoodLogic { [Fact] public void CallsProductionFixture() =>
    Assert.Equal(42, ProductFixture.Calculation.Double(21)); }
'''), dict(path='UiApi.cs', content='''using System;
using Xunit;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Vega.AutoTest.Harness;
namespace Vega.AutoTest.Authored;
public class UiApi {
 [Fact] public void ContractCompiles() => Assert.True(typeof(AutomationElement).IsClass);
 private static void Signatures(AutomationElement root) {
  UiFind.Require(root, "list", automationId: "list");
  UiFind.Wait(root, "list", TimeSpan.FromSeconds(1), automationId: "list");
  UiFind.Optional(root, automationId: "list");
  UiFind.All(root, controlType: ControlType.Button);
 }
}
'''), dict(path='InvalidUi.cs', content='#error INTENTIONAL_UI_ISOLATION_FAILURE\n')]
        cases = [dict(key=key, title=key, className='Vega.AutoTest.Authored.' + class_name,
            layer=layer, profiles=['Vega500'] if layer == 'uia' else [], minAssertions=1, sourceFiles=[filename])
            for key, class_name, layer, filename in [('logic', 'GoodLogic', 'logic', 'Logic.cs'),
                ('ui-api', 'UiApi', 'uia', 'UiApi.cs'), ('invalid-ui', 'InvalidUi', 'uia', 'InvalidUi.cs')]]
        response = dict(neededSources=[], files=files, cases=cases,
            requirements=[dict(id='fixture', workItems=[], steps=['infrastructure contract'],
                expected='compiler isolation', oracle='synthetic harness contract', uiRequired=False,
                profiles=[], profileBasis='', caseKeys=[c['key'] for c in cases], existingCaseIds=[], gap='')])
        if args.model_smoke:
            payload = dict(sha='a' * 40,
                commit=dict(title='Synthetic generator infrastructure contract; not product acceptance',
                            workItems=[1000], files=['Vega/Calculation.cs']),
                diff='A synthetic fixture adds Calculation.Double(int).', diffTruncated=False,
                workItems=dict(workItems=[dict(id=1000, state='AVAILABLE', fields={
                    'System.Description': 'Calculation.Double returns twice its input. Author ONE logic case '
                    'with explicit expected values for 0, -3 and 21. No UI, hardware, data or other APIs are needed.'})]),
                attachments={}, sourceInventory=['Vega/Calculation.cs'],
                sources={'Vega/Calculation.cs': project.with_name('Calculation.cs').read_text(encoding='utf-8')},
                existingCases=[], profiles=['Vega500'], remainingSourceRequests=0, remainingAuthorSeconds=180,
                recorder=(ROOT / 'harness/AssertionRecorder.cs').read_text(encoding='utf-8'),
                uiBase=(ROOT / 'harness/L2TestBase.cs').read_text(encoding='utf-8'),
                harnessContracts=m.harness_contracts(d), profilePreparationContract='Synthetic logic-only fixture')
            inference = args.evidence.resolve() / ('model-smoke-' + uuid.uuid4().hex[:10])
            response = d.load(args.response.resolve()) if args.response else m.infer(d, inference, payload, timeout=180)
            if response['neededSources'] or not response['cases'] or any(
                    c['layer'] != 'logic' or not c.get('sourceFiles') for c in response['cases']):
                raise RuntimeError('Model smoke did not emit isolated executable logic coverage')
        api = SimpleNamespace(ROOT=automation, REGISTRY=automation / 'registry.json',
                              SCHEDULING=automation / 'scheduling.json', save=d.save)
        registry = dict(cases=[])
        m.install(api, root / 'child', dict(head='a' * 40, commits=[dict(
            workItems=[1000] if args.model_smoke else [], noProductChange=False)]),
            response, registry, dict(profileOrder=['Vega500'], caseProfiles={}), 'vcontract')
        for case in registry['cases']:
            key, layer = next(iter(case['layers'].items()))
            label = ('model-generated-run' if args.model_smoke else 'invalid-ui-build' if 'invalid-ui' in case['id']
                     else 'ui-api-build' if 'ui-api' in case['id'] else 'logic-run')
            run(label, ['test', str(automation / layer['project']),
                '--filter', layer['filter'], '--artifacts-path', str(root / 'scratch' / label),
                '-p:UseSharedCompilation=false', '-nodeReuse:false'] + ([] if key == 'logic' else ['--list-tests']),
                expected=1 if label == 'invalid-ui-build' else 0,
                marker='INTENTIONAL_UI_ISOLATION_FAILURE' if label == 'invalid-ui-build' else None)
        if args.model_smoke:
            assertions = [row for path in (root / 'assertions').glob('*.json') for row in d.load(path)]
            if len(assertions) < 3 or any(row['Result'] != 'PASS' for row in assertions):
                raise RuntimeError('Generated fixture did not record all three passing comparisons')
            (args.evidence / 'model-smoke-assertions.json').write_text(json.dumps(assertions, indent=2), encoding='utf-8')
    name = 'model-smoke.json' if args.model_smoke else 'authored-isolation.json'
    (args.evidence / name).write_text(json.dumps(results, indent=2), encoding='utf-8')
    print(json.dumps(results, indent=2))


if __name__ == '__main__':
    main()
