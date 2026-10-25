using DynamicFrontline.Core;
using DynamicFrontline.Core.ExperimentalCombat;
static partial class Tests
{
    static partial void Missiles()
    {
        Test("F11 power and zero count",()=>{Eq(CombatFormulas.Countermeasures(.2f,.5f,2),.16);Eq(CombatFormulas.Countermeasures(1,0,0),1);Eq(CombatFormulas.Countermeasures(1,.5f,1),0);Eq(CombatFormulas.Countermeasures(0,2,2),4);Reject(()=>CombatFormulas.Countermeasures(.2f,.5f,-1));});
        Test("F12 direct product no cap",()=>{Eq(CombatFormulas.MissileHitChance(.8f,.9f,.16f,1,1,1),.1152);Eq(CombatFormulas.MissileHitChance(2,2,1,1,1,1),4);Reject(()=>SeekerFragments.CompareProbability(4,.5f));Reject(()=>CombatFormulas.MissileHitChance(float.NaN,1,1,1,1,1));});
        Test("Probability equality separate policy",()=>{Check(SeekerFragments.CompareProbability(.5f,.5f));Check(!SeekerFragments.CompareProbability(.49f,.5f));});
        Test("CF18 increments first and wraps",()=>{var q=new float[100];q[7]=.25f;q[0]=1;var r=new CyclicRandom(q,6);Eq(r.NextFloat(10,14),11);Check(r.Index==7);r=new CyclicRandom(q,99);Eq(r.NextFloat(10,14),14);Check(r.Index==0);});
        Test("CF18 constant and invalid ranges do not advance",()=>{var r=new CyclicRandom(17,5);Eq(r.NextFloat(3,3),3);Check(r.Index==0);Reject(()=>r.NextFloat(5,3));Check(r.Index==0);Reject(()=>new CyclicRandom(new float[99]));});
        Test("CF18 deterministic period and copied fixture",()=>{var a=new CyclicRandom(42,7);var b=new CyclicRandom(42,7);var samples=new float[100];for(int i=0;i<100;i++){samples[i]=a.NextFloat(0,1);Eq(samples[i],b.NextFloat(0,1),0);}for(int i=0;i<100;i++)Eq(a.NextFloat(0,1),samples[i],0);var q=new float[100];var c=new CyclicRandom(q);q[1]=1;Eq(c.NextFloat(0,1),0);});
        Test("Seeker gate prefix no unconditional reroll",()=>{Check(SeekerFragments.Gate(200,true,false,0,0,0,false).Gate==SeekerGate.UnsupportedSeeker);Check(SeekerFragments.Gate(10,false,false,0,0,0,false).Gate==SeekerGate.MissingRandom);Check(SeekerFragments.Gate(10,true,false,1,4,2,false).Gate==SeekerGate.ResetNoTarget);Check(SeekerFragments.Gate(10,true,true,1,2,2,false).Gate==SeekerGate.KeepState);Check(SeekerFragments.Gate(10,true,true,1,3,2,false).ForceReroll);Check(!SeekerFragments.Gate(10,true,true,2,3,2,false).ForceReroll);});
        Test("Differential missile composition seeded 1000",()=>{var r=new Random(214);for(int i=0;i<1000;i++){float resistance=(float)r.NextDouble(),m=(float)r.NextDouble();int n=r.Next(10);Eq(CombatFormulas.Countermeasures(resistance,m,n),n==0?1:Math.Pow((1-resistance)*m,n));float a=(float)r.NextDouble()*2;Eq(CombatFormulas.MissileHitChance(a,2,1,1,1,1),a*2);}});
    }
}
