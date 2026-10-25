using System.Numerics;
namespace DynamicFrontline.Core.VerifiedCombat;

public readonly record struct ExplosionResult(float SurfaceDistance,float HealthAOE,float StressAOE,float HealthBeforeOtherModifiers,float DirectStressBeforeOtherModifiers);
public static class ExplosionMath
{
    // CF03 #24202, A. Target center (including any vertical offset) supplied by caller.
    public static float SurfaceDistance(Vector3 center,Vector3 hit,float roughRadius)
    {
        VectorMath.Validate(center);VectorMath.Validate(hit);FormulaDomain.Nonnegative(roughRadius,nameof(roughRadius));
        return Math.Max(0,FormulaDomain.Result(Vector3.Distance(center,hit))-roughRadius);
    }
    public static float Falloff(float distance,float radius,bool noFalloff=false)
    {
        FormulaDomain.Nonnegative(distance,nameof(distance));FormulaDomain.Nonnegative(radius,nameof(radius));
        return distance>=radius?0:noFalloff?1:Math.Clamp(1-distance/radius,0,1);
    }
    public static ExplosionResult Evaluate(Vector3 center,Vector3 hit,float roughRadius,float damage,float stress,float healthRadius,float stressRadius,bool noFalloff=false)
    {
        FormulaDomain.Nonnegative(damage,nameof(damage));FormulaDomain.Nonnegative(stress,nameof(stress));
        float d=SurfaceDistance(center,hit,roughRadius),h=Falloff(d,healthRadius,noFalloff),s=Falloff(d,stressRadius,noFalloff);
        return new(d,h,s,FormulaDomain.Result(damage*h),FormulaDomain.Result(stress*s));
    }
}
public static class VectorMath
{
    public static void Validate(Vector3 v){FormulaDomain.Finite(v.X,nameof(v));FormulaDomain.Finite(v.Y,nameof(v));FormulaDomain.Finite(v.Z,nameof(v));}
}
