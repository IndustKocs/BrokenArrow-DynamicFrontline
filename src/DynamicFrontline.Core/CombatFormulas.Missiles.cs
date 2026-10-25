namespace DynamicFrontline.Core;
public static partial class CombatFormulas
{
    // F11/P02 #23970, B. Supported finite domain does not normalize result.
    public static float Countermeasures(float resistivity,float accuracyMultiplier,int count)
    {
        Finite(resistivity,nameof(resistivity));Nonnegative(accuracyMultiplier,nameof(accuracyMultiplier));
        if(count<0||resistivity<0||resistivity>1)throw new ArgumentOutOfRangeException(nameof(count));
        return count==0?1:Result(MathF.Pow((1-resistivity)*accuracyMultiplier,count));
    }
    // F12/P02 #23969, B. Left-to-right product; deliberately no probability clamp.
    public static float MissileHitChance(float accuracy,float ecm,float countermeasures,float stress,float veterancy,float critical)
    {
        Nonnegative(accuracy,nameof(accuracy));Nonnegative(ecm,nameof(ecm));Nonnegative(countermeasures,nameof(countermeasures));
        Nonnegative(stress,nameof(stress));Nonnegative(veterancy,nameof(veterancy));Nonnegative(critical,nameof(critical));
        return Result(accuracy*ecm*countermeasures*stress*veterancy*critical);
    }
}
