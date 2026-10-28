"""User-authorized targeted campaign helpers; never edits a historical frozen plan."""
import argparse
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
import daily as d
import coverage_preparation as coverage


def requirements(commit):
    sha = commit['sha'][:8]
    def req(id, steps, expected, ui=False, gap=None):
        cid = {'5b822733':'wi3690-recovery','f7ed2b5d':'wi3693-recovery','e99f34a1':'wi3652-dialog-recovery'}.get(sha,'dependency-lock-recovery-v3')
        result = dict(id=id, workItems=commit.get('workItems',[]), steps=steps, expected=expected,
                      uiRequired=ui, profiles=(['Vega100-PIC'] if sha=='5b822733' else ['Vega300-HWv1']) if ui else [], caseIds=[cid])
        if gap: result['gap']=gap
        return result
    if sha=='5b822733':
        return [
            req('3690-resolver', ['Run target-SHA source-linked resolver on PIC/Coherent/HWv1/HWv2 fixtures, dead exact tokens, OR alternative, absent profile map and disabled cross-model Feature'], '12 recorded cases: selected grants control state; Gen12/Vega3000/gen2 are dead; known OR survives; disabled Feature tooltip; empty map is NotGated; display properties do not serialize. Logic is appropriate for deterministic resolver and serialization behavior.'),
            req('3690-editor-ui', ['Official authorized Vega100 Simulator startup/login', 'Open actual target ComponentConfigEditor; observe Profile column; switch profile; Filter and Show All; inspect dead/Feature state; save and reopen isolated config'], 'Actual UI displays token-to-profile mapping, refreshes selection, applies fourth filter, exposes warnings and preserves saved JSON.', True)]
    if sha=='f7ed2b5d':
        return [
            req('3693-nearest-merge', ['Invoke target DLL MergeDefects with candidates in all four quadrants, two candidates, and an ineligible nearest candidate'], 'Six deterministic fixtures check centered search, exactly one nearest merge, and no fall-through. Inputs and expected state derive from explicit commit merge semantics, not guessed production result counts.'),
            req('3693-spa-and-ui', ['Authorized Vega300 Simulator login', 'Configure original Normal/Oblique offline inputs; run postprocess and inspect actual output', 'Validate SPA input/output collection index, unique event IDs and EventLookup against a trusted baseline'], 'Postprocessing completes and output matches an independently defined expected result; no cross-collection SPA contamination.', True,
                'Real merge fixtures prepared and discovered (6 cases); work-item snapshot contains title only. No trusted multi-collection SPA output oracle has been supplied or derived in the bounded preparation. Actual UI can exercise workflow, but full SPA collection/event-link result acceptance remains unverified. This case remains partial-acceptance.')]
    if sha=='e99f34a1':
        return [
            dict(req('3652-original-disabled-binning', ['Run existing unchanged SummaryBinningEnabledTests through daily-supplement with enabled/disabled/missing Binning and persisted summaries'], 'Disabled or missing Binning shows a single unbinned row and does not reuse an enabled bin configuration; original product assertions retained.'), caseIds=['daily-supplement']),
            req('3652-dialog-matrix', ['Invoke unchanged product RecipeElementDialogCompletionTests across all supplied MemberData combinations on this SHA'], 'Binning/Counters delete and clear respect confirm/cancel and MouseLeave timing; criteria confirmation updates valid names; duplicate notifications preserve intended summary behavior. Original assertions are retained.'),
            req('3652-actual-ui', ['Authorized Vega300 Simulator login', 'Edit recipe Binning and Counters; confirm deletion/criteria; inspect immediate downstream summary before further MouseLeave'], 'Deleted entries disappear and confirmed valid counters update immediately in actual UI.', True)]
    return [req('nuget-lock-graph', ['Parse this exact committed packages.lock.json and check all framework/package nodes and dependency references', 'Build this exact SHA and execute daily regressions'], 'Version2 lock parses; dependency references resolve; fresh build and tests provide integration evidence. No UI workflow changes in this diff; graph validation is headless.', False,
                'Daily build uses unlocked restore, so successful build is not proof that this committed lock passes locked restore. Dependency graph is only partial acceptance; lockfile reproducibility remains unverified.')]


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('root',type=Path)
    parser.add_argument('--review',required=True)
    args=parser.parse_args()
    child=args.root/'commits'/args.review
    if (child/'plan.json').exists(): raise ValueError('Already frozen')
    manifest=d.load(child/'manifest.json');scope=d.load(child/'scope.json')
    source=child/'preparation'/'workitems.json'
    review=dict(sha=manifest['head'],diffSha256=coverage.digest(Path(scope['diff'])),workItemsSha256=coverage.digest(source),requirements=requirements(manifest['commits'][0]))
    d.save(child/'preparation'/'review.json',review)
    print(child/'preparation'/'review.json')


if __name__=='__main__':main()
