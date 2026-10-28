import hashlib
import json
from pathlib import Path
from types import SimpleNamespace
import tempfile
import unittest
import artifact_cleanup as a


class CleanupTests(unittest.TestCase):
    def test_only_owned_scratch_is_removed_and_raw_results_remain(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);root=base/'scratch';child=base/'commit';layer=child/'runner'/'run'/'logic';layer.mkdir(parents=True)
            target=root/hashlib.sha256(str(layer.resolve()).encode()).hexdigest().upper()[:24]
            target.mkdir(parents=True);(target/'compiled.dll').write_bytes(b'compiled fixture')
            (layer/'test-artifacts.json').write_text(json.dumps({'artifacts':str(target)}))
            result=layer.parent/'result.json';result.write_text('{"verdict":"PASS"}')
            def save(path,value):path.write_text(json.dumps(value))
            d=SimpleNamespace(save=save,now_cn=lambda:SimpleNamespace(isoformat=lambda:'fixture'))
            records=a.reclaim(d,child,root)
            self.assertFalse(target.exists());self.assertTrue(result.exists());self.assertTrue(records[0]['rawEvidencePreserved'])
            self.assertEqual(a.reclaim(d,child,root),[])

    def test_mapping_cannot_delete_another_directory(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);child=base/'commit';child.mkdir();outside=base/'important';outside.mkdir();(outside/'keep.txt').write_text('keep')
            (child/'test-artifacts.json').write_text(json.dumps({'artifacts':str(outside)}))
            result=a.reclaim(None,child,base/'scratch')
            self.assertEqual(result[0]['state'],'SKIPPED');self.assertTrue((outside/'keep.txt').exists())

    def test_product_intermediates_cleanup_keeps_source_deployment_and_tracked_inputs(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);workspaces=base/'workspaces';repo=workspaces/'batch_attempt';child=base/'commit';child.mkdir()
            for name in ['Alpha','Kept','Vega','保留']:
                project=repo/name;project.mkdir(parents=True);(project/(name+'.csproj')).write_text('<Project />')
            for name in ['Alpha/obj/temp.bin','Alpha/bin/temp.dll','Kept/bin/keep.dll','Vega/bin/Vega.dll','保留/bin/keep.dll']:
                path=repo/name;path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b'fixture bytes')
            (child/'manifest.json').write_text(json.dumps({'batchId':'batch','head':'sha'}))
            (child/'execution-target.json').write_text(json.dumps({'repo':str(repo)}))
            (child/'completion.json').write_text('{"finished":true}')
            def load(path,default=None):return json.loads(path.read_text()) if path.exists() else default
            def git(*args,repo):return 'sha' if args[0]=='rev-parse' else ('kept/bin/keep.dll\0保留/bin/keep.dll\0' if args[0]=='ls-files' else '')
            d=SimpleNamespace(WORKSPACES=workspaces,load=load,git=git,save=lambda path,value:path.write_text(json.dumps(value)))
            result=a.reclaim_workspace(d,child,compress_below_gib=0)
            self.assertEqual(result['state'],'RECLAIMED_INTERMEDIATES')
            self.assertFalse((repo/'Alpha/obj').exists());self.assertFalse((repo/'Alpha/bin').exists())
            self.assertEqual((repo/'Vega/bin/Vega.dll').read_bytes(),b'fixture bytes')
            self.assertTrue((repo/'Kept/bin/keep.dll').exists());self.assertTrue((repo/'Alpha/Alpha.csproj').exists())
            self.assertTrue((repo/'保留/bin/keep.dll').exists())

    def test_product_cleanup_refuses_unfinished_or_unowned_workspace(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);child=root/'commit';child.mkdir();repo=root/'workspaces'/'user-checkout';repo.mkdir(parents=True)
            (repo/'keep.txt').write_text('user source')
            data={'manifest.json':{'batchId':'batch','head':'sha'},'execution-target.json':{'repo':str(repo)},'completion.json':{'finished':True}}
            d=SimpleNamespace(WORKSPACES=root/'workspaces',load=lambda path,default=None:data.get(path.name,default))
            self.assertEqual(a.reclaim_workspace(d,child)['state'],'SKIPPED')
            self.assertTrue((repo/'keep.txt').exists())

if __name__=='__main__':unittest.main()
