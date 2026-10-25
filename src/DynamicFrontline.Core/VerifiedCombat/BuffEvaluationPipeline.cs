namespace DynamicFrontline.Core.VerifiedCombat;

// Names resolved from BuffDebuffSystem fields0x60..0x84, #24106/#24109/#24110.
public readonly record struct BuffAccumulators(float AimTime,float TimeBetweenBurstsStress,float MagazineReloadStress,float MagazineBurstReloadCritical,float Dispersion,float DamageReceived,float MovementSpeed,float RotateSpeed,float MissileAccuracy,float Optics)
{
    public static BuffAccumulators Reset=>new(1,1,1,1,1,1,1,1,1,1);
    public void Validate()
    {
        FormulaDomain.Nonnegative(AimTime,nameof(AimTime));FormulaDomain.Nonnegative(TimeBetweenBurstsStress,nameof(TimeBetweenBurstsStress));FormulaDomain.Nonnegative(MagazineReloadStress,nameof(MagazineReloadStress));
        FormulaDomain.Nonnegative(MagazineBurstReloadCritical,nameof(MagazineBurstReloadCritical));FormulaDomain.Nonnegative(Dispersion,nameof(Dispersion));FormulaDomain.Nonnegative(DamageReceived,nameof(DamageReceived));
        FormulaDomain.Nonnegative(MovementSpeed,nameof(MovementSpeed));FormulaDomain.Nonnegative(RotateSpeed,nameof(RotateSpeed));FormulaDomain.Nonnegative(MissileAccuracy,nameof(MissileAccuracy));FormulaDomain.Nonnegative(Optics,nameof(Optics));
    }
}
public interface IStressModifierProvider { BuffAccumulators Evaluate(BuffAccumulators reset); string Provenance {get;} }
// Caller-supplied post-stress values, not an invented stress formula or active config.
public sealed record SuppliedStressModifiers(BuffAccumulators Values,string Provenance):IStressModifierProvider
{public BuffAccumulators Evaluate(BuffAccumulators reset){Values.Validate();if(string.IsNullOrWhiteSpace(Provenance))throw new ArgumentException("Stress provenance required");return Values;}}
public readonly record struct BuffEvaluation(BuffAccumulators Reset,BuffAccumulators AfterStress,BuffAccumulators AfterCritical,BuffAccumulators Final,string StressProvenance,bool PotentialMovementCommandReview,bool WouldDisableBuffUpdate);
public static class BuffEvaluationPipeline
{
    public static BuffAccumulators ApplyStress(BuffAccumulators reset,IStressModifierProvider? provider)
    {reset.Validate();var result=provider?.Evaluate(reset)??reset;result.Validate();return result;}
    public static BuffAccumulators ApplyCritical(BuffAccumulators input,CriticalLevels levels,CriticalTable table)
    {
        input.Validate();var r=CriticalMath.Apply(levels,table,new(input.Optics,input.AimTime,input.MovementSpeed,input.RotateSpeed,input.Dispersion,input.MagazineBurstReloadCritical));
        return input with{Optics=r.Optics,AimTime=r.AimTime,MovementSpeed=r.MovementSpeed,RotateSpeed=r.RotateSpeed,Dispersion=r.Dispersion,MagazineBurstReloadCritical=r.MagazineBurstReload};
    }
    public static BuffEvaluation Evaluate(IStressModifierProvider? stress,CriticalLevels? critical,CriticalTable? table)
    {
        var reset=BuffAccumulators.Reset;var s=ApplyStress(reset,stress);
        if(critical.HasValue&&table==null)throw new ArgumentException("Critical table required");
        var c=critical.HasValue?ApplyCritical(s,critical.Value,table!):s;
        // ApplyBuffModifiers publishes these accumulators; no ECS, movement or queue writes here.
        return new(reset,s,c,c,stress?.Provenance??"No StressComponent supplied",c.MovementSpeed<=float.Epsilon,true);
    }
}
