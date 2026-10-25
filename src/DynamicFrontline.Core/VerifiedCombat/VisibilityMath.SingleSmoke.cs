using System.Numerics;
namespace DynamicFrontline.Core.VerifiedCombat;
public readonly record struct SingleSmokeInterval(bool HasInterval,float Start,float End,float RayLength,float OriginDistance,float TargetDistance,float PerpendicularDistance,float HalfChord,bool InsideOrigin,bool InsideTarget);
public static partial class VisibilityMath
{
    // P4-F09 #20415 A: magnitude projection, deliberately NOT standard signed segment clipping.
    public static SingleSmokeInterval GetSingleSphereSmokeInterval(Vector3 origin,Vector3 target,Vector3 center,float radius)
    {
        VectorMath.Validate(origin);VectorMath.Validate(target);VectorMath.Validate(center);FormulaDomain.Nonnegative(radius,nameof(radius));
        var d=target-origin;float squared=FormulaDomain.Result(d.LengthSquared());
        if(squared<=0)throw new FormulaStageException("Smoke.Geometry","Zero/underflowed ray is unsupported");
        float length=MathF.Sqrt(squared),a=FormulaDomain.Result((center-origin).Length()),b=FormulaDomain.Result((center-target).Length());
        float rhoSquared=FormulaDomain.Result(Vector3.Cross(center-origin,d).LengthSquared()/squared),rho=MathF.Sqrt(rhoSquared);
        bool insideO=a<radius,insideT=b<radius;
        if(rho>radius||Math.Max(a,b)>FormulaDomain.Result(length+radius))return new(false,-1,-1,length,a,b,rho,0,insideO,insideT);
        float radicand=FormulaDomain.Result(radius*radius-rhoSquared);
        if(radicand<0)throw new FormulaStageException("Smoke.HalfChord","Negative rounded radicand outside supported domain");
        float half=MathF.Sqrt(radicand);
        float MagnitudeProjection(Vector3 v)=>FormulaDomain.Result((d*(Vector3.Dot(v,d)/squared)).Length());
        float start=insideO?0:MagnitudeProjection(center-origin)-half;
        float end=insideT?length:!insideO?start+2*half:length-(MagnitudeProjection(center-target)-half);
        return new(true,FormulaDomain.Result(start),FormulaDomain.Result(end),length,a,b,rho,half,insideO,insideT);
    }
}
