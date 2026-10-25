using DynamicFrontline.Core;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
static partial class Tests
{
    static partial void Infantry()
    {
        Test("Infantry count ceil boundaries",()=>{foreach(var (hp,n) in new[]{(80f,8),(20f,2),(20.01f,3),(0f,0),(-1f,0),(100f,8)})Check(InfantryMath.Count(hp,80,8,8).Desired==n);Eq(InfantryMath.Count(20,80,8,8).HealthPerSoldier,10);});
        Test("Infantry count slots distinct from initial",()=>{Check(InfantryMath.Count(80,80,8,4).Desired==4);Reject(()=>InfantryMath.Count(20,80,0,8));Reject(()=>InfantryMath.Count(float.NaN,80,8,8));});
        Test("Death priority stable tie order injected RNG",()=>{SoldierState[] s={new(true,1,1),new(true,3,1),new(true,3,1),new(false,9,1)};var rng=new ScriptRandom(new[]{1});Check(InfantryMath.SelectDeath(s,rng)==2);Check(s[2].Alive&&rng.IndexDraws==1);});
        Test("Heal first minimum no RNG",()=>{SoldierState[] s={new(false,5,1),new(false,2,1),new(false,2,1)};Check(InfantryMath.SelectHeal(s)==1);Check(InfantryMath.SelectHeal(Array.Empty<SoldierState>())==-1);});
        Test("Death empty and invalid random",()=>{var r=new ScriptRandom(Array.Empty<int>());Check(InfantryMath.SelectDeath(Array.Empty<SoldierState>(),r)==-1);Check(r.IndexDraws==0);Reject(()=>InfantryMath.SelectDeath(new[]{new SoldierState(true,1,1)},new ScriptRandom(new[]{1})));});
        Test("Ammo selected first ID and no refill",()=>{var s=new[]{new SoldierState(true,0,4),new(true,0,4),new(true,0,4),new(true,0,5),new(false,0,4)};var a=InfantryMath.Ammunition(s,4,10,55,20,18);Check(a==new AmmunitionResult(3,30,30,20,18,18));a=InfantryMath.Ammunition(s.AsSpan(0,1),4,10,a.CurrentAmmo,20,a.Magazine);Check(a.MaxAmmo==10&&a.CurrentAmmo==10&&a.Magazine==10);a=InfantryMath.Ammunition(s,4,10,a.CurrentAmmo,20,a.Magazine);Check(a.MaxAmmo==30&&a.CurrentAmmo==10&&a.Reserved==10);});
        Test("Ammo zero and overflow guards",()=>{Check(InfantryMath.Ammunition(Array.Empty<SoldierState>(),4,10,10,20,18).CurrentAmmo==0);Reject(()=>InfantryMath.Ammunition(Array.Empty<SoldierState>(),4,-1,10,20,18));Reject(()=>InfantryMath.Ammunition(new[]{new SoldierState(true,0,4),new(true,0,4)},4,int.MaxValue,0,0,0));});
        Test("Stress boundaries recovery order no extra application",()=>{var th=CombatFormulas.Thresholds(1000,.5f,.8f);Check(CombatFormulas.Level(500,th)==StressLevel.Shocked);Check(CombatFormulas.Level(800,th)==StressLevel.Panicked);var step=CombatFormulas.RecoverStressTick(20,0,2);Eq(step.Stress,17);Eq(step.TimeAfterLastDamage,1);Eq(CombatFormulas.StressDamage(6,100,1000,20),80);});
        Test("Readiness preserves observed state and explicit time model",()=>{var d=CombatReadiness.Evaluate(2,8,3,1000,StressLevel.Panicked,6,100,20,0,0,2);Eq(d.CasualtyFraction,.75);Check(d.ObservedState==StressLevel.Panicked);Eq(d.PotentialStress,80);Eq(d.RecoveryTime!.Value,6);Eq(d.Stress,3);var limited=CombatReadiness.Evaluate(8,8,3,1000,StressLevel.Calm,0,100,0,0,0,1,1);Check(limited.RecoveryHorizonExceeded&&limited.RecoveryTime==null);});
        Test("Ammo invariant 500 seeded populations",()=>{var rng=new Random(212);for(int i=0;i<500;i++){int n=rng.Next(20),old=rng.Next(200);var s=Enumerable.Repeat(new SoldierState(true,0,4),n).ToArray();var a=InfantryMath.Ammunition(s,4,10,old,20,18);Check(a.CurrentAmmo<=old&&a.CurrentAmmo<=a.MaxAmmo&&a.Magazine<=a.CurrentAmmo);}});
    }
}
sealed class ScriptRandom(int[] indices,double[]? values=null):ICombatRandom
{
    public int IndexDraws{get;private set;}public int UnitDraws{get;private set;}
    public int NextIndex(int maximum)=>indices[IndexDraws++];
    public double NextUnit()=>(values??new[]{0.5})[UnitDraws++];
}
