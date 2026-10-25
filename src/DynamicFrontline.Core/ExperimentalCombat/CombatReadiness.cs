using DynamicFrontline.Core.VerifiedCombat;
namespace DynamicFrontline.Core.ExperimentalCombat;

public readonly record struct ReadinessDiagnostic(int Alive,int Initial,float CasualtyFraction,float Stress,float MaxStress,StressLevel ObservedState,float PotentialStress,float? RecoveryTime,bool RecoveryHorizonExceeded);
public static class CombatReadiness
{
    // Experimental diagnostic: assumes no new damage and supplied recovery period.
    public static ReadinessDiagnostic Evaluate(int alive,int initial,float stress,float maximum,StressLevel observed,float hpDamage,float maxHp,float directStress,float timeAfterDamage,float recoveryMultiplier,float recoveryPeriod,int maximumTicks=100000)
    {
        FormulaDomain.Count(alive,nameof(alive));if(initial<=0||alive>initial)throw new ArgumentOutOfRangeException(nameof(initial));
        FormulaDomain.Nonnegative(stress,nameof(stress));FormulaDomain.Positive(maximum,nameof(maximum));
        FormulaDomain.Nonnegative(timeAfterDamage,nameof(timeAfterDamage));FormulaDomain.Nonnegative(recoveryMultiplier,nameof(recoveryMultiplier));
        FormulaDomain.Positive(recoveryPeriod,nameof(recoveryPeriod));if(maximumTicks<0||!Enum.IsDefined(typeof(StressLevel),observed))throw new ArgumentOutOfRangeException(nameof(maximumTicks));
        float potential=CombatFormulas.StressDamage(hpDamage,maxHp,maximum,directStress);
        var step=new StressRecoveryStep(stress,timeAfterDamage);int ticks=0;
        while(step.Stress>0&&ticks<maximumTicks){step=CombatFormulas.RecoverStressTick(step.Stress,step.TimeAfterLastDamage,recoveryMultiplier);ticks++;}
        return new(alive,initial,1-(float)alive/initial,stress,maximum,observed,potential,step.Stress==0?FormulaDomain.Result(ticks*recoveryPeriod):null,step.Stress>0);
    }
}
