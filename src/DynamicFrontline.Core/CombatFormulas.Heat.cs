namespace DynamicFrontline.Core;

public readonly record struct HeatCalculation(float PenetrationPower,float ArmorPower,float Numerator,float Denominator,float Damage);
public static partial class CombatFormulas
{
    // F10/P02 #24006, B. Finite positive bases; native special-value equivalence is unknown.
    public static HeatCalculation HeatDetails(float damage,float penetration,float armor,float effectiveness,float curve)
    {
        Nonnegative(damage,nameof(damage));Positive(penetration,nameof(penetration));Positive(armor,nameof(armor));
        Positive(effectiveness,nameof(effectiveness));Positive(curve,nameof(curve));
        return HeatExpression(damage,penetration,armor,effectiveness,curve);
    }
    private static HeatCalculation HeatExpression(float d,float p,float a,float e,float k)
    {
        float pk=Result(MathF.Pow(p,k)),ak=Result(MathF.Pow(a,k));
        float numerator=Result(d*pk),denominator=Result(pk+e*ak);
        if(denominator==0)throw new ArithmeticException("HEAT denominator underflow");
        return new(pk,ak,numerator,denominator,Result(numerator/denominator));
    }
    public static float HeatDamage(float damage,float penetration,float armor,float effectiveness,float curve)=>HeatDetails(damage,penetration,armor,effectiveness,curve).Damage;
}
