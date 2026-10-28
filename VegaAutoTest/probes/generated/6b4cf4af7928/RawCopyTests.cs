using System.Collections.Concurrent;
using System.Diagnostics;
using Vega.AutoTest.Harness;
using Xunit;

// These fixtures execute the unchanged source from Runner's exact target SHA.
// Peripheral config/logging types are stubs; this is not a real wafer/UI test.
namespace Galaxy.Platform.ILogging {
 public interface ILogService { void Information(string c,string s); void Warning(string c,string s); void Error(string c,string s); void Debug(string c,string s); void StackTracedError(string c,string s,Exception e); }
}
namespace Vega.Data.Commons { public enum ChannelMode { DIC_PD1,DIC_PD2 } }
namespace Vega.Data.Measurements {
 public class WaferRunInfo { public string WaferID {get;set;}="FIXTURE"; public long ID {get;set;}=1; public string? RawDataPath{get;set;} public Recipe RecipeSnapshot{get;set;}=new(); public static string GetSafeFilename(string s)=>s; public string GenerateRawDataFileName()=>WaferID; }
 public class Recipe { public string Name{get;set;}="Fixture"; }
}
namespace Vega.Utility.Common {
 public interface IConfigsManager { Collector DataCollectorConfig{get;} Analysis AnalysisConfig{get;} Wafer WaferRunConfig{get;} }
 public class Collector { public bool SaveDICOriginalChannelCollection{get;set;} }
 public class Analysis { public string SharedFolderAddress{get;set;}=""; }
 public class Wafer { public string RawDataStorageDir{get;set;}=""; public int RawDataCopyWaitTimeoutSeconds{get;set;}=1; }
}
namespace Vega.Utility.Helper {
 public static class FolderHelper { public static bool IsLocalWindowsDrivePath(string p)=>Path.IsPathFullyQualified(p)&&!p.StartsWith("\\\\"); }
 public enum EncryptedJsonPurpose { Recipe }
 public static class EncryptedJsonFileSerializer { public static void SerializeLargeObject(object o,string p,EncryptedJsonPurpose purpose)=>throw new NotSupportedException("Not part of copy behavior fixtures"); }
}
namespace Vega.AutoTest.Generated {
 public class Config : Vega.Utility.Common.IConfigsManager { public Vega.Utility.Common.Collector DataCollectorConfig{get;}=new(); public Vega.Utility.Common.Analysis AnalysisConfig{get;}=new(); public Vega.Utility.Common.Wafer WaferRunConfig{get;}=new(); }
 public class Logger : Galaxy.Platform.ILogging.ILogService {
  public ConcurrentQueue<string> Lines=new(); public void Information(string c,string s)=>Lines.Enqueue("INFO "+s); public void Warning(string c,string s)=>Lines.Enqueue("WARN "+s); public void Error(string c,string s)=>Lines.Enqueue("ERROR "+s); public void Debug(string c,string s)=>Lines.Enqueue("DEBUG "+s); public void StackTracedError(string c,string s,Exception e)=>Error(c,s);
 }
 public class RawCopyTests : IDisposable {
  private readonly string root=Path.Combine(Path.GetTempPath(),"vega-copy-contract-"+Guid.NewGuid().ToString("N"));
  public RawCopyTests(){Directory.CreateDirectory(root);}
  private async Task Check(string id,Func<Task> body) { var r=new AssertionRecorder();try{await body();r.Pass(id,"logic",id,"Copy behavior matches contract","Matched");}catch(Exception e){r.Fail(id,"logic",id,"Copy behavior matches contract",e.ToString());throw;}finally{r.Flush();} }
  private Config Configuration(){var c=new Config();c.AnalysisConfig.SharedFolderAddress=root;c.WaferRunConfig.RawDataStorageDir=root;return c;}
  [Fact] public Task FailedDestinationIsThrottledAndWarnedOnce()=>Check("raw-copy.retry-and-warning",async()=>{
   var source=Path.Combine(root,"FIXTURE_1");Directory.CreateDirectory(source);
   for(int i=0;i<32;i++)File.WriteAllText(Path.Combine(source,$"{i}.bin"),"original");
   var blocker=Path.Combine(root,"blocker");File.WriteAllText(blocker,"Not a directory");
   var logger=new Logger();var stopwatch=Stopwatch.StartNew();
   await new Vega.BL.DataReporting.RawDataStorageHelper(logger,Configuration()).CopyRawDataToLocal(new(){RawDataPath=Path.Combine(blocker,"out")});
   Assert.Equal(1,logger.Lines.Count(s=>s.StartsWith("WARN ")&&s.Contains("hit a failure")));
   Assert.DoesNotContain(logger.Lines,s=>s.StartsWith("ERROR "));
   Assert.Contains(logger.Lines,s=>s.Contains("retry_exhausted"));
   Assert.Equal(32,Directory.GetFiles(source).Length);
   Assert.InRange(stopwatch.ElapsedMilliseconds,500,7000);
  });
  [Fact] public Task ConcurrentCopiesKeepWaferPayloadsSeparate()=>Check("raw-copy.concurrent-source-isolation",async()=>{
   foreach(var id in new[]{"A","B"}){var source=Path.Combine(root,id+"_1");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"data.bin"),id);}
   var helper=new Vega.BL.DataReporting.RawDataStorageHelper(new Logger(),Configuration());
   await Task.WhenAll(new[]{"A","B"}.Select(id=>helper.CopyRawDataToLocal(new(){WaferID=id,RawDataPath=Path.Combine(root,"output-"+id)})));
   foreach(var id in new[]{"A","B"})Assert.Equal(id,File.ReadAllText(Path.Combine(root,"output-"+id,"data.bin")));
  });
  public void Dispose(){Directory.Delete(root,true);}
 }
}
