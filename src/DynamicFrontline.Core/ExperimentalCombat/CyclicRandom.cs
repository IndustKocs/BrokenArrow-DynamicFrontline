using DynamicFrontline.Core.VerifiedCombat;
namespace DynamicFrontline.Core.ExperimentalCombat;

// Autonomous CF18 reconstruction. System.Random compatibility with native runtime is NOT claimed.
// Does not implement ICombatRandom: critical selection must receive a separate source.
public sealed class CyclicRandom
{
    private readonly float[] queue;
    public int Index {get;private set;}
    public int? Seed {get;}
    public int? NetId {get;}
    public CyclicRandom(int seed,int netId)
    {
        Seed=seed;NetId=netId;queue=new float[100];var random=new Random(unchecked(seed-netId));
        for(int i=0;i<100;i++)queue[i]=1-(float)random.NextDouble();
    }
    public CyclicRandom(ReadOnlySpan<float> values,int previousIndex=0)
    {
        if(values.Length!=100||previousIndex<0||previousIndex>=100)throw new ArgumentOutOfRangeException(nameof(values));
        foreach(float v in values)FormulaDomain.Probability(v,nameof(values));queue=values.ToArray();Index=previousIndex;
    }
    public float NextFloat(float minimum,float maximum)
    {
        FormulaDomain.Finite(minimum,nameof(minimum));FormulaDomain.Finite(maximum,nameof(maximum));
        if(minimum>maximum)throw new ArgumentOutOfRangeException(nameof(minimum));
        if(minimum==maximum)return minimum;
        float range=FormulaDomain.Result(maximum-minimum);int next=(Index+1)%100;
        float result=FormulaDomain.Result(minimum+range*queue[next]);Index=next;return result;
    }
}
public enum SeekerGate { UnsupportedSeeker, MissingRandom, ResetNoTarget, KeepState, EligibleForFurtherChecks }
public readonly record struct SeekerGateResult(SeekerGate Gate,bool ForceReroll,int ObservedCountermeasures);
public static class SeekerFragments
{
    // Pass03 #24357 ordered prefix only. No missile trajectory, shooter validation, or full state machine.
    public static SeekerGateResult Gate(int seeker,bool hasRandom,bool hasTarget,int missType,int activeCount,int lastCount,bool forceReroll)
    {
        FormulaDomain.Count(activeCount,nameof(activeCount));FormulaDomain.Count(lastCount,nameof(lastCount));
        if(missType<0||missType>3)throw new ArgumentOutOfRangeException(nameof(missType));
        if(seeker is 200 or 300)return new(SeekerGate.UnsupportedSeeker,forceReroll,lastCount);
        if(!hasRandom)return new(SeekerGate.MissingRandom,forceReroll,lastCount);
        if(!hasTarget)return new(SeekerGate.ResetNoTarget,false,0);
        bool reroll=forceReroll||missType==0||(missType==1&&activeCount>lastCount);
        return new(missType!=0&&!reroll?SeekerGate.KeepState:SeekerGate.EligibleForFurtherChecks,reroll,activeCount);
    }
    // Test-only probability use policy, separate from unbounded F12 helper.
    public static bool CompareProbability(float probability,float roll)
    {FormulaDomain.Probability(probability,nameof(probability));FormulaDomain.Probability(roll,nameof(roll));return probability>=roll;}
}
