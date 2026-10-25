using System.Numerics;
using System.Text.Json;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
namespace DynamicFrontline.Core.Diagnostics;
public static partial class FormulaExplorer
{
    private static readonly CorrectionTableCache researchCurveCache=new();
    private static readonly object curveCacheLock=new();
    private static ExplorerResult EvaluatePass04(ExplorerRequest request)
    {
        if(request.Profile!=null)throw new FormulaStageException("Profile","Pass04 accepts explicit synthetic inputs only; no active profile inferred");
        var p=new Dictionary<string,JsonElement>(request.Parameters);string id=request.Formula;
        T Read<T>(string key)=>p.TryGetValue(key,out var e)?e.Deserialize<T>()??throw new FormulaStageException("Input",$"Null {key}"):throw new FormulaStageException("Input",$"Missing {key}");
        float F(string key)=>Read<float>(key);bool B(string key)=>p.ContainsKey(key)&&Read<bool>(key);
        Vector3 V(string key){var a=Read<float[]>(key);if(a.Length!=3)throw new FormulaStageException("Input",$"Expected 3-vector {key}");return new(a[0],a[1],a[2]);}
        CyclicRandom Rng()=>new(Read<float[]>("Queue"),p.ContainsKey("PreviousIndex")?Read<int>("PreviousIndex"):0);
        CorrectionTable Table()
        {
            var c=new LinearTestCurve(Read<CurveKey[]>("CDFKeys"));var source=new CurveSource(Read<string>("SourceId"),Read<string>("SourceVersion"),c.Identity);
            lock(curveCacheLock)return researchCurveCache.Get(source,c,new LinearInverseFactory());
        }
        object calculation;string method,evidence="B",status="Verified Fragment",domain="Finite validated inputs; synthetic explicit configuration",limitations="Static fragment only, no game integration or native bit-exact claim.";
        switch(id)
        {
            case "P4-F01":
                var t=Table();var indices=p.ContainsKey("Indices")?Read<int[]>("Indices"):new[]{0,1,250,500,999};
                calculation=new{t.Source,t.SourceEvaluator,t.InverseInterpolation,KeyCount=t.InverseKeys.Count,TableLength=t.Count,Selected=indices.Select(i=>new{Index=i,Value=t[i]}).ToArray(),Values=B("IncludeTable")?t.CopyValues():null,Keys=B("IncludeKeys")?t.InverseKeys:null};
                method="GameController.BattleSystemConstantsLoad #21700";status="Verified Fragment + Experimental interpolation";limitations="Strict rejection is library policy; linear evaluator is not Unity AnimationCurve.";break;
            case "P4-Polar":
                var table=Table();var random=Rng();var polar=ArtilleryDispersionMath.Polar(table,random);
                calculation=new{Sample=polar,QueueIndex=random.Index,ExperimentalVector=B("ExperimentalCosSin")?(Vector2?)ExperimentalPolarOrientation.CosSin(polar):null,table.Source,table.InverseInterpolation};
                method="ShootingSystem.CalculateDeviationPolar #24225";status="Verified Fragment + Experimental curve/RNG";limitations="Angle/index/radius order verified; optional CosSin orientation is experimental; index1000 rejects.";break;
            case "P4-F03":calculation=ArtilleryDispersionMath.FastDimond(Rng());method="ShootingSystem.CalculateDeviationFastDimond #24224";limitations="Truncation bands are not uniform area sampling; CyclicRandom remains experimental.";break;
            case "P4-F02":calculation=ArtilleryDispersionMath.Apply(V("Direction"),F("DistanceDelta"),F("EffectiveRange"),F("HorizontalRadius"),F("VerticalRadius"),F("U"),F("V"));method="ShootingSystem.AddTrajectoryDeviation #24223";domain="Finite nondegenerate cross for positive range; range<=0 returns unchanged";break;
            case "BuffPipeline":calculation=BuffEvaluationPipeline.Evaluate(p.ContainsKey("Stress")?new SuppliedStressModifiers(Read<BuffAccumulators>("Stress"),Read<string>("StressProvenance")):null,p.ContainsKey("Levels")?Read<CriticalLevels>("Levels"):null,p.ContainsKey("Rows")?new CriticalTable(Read<CriticalRow[]>("Rows"),F("MobilityRotateSpeed")):null);method="BuffDebuffSystem #24106/#24109/#24110";limitations="Stress values supplied, not reconstructed producers; no movement/commands or dirty-component writes.";break;
            case "P4-F09":calculation=VisibilityMath.GetSingleSphereSmokeInterval(V("Origin"),V("Target"),V("Center"),F("Radius"));method="SmokeHelper.GetSmokeDistances #20415";evidence="A";domain="Finite nonzero ray and R>=0";limitations="Magnitude projection; no standard segment clipping, overlap resolution or opacity union.";break;
            case "P4-F07":calculation=new{Yaw=AircraftMath.CalculateYawRate(F("Speed"),Read<AircraftSpeedPreset>("Speeds"),Read<AircraftRatePreset>("Yaw")),Pitch=AircraftMath.CalculatePitchRate(F("Speed"),Read<AircraftSpeedPreset>("Speeds"),Read<AircraftRatePreset>("Pitch"))};method="AircraftControl.SetManeuverability #11974";domain="Finite ordered speed presets; denominator<=.001f uses t0";break;
            case "P4-F05":calculation=NavigationMath.AstarPriority(F("Dx"),F("Dy"),Read<double>("G"));method="PathFinder.GetAstarPriority #26017";evidence="A";limitations="Single addition before double promotion; not a pathfinder, optimum guarantee or meters.";break;
            case "P4-F04":
                var ring=Rng();var ranges=Read<float[][]>("Ranges");if(ranges.Any(x=>x.Length!=2))throw new FormulaStageException("RNG.Input","Ranges require pairs");
                var values=ranges.Select(x=>ring.NextFloat(x[0],x[1])).ToArray();calculation=new{Values=values,ring.Index,ring.ValuesConsumed};method="RandomComponent.Init/NextFloat #24442/#24443";status="Experimental";limitations="Queue fixture only; no IL2CPP System.Random equivalence, remote index or seed producer claim.";break;
            case "VersionAwareIdentity":calculation=new{Decision=IdentityValidation.Validate(Read<LifecycleObservation>("Snapshot"),Read<LifecycleObservation>("Current")).ToString()};method="DefaultEcs.World.On(EntityDisposedMessage) #190347; proposed snapshot policy";evidence="E (based on B disposal evidence)";status="Experimental";limitations="Proposal only; existing GameUnitIdentityResolver untouched.";break;
            case "P4-F08":calculation=H1FiniteResearch.Evaluate(F("X"));method="native 0x1867662D0 / 0x18677C900";status="Experimental";limitations="Finite rational transcription; not native atan NaN/FMA/ULP contract; no CF19 solver wiring.";break;
            default:throw new FormulaStageException("Formula","Unsupported Pass04 formula; P4-F06 is internal research-only diagnostic, not public contact export");
        }
        return new(id,evidence,"combat_systems_audit/pass_04/COMBAT_FORMULAS_PASS04.md",limitations,null,p,calculation,method,domain,status,"Explicit synthetic inputs; runtime profile unknown");
    }
}
