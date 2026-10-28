using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Vega.AutoTest.Harness;

namespace Vega.AutoTest.DailyRecovery;
public static class DesktopEvidence
{
 public static void Audit(string caseId,string[] required) {
  var r=new AssertionRecorder();
  var path=Environment.GetEnvironmentVariable("VEGA_DAILY_UI_EVIDENCE");
  if(string.IsNullOrEmpty(path)||!File.Exists(path)) {r.Skip(caseId+".missing","ui-evidence","Actual target desktop workflow","No actual UI receipt supplied");r.Flush();return;}
  using var doc=JsonDocument.Parse(File.ReadAllText(path));var e=doc.RootElement;
  var receiptRoot=Path.GetDirectoryName(Path.GetFullPath(path))!;
  void Check(string id,bool ok,string expected,string actual) {if(ok)r.Pass(caseId+"."+id,"ui-evidence",id,expected,actual);else r.Fail(caseId+"."+id,"ui-evidence",id,expected,actual);}
  var sha=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git") {ArgumentList={"-c","safe.directory="+Evidence.Repo.Replace('\\','/'),"-C",Evidence.Repo,"rev-parse","HEAD"},RedirectStandardOutput=true,UseShellExecute=false,CreateNoWindow=true})!;
  var actualSha=sha.StandardOutput.ReadToEnd().Trim();sha.WaitForExit();
  Check("target",e.GetProperty("sha").GetString()==actualSha,"Current exact commit",actualSha);
  var exe=Path.Combine(Evidence.Repo,"Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64/Vega.exe");
  Check("fingerprint",Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exe)))==e.GetProperty("exeSha256").GetString(),"Target EXE SHA256",e.GetProperty("exeSha256").GetString()!);
  Check("process",e.GetProperty("pid").GetInt32()>0&&DateTimeOffset.TryParse(e.GetProperty("startedAt").GetString(),out _),"Recorded real PID/start time",e.GetProperty("pid").ToString());
  foreach(var id in required) {
   var observed=e.GetProperty("observations").EnumerateArray().FirstOrDefault(x=>x.GetProperty("id").GetString()==id);
   if(observed.ValueKind==JsonValueKind.Undefined) {r.Skip(caseId+"."+id,"ui-evidence",id,"Required actual UI step not completed");continue;}
   var evidence=observed.GetProperty("evidence").GetString()!;
   var file=Path.GetFullPath(Path.Combine(receiptRoot,evidence));
   Check(id,observed.GetProperty("passed").GetBoolean()&&file.StartsWith(receiptRoot+Path.DirectorySeparatorChar)&&File.Exists(file),observed.GetProperty("expected").GetString()!,observed.GetProperty("actual").GetString()!);
  }
  r.Flush();
 }
}
public class WorkItem3690L2Evidence { [Fact] public void ActualEditorWorkflow()=>DesktopEvidence.Audit("3690",["profile-column","selection-refresh","filter","show-all-dead-feature","save-reopen"]); }
public class WorkItem3693L2Evidence { [Fact] public void ActualPostprocessWorkflow()=>DesktopEvidence.Audit("3693",["authorized-login","postprocess-inputs","postprocess-output-comparison"]); }
public class WorkItem3652L2Evidence { [Fact] public void ActualRecipeWorkflow()=>DesktopEvidence.Audit("3652",["authorized-login","binning-delete","counter-delete-and-criteria","immediate-summary"]); }
