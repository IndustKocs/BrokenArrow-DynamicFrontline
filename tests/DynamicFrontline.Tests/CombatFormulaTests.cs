using DynamicFrontline.Core;

static class CombatFormulaTests
{
    private static void Equal(float actual, float expected)
    { if (Math.Abs(actual - expected) > 0.0001f || !float.IsFinite(actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Check(bool condition)
    { if (!condition) throw new Exception("Formula assertion failed"); }
    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Invalid input was accepted");
    }
    public static void Run(Action<string, Action> test)
    {
        test("F01 profiles, existing multiplier and saturation", () => {
            Equal(CombatFormulas.InfantryReceivedDamageMultiplier(1,8,.72f,.02f),.88f);
            Equal(CombatFormulas.InfantryReceivedDamageMultiplier(2,8,.36f,.02f),1.04f);
            Equal(CombatFormulas.InfantryReceivedDamageMultiplier(2,100,.72f,.02f),2);
            Equal(CombatFormulas.InfantryReceivedDamageMultiplier(1,0,-1,.02f),0);
        });
        test("F02 F06 exact boundaries and panic precedence", () => {
            var t=CombatFormulas.Thresholds(1000,.5f,.8f); Equal(t.Shock,500); Equal(t.Panic,800);
            Check(CombatFormulas.Level(499,t)==StressLevel.Calm);
            Check(CombatFormulas.Level(500,t)==StressLevel.Shocked);
            Check(CombatFormulas.Level(800,t)==StressLevel.Panicked);
            Check(CombatFormulas.Level(500,new(500,500))==StressLevel.Panicked);
        });
        test("F03 no implicit stress cap; F04 never reduces stress", () => {
            Equal(CombatFormulas.StressDamage(10,100,1000,20),120);
            Equal(CombatFormulas.StressDamage(200,100,1000,20),2020);
            Equal(CombatFormulas.StressToTarget(200,1000,500),300);
            Equal(CombatFormulas.StressToTarget(200,1000,100),0);
            Equal(CombatFormulas.StressToTarget(200,1000,2000),800);
        });
        test("F05 one tick, truncation, exhaustion and inactive zero branch", () => {
            var step=CombatFormulas.RecoverStressTick(100,2,1); Equal(step.Stress,96); Equal(step.TimeAfterLastDamage,3);
            step=CombatFormulas.RecoverStressTick(100,2.75f,1); Equal(step.Stress,96); Equal(step.TimeAfterLastDamage,3.75f);
            step=CombatFormulas.RecoverStressTick(2,2,1); Equal(step.Stress,0);
            step=CombatFormulas.RecoverStressTick(0,3,1); Equal(step.TimeAfterLastDamage,3);
        });
        test("F07 minimum damage does not revive zero or negative damage", () => {
            Equal(CombatFormulas.KineticDamage(10,525,1000,1,.1f),1);
            Equal(CombatFormulas.KineticDamage(10,525,1050,1,.1f),0);
            Equal(CombatFormulas.KineticDamage(10,525,1100,1,.1f),0);
            Equal(CombatFormulas.KineticDamage(10,525,525,1,.1f),10);
        });
        test("F08 strict threshold and HEAT bypass", () => {
            Equal(CombatFormulas.DamageWithThreshold(10,10,false),0);
            Equal(CombatFormulas.DamageWithThreshold(10,10,true),10);
            Equal(CombatFormulas.DamageWithThreshold(11,10,false),11);
        });
        test("F09 raw distance with nonzero minimum range and armor branches", () => {
            Equal(CombatFormulas.Penetration(1,840,525,0,700,350),682.5f);
            Equal(CombatFormulas.Penetration(1,840,525,100,800,350),682.5f);
            Equal(CombatFormulas.Penetration(1,840,525,0,700,3000),0);
            Check(CombatFormulas.Penetration(0,0,0,0,0,0)==float.MaxValue);
            Equal(CombatFormulas.Penetration(2,0,-1,0,0,0),0);
        });
        test("F13 independent overrides and absent ammunition", () => {
            Check(CombatFormulas.AimTimes(1,2,null)==(1f,2f));
            Check(CombatFormulas.AimTimes(1,2,(0,3))==(1f,3f));
            Check(CombatFormulas.AimTimes(1,2,(3,0))==(3f,2f));
        });
        test("formula supported-domain guards", () => {
            Reject(()=>CombatFormulas.StressDamage(10,0,1000,20));
            Reject(()=>CombatFormulas.Thresholds(100,.8f,.5f));
            Reject(()=>CombatFormulas.Level(float.NaN,new(50,80)));
            Reject(()=>CombatFormulas.StressToTarget(101,100,200));
            Reject(()=>CombatFormulas.KineticDamage(10,0,100,1,.1f));
            Reject(()=>CombatFormulas.KineticDamage(10,100,200,0,.1f));
            Reject(()=>CombatFormulas.Penetration(1,840,525,100,100,50));
            Reject(()=>CombatFormulas.RecoverStressTick(10,0,-1));
        });
        test("optional stress condition requires timer and observed calm; no stress writes", () => {
            var f=new Fixture(recoveryCondition:new(.5f,.8f));
            f.Game.Units[f.Key]=f.Game.Units[f.Key] with { CurrentStressValue=50, CurrentStressLevel="Calm" };
            Check(f.C.Activate(f.Key,0)); f.Arrive(); f.C.Tick(1); f.C.Tick(11);
            Check(f.C.State==DesertionState.Recovering); // Equality is Shocked despite the stale label.
            f.Game.Units[f.Key]=f.Game.Units[f.Key] with { CurrentStressValue=49 };
            f.C.Tick(12); Check(f.C.State==DesertionState.Controlled);
            Equal(f.Game.Units[f.Key].CurrentStressValue,49); Check(f.Game.Calls.Count==2);
            var g=new Fixture(recoveryCondition:new(.5f,.8f));
            Check(g.C.Activate(g.Key,0)); g.Arrive(); g.C.Tick(1); g.C.Tick(2);
            Check(g.C.State==DesertionState.Recovering);
        });
        test("invalid observed stress waits then releases lock on recovery timeout", () => {
            var f=new Fixture(recoveryCondition:new(.5f,.8f)); Check(f.C.Activate(f.Key,0));
            f.Arrive(); f.C.Tick(1);
            f.Game.Units[f.Key]=f.Game.Units[f.Key] with { CurrentStressValue=float.NaN };
            f.C.Tick(11); Check(f.C.Locked); f.C.Tick(121);
            Check(!f.C.Locked && f.C.LastReason=="stress-recovery-timeout" && f.Game.Calls.Count==2);
            var condition=new CalmStressRecoveryCondition(.5f,.8f);
            Check(!condition.IsSatisfied(f.Game.Units[f.Key] with {CurrentStressValue=0,MaxStress=0}));
        });
    }
}
