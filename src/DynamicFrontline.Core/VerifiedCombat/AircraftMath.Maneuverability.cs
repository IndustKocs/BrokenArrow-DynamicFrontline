namespace DynamicFrontline.Core.VerifiedCombat;
public readonly record struct AircraftSpeedPreset(float MinSpeed,float MaxSpeed,float AfterburnSpeed);
public readonly record struct AircraftRatePreset(float MinRate,float MaxRate,float AfterburnRate);
public readonly record struct AircraftRateResult(float Rate,float Interpolation,float Denominator,bool AfterburnBranch);
public static partial class AircraftMath
{
    // P4-F07 #11974 B. Ordered finite presets are library domain policy, not native validation.
    public static AircraftRateResult CalculateRate(float speed,AircraftSpeedPreset speeds,AircraftRatePreset rates)
    {
        FormulaDomain.Finite(speed,nameof(speed));FormulaDomain.Nonnegative(speeds.MinSpeed,nameof(speeds));FormulaDomain.Nonnegative(speeds.MaxSpeed,nameof(speeds));FormulaDomain.Nonnegative(speeds.AfterburnSpeed,nameof(speeds));
        if(speeds.MinSpeed>speeds.MaxSpeed||speeds.MaxSpeed>speeds.AfterburnSpeed)throw new FormulaStageException("Aircraft.Preset","Unordered speeds unsupported");
        FormulaDomain.Nonnegative(rates.MinRate,nameof(rates));FormulaDomain.Nonnegative(rates.MaxRate,nameof(rates));FormulaDomain.Nonnegative(rates.AfterburnRate,nameof(rates));
        bool after=speed>speeds.MaxSpeed;float denominator=after?speeds.AfterburnSpeed-speeds.MaxSpeed:speeds.MaxSpeed-speeds.MinSpeed;
        float t=denominator<=.001f?0:Math.Clamp((speed-(after?speeds.MaxSpeed:speeds.MinSpeed))/denominator,0,1);
        float start=after?rates.MaxRate:rates.MinRate,end=after?rates.AfterburnRate:rates.MaxRate;
        return new(FormulaDomain.Result(start+(end-start)*t),t,denominator,after);
    }
    public static AircraftRateResult CalculateYawRate(float speed,AircraftSpeedPreset speeds,AircraftRatePreset yaw)=>CalculateRate(speed,speeds,yaw);
    public static AircraftRateResult CalculatePitchRate(float speed,AircraftSpeedPreset speeds,AircraftRatePreset pitch)=>CalculateRate(speed,speeds,pitch);
}
