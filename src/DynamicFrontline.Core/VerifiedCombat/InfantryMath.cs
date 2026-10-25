namespace DynamicFrontline.Core.VerifiedCombat;

// Separate from missile RandomComponent reconstruction; no seed is chosen here.
public interface ICombatRandom { int NextIndex(int exclusiveMaximum); double NextUnit(); }
public readonly record struct SoldierState(bool Alive,int DeathPriority,int? FirstAmmoId);
public readonly record struct InfantryCount(float HealthPerSoldier,int Desired);
public readonly record struct AmmunitionResult(int MatchingAliveSoldiers,int MaxAmmo,int CurrentAmmo,int Capacity,int Magazine,int Reserved);
public static class InfantryMath
{
    // CF12 #11030/#12600. Never substitutes for an observed AliveSoldierCount.
    public static InfantryCount Count(float hp,float armorMaxHealth,int initialSquadMembers,int soldierSlots)
    {
        FormulaDomain.Finite(hp,nameof(hp));FormulaDomain.Positive(armorMaxHealth,nameof(armorMaxHealth));
        if(initialSquadMembers<=0)throw new ArgumentOutOfRangeException(nameof(initialSquadMembers));FormulaDomain.Count(soldierSlots,nameof(soldierSlots));
        float per=armorMaxHealth/initialSquadMembers;FormulaDomain.Positive(per,nameof(per));
        float desired=Math.Clamp(MathF.Ceiling(hp/per),0,soldierSlots);
        return new(per,checked((int)desired));
    }
    // CF13 #12602. Returns an index, never mutates the caller's array or ECS.
    public static int SelectDeath(ReadOnlySpan<SoldierState> soldiers,ICombatRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);int priority=int.MinValue,count=0;
        foreach(var s in soldiers)if(s.Alive){if(s.DeathPriority>priority){priority=s.DeathPriority;count=1;}else if(s.DeathPriority==priority)count++;}
        if(count==0)return -1;
        int selected=random.NextIndex(count);if(selected<0||selected>=count)throw new ArgumentOutOfRangeException(nameof(random));
        for(int i=0;i<soldiers.Length;i++)if(soldiers[i].Alive&&soldiers[i].DeathPriority==priority&&selected--==0)return i;
        throw new InvalidOperationException();
    }
    // CF13 #12601. Stable first minimum among dead candidates supplied by caller.
    public static int SelectHeal(ReadOnlySpan<SoldierState> soldiers)
    {
        int index=-1,priority=int.MaxValue;
        for(int i=0;i<soldiers.Length;i++)if(!soldiers[i].Alive&&(index<0||soldiers[i].DeathPriority<priority)){index=i;priority=soldiers[i].DeathPriority;}
        return index;
    }
    // P3-F01 #12603. Only the selected ammo container; no implicit resupply.
    public static AmmunitionResult Ammunition(ReadOnlySpan<SoldierState> soldiers,int selectedAmmoId,int ammoPerSoldier,int oldCurrent,int magazineSize,int oldMagazine)
    {
        FormulaDomain.Count(ammoPerSoldier,nameof(ammoPerSoldier));FormulaDomain.Count(oldCurrent,nameof(oldCurrent));
        FormulaDomain.Count(magazineSize,nameof(magazineSize));FormulaDomain.Count(oldMagazine,nameof(oldMagazine));
        int n=0;foreach(var s in soldiers)if(s.Alive&&s.FirstAmmoId==selectedAmmoId)n++;
        int max=checked(ammoPerSoldier*n),current=Math.Min(oldCurrent,max),capacity=Math.Max(0,Math.Min(magazineSize,current)),magazine=Math.Max(0,Math.Min(oldMagazine,capacity));
        return new(n,max,current,capacity,magazine,magazine);
    }
}
