using System.Numerics;
namespace DynamicFrontline.Core.VerifiedCombat;
public readonly record struct GroundVisionInput(float DistanceGround,float WeaponFlash,float SpottedPenalty,float AntiVisible,float TerrainCover,bool InsideSmoke,float SmokeCover,bool SneakEnabled,bool Airdrop,float CoverBonus,float SneakMinAntiVisible,float MaxAntiVisible,float? OpticsMultiplier);
public readonly record struct VisionRange(float Flash,float Cover,float AntiVisibleFactor,float BeforeOptics,float Range);
public readonly record struct TerrainSample(int Type,int RawFlags,float HeightAboveGround,float Height,float Opacity,float Multiplier);
public readonly record struct TerrainCost(float Cost,int Samples,int FirstBudgetExceededSample);
public readonly record struct SmokeInterval(float Start,float End,float Opacity);
public readonly record struct SmokeCost(float Cost,float RetainedLength,long Samples);
public readonly record struct SmokeSphere(float PerpendicularDistance,bool IntersectsInfiniteLine,float HalfChord);
public static partial class VisibilityMath
{
    // CF14 #20385 B. Inputs already resolved from both components. Not a contact decision.
    public static VisionRange Ground(GroundVisionInput v)
    {
        FormulaDomain.Nonnegative(v.DistanceGround,nameof(v));FormulaDomain.Finite(v.WeaponFlash,nameof(v));FormulaDomain.Finite(v.SpottedPenalty,nameof(v));
        FormulaDomain.Nonnegative(v.AntiVisible,nameof(v));FormulaDomain.Nonnegative(v.TerrainCover,nameof(v));FormulaDomain.Nonnegative(v.SmokeCover,nameof(v));
        FormulaDomain.Nonnegative(v.CoverBonus,nameof(v));FormulaDomain.Nonnegative(v.SneakMinAntiVisible,nameof(v));FormulaDomain.Positive(v.MaxAntiVisible,nameof(v));
        if(v.MaxAntiVisible<float.Epsilon||v.SneakMinAntiVisible>v.MaxAntiVisible)throw new ArgumentOutOfRangeException(nameof(v));
        if(v.OpticsMultiplier is float optics)FormulaDomain.Nonnegative(optics,nameof(v));
        float f=Math.Max(v.WeaponFlash,v.SpottedPenalty),c=FormulaDomain.Result(v.TerrainCover*(v.InsideSmoke?v.SmokeCover:1));
        float a=FormulaDomain.Result(Math.Clamp(f,1,float.MaxValue)*v.AntiVisible/Math.Clamp(c,1,float.MaxValue));
        if(v.SneakEnabled&&!v.Airdrop&&v.WeaponFlash<=1)
        {float sneaking=Math.Clamp(v.AntiVisible/Math.Clamp(FormulaDomain.Result(c*v.CoverBonus),1,float.MaxValue),v.SneakMinAntiVisible,v.MaxAntiVisible);a=Math.Min(a,sneaking);}
        a=Math.Clamp(a,float.Epsilon,v.MaxAntiVisible);float before=FormulaDomain.Result(v.DistanceGround*a);
        return new(f,c,a,before,FormulaDomain.Result(before*(v.OpticsMultiplier??1)));
    }
    // CF15: caller selects ground/low/high sensor and applicable radar/optics branch.
    public static float Air(float selectedSensorDistance,float radarMultiplier,float flash,float spottedPenalty,float antiVisible,float? opticsMultiplier=null)
    {
        FormulaDomain.Nonnegative(selectedSensorDistance,nameof(selectedSensorDistance));FormulaDomain.Nonnegative(radarMultiplier,nameof(radarMultiplier));FormulaDomain.Nonnegative(flash,nameof(flash));FormulaDomain.Nonnegative(spottedPenalty,nameof(spottedPenalty));FormulaDomain.Nonnegative(antiVisible,nameof(antiVisible));
        if(opticsMultiplier is float o)FormulaDomain.Nonnegative(o,nameof(opticsMultiplier));
        return FormulaDomain.Result(selectedSensorDistance*radarMultiplier*Math.Max(flash,spottedPenalty)*antiVisible*(opticsMultiplier??1));
    }
    public static int InclusiveSampleCount(float length){FormulaDomain.Nonnegative(length,nameof(length));return checked((int)MathF.Truncate(length)+1);}
    // CF16 #20344 B: prepared ordered terrain samples; no raycast or projectile branch.
    public static TerrainCost Terrain(ReadOnlySpan<TerrainSample> samples,bool ignoreTerrain,bool ignoreForestOnly,bool forcedNear,float smokeExtra,float maxVision)
    {
        FormulaDomain.Nonnegative(smokeExtra,nameof(smokeExtra));FormulaDomain.Nonnegative(maxVision,nameof(maxVision));
        Span<int> counts=stackalloc int[16];counts.Clear();float sum=0;int first=-1;
        for(int i=0;i<samples.Length;i++)
        {
            var t=samples[i];if(t.Type<0||t.Type>=16)throw new ArgumentOutOfRangeException(nameof(samples));
            FormulaDomain.Finite(t.HeightAboveGround,nameof(samples));FormulaDomain.Finite(t.Height,nameof(samples));FormulaDomain.Finite(t.Opacity,nameof(samples));FormulaDomain.Finite(t.Multiplier,nameof(samples));
            bool eligible=!ignoreTerrain&&t.HeightAboveGround<=t.Height&&!(ignoreForestOnly&&t.Type==2);float cost=1;
            if(eligible){int k=++counts[t.Type];cost=forcedNear&&(t.RawFlags&2)!=0?1:Math.Max(1,FormulaDomain.Result(t.Opacity+k*t.Multiplier));}
            sum=FormulaDomain.Result(sum+cost);if(first<0&&sum>maxVision)first=i;
        }
        sum=FormulaDomain.Result(sum+(forcedNear?0:smokeExtra));
        return new(sum,samples.Length,first); // smoke-only exceedance has no terrain sample index.
    }
    // P3-F05 #20576, called only on timer event; no time integration here.
    public static float WeaponFlashTimer(float flash,float reduction,bool visible,float spottedPenalty)
    {FormulaDomain.Finite(flash,nameof(flash));FormulaDomain.Nonnegative(reduction,nameof(reduction));FormulaDomain.Finite(spottedPenalty,nameof(spottedPenalty));return Math.Clamp(FormulaDomain.Result(flash-reduction),visible?Math.Max(1,spottedPenalty):1,float.MaxValue);}
    // P3-F08 #20415: infinite-line perpendicular distance and half chord only.
    public static SmokeSphere Sphere(Vector3 origin,Vector3 target,Vector3 center,float radius)
    {
        VectorMath.Validate(origin);VectorMath.Validate(target);VectorMath.Validate(center);FormulaDomain.Nonnegative(radius,nameof(radius));
        var ray=target-origin;float length=FormulaDomain.Result(ray.Length());FormulaDomain.Positive(length,nameof(target));
        float distance=FormulaDomain.Result(Vector3.Cross(ray,center-origin).Length()/length);
        bool hit=distance<=radius;float h=hit?FormulaDomain.Result(MathF.Sqrt(FormulaDomain.Result(radius*radius-distance*distance))):0;
        return new(distance,hit,h);
    }
    // P3-F07 #20414: caller supplies resolved, sorted, non-overlapping intervals.
    // Explicit workload budget rejects before calculation; no clipping or overlap policy invented.
    public static SmokeCost Smoke(ReadOnlySpan<SmokeInterval> intervals,float multiplier,int maximumSamples=1000000)
    {
        FormulaDomain.Finite(multiplier,nameof(multiplier));FormulaDomain.Count(maximumSamples,nameof(maximumSamples));
        long total=0;float previous=float.NegativeInfinity;
        foreach(var interval in intervals)
        {
            FormulaDomain.Finite(interval.Start,nameof(intervals));FormulaDomain.Finite(interval.End,nameof(intervals));FormulaDomain.Finite(interval.Opacity,nameof(intervals));
            if(interval.End<interval.Start||interval.Start<previous)throw new ArgumentOutOfRangeException(nameof(intervals));previous=interval.End;
            float length=FormulaDomain.Result(interval.End-interval.Start);if(length>0)total+=InclusiveSampleCount(length);
        }
        if(total>maximumSamples)throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        float sum=0,lengthSum=0;int k=0;
        foreach(var interval in intervals)
        {
            float length=interval.End-interval.Start;if(length<=0)continue;lengthSum=FormulaDomain.Result(lengthSum+length);
            int n=InclusiveSampleCount(length);for(int j=0;j<n;j++)sum=FormulaDomain.Result(sum+FormulaDomain.Result(interval.Opacity+(++k)*multiplier));
        }
        return new(sum,lengthSum,k);
    }
}
