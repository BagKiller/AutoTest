import tempfile
import unittest
from pathlib import Path

import daily as d
import latest_head as latest
import ui_repro


def record(repro='', acceptance='', state='AVAILABLE', wid=1):
    return dict(id=wid, state=state, fields={ui_repro.REPRO_FIELD: repro, ui_repro.ACCEPTANCE_FIELD: acceptance})


class ParseTests(unittest.TestCase):
    def test_bracket_sections_from_real_ado_bug(self):
        # #3752 as stored in ADO (HTML).
        html = ('<div>【前置条件】：打开Vega并登录</div><div>【复现流程】：1、Recipe中添加Linear、SPA</div>'
                '<div>2、修改Recipe单位为Rppm</div><div>3、切换界面再次切换回来</div>'
                '<div>【预期结果】：Recipe显示复合预期</div><div>【实际结果】：显示异常，置灰处理失效</div>'
                '<div>【版本】：1.2.8.11234</div>')
        spec = ui_repro.spec_for(record(html))
        self.assertEqual(spec['preconditions'], ['打开Vega并登录'])
        self.assertEqual(spec['steps'], ['Recipe中添加Linear、SPA', '修改Recipe单位为Rppm', '切换界面再次切换回来'])
        self.assertEqual(spec['expected'], 'Recipe显示复合预期')
        self.assertNotIn('置灰处理失效', ' '.join(spec['steps']))

    def test_inline_expected_and_actual_lines(self):
        # #3723 style: numbered lines with '实际：' / '期望：' prefixes.
        spec = ui_repro.spec_for(record('打开 Recipe，准备 2 个元素\n把选择通道设为同一个\n实际：无提醒\n期望：显示红字提醒'))
        self.assertEqual(spec['steps'], ['打开 Recipe，准备 2 个元素', '把选择通道设为同一个'])
        self.assertEqual(spec['expected'], '显示红字提醒')

    def test_decimal_values_are_not_step_numbers(self):
        spec = ui_repro.spec_for(record('【复现流程】：1、设置阈值为1.5 2、点击应用 【预期结果】：显示1.5'))
        self.assertEqual(spec['steps'], ['设置阈值为1.5', '点击应用'])

    def test_acceptance_only_item_uses_acceptance_as_steps_and_expectation(self):
        spec = ui_repro.spec_for(record(acceptance='1. 打开设置页 2. 勾选A'))
        self.assertEqual(spec['steps'], ['打开设置页', '勾选A'])
        self.assertIn('勾选A', spec['expected'])

    def test_no_repro_or_unavailable_item_has_no_spec(self):
        self.assertIsNone(ui_repro.spec_for(record()))
        self.assertIsNone(ui_repro.spec_for(record('1、x', state='UNAVAILABLE')))
        self.assertEqual(ui_repro.specs(dict(workItems=[record('1、a 2、b', wid=5)]), [6]), [])

    def test_captured_steps_reads_literal_step_numbers(self):
        source = 'StepEvidence.Capture(MainWindow, "step-1-open");\nStepEvidence.Capture(MainWindow,\n "step-2-x"); ' \
                 'StepEvidence.Capture(MainWindow, "result");'
        self.assertEqual(ui_repro.captured_steps(source), {1, 2})


class UiPrimacyTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / 'batch'
        self.child = self.root / 'commits' / ('h' * 40)

    def results(self, runs, requirements):
        d.save(self.root / 'requirement-scopes.json', [dict(sha='a' * 40, title='bug', workItems=[1])])
        d.save(self.child / 'plan.json', dict(coverageRequirements=requirements))
        return latest.commit_results(d, self.root, self.child, runs)[0]

    def repro_req(self, cases):
        return dict(id='r', sourceCommit='a' * 40, caseIds=cases, gaps=[], reproDriven=True,
                    reproSpec=dict(steps=['打开页面', '点击'], expected='显示结果'))

    def test_ui_result_decides_and_logic_cannot_override(self):
        layer = self.child / 'runner' / 'run-ui' / 'uia'
        layer.mkdir(parents=True)
        (layer / 'step-01-step-1-open.png').write_bytes(b'png')
        runs = [dict(caseId='ui', phase='ui-Vega500', runId='u1', verdict='PASS',
                     resultPath=str(layer.parent / 'result.json')),
                dict(caseId='logic', phase='headless', runId='l1', verdict='FAIL')]
        logic_req = dict(id='other', sourceCommit='a' * 40, caseIds=['logic'], gaps=[])
        result = self.results(runs, [self.repro_req(['ui']), logic_req])
        self.assertEqual(result['status'], 'PASSED_AT_HEAD')
        self.assertEqual(result['verificationLayer'], 'UI')
        self.assertTrue(result['screenshots'][0].endswith('step-01-step-1-open.png'))
        self.assertIn('非复现需求', result['reason'])
        lines = '\n'.join(latest.commit_lines(d, [result], 'h' * 40))
        self.assertIn('复现步骤 UI 验证', lines)
        self.assertIn('1. 打开页面', lines)

    def test_ui_fail_is_fail_and_missing_ui_is_never_pass(self):
        fail = self.results([dict(caseId='ui', phase='ui-Vega500', runId='u1', verdict='FAIL')], [self.repro_req(['ui'])])
        self.assertEqual(fail['status'], 'FAIL')
        # A headless PASS of the same case id does not count as UI verification.
        missing = self.results([dict(caseId='ui', phase='headless', runId='h1', verdict='PASS')], [self.repro_req(['ui'])])
        self.assertNotEqual(missing['status'], 'PASSED_AT_HEAD')
        none = self.results([], [dict(self.repro_req([]), gaps=['Mandatory repro UI not executed'])])
        self.assertEqual(none['status'], 'NEEDS_CASE')


if __name__ == '__main__':
    unittest.main()
