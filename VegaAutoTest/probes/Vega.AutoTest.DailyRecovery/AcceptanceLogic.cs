using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ComponentConfigEditor.Models;
using ComponentConfigEditor.Services;
using Vega.AutoTest.Harness;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace Vega.AutoTest.DailyRecovery;

public static class Evidence
{
 public static string Repo => Environment.GetEnvironmentVariable("VEGA_SW_ROOT") ?? throw new InvalidOperationException("VEGA_SW_ROOT required");
 public static void Check(string id, Action body) {
  var r = new AssertionRecorder();
  try { body(); r.Pass(id,"logic",id,"requirement holds","matched"); }
  catch(Exception ex) { r.Fail(id,"logic",id,"requirement holds",ex.ToString()); throw; }
  finally { r.Flush(); }
 }
 public static void Binary(string name) => Assert.Equal(
  SHA256.HashData(File.ReadAllBytes(Path.Combine(Repo,"Vega/bin/Debug/net8.0-windows10.0.19041.0/win-x64",name))),
  SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,name))));
}

public class WorkItem3690Logic
{
 private static readonly HardwareProfilesRoot Profiles = new() { Profiles = new() {
  ["Vega100-PIC"] = new() {Grants=["HasLaserPIC"]}, ["Vega100-Coherent"] = new() {Grants=["HasLaserCoherent"]},
  ["Vega300-HWv1"] = new() {Grants=["HasF2Thorlabs"]}, ["Vega300-HWv2"] = new() {Grants=["HasF2Actus"]} } };
 private static readonly RootObject Root = new() {ToolConfigCollection = new() { ToolConfigs = [
  new() {ToolType="Vega100",RootCondition="Vega100,Simulator,GemSim,Gen2",Features="OuterToInner,DIC"},
  new() {ToolType="Vega300",RootCondition="Vega300,Simulator,GemSim,Gen2",Features="EFEM"}] } };
 private static HardwareProfileResolver Resolver => new(Profiles,Root,["Gen1","Gen2","Gen3","Tool","Simulator"]);
 [Theory]
 [InlineData("Vega100-PIC", "Vega100", "HasLaserPIC", ProfileState.Granted)]
 [InlineData("Vega100-Coherent", "Vega100", "HasLaserPIC", ProfileState.Excluded)]
 [InlineData("Vega300-HWv1", "Vega300", "HasF2Thorlabs", ProfileState.Granted)]
 [InlineData("Vega300-HWv1", "Vega300", "HasF2Actus", ProfileState.Excluded)]
 [InlineData("Vega300-HWv2", "Vega300", "HasF2Actus", ProfileState.Granted)]
 public void ActualResolverMatchesSelectedProfile(string profile,string tool,string token,ProfileState state) => Evidence.Check("3690.profile."+profile+token,()=>{
  var match=Resolver.Resolve(Profiles.Profiles.Keys.Where(p=>p.StartsWith(tool+"-")).ToArray(),profile,tool,token);
  Assert.Equal(state,match.State); Assert.Contains(token,match.Display);
 });
 [Theory]
 [InlineData("Gen12")][InlineData("Vega3000")][InlineData("gen2")][InlineData("HasBogusToken")]
 public void DeadTokensAreRejectedWithoutPrefixGuess(string token) => Evidence.Check("3690.dead."+token,()=>
  Assert.Equal(ProfileState.Ungranted,Resolver.Resolve(["Vega100-PIC"],"Vega100-PIC","Vega100",token).State));
 [Fact] public void OrAlternativeAndCrossModelFeature() => Evidence.Check("3690.or-feature",()=>{
  Assert.Equal(ProfileState.Granted,Resolver.Resolve(["Vega100-PIC"],"Vega100-PIC","Vega100","HasLaserPIC|HasBogus").State);
  var feature=Resolver.Resolve(["Vega300-HWv1"],"Vega300-HWv1","Vega300","DIC");
  Assert.Equal(ProfileState.NotGated,feature.State);Assert.Contains("未启用",feature.Detail);
 });
 [Fact] public void MissingProfilesDoNotFalselyMarkDead() => Evidence.Check("3690.empty",()=>{
  var match=new HardwareProfileResolver(null,Root,[]).Resolve([],null,"Vega100","HasLaserPIC");
  Assert.Equal(ProfileState.NotGated,match.State);Assert.Contains("缺失或为空",match.Detail);
 });
 [Fact] public void DisplayPropertiesDoNotSerialize() => Evidence.Check("3690.serialize",()=>{
  var c=new Component {Name="Fixture",Condition="HasLaserPIC"};var before=JsonSerializer.Serialize(c);
  c.ProfileDisplay="display";c.ProfileDetail="tooltip";c.ProfileState=ProfileState.Granted;
  Assert.Equal(before,JsonSerializer.Serialize(c));
 });
}

public class DependencyLockLogic
{
 [Fact] public void CommittedLockHasResolvableDependencyGraph() => Evidence.Check("dependencies.lock.graph",()=>{
  using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(Evidence.Repo,"packages.lock.json")));
  Assert.Equal(2,doc.RootElement.GetProperty("version").GetInt32());
  var frameworks=doc.RootElement.GetProperty("dependencies").EnumerateObject().ToArray();Assert.NotEmpty(frameworks);
  foreach(var tfm in frameworks) {
   Assert.StartsWith("net8.0-windows",tfm.Name); var packages=tfm.Value.EnumerateObject().ToDictionary(p=>p.Name,p=>p.Value,StringComparer.OrdinalIgnoreCase);
   Assert.NotEmpty(packages);
   foreach(var p in packages.Values) {
    Assert.False(string.IsNullOrWhiteSpace(p.GetProperty("resolved").GetString()));
    if(p.TryGetProperty("dependencies",out var deps))foreach(var dep in deps.EnumerateObject())Assert.True(packages.ContainsKey(dep.Name),"Missing package node: "+dep.Name);
   }
  }
 });
}

public class WorkItem3652Logic
{
 [Fact] public void OriginalDialogCompletionMatrix() => Evidence.Check("3652.original-dialog-matrix",()=>{
#if HAS_DIALOG_TESTS
  Evidence.Binary("Vega.UI.Inspection.dll");
  using var dispatcher=new Vega.UI.Inspection.Tests.SummaryBinningDispatcher();
  var instance=new Vega.UI.Inspection.Tests.RecipeElementDialogCompletionTests(dispatcher);
  int count=0;
  foreach(var method in instance.GetType().GetMethods().Where(m=>m.IsDefined(typeof(FactAttribute)))) {
   var members=method.GetCustomAttributes<MemberDataAttribute>().ToArray();
   IEnumerable<object[]> rows=members.Length==0 ? [Array.Empty<object>()] : members.SelectMany(a=>a.GetData(method));
   foreach(var row in rows) {
    var index=count++;
    Evidence.Check("3652.dialog."+method.Name+"."+index,()=>{
     try {method.Invoke(instance,row);} catch(TargetInvocationException ex){throw ex.InnerException!;}
    });
   }
  }
  Assert.True(count>=40,"Original confirmation/cancellation/mouseleave scenarios must execute");
#else
  throw new InvalidOperationException("This SHA predates RecipeElementDialogCompletionTests; wrong case/commit mapping");
#endif
 });
}
