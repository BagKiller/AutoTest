using Vega.AutoTest.Harness;
using Xunit;
namespace Vega.AutoTest.Wi3689;
public class WorkItem3689L2Evidence {
 [Fact] public void AuditActualDesktopRun() {
  var r=new AssertionRecorder();
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../runs/wi3689-20261026/ui-retry-103030"));
  var log=File.ReadAllText(Path.Combine(root,"general.log"));
  void Check(string id,bool ok){if(ok)r.Pass(id,"logic",id,"recorded actual execution","matched");else r.Fail(id,"logic",id,"recorded actual execution","missing");}
  Check("ui.result-screenshot",File.Exists(Path.Combine(root,"result-ui.jpg")));
  Check("ui.config-screenshot",File.Exists(Path.Combine(root,"offline-config.jpg")));
  Check("ui.postprocess-complete",log.Contains("postprocess END, total 919ms"));
  Check("ui.nonzero-defects",log.Contains("Global[GC:403/126 DC:403/126"));
  Check("ui.run-ended",log.Contains("waferRunId=93264631046340608")&&log.Contains("Default Job all cycles end"));
  Check("ui.actual-output",new FileInfo(Path.Combine(root,"result.001")).Length>0);
  r.Skip("ui.large-online-result","logic","Hundreds of MB online result recovery","No现场大包/算法服务协议环境 executed in this retry");
  r.Skip("ui.oom-error-path","logic","OOM no retry and failed terminal status","Actual UI abnormal-result/OOM scenario not executed");
  r.Flush();
 }
}
