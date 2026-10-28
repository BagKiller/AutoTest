"""Reviewed scope for this repair regression; full UI/remote gaps are explicit.

All reused/generated component methods were actually trial-run before freeze.
No component result is promoted to actual application/hardware acceptance.
"""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[2]))
import daily as d

root=Path(Path('C:/AutoTest/repair-campaign-path.txt').read_text().strip())
registry=d.load(d.REGISTRY);sched=d.load(d.SCHEDULING);plan=d.load(root/'plan.json')

# These expectations were checked against each SHA's diff, original assertions
# and work-item snapshot. They are not instructions extracted from the work item.
scopes={
 '00b3004a':('Ramdisk filename excludes nonzero run seed and exactly equals AnalysisDataStorage.DataFileName',
             'Remote Linux writer filename, algorithm request path and defect output; failed scan followed by same-wafer rerun',
             ['Vega300-HWv1','Vega500']),
 '6b4cf4af':('An actual destination I/O failure produces one warning, throttled retry and retained source; concurrent wafers retain distinct payloads',
             'Actual disk-full wafer workflow, status-bar refresh throttling and independent warning/error history', ['Vega500']),
 '227ace85':('Every V100 throughput/polarizer/power combination has one shipped BPS rule, and PMT/config subpaths match resources',
             'Actual V100 recipe editor recommendations and HT/Oblique/S Simulator scan setup using the same subtable', ['Vega100-PIC']),
 'bd9ccb55':('Built-in PMT conditions equal shipped JSON and failed loading emits a warning',
             'PMT Gain calibration with missing/invalid condition JSON on supported shared-table profiles', ['Vega300-HWv1','Vega500']),
 '19c18be5':('Notch resolver maps incidence/family and invalid configuration to the documented DN/DW channels',
             'Actual Vega100 DN/DW selection, recipe without the requested alignment channel, collection routing and unchanged recipe/result channels', ['Vega100-PIC']),
 '9fa008a9':('SPA toggles update TargetDefectMap, composite channels reconcile idempotently, and unmanaged definitions remain intact',
             'Actual recipe edit/save/reopen and WaferRun DCA defect display against the work-item input', ['Vega300-HWv1']),
 'd117a21d':('Small nonzero values use scientific notation with documented rounding/culture behavior',
             'Actual Uniformity Verify result grid displays a known small nonzero standard deviation using the converter', ['Vega300-HWv1']),
 'eba9945a':('Known flat/circle geometry, invalid edges, scan-window limits, offsets, serialization and backward compatibility satisfy original regression assertions',
             'Actual flat/notch recipe configuration, offset alarms, alignment and diagnostic/result image workflow', ['Vega300-HWv1']),
 'b428717a':('Known rectangular bright bands return the independent outer crossing; invalid/too-short signals remain unknown; smoothing does not cross an invalid gap',
             'Actual DUV and bright-field alignment using captured signal fixtures, including masked/NaN and wraparound cases', ['Vega300-HWv1']),
 'c30defb8':('Committed lock graph resolves Project nodes and inherited RID dependencies; target builds from this SHA',None,[]),
}

with d.operation_lock.acquire(d.ROOT/'daily'/'.operation.lock'):
 for slot in plan['commitSlots']:
    child=Path(slot['batch']);cm=d.load(child/'manifest.json');sha=slot['sha'];prefix=sha[:8]
    if (child/'plan.json').exists():
        continue
    # Verify the actual copied/trial evidence before referring to it in a gap.
    authoring=d.load(child/'preparation'/'authoring.json');cid=authoring['caseId']
    expectation,ui_scope,profiles=scopes[prefix]
    evidence=[]
    for path in [child/'preparation'/'case-check.json',child/'preparation'/'authoring.json',child/'preparation'/'attachments'/'index.json']:
        if path.exists():evidence.append(dict(path=str(path.relative_to(child)),sha256=d.coverage_contract.digest(path),
            action='Generate/reuse real component assertions, compile/discover them, retrieve work-item assets',
            outcome='Actual component trial recorded separately; UI and physical collector coverage are not inferred'))
    trial=d.load(child/'preparation'/'trial'/'outcomes.json',{})
    for run in trial.get('runs',[]):
        path=Path(run['resultPath'])
        evidence.append(dict(path=str(path.relative_to(child)),sha256=d.coverage_contract.digest(path),
                             action='Execute the authored component test before freezing',outcome=run['verdict']+'/'+run['verdictCode']))
    wis=cm['commits'][0]['workItems']
    if prefix=='bd9ccb55':
        failure=d.load(child/'preparation'/'case-check-error.json');path=Path(failure['evidence'])
        evidence.append(dict(path=str(path.relative_to(child)),sha256=d.coverage_contract.digest(path),
                             action='Build the exact independent SHA before running the generated PMT tests',outcome=failure['reason']))
        requirements=[dict(id='pmt-exact-sha-build',workItems=wis,steps=['Build isolated target SHA','Discover generated PMT fallback tests'],
            expected=expectation,uiRequired=False,profiles=[],caseIds=[],gap='Exact product SHA fails CS0103 at CaliCommonDataDefinition.cs:68,73; no product DLL or fabricated Runner verdict is substituted.',attemptEvidence=evidence)]
    else:
        requirements=[dict(id=prefix+'-component-regression',workItems=[3706] if prefix=='00b3004a' else wis,
            steps=['Use this SHA isolated build and original/authored deterministic inputs','Execute the scoped component regression assertions'],
            expected=expectation,uiRequired=False,profiles=[],caseIds=[cid])]
    if prefix=='00b3004a':
        requirements.append(dict(id='same-wafer-cache-isolation',workItems=[3660],
            steps=['Create two same-wafer runs with distinct IDs','Check collection separation and concurrent zero-ID fallback'],
            expected='Different runs/collections have different cache keys; concurrent first access in one run has one stable key',
            uiRequired=False,profiles=[],caseIds=['commit-'+sha[:12]+'-cache-isolation']))
    if ui_scope:
        # No historical UI receipt, merely instantiated control, or managed unit
        # probe is used as a substitute for an actual target-application flow.
        if prefix=='00b3004a':
            gap='Managed filename/cache cases were generated/reused and executed. Full remote Linux collector/algorithm deployment and trusted capture/result fixture are not available in this Simulator-only campaign; actual remote UI rerun/analysis remains unverified.'
        elif prefix=='bd9ccb55':
            gap='The exact target product does not compile; this UI cannot be launched. Original compile errors and generated test source are retained.'
        else:
            gap='Targeted component tests were authored/adapted, compiled, discovered and trial-run. They do not execute this actual application workflow; UI automation/data prerequisites for this specific flow are still not completed. No UI PASS is claimed.'
        requirements.append(dict(id=prefix+'-actual-ui',workItems=wis,steps=[ui_scope],expected='Complete the actual target-version flow and compare the observed output against the work-item acceptance criteria',
            uiRequired=True,profiles=profiles,caseIds=[],gap=gap,attemptEvidence=evidence))
    if prefix=='c30defb8':
        requirements[0].update(gap='Graph validation and the normal isolated build are exercised. Build uses unlocked restore; reproducible locked-mode restore is not established.',attemptEvidence=evidence)
    source=child/'preparation'/'workitems.json';diff=root/(prefix+'.diff')
    review=dict(sha=sha,diffSha256=d.coverage_contract.digest(diff),workItemsSha256=d.coverage_contract.digest(source),requirements=requirements)
    path=child/'preparation'/'review.json';d.save(path,review)
    d.individual.freeze_one(d,plan,sha,registry,sched,path)
    print('FROZEN '+prefix,flush=True)
 d.individual.aggregate(d,root,d.load(root/'manifest.json'),plan,registry)
