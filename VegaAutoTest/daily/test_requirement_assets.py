import io
import json
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch
import requirement_assets as a


class AssetsTests(unittest.TestCase):
    def test_attachment_url_cannot_forward_credentials_to_other_host_or_endpoint(self):
        org='http://ado/DefaultCollection'
        self.assertTrue(a.allowed_url(org+'/_apis/wit/attachments/id',org,'Vega_SW'))
        self.assertTrue(a.allowed_url(org+'/Vega_SW/_apis/wit/attachments/id',org,'Vega_SW'))
        self.assertTrue(a.allowed_url(org+'/8dbcd5b4-eeb3-44c5-ac52-dcec2c44d052/_apis/wit/attachments/id',org,'Vega_SW'))
        for url in ['http://other/DefaultCollection/_apis/wit/attachments/id',
                    'http://user:password@ado/DefaultCollection/_apis/wit/attachments/id',
                    'http://ado/DefaultCollection/_apis/wit/workitems/1',
                    'http://ado/DefaultCollection/_apis/wit/attachments/%2e%2e/secret']:
            self.assertFalse(a.allowed_url(url,org,'Vega_SW'))

    def fixture(self, root):
        child=root/'commit'; source=child/'preparation'/'workitems.json'
        source.parent.mkdir(parents=True)
        source.write_text(json.dumps({'workItems':[{'id':1234,'state':'AVAILABLE','revision':7}]}))
        def load(path, default=None):
            if path.name=='config.json':return {'azure_devops':{'org_url':'http://ado/Collection','project':'Vega','pat':'fixture-secret'}}
            return json.loads(path.read_text()) if path.exists() else default
        def save(path, value):
            path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(value))
        return child,SimpleNamespace(ROOT=root/'automation',load=load,save=save)

    def test_unicode_project_url_and_backslash_traversal(self):
        org = 'http://ado/Collection'
        self.assertTrue(a.allowed_url(org+'/%E6%B5%8B%E8%AF%95/_apis/wit/attachments/id', org, '测试'))
        self.assertFalse(a.allowed_url(org+'/_apis/wit/attachments/%5c..%5csecret', org, '测试'))

    def test_attachment_is_saved_with_hash_without_executing_or_exposing_secret(self):
        with tempfile.TemporaryDirectory() as tmp:
            child,d=self.fixture(Path(tmp))
            data={'rev':7,'relations':[{'rel':'AttachedFile','url':'http://ado/Collection/_apis/wit/attachments/id',
                                      'attributes':{'name':'../../fixture.json'}}]}
            opener=SimpleNamespace(open=lambda request,timeout:io.BytesIO(json.dumps(data).encode()) if 'workitems' in request.full_url else io.BytesIO(b'{"fixture":1}'))
            with patch.object(a.urllib.request,'build_opener',return_value=opener):result=a.fetch(d,child)
            attachment=result['attachments'][0]
            self.assertEqual(attachment['state'],'AVAILABLE')
            self.assertTrue((child/attachment['path']).resolve().is_relative_to(child.resolve()))
            self.assertEqual((child/attachment['path']).read_bytes(),b'{"fixture":1}')
            self.assertLess(len(Path(attachment['path']).name),40)
            self.assertNotIn('fixture-secret',json.dumps(result))
            # Existing receipt is immutable and avoids silently replacing data.
            with patch.object(a.urllib.request,'build_opener',side_effect=AssertionError('Should not refetch')):
                self.assertEqual(a.fetch(d,child),result)

    def test_changed_revision_cannot_supply_different_fixture(self):
        with tempfile.TemporaryDirectory() as tmp:
            child,d=self.fixture(Path(tmp))
            opener=SimpleNamespace(open=lambda request,timeout:io.BytesIO(b'{"rev":8,"relations":[]}'))
            with patch.object(a.urllib.request,'build_opener',return_value=opener):result=a.fetch(d,child)
            self.assertFalse(result['attachments'])
            self.assertIn('revision changed',result['notes'][0])

    def test_oversize_attachment_keeps_failure_evidence(self):
        with tempfile.TemporaryDirectory() as tmp:
            child,d=self.fixture(Path(tmp))
            data={'rev':7,'relations':[{'rel':'AttachedFile','url':'http://ado/Collection/_apis/wit/attachments/id','attributes':{'name':'fixture.zip'}}]}
            opener=SimpleNamespace(open=lambda request,timeout:io.BytesIO(json.dumps(data).encode()) if 'workitems' in request.full_url else io.BytesIO(b'oversized-data'))
            with patch.object(a.urllib.request,'build_opener',return_value=opener):result=a.fetch(d,child,max_file_bytes=4)
            self.assertEqual(result['attachments'][0]['state'],'UNAVAILABLE')
            self.assertFalse(list((child/'preparation'/'attachments').glob('*.zip')))

if __name__=='__main__':unittest.main()
