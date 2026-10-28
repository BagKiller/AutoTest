using System.Reflection;
using Vega.AutoTest.Harness;
using Xunit;

namespace Vega.AutoTest.Generated;
public class DuvNotchTests {
 private static void Check(string id,Action body){var r=new AssertionRecorder();try{body();r.Pass(id,"logic",id,"Known synthetic geometry contract","Matched");}catch(Exception e){r.Fail(id,"logic",id,"Known synthetic geometry contract",e.ToString());throw;}finally{r.Flush();}}
 private static double Outer(ushort[,] data)=> (double)Assembly.Load("Vega.BL.WaferAlignment").GetType("Vega.BL.WaferAlignment.WaferAlignmentService",true)!.GetMethod("FindDuvNotchOuterEdgeTrack",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{data,0,data.GetLength(0)})!;
 [Fact] public void KnownBrightBandUsesOuterThresholdCrossing()=>Check("duv.outer-band",()=>{
  var data=new ushort[40,1];for(int i=12;i<=24;i++)data[i,0]=100;
  Assert.Equal(24.5,Outer(data),8);
 });
 [Fact] public void StrongerBandWinsOverSmallInnerSignal()=>Check("duv.stronger-band",()=>{
  var data=new ushort[50,1];for(int i=7;i<=12;i++)data[i,0]=30;for(int i=20;i<=33;i++)data[i,0]=100;
  Assert.Equal(33.5,Outer(data),8);
 });
 [Fact] public void MissingSignalAndTooShortProfilesStayUnknown()=>Check("duv.unknown",()=>{
  Assert.True(double.IsNaN(Outer(new ushort[40,1])));Assert.True(double.IsNaN(Outer(new ushort[7,1])));
 });
 [Fact] public void SmoothingNeverCrossesUnknownGap()=>Check("duv.invalid-gap",()=>{
  var method=Assembly.Load("Vega.Algo.Imaging").GetType("Vega.Algo.Imaging.WECDHelper",true)!.GetMethod("SmoothNotchRadius")!;
  var result=(double[])method.Invoke(null,new object[]{new[]{5.0,double.NaN,10.0},new[]{true,false,true},false})!;
  Assert.Equal(5.0,result[0]);Assert.True(double.IsNaN(result[1]));Assert.Equal(10.0,result[2]);
 });
}
