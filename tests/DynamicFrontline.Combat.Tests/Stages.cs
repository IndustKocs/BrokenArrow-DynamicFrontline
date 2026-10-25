using System.Numerics;
using DynamicFrontline.Core;
using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static partial void Stages()
    {
        Damage(); Infantry(); Missiles(); Criticals(); AirFow(); Profiles();
    }
    static partial void Infantry();static partial void Missiles();static partial void Criticals();static partial void AirFow();static partial void Profiles();
    static void Damage()
    {
        Test("HEAT equality and asymmetry",()=>{Eq(CombatFormulas.HeatDamage(10,100,100,1,2),5);Eq(CombatFormulas.HeatDamage(10,200,100,1,2),8);Eq(CombatFormulas.HeatDamage(10,50,100,1,2),2);});
        Test("HEAT operation intermediates",()=>{var h=CombatFormulas.HeatDetails(10,100,100,2,2);Eq(h.Numerator,100000);Eq(h.Denominator,30000);Eq(h.Damage,10f/3);});
        Test("HEAT finite supported domain",()=>{Reject(()=>CombatFormulas.HeatDamage(10,0,0,1,2));Reject(()=>CombatFormulas.HeatDamage(10,-1,1,1,2));Reject(()=>CombatFormulas.HeatDamage(float.NaN,1,1,1,2));Reject(()=>CombatFormulas.HeatDamage(10,1e30f,1,1,4));});
        Test("Kinetic positive minimum only",()=>{Eq(CombatFormulas.KineticDamage(10,100,200,1,.2f),0);Eq(CombatFormulas.KineticDamage(10,100,199,1,.2f),2);Eq(CombatFormulas.KineticDamage(10,200,100,1,.2f),10);});
        Test("Threshold strict equality HEAT bypass",()=>{Eq(CombatFormulas.DamageWithThreshold(2,2,false),0);Eq(CombatFormulas.DamageWithThreshold(2,2,true),2);Eq(CombatFormulas.DamageWithThreshold(2.001f,2,false),2.001f);Reject(()=>CombatFormulas.DamageWithThreshold(-1,2,false));});
        Test("Range actual not offset",()=>{Eq(CombatFormulas.Penetration(1,100,50,10,110,10),95);Reject(()=>CombatFormulas.Penetration(1,100,50,10,10,10));});
        Test("AoE ammunition 4 surface example",()=>{var r=ExplosionMath.Evaluate(new(25,0,0),Vector3.Zero,2.5f,12,310,45,67.5f);Eq(r.SurfaceDistance,22.5);Eq(r.HealthAOE,.5);Eq(r.StressAOE,2d/3);Eq(r.HealthBeforeOtherModifiers,6);Eq(r.DirectStressBeforeOtherModifiers,206.6666667);});
        Test("AoE zero radius and no falloff boundaries",()=>{Eq(ExplosionMath.Falloff(0,0,true),0);Eq(ExplosionMath.Falloff(5,5,true),0);Eq(ExplosionMath.Falloff(4.999f,5,true),1);Eq(ExplosionMath.SurfaceDistance(Vector3.Zero,Vector3.One,5),0);Reject(()=>ExplosionMath.Falloff(-1,2));});
        Test("Differential finite HEAT and AoE seeded 1000",()=>{var rng=new Random(210);for(int i=0;i<1000;i++){float p=rng.Next(1,500),a=rng.Next(1,500),d=rng.Next(1,50);Eq(CombatFormulas.HeatDamage(d,p,a,1,2),d*(double)p*p/(p*p+a*a));float r=rng.Next(1,100),x=rng.Next(100,200);Eq(ExplosionMath.Falloff(x,r),0);}});
    }
}
