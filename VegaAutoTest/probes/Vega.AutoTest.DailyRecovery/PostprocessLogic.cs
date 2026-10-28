using System.Reflection;
using System.Runtime.CompilerServices;
using Vega.BL.RecipeProcessing;
using Vega.Data.Defects;
using Vega.Data.Commons;

namespace Vega.AutoTest.DailyRecovery;
public class WorkItem3693Logic
{
 private static PostProcessingHelper Helper() {
  Evidence.Binary("Vega.BL.RecipeProcessing.dll");
  var h=(PostProcessingHelper)RuntimeHelpers.GetUninitializedObject(typeof(PostProcessingHelper));
  typeof(PostProcessingHelper).GetField("_logger",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(h,new Vega.AutoTest.Spike.StubLogService());
  return h;
 }
 private static DefectInfo Point(double x,double y,double size=5,SizeType type=SizeType.LPD_SIZED) => new() {
  ID=(int)(100+x*10+y),DefectName="LPD",ChannelMode=ChannelMode.DN_Normal,SizeType=type,
  Attributes=new() {["X"]=x,["Y"]=y,["Size"]=size}
 };
 private static List<DefectInfo> Merge(DefectInfo a,params DefectInfo[] b) =>
  (List<DefectInfo>)typeof(PostProcessingHelper).GetMethod("MergeDefects",BindingFlags.Instance|BindingFlags.NonPublic)!
   .Invoke(Helper(),[new List<DefectInfo>{a},b.ToList(),3f,MergeType.DC_SAME_INCIDENCE])!;
 [Theory]
 [InlineData(-1,0)][InlineData(1,0)][InlineData(0,-1)][InlineData(0,1)]
 public void CenteredSearchFindsAllDirections(double x,double y) => Evidence.Check("3693.centered."+x+"."+y,()=>{
  var a=Point(0,0);var b=Point(x,y,2); var output=Merge(a,b);
  Assert.True(b.Is_Excluded);Assert.Single(output);
 });
 [Fact] public void OnlyNearestCandidateIsMerged() => Evidence.Check("3693.nearest-only",()=>{
  var a=Point(0,0);var farther=Point(2,0,1);var near=Point(1,0,2);
  var output=Merge(a,farther,near);Assert.True(near.Is_Excluded);Assert.False(farther.Is_Excluded);Assert.Equal(2,output.Count);
 });
 [Fact] public void IneligibleNearestDoesNotFallThrough() => Evidence.Check("3693.nearest-ineligible",()=>{
  var a=Point(0,0);var nearest=Point(1,0,1,(SizeType)999);var farther=Point(2,0,2);
  var output=Merge(a,nearest,farther);Assert.False(nearest.Is_Excluded);Assert.False(farther.Is_Excluded);Assert.Equal(3,output.Count);
 });
}
