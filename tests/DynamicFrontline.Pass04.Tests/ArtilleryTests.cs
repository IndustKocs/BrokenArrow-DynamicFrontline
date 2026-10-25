using System.Numerics;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
static partial class Tests
{
    static readonly CurveSource Source=new("synthetic-identity","1",new string('a',64));
    static CorrectionTable Table()=>ArtilleryDispersionMath.Build(Source,new LinearTestCurve(new[]{new CurveKey(0,0),new(1,1)}),new LinearInverseFactory());
    static partial void Artillery()
    {
        Test("CDF identity length keys endpoints",()=>{var t=Table();Check(t.Count==1000&&t.InverseKeys.Count==10001);foreach(int i in new[]{0,1,250,500,999})Eq(t[i],Math.Sqrt(i/1000d));Eq(t[250],.5);Reject(()=>{_=t[1000];});});
        Test("CDF nonlinear monotone synthetic and repeatability",()=>{var curve=new LinearTestCurve(new[]{new CurveKey(0,0),new(.5f,.25f),new(1,1)});var a=ArtilleryDispersionMath.Build(Source,curve,new LinearInverseFactory());var b=ArtilleryDispersionMath.Build(Source,curve,new LinearInverseFactory());Check(a.CopyValues().SequenceEqual(b.CopyValues()));for(int i=1;i<1000;i++)Check(a[i]>=a[i-1]);Eq(a[250],2d/3);});
        Test("CDF invalid strict monotonic rejects",()=>{Reject(()=>ArtilleryDispersionMath.Build(Source,new LinearTestCurve(new[]{new CurveKey(0,0),new(.5f,0),new(1,1)}),new LinearInverseFactory()));Reject(()=>new LinearTestCurve(new[]{new CurveKey(0,0),new(0,1)}));});
        Test("CDF explicit version cache and immutable table",()=>{var c=new CorrectionTableCache();var curve=new LinearTestCurve(new[]{new CurveKey(0,0),new(1,1)});var factory=new LinearInverseFactory();var a=c.Get(Source,curve,factory);Check(ReferenceEquals(a,c.Get(Source,curve,factory)));Check(!ReferenceEquals(a,c.Get(Source with{Version="2"},curve,factory)));var copy=a.CopyValues();copy[250]=99;Eq(a[250],.5);});
        Test("Polar ordered draws and truncation",()=>{var r=new ScriptFloat(90,250.9f);var p=ArtilleryDispersionMath.Polar(Table(),r);Check(p.Index==250&&p.ValuesConsumed==2);Eq(p.Radius,.5);Check(r.Bounds.SequenceEqual(new[]{(0f,360f),(0f,1000f)}));});
        Test("Polar endpoint1000 explicit failure consumes both draws",()=>{var r=new ScriptFloat(360,1000);Reject(()=>ArtilleryDispersionMath.Polar(Table(),r));Check(r.ValuesConsumed==2);});
        Test("FastDimond exact edge samples",()=>{foreach(float x in new[]{0,500,-500,1000,-1000}){var r=new ScriptFloat(x,0);var d=ArtilleryDispersionMath.FastDimond(r);Eq(d.Result.X,x/1000);Eq(d.YMaximum,1000-Math.Abs(x));Check(d.ValuesConsumed==(Math.Abs(x)==1000?1:2));}});
        Test("FastDimond fractional band not ideal diamond",()=>{var d=ArtilleryDispersionMath.FastDimond(new ScriptFloat(500.9f,500));Check(d.QuantizedAbsoluteX==500);Check(d.Result.X+d.Result.Y>1);});
        Test("FastDimond CyclicRandom endpoint does not consume second",()=>{var q=new float[100];q[1]=1;var r=new CyclicRandom(q);var d=ArtilleryDispersionMath.FastDimond(r);Eq(d.Result.X,1);Eq(d.Result.Y,0);Check(r.Index==1&&r.ValuesConsumed==1);});
        Test("Trajectory scale direction and native lower bound",()=>{var d=ArtilleryDispersionMath.Apply(Vector3.UnitZ,10,1000,20,40,.5f,-.5f);Eq(d.Direction.X,-.01);Eq(d.Direction.Y,0);Eq(d.DistanceDelta,9.8);Eq(d.HorizontalScale,.01);Check(BitConverter.SingleToInt32Bits(ArtilleryDispersionMath.MinimumDistanceDelta)==0x3727C5AC);Eq(ArtilleryDispersionMath.Apply(Vector3.UnitZ,10,1,1,2,0,-1).DistanceDelta,1e-5);});
        Test("Zero effective range no draw and degenerate reject",()=>{int draws=0;foreach(float l in new[]{0,-1})Check(!ArtilleryDispersionMath.GenerateAndApply(Vector3.UnitZ,10,l,20,40,()=>{draws++;return default;}).Applied);Check(draws==0);Reject(()=>ArtilleryDispersionMath.Apply(Vector3.UnitY,10,1,1,1,0,0));});
    }
}
sealed class ScriptFloat(params float[] values):IFloatRandom
{
    public long ValuesConsumed{get;private set;}public readonly List<(float,float)> Bounds=new();
    public float NextFloat(float min,float max){Bounds.Add((min,max));if(min==max)return min;return values[(int)ValuesConsumed++];}
}
