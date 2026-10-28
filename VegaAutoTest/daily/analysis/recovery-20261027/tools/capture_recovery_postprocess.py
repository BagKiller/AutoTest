"""Archive this observed Simulator replay's outputs and scoped product logs."""
from pathlib import Path
import shutil
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
import daily as d
import coverage_preparation as c

campaign=d.ROOT/'daily/runs/20261026T233206+0800_ed766f/revalidation-20261027T101806_3125fe'
ui=campaign/'commits/f7ed2b5dd68c9f17b67a633594cdb59ea6f1af38/ui/ui-Vega300-HWv1'
log=Path(r'C:\Users\Administrator\AppData\Local\Vega\LogFiles\Log_20261027\General\Log_General_2026102710.txt')
lines=[line for line in log.read_text(encoding='utf-8-sig',errors='replace').splitlines() if line.startswith('2026-10-27 10:51:') and ':10536]' in line]
(ui/'general.log').write_text('\n'.join(lines),encoding='utf-8')
source=Path(r'C:\AutoTest\data\validation\Vega300-HWv1\KLARF\Vega200-0_JRH_EPI_Particle_dual_LP01__Slot1__261027105120_Reprocess.001')
shutil.copy2(source,ui/'result.001')
inputs=[d.ROOT/'daily/analysis/individual-20261022'/name for name in ('JRH_EPI_Particle_dual.recipe','Wafer_1_202610215854_Oblique_20261021215958.json','Carr003-01_261021194832_Normal_20261021195012.json')]
d.save(ui/'product-output.json',dict(waferRunId='93355853799751680',waferID='Wafer_1_202610275120',
    processPid=10536,postprocessMilliseconds=103,gc=359,dc=359,filteredGc=82,filteredDc=82,events=1274,
    inputs=[dict(path=str(p),sha256=c.digest(p)) for p in inputs],
    output=dict(path=str(source),archived='result.001',size=source.stat().st_size,sha256=c.digest(source)),
    limitations='No independent expected full SPA output baseline. Counts are observations, not newly invented acceptance oracles.'))
print(ui/'product-output.json')
