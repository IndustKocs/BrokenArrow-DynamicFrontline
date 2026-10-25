namespace DynamicFrontline.Core;

public enum StressLevel { Calm, Shocked, Panicked }
public readonly record struct StressThresholds(float Shock, float Panic);
public readonly record struct StressRecoveryStep(float Stress, float TimeAfterLastDamage);

// Pure reference calculations from FORMULAS.md, not a replacement for the game's combat pipeline.
// Guards define the prototype's supported domain; they are not claimed to exist in native code.
public static class CombatFormulas
{
    private static void Finite(float value, string name)
    { if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(name); }
    private static void Nonnegative(float value, string name)
    { Finite(value, name); if (value < 0) throw new ArgumentOutOfRangeException(name); }
    private static void Positive(float value, string name)
    { Finite(value, name); if (value <= 0) throw new ArgumentOutOfRangeException(name); }
    private static float Result(float value)
    { if (!float.IsFinite(value)) throw new ArithmeticException("Formula overflowed the finite domain."); return value; }

    // F01. Caller must establish UnitType=2 and absence of LoadedComponent first.
    // No implicit profile selection, and this is received damage, not damage dealt.
    public static float InfantryReceivedDamageMultiplier(float incoming, uint aliveSoldiers, float floor, float perSoldier)
    {
        Nonnegative(incoming, nameof(incoming)); Finite(floor, nameof(floor)); Finite(perSoldier, nameof(perSoldier));
        return Result(incoming * Math.Clamp(Result(floor + aliveSoldiers * perSoldier), 0, 1));
    }

    // F02. Ratios are explicit configuration, not universal constants.
    public static StressThresholds Thresholds(float maximum, float shockRatio, float panicRatio)
    {
        Positive(maximum, nameof(maximum));
        Nonnegative(shockRatio, nameof(shockRatio)); Nonnegative(panicRatio, nameof(panicRatio));
        if (shockRatio > panicRatio || panicRatio > 1) throw new ArgumentOutOfRangeException(nameof(panicRatio));
        return new(Result(maximum * shockRatio), Result(maximum * panicRatio));
    }

    // F03. Intentionally no saturation to maximum stress.
    public static float StressDamage(float healthDamage, float maximumHealth, float maximumStress, float directStressDamage)
    {
        Nonnegative(healthDamage, nameof(healthDamage)); Positive(maximumHealth, nameof(maximumHealth));
        Positive(maximumStress, nameof(maximumStress)); Nonnegative(directStressDamage, nameof(directStressDamage));
        return Result(healthDamage / maximumHealth * maximumStress + directStressDamage);
    }

    // F04.
    public static float StressToTarget(float current, float maximum, float target)
    {
        Nonnegative(current, nameof(current)); Positive(maximum, nameof(maximum)); Finite(target, nameof(target));
        if (current > maximum) throw new ArgumentOutOfRangeException(nameof(current));
        return Math.Clamp(Result(target - current), 0, maximum - current);
    }

    // F05. Exactly one recovery call, never one render frame. Zero stress skips the branch.
    public static StressRecoveryStep RecoverStressTick(float current, float timeAfterLastDamage, float multiplier)
    {
        Nonnegative(current, nameof(current)); Nonnegative(timeAfterLastDamage, nameof(timeAfterLastDamage));
        Nonnegative(multiplier, nameof(multiplier));
        if (current == 0) return new(current, timeAfterLastDamage);
        float nextTime = Result(timeAfterLastDamage + 1);
        float recovery = Result(1 + MathF.Truncate(nextTime) * multiplier);
        return new(Math.Max(0, current - recovery), nextTime);
    }

    // F06. Panic has precedence; equality belongs to the higher level.
    public static StressLevel Level(float current, StressThresholds thresholds)
    {
        Nonnegative(current, nameof(current)); Nonnegative(thresholds.Shock, nameof(thresholds));
        Nonnegative(thresholds.Panic, nameof(thresholds));
        if (thresholds.Shock > thresholds.Panic) throw new ArgumentOutOfRangeException(nameof(thresholds));
        return current >= thresholds.Panic ? StressLevel.Panicked : current >= thresholds.Shock ? StressLevel.Shocked : StressLevel.Calm;
    }

    // F07. The minimum applies only when the intermediate damage is strictly positive.
    public static float KineticDamage(float damage, float penetration, float armor, float effectiveness, float minimumCoefficient)
    {
        Nonnegative(damage, nameof(damage)); Positive(penetration, nameof(penetration)); Nonnegative(armor, nameof(armor));
        Positive(effectiveness, nameof(effectiveness)); Nonnegative(minimumCoefficient, nameof(minimumCoefficient));
        if (penetration >= armor) return damage;
        float denominator = Result(penetration * effectiveness);
        if (denominator == 0) throw new ArithmeticException("Formula denominator underflowed.");
        float x = Result(damage + damage / denominator * (penetration - armor));
        return x > 0 ? Math.Max(x, Result(damage * minimumCoefficient)) : 0;
    }

    // F08.
    public static float DamageWithThreshold(float damage, float threshold, bool isHeat)
    {
        Nonnegative(damage, nameof(damage)); Nonnegative(threshold, nameof(threshold));
        return isHeat || damage > threshold ? damage : 0;
    }

    // F09. Raw ArmorType values from the evidence, without guessed enum names.
    public static float Penetration(int armorType, float minimumPenetration, float effectivePenetration,
        float minimumRange, float effectiveRange, float actualRange)
    {
        if (armorType == 0) return float.MaxValue;
        Finite(effectivePenetration, nameof(effectivePenetration));
        if (armorType != 1) return Math.Clamp(effectivePenetration, 0, float.MaxValue);
        Finite(minimumPenetration, nameof(minimumPenetration)); Nonnegative(minimumRange, nameof(minimumRange));
        Nonnegative(effectiveRange, nameof(effectiveRange)); Nonnegative(actualRange, nameof(actualRange));
        float range = Result(effectiveRange - minimumRange);
        if (range == 0) throw new ArgumentOutOfRangeException(nameof(effectiveRange));
        float value = minimumPenetration - (minimumPenetration - effectivePenetration) / range * actualRange;
        // Preserve the native saturation, including +/- infinity from finite inputs.
        if (float.IsNaN(value)) throw new ArithmeticException("Indeterminate penetration.");
        return Math.Clamp(value, 0, float.MaxValue);
    }

    // F13. Each override is independent; zero means fallback, not a zero aim time.
    public static (float Minimum, float Maximum) AimTimes(float weaponMinimum, float weaponMaximum,
        (float Minimum, float Maximum)? ammunitionOverrides)
    {
        Nonnegative(weaponMinimum, nameof(weaponMinimum)); Nonnegative(weaponMaximum, nameof(weaponMaximum));
        if (ammunitionOverrides is not { } ammo) return (weaponMinimum, weaponMaximum);
        Nonnegative(ammo.Minimum, nameof(ammunitionOverrides)); Nonnegative(ammo.Maximum, nameof(ammunitionOverrides));
        return (ammo.Minimum != 0 ? ammo.Minimum : weaponMinimum, ammo.Maximum != 0 ? ammo.Maximum : weaponMaximum);
    }
}

// Optional mod rule, not a recovered vanilla desertion mechanic.
// Evaluates observed stress only; never ticks or writes the game's stress a second time.
public sealed class CalmStressRecoveryCondition
{
    public float ShockRatio { get; }
    public float PanicRatio { get; }
    public CalmStressRecoveryCondition(float shockRatio, float panicRatio)
    {
        CombatFormulas.Thresholds(1, shockRatio, panicRatio);
        if (shockRatio == 0) throw new ArgumentOutOfRangeException(nameof(shockRatio));
        ShockRatio = shockRatio; PanicRatio = panicRatio;
    }
    public bool IsSatisfied(UnitSnapshot unit)
    {
        if (!float.IsFinite(unit.MaxStress) || unit.MaxStress <= 0 ||
            !float.IsFinite(unit.CurrentStressValue) || unit.CurrentStressValue < 0) return false;
        return CombatFormulas.Level(unit.CurrentStressValue,
            CombatFormulas.Thresholds(unit.MaxStress, ShockRatio, PanicRatio)) == StressLevel.Calm;
    }
}
