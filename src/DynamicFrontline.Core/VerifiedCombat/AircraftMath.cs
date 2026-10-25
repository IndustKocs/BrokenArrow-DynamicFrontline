using System.Numerics;
namespace DynamicFrontline.Core.VerifiedCombat;
public readonly record struct HelicopterShift(Vector3 Result,float DistanceSquared,float Step,bool ReachedTarget);
public readonly record struct RequestedFuel(float Old,float Requested,bool Applied=false);
public static class AircraftMath
{
    // P3-F02 #12239 A. Negative finite inputs intentionally preserve the recovered direction.
    public static HelicopterShift ShiftFinite(Vector3 current,Vector3 target,float agility,float deltaTime)
    {
        VectorMath.Validate(current);VectorMath.Validate(target);FormulaDomain.Finite(agility,nameof(agility));FormulaDomain.Finite(deltaTime,nameof(deltaTime));
        Vector3 d=target-current;float squared=FormulaDomain.Result(d.LengthSquared()),s=FormulaDomain.Result(.25f*agility*deltaTime);
        bool reached=squared==0||(s>=0&&FormulaDomain.Result(s*s)>=squared);
        Vector3 result=reached?target:current+d/MathF.Sqrt(squared)*s;VectorMath.Validate(result);
        return new(result,squared,s,reached);
    }
    public static void ValidateNormalUsage(float agility,float deltaTime){FormulaDomain.Nonnegative(agility,nameof(agility));FormulaDomain.Nonnegative(deltaTime,nameof(deltaTime));}
    // P3-F03 #12238 B. Zero agility explicitly outside finite time domain.
    public static float ShiftTime(Vector3 current,Vector3 target,float agility)
    {VectorMath.Validate(current);VectorMath.Validate(target);FormulaDomain.Positive(agility,nameof(agility));return FormulaDomain.Result(Vector3.Distance(current,target)/(.25f*agility));}
    // P3-F04 #10761/#9147 B. Does NOT reproduce or bypass the protected setter.
    public static RequestedFuel Fuel(float old,float deltaTime,float afterburnerMultiplier)
    {FormulaDomain.Nonnegative(old,nameof(old));FormulaDomain.Nonnegative(deltaTime,nameof(deltaTime));FormulaDomain.Nonnegative(afterburnerMultiplier,nameof(afterburnerMultiplier));return new(old,FormulaDomain.Result(old-deltaTime*afterburnerMultiplier));}
}
