using System.Numerics;
using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static partial void Smoke()
    {
        Test("Single sphere six control intervals",()=>{foreach(var c in new[]{(new Vector3(5,0,0),2f,3f,7f),(Vector3.Zero,2f,0f,2f),(new Vector3(10,0,0),2f,8f,10f),(new Vector3(5,0,0),20f,0f,10f),(new Vector3(5,2,0),2f,5f,5f)}){var s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),c.Item1,c.Item2);Check(s.HasInterval);Eq(s.Start,c.Item3);Eq(s.End,c.Item4);}Check(!VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(20,0,0),2).HasInterval);});
        Test("Smoke strict inside and endpoint surface",()=>{var s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(2,0,0),2);Check(!s.InsideOrigin&&!s.InsideTarget);Eq(s.Start,0);Eq(s.End,4);s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(1,0,0),2);Check(s.InsideOrigin);Eq(s.End,3);});
        Test("Smoke magnitude projection not signed clipping",()=>{var s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(-2,0,0),2);Check(s.HasInterval&&!s.InsideOrigin);Eq(s.Start,0);Eq(s.End,4);Check(!VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(-3,0,0),2).HasInterval);});
        Test("Smoke zero radius tangent and unsupported zero ray",()=>{var s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(5,0,0),0);Check(s.HasInterval&&!s.InsideOrigin);Eq(s.Start,5);Eq(s.End,5);Reject(()=>VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,Vector3.Zero,Vector3.One,1));Reject(()=>VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,Vector3.One,Vector3.Zero,-1));});
        Test("Single sphere does not resolve multiple intervals",()=>{Eq(VisibilityMath.Smoke(new[]{new SmokeInterval(0,1,3),new(2,3,3)},.5f).Cost,17);Reject(()=>VisibilityMath.Smoke(new[]{new SmokeInterval(0,3,3),new(2,4,3)},.5f));});
    }
}
