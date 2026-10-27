import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET

root = Path(r'C:\AutoTest\workspaces\individual-20261022-20261023')
out = Path(__file__).resolve().parent
project = out.parents[2] / 'probes' / 'Vega.AutoTest.IndividualCommits'
refs = {}
for module in ('Vega.BL.WaferAlignment', 'Vega.BL.WaferRun'):
    for dll in (root / module / 'bin' / 'Debug' / 'net8.0-windows').glob('*.dll'):
        refs[dll.name.lower()] = dll
    assets = json.loads((root / module / 'obj' / 'project.assets.json').read_text(encoding='utf-8'))
    packages = Path(next(iter(assets['packageFolders'])))
    for target in assets['targets'].values():
        for name, library in target.items():
            metadata = assets['libraries'][name]
            if metadata['type'] != 'package':
                continue
            for rel in library.get('runtime', {}):
                path = packages / metadata['path'] / rel
                if path.suffix == '.dll' and path.exists():
                    refs.setdefault(path.name.lower(), path)
xml = ET.Element('Project')
group = ET.SubElement(xml, 'ItemGroup')
for path in refs.values():
    reference = '$(VEGA_SW_ROOT)\\' + str(path.relative_to(root)) if path.is_relative_to(root) else str(path)
    ref = ET.SubElement(group, 'Reference', Include=reference)
    ET.SubElement(ref, 'Private').text = 'true'
ET.indent(xml)
ET.ElementTree(xml).write(project / 'FreshReferences.props', encoding='utf-8', xml_declaration=True)
proof = {'repo': str(root), 'targetHead': '10c33df5e69aa932bea8d843a29d8f6530d1fd73',
         'scope': 'Fresh targeted modules; no prior-batch product DLLs',
         'dlls': [{'path': str(p), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
                  for p in refs.values() if p.is_relative_to(root)]}
(out / 'build-reference.json').write_text(json.dumps(proof, indent=2), encoding='utf-8')
print(f'Prepared {len(refs)} references; {len(proof["dlls"])} freshly built project DLL fingerprints')
