using Vega.AutoTest.Harness;
using Xunit;
namespace Vega.AutoTest.Wi3688;
public class WorkItem3688L2Evidence {
 [Fact] public void ActualProductDiskFullContract(){
  var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../runs/wi3688-20261026/ui"));
  var log=File.ReadAllLines(Path.Combine(root,"general.log"));
  var relevant=log.Where(x=>x.Contains("[RawDataStorageHelper]")&&x.Contains("Carr001-10_261026113428")).ToArray();
  var r=new AssertionRecorder();
  void Check(string id,bool ok,string expected,string actual){if(ok)r.Pass(id,"logic",id,expected,actual);else r.Fail(id,"logic",id,expected,actual);}
  Check("wi3688.ui-warning-history",File.Exists(Path.Combine(root,"warning-history.jpg")),"actual UI warning history retained","warning-history.jpg");
  Check("wi3688.ui-actual-source-copy",relevant.Any(x=>x.Contains("source files: 4 (timeout 8s)")),"real product copying four fixture files",string.Join("\n",relevant.Where(x=>x.Contains("Start to copy"))));
  Check("wi3688.ui-first-error-not-flood",relevant.Count(x=>x.Contains("hit a failure"))==1&&relevant.Any(x=>x.Contains("磁盘空间不足")),"single initial disk-full warning",string.Join("\n",relevant.Where(x=>x.Contains("hit a failure"))));
  Check("wi3688.ui-stop-without-retry-timeout",!relevant.Any(x=>x.Contains("timed out")||x.Contains("outcome: timed_out")),"stop fatally before timeout",string.Join("\n",relevant.Where(x=>x.Contains("End copying"))));
  Check("wi3688.ui-single-warning",relevant.Count(x=>x.Contains("<Warning>"))==1,"one task failure notice",relevant.Count(x=>x.Contains("<Warning>")).ToString());
  r.Flush();
 }
}
