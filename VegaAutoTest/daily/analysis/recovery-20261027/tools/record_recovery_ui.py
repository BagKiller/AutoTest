"""Package observed desktop evidence, not a verdict. Runner audits the receipt."""
import argparse
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
import daily as d
import coverage_preparation as coverage

p=argparse.ArgumentParser()
p.add_argument('--child',type=Path,required=True)
p.add_argument('--slot',required=True)
p.add_argument('--session',type=Path,required=True)
p.add_argument('--observations',type=Path,required=True)
a=p.parse_args()
session=d.load(a.session/'session.json')
target=d.load(a.child/'execution-target.json')
if session['repo']!=target['repo']:raise ValueError('Session target mismatch')
process=session.get('process',{})
data=dict(sha=target['head'],exeSha256=coverage.digest(Path(session['bin'])/'Vega.exe').upper(),
          pid=process.get('pid',0),startedAt=process.get('startedAt'),profile=session['profile'],
          simulator=True,gem=False,generation='Gen2',sessionPath=str(a.session),
          userDataFolder=session['userDataFolder'],recordedAt=d.now_cn().isoformat(),
          observations=d.load(a.observations),inputs=[],source='Actual computer-use observations; verdict remains exclusively Runner output')
folder=a.child/'ui'/a.slot
data['files']={str(f.relative_to(folder)):coverage.digest(f) for f in folder.glob('*') if f.is_file() and f.suffix in ('.png','.txt','.json') and f.name!='evidence.json'}
d.save(folder/'evidence.json',data)
print(folder/'evidence.json')
