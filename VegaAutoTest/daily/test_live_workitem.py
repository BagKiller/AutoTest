import unittest

import live_workitem as live


class LiveWorkItemTests(unittest.TestCase):
    def test_commit_message_work_item_is_the_validation_unit(self):
        bound = dict(sha='a' * 40, files=['Vega/MainWindow.xaml'], workItems=[3774], title='fix #3774')
        self.assertEqual(live.classify_commit(bound), 'bound')
        self.assertEqual(live.classify_commit(dict(files=['src/packages.lock.json'], workItems=[1])), 'lock')
        self.assertEqual(live.classify_commit(dict(files=['Vega/App.cs'], workItems=[])), 'unbound')
        self.assertEqual(live.classify_commit(dict(files=['docs/a.md'], workItems=[], noProductChange=True)), 'docs')

    def test_screen_controls_keep_the_ids_the_next_attempt_must_see(self):
        text = live.screen_controls(dict(windowName='Vega', nodes=[
            dict(controlType='Button', automationId='InspectionButton', name='检查', x=8, y=16, w=40, h=16),
            dict(controlType='Text', automationId='', name='', x=0, y=0, w=0, h=0)]))
        self.assertIn('InspectionButton', text)
        self.assertIn('window: Vega', text)
        self.assertNotIn('id= name=', text)

    def test_desktop_input_is_rejected_before_launch(self):
        source = 'public sealed class LiveCheck : L2TestBase { void Run(){ SendInput(); Assertions.Pass("a","UI","d","e","a"); } }'
        self.assertIn('不要向桌面', live.reject_source(source))
        ok = source.replace('SendInput();', '')
        self.assertEqual(live.reject_source(ok), '')

    def test_script_loop_is_not_the_nightly_driver(self):
        with self.assertRaisesRegex(RuntimeError, 'not the nightly driver'):
            live.execute()

    def test_next_attempt_sees_the_previous_screen_and_the_lookup(self):
        text = live.prompt(dict(
            sha='ab2ced6a19a7', workItem=3774, title='插入元素', message='#3774',
            diff='RecipeExpander', state='AVAILABLE', wiTitle='SPA',
            repro='右键采集设置空白处', acceptance='新元素出现', attachments='(none)',
            previous=dict(verdict='ERROR', reason='找不到 Inspection', screen='id=InspectionButton name=检查',
                          log='UiElementNotFoundException', source='class LiveCheck {}', note='menu')))
        self.assertIn('InspectionButton', text)
        self.assertIn('采集设置', text)
        self.assertIn('3774', text)
        self.assertNotIn('SendInput', text.split('Do not call')[0])


if __name__ == '__main__':
    unittest.main()
