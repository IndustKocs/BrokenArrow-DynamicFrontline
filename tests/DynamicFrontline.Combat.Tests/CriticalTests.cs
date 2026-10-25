using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static partial void Criticals()
    {
        Test("Critical remainder no hidden division or clamp",()=>{var a=CriticalMath.Attempts(250,.1f,.02f,.99);Check(a.BaseAttempts==2&&a.Attempts==3);Eq(a.ExtraCheck,5.02);});
        Test("Critical comparison inclusive and zero remainder",()=>{Check(CriticalMath.Attempts(200,1,.5f,.5).Attempts==3);Check(CriticalMath.Attempts(200,1,0,.1).Attempts==2);Reject(()=>CriticalMath.Attempts(-1,1,0,0));Reject(()=>CriticalMath.Attempts(1,1,0,1));});
        Test("Critical reroll does not consume attempt",()=>{var rng=new ScriptRandom(new[]{0,1});var r=CriticalMath.Select(new(1,0,0,0),1,3,rng);Check(r.Trace.Count==2&&r.Trace[0].Rerolled&&r.Levels==new CriticalLevels(1,1,0,0));Check(r.RemainingAttempts==0&&rng.IndexDraws==2);});
        Test("Critical red removal preserves candidate ordering",()=>{var r=CriticalMath.Select(new(1,1,1,1),2,4,new ScriptRandom(new[]{0,0}));Check(r.Levels==new CriticalLevels(2,2,1,1));});
        Test("Critical explicit budget reports incomplete not false success",()=>{var r=CriticalMath.Select(new(1,0,0,0),1,4,new ScriptRandom(new[]{0,0}),2);Check(r.BudgetExhausted&&r.RemainingAttempts==1&&r.Levels.Optics==1);});
        Test("Critical no candidates and invalid levels",()=>{var r=CriticalMath.Select(new(2,2,2,2),10,4,new ScriptRandom(Array.Empty<int>()));Check(!r.BudgetExhausted&&r.Trace.Count==0&&r.RemainingAttempts==10);Reject(()=>CriticalMath.Select(new(3,0,0,0),1,1,new ScriptRandom(new[]{0})));});
        Test("Critical table nonlinear per type and level",()=>{var rows=Enumerable.Repeat(new CriticalRow(1,1),12).ToArray();rows[2]=new(.3f,1.7f);rows[4]=new(.4f,99);rows[8]=new(2.6f,1);rows[10]=new(1.8f,1);var table=new CriticalTable(rows,.2f);rows[2]=new(99,99);var r=CriticalMath.Apply(new(2,1,2,1),table,new(2,3,4,5,6,7));Eq(r.Optics,.6);Eq(r.AimTime,5.1);Eq(r.MovementSpeed,1.6);Eq(r.RotateSpeed,.2);Eq(r.Dispersion,15.6);Eq(r.MagazineBurstReload,12.6);});
        Test("Critical table no mobility override at zero",()=>{var t=new CriticalTable(Enumerable.Repeat(new CriticalRow(1,1),12).ToArray(),.2f);Eq(CriticalMath.Apply(new(0,0,0,0),t,new(1,1,1,5,1,1)).RotateSpeed,5);Reject(()=>new CriticalTable(new CriticalRow[11],1));});
    }
}
