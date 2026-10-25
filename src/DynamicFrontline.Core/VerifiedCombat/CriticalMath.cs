using System.Collections.ObjectModel;
namespace DynamicFrontline.Core.VerifiedCombat;

public enum CriticalType { Optics, Mobility, Targeting, Loading }
public readonly record struct CriticalLevels(int Optics,int Mobility,int Targeting,int Loading)
{
    public int this[int type]=>type switch{0=>Optics,1=>Mobility,2=>Targeting,3=>Loading,_=>throw new ArgumentOutOfRangeException(nameof(type))};
}
public readonly record struct CriticalAttemptCount(int BaseAttempts,float ExtraCheck,int Attempts);
public readonly record struct CriticalTransition(CriticalType Type,int Before,int After,bool Rerolled);
public sealed record CriticalSelection(CriticalLevels Levels,int RemainingAttempts,bool BudgetExhausted,ReadOnlyCollection<CriticalTransition> Trace);
public readonly record struct CriticalRow(float Primary,float Secondary);
public readonly record struct CombatModifiers(float Optics,float AimTime,float MovementSpeed,float RotateSpeed,float Dispersion,float MagazineBurstReload);
public sealed class CriticalTable
{
    private readonly CriticalRow[] rows;
    public float MobilityRotateSpeed {get;}
    // Twelve explicit rows, ordered type then levels 0,1,2. Never exponentiate a penalty.
    public CriticalTable(ReadOnlySpan<CriticalRow> entries,float mobilityRotateSpeed)
    {
        if(entries.Length!=12)throw new ArgumentOutOfRangeException(nameof(entries));
        foreach(var r in entries){FormulaDomain.Nonnegative(r.Primary,nameof(entries));FormulaDomain.Nonnegative(r.Secondary,nameof(entries));}
        FormulaDomain.Nonnegative(mobilityRotateSpeed,nameof(mobilityRotateSpeed));rows=entries.ToArray();MobilityRotateSpeed=mobilityRotateSpeed;
    }
    public CriticalRow Get(CriticalType type,int level){if((int)type<0||(int)type>3||level<0||level>2)throw new ArgumentOutOfRangeException(nameof(level));return rows[(int)type*3+level];}
}
public static class CriticalMath
{
    // CF09/P02 #24129. ExtraCheck can exceed one. RNG is deliberately distinct from CF18.
    public static CriticalAttemptCount Attempts(float multiplier,float hpLossRate,float baseProbability,double roll)
    {
        FormulaDomain.Nonnegative(multiplier,nameof(multiplier));FormulaDomain.Nonnegative(hpLossRate,nameof(hpLossRate));FormulaDomain.Nonnegative(baseProbability,nameof(baseProbability));
        if(!double.IsFinite(roll)||roll<0||roll>=1)throw new ArgumentOutOfRangeException(nameof(roll));
        int q=checked((int)MathF.Floor(multiplier/100));
        float p=FormulaDomain.Result((multiplier-100f*q)*hpLossRate+baseProbability);
        return new(q,p,checked(q+(p>=roll?1:0)));
    }
    public static CriticalSelection Select(CriticalLevels initial,int attempts,int redThreshold,ICombatRandom random,int maximumDraws=10000)
    {
        Validate(initial);FormulaDomain.Count(attempts,nameof(attempts));FormulaDomain.Count(maximumDraws,nameof(maximumDraws));
        if(redThreshold<0||redThreshold>4)throw new ArgumentOutOfRangeException(nameof(redThreshold));ArgumentNullException.ThrowIfNull(random);
        Span<int> values=stackalloc int[]{initial.Optics,initial.Mobility,initial.Targeting,initial.Loading};
        Span<int> candidates=stackalloc int[4];int count=0,draws=0;
        for(int i=0;i<4;i++)if(values[i]<2)candidates[count++]=i;
        var trace=new List<CriticalTransition>();
        while(attempts>0&&count>0&&draws<maximumDraws)
        {
            int affected=4-count;for(int i=0;i<count;i++)if(values[candidates[i]]==1)affected++;
            int pick=random.NextIndex(count);if(pick<0||pick>=count)throw new ArgumentOutOfRangeException(nameof(random));draws++;
            int type=candidates[pick],old=values[type];bool reroll=old==1&&affected<4&&redThreshold>affected;
            trace.Add(new((CriticalType)type,old,reroll?old:old+1,reroll));
            if(reroll)continue;
            values[type]++;attempts--;
            if(values[type]==2){for(int j=pick;j<count-1;j++)candidates[j]=candidates[j+1];count--;}
        }
        return new(new(values[0],values[1],values[2],values[3]),attempts,attempts>0&&count>0&&draws==maximumDraws,trace.AsReadOnly());
    }
    public static CombatModifiers Apply(CriticalLevels levels,CriticalTable table,CombatModifiers input)
    {
        Validate(levels);ArgumentNullException.ThrowIfNull(table);
        FormulaDomain.Nonnegative(input.Optics,nameof(input));FormulaDomain.Nonnegative(input.AimTime,nameof(input));FormulaDomain.Nonnegative(input.MovementSpeed,nameof(input));
        FormulaDomain.Nonnegative(input.RotateSpeed,nameof(input));FormulaDomain.Nonnegative(input.Dispersion,nameof(input));FormulaDomain.Nonnegative(input.MagazineBurstReload,nameof(input));
        var o=table.Get(CriticalType.Optics,levels.Optics);var m=table.Get(CriticalType.Mobility,levels.Mobility);var t=table.Get(CriticalType.Targeting,levels.Targeting);var l=table.Get(CriticalType.Loading,levels.Loading);
        return new(FormulaDomain.Result(input.Optics*o.Primary),FormulaDomain.Result(input.AimTime*o.Secondary),FormulaDomain.Result(input.MovementSpeed*m.Primary),levels.Mobility>0?table.MobilityRotateSpeed:input.RotateSpeed,FormulaDomain.Result(input.Dispersion*t.Primary),FormulaDomain.Result(input.MagazineBurstReload*l.Primary));
    }
    private static void Validate(CriticalLevels levels){for(int i=0;i<4;i++)if(levels[i]<0||levels[i]>2)throw new ArgumentOutOfRangeException(nameof(levels));}
}
