using System.Collections.Concurrent;
using System.Diagnostics;
using Vega.AutoTest.Harness;
using Vega.BL.DataReporting;
using Vega.Data.Measurements;
using Vega.Utility.Common;
using Xunit;
namespace Galaxy.Platform.ILogging { public interface ILogService {void Information(string c,string s);void Warning(string c,string s);void Error(string c,string s);void Debug(string c,string s);} }
namespace Vega.Data.Commons {public enum ChannelMode {DIC_PD1,DIC_PD2} }
namespace Vega.Data.Measurements {public class WaferRunInfo {public string WaferID{get;set;}="WI3688";public long ID{get;set;}=1;public string? RawDataPath{get;set;} public Recipe RecipeSnapshot{get;set;}=new();public static string GetSafeFilename(string s)=>s;public string GenerateRawDataFileName()=>"WI3688";}public class Recipe {public string Name{get;set;}="WI3688";} }
namespace Vega.Utility.Common {public interface IConfigsManager {Collector DataCollectorConfig{get;} Analysis AnalysisConfig{get;} Wafer WaferRunConfig{get;}}public class Collector{public bool SaveDICOriginalChannelCollection{get;set;}}public class Analysis{public string SharedFolderAddress{get;set;}="";}public class Wafer{public string RawDataStorageDir{get;set;}=""; public int RawDataCopyWaitTimeoutSeconds{get;set;}=4;} }
namespace Vega.Utility.Helper { public static class FolderHelper {public static bool IsLocalWindowsDrivePath(string p)=>Path.IsPathFullyQualified(p)&&!p.StartsWith("\\\\");}public enum EncryptedJsonPurpose{Recipe}public static class EncryptedJsonFileSerializer{public static void SerializeLargeObject(object o,string p,EncryptedJsonPurpose purpose)=>throw new NotSupportedException("Not part of raw-copy tests");} }
namespace Vega.AutoTest.Wi3688 {
public class Config : IConfigsManager{public Collector DataCollectorConfig{get;}=new();public Analysis AnalysisConfig{get;}=new();public Wafer WaferRunConfig{get;}=new();}
public class Logger : Galaxy.Platform.ILogging.ILogService {public ConcurrentQueue<string> Lines=new();public void Information(string c,string s)=>Lines.Enqueue("INFO "+s);public void Warning(string c,string s)=>Lines.Enqueue("WARN "+s);public void Error(string c,string s)=>Lines.Enqueue("ERROR "+s);public void Debug(string c,string s)=>Lines.Enqueue("DEBUG "+s);}
public class WorkItem3688Logic {
 [Fact] public async Task RealDiskFullDoesNotRepeatCopyUntilTimeout(){
  var r=new AssertionRecorder();var root=Path.Combine(Path.GetTempPath(),"wi3688-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var source=Path.Combine(root,"WI3688_1");Directory.CreateDirectory(source);
  for(int i=0;i<4;i++){using var f=File.Create(Path.Combine(source,$"raw{i}.bin"));f.SetLength(4*1024*1024);}
  var mount=@"C:\AutoTest\data\wi3688-volume";var output=Path.Combine(mount,"out");Directory.CreateDirectory(output);
  var drive=DriveInfo.GetDrives().First(d=>d.VolumeLabel=="WI3688_TEST");
  using(var f=File.Create(Path.Combine(mount,"filler.bin"))){f.SetLength(drive.AvailableFreeSpace-1024*1024);}
  var config=new Config();config.AnalysisConfig.SharedFolderAddress=root;config.WaferRunConfig.RawDataStorageDir=mount;
  var log=new Logger();var sw=Stopwatch.StartNew();await new RawDataStorageHelper(log,config).CopyRawDataToLocal(new WaferRunInfo{RawDataPath=output});sw.Stop();
  File.WriteAllLines(Path.Combine(RunContext.Dir,"disk-full.log"),log.Lines);var lines=log.Lines.ToArray();
  bool diskFull=lines.Any(x=>x.Contains("磁盘空间不足")||x.Contains("not enough space",StringComparison.OrdinalIgnoreCase)||x.Contains("disk is full",StringComparison.OrdinalIgnoreCase));
  void Check(string id,bool pass,string expected,string actual){if(pass)r.Pass(id,"logic",id,expected,actual);else r.Fail(id,"logic",id,expected,actual);}
  Check("wi3688.real-disk-full",diskFull,"Actual disk-full File.Copy failure",string.Join("\n",lines.Where(x=>x.Contains("First failure"))));
  Check("wi3688.one-first-warning",lines.Count(x=>x.Contains("hit a failure"))==1,"one initial failure warning",lines.Count(x=>x.Contains("hit a failure")).ToString());
  Check("wi3688.no-file-error-flood",!lines.Any(x=>x.StartsWith("ERROR")),"no per-file error flood",lines.Count(x=>x.StartsWith("ERROR")).ToString());
  Check("wi3688.source-retained",Directory.GetFiles(source).Length==4,"4 original source files retained",Directory.GetFiles(source).Length.ToString());
  Check("wi3688.stop-before-timeout",sw.ElapsedMilliseconds<2000&&!lines.Any(x=>x.Contains("outcome: timed_out")),"fatal disk-full stops within 2s, before configured 4s timeout",sw.ElapsedMilliseconds+"ms; "+lines.LastOrDefault());
  Check("wi3688.single-failure-notice",lines.Count(x=>x.StartsWith("WARN"))==1,"one failure notice for task",lines.Count(x=>x.StartsWith("WARN")).ToString());
  r.Flush();Assert.True(diskFull,"Test must hit actual disk-full, not permissions or other errors");
 }
}
}
