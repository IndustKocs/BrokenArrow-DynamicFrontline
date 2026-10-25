using DynamicFrontline.Core.VerifiedCombat;
static partial class Tests
{
    static CriticalTable CritTable(){var rows=Enumerable.Repeat(new CriticalRow(1,1),12).ToArray();rows[1]=new(.5f,2);rows[4]=new(.4f,1);rows[7]=new(3,1);rows[10]=new(4,1);return new(rows,.2f);}
    static partial void Buffs()
    {
        Test("Buff reset ten accumulators order and final",()=>{var s=BuffAccumulators.Reset with{MovementSpeed=.5f,RotateSpeed=.6f,AimTime=1.5f};var p=BuffEvaluationPipeline.Evaluate(new SuppliedStressModifiers(s,"synthetic-test"),new(1,1,1,1),CritTable());Check(p.Reset==BuffAccumulators.Reset&&p.AfterStress==s&&p.Final==p.AfterCritical);Eq(p.Final.MovementSpeed,.2);Eq(p.Final.RotateSpeed,.2);Eq(p.Final.AimTime,3);Eq(p.Final.Dispersion,3);Eq(p.Final.MagazineBurstReloadCritical,4);Check(s.RotateSpeed==.6f);});
        Test("Buff assignment differs from multiply and reversed order",()=>{var input=BuffAccumulators.Reset with{RotateSpeed=.6f};var correct=BuffEvaluationPipeline.ApplyCritical(input,new(0,1,0,0),CritTable());Eq(correct.RotateSpeed,.2);Check(correct.RotateSpeed!=.6f*.2f);var reversed=BuffEvaluationPipeline.ApplyStress(correct,new SuppliedStressModifiers(input,"synthetic"));Eq(reversed.RotateSpeed,.6);});
        Test("Buff zero crit no added effect for identity zero rows",()=>{var input=BuffAccumulators.Reset with{Optics=.7f};Check(BuffEvaluationPipeline.Evaluate(new SuppliedStressModifiers(input,"synthetic"),new(0,0,0,0),CritTable()).Final==input);Check(BuffEvaluationPipeline.Evaluate(null,null,null).Final==BuffAccumulators.Reset);});
        Test("Buff invalid table and stress provider reject",()=>{Reject(()=>BuffEvaluationPipeline.Evaluate(null,new(1,0,0,0),null));Reject(()=>new CriticalTable(new CriticalRow[11],1));Reject(()=>BuffEvaluationPipeline.Evaluate(new SuppliedStressModifiers(BuffAccumulators.Reset with{Optics=float.NaN},"synthetic"),null,null));});
        Test("Buff potential movement review no commands",()=>{var p=BuffEvaluationPipeline.Evaluate(new SuppliedStressModifiers(BuffAccumulators.Reset with{MovementSpeed=0},"synthetic"),null,null);Check(p.PotentialMovementCommandReview&&p.WouldDisableBuffUpdate);});
    }
}
