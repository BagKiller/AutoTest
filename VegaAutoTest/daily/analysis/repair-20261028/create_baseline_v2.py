"""Version a brittle Windows path assertion without touching frozen baselines."""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import daily as d

source=d.ROOT/'probes'/'Vega.AutoTest.CommitProbe'
version=sys.argv[1] if len(sys.argv)>1 else '2'
target=d.ROOT/'probes'/('Vega.AutoTest.CommitProbeV'+version)
origin={}
for path in source.iterdir():
    if path.suffix not in ('.cs','.csproj'):
        continue
    content=path.read_text(encoding='utf-8-sig')
    name='Vega.AutoTest.CommitProbeV'+version+'.csproj' if path.suffix=='.csproj' else path.name
    origin[path.name]=d.coverage_contract.digest(path)
    if path.name=='DailyCommitBatchLogic.cs':
        before='Equal("pmt-config-subpath", ToolFeaturesHelper.IsVega100 ? Path.Combine("Configs", "Vega100Configs") : "Configs",\n            PMTCaliConditions.ConfigsSubPath);'
        after='Equal("pmt-config-subpath", ToolFeaturesHelper.IsVega100 ? "Configs/Vega100Configs" : "Configs",\n            PMTCaliConditions.ConfigsSubPath.Replace(\'\\\\\', \'/\'));'
        if before not in content:raise RuntimeError('Expected original assertion changed')
        content=content.replace(before,after)
    if version!='2' and path.suffix=='.csproj':
        content=content.replace('<PropertyGroup>','<PropertyGroup>\n    <AssemblyName>Vega.AutoTest.CommitProbe</AssemblyName>',1)
    if version!='2' and path.name=='WorkItem3644Logic.cs':
        content=content.replace('"Vega.AutoTest.Spike.StubLogService, Vega.AutoTest.CommitProbe"','typeof(Vega.AutoTest.Spike.StubLogService).AssemblyQualifiedName')
        content=content.replace('"Vega.AutoTest.CommitProbe.ModelMarkersService, Vega.AutoTest.CommitProbe"','typeof(ModelMarkersService).AssemblyQualifiedName')
    output=target/name
    output.parent.mkdir(parents=True,exist_ok=True)
    if output.exists() and output.read_text(encoding='utf-8')!=content:raise RuntimeError('V2 snapshot already differs')
    output.write_text(content,encoding='utf-8')
d.save(target/'origin.json',dict(source=str(source),sha256=origin,
    correction='Normalize only directory separators; expected model-specific folder and assertion counts unchanged. Wrong folders still fail.'))
registry=d.load(d.REGISTRY)
old=next(c for c in registry['cases'] if c['id']=='daily-recent10')
import copy
full=copy.deepcopy(old);full['id']='daily-recent10-v'+version;full['title']='Daily core probes v'+version+': canonical paths and stable bootstrap identity'
for layer in full['layers'].values():layer['project']='probes/Vega.AutoTest.CommitProbeV'+version+'/Vega.AutoTest.CommitProbeV'+version+'.csproj'
focused=copy.deepcopy(full);focused.update(id='daily-pmt-path-separators-v'+version,title='PMT config path semantic equivalence across slash styles')
for layer in focused['layers'].values():layer.update(filter='FullyQualifiedName~DailyCommitBatchLogic.PmtModelConfigurationPathAndLaserDefault',minAssertions=2)
for case in (full,focused):
    if not any(c['id']==case['id'] for c in registry['cases']):registry['cases'].append(case)
d.save(d.REGISTRY,registry)
sched=d.load(d.SCHEDULING)
sched['baselineCases']=[full['id'] if cid in ('daily-recent10','daily-recent10-v2') else cid for cid in sched['baselineCases']]
for tag,ids in sched['tagCases'].items():sched['tagCases'][tag]=[full['id'] if cid in ('daily-recent10','daily-recent10-v2') else cid for cid in ids]
d.save(d.SCHEDULING,sched)
print('New independent baseline v2 registered; original source/definitions/results unchanged')
