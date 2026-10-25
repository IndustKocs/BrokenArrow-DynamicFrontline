using System.Numerics;
using System.Text.Json;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
namespace DynamicFrontline.Core.Diagnostics;
public sealed record ExplorerRequest(string Formula,string? Profile,Dictionary<string,JsonElement> Parameters);
public sealed record ExplorerResult(string Formula,string Evidence,string Source,string Limitations,ResearchProfile? Profile,Dictionary<string,JsonElement> EffectiveInputs,object Calculation);
public static class FormulaExplorer
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,IncludeFields=true};
    public static ExplorerResult Evaluate(ExplorerRequest request,ResearchProfileCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(request);ArgumentNullException.ThrowIfNull(request.Parameters);
        var p=new Dictionary<string,JsonElement>();ResearchProfile? profile=null;
        if(request.Profile!=null){profile=catalog.Get(request.Profile);if(!profile.FormulaIds.Contains(request.Formula))throw new ArgumentException("Profile does not apply to this formula");foreach(var kv in profile.Values)p[kv.Key]=JsonSerializer.SerializeToElement(kv.Value);}
        foreach(var kv in request.Parameters)p[kv.Key]=kv.Value;
        float F(string key)=>p.TryGetValue(key,out var v)?v.GetSingle():throw new ArgumentException("Missing "+key);
        int I(string key){float f=F(key);if(!float.IsFinite(f)||f!=MathF.Truncate(f))throw new ArgumentException("Expected integer "+key);return checked((int)f);}
        bool B(string key)=>p.TryGetValue(key,out var v)&&v.GetBoolean();
        Vector3 V(string key){var a=p[key].Deserialize<float[]>()!;if(a.Length!=3)throw new ArgumentException("Expected 3-vector "+key);return new(a[0],a[1],a[2]);}
        T Read<T>(string key)=>p[key].Deserialize<T>()??throw new ArgumentException("Missing "+key);
        string id=request.Formula;
        object result=id switch
        {
            "F01"=>CombatFormulas.InfantryReceivedDamageMultiplier(F("Incoming"),checked((uint)I("Alive")),F("InfantryDmgModFloor"),F("InfantryDmgModPerSoldier")),
            "F02"=>CombatFormulas.Thresholds(F("Maximum"),F("ShockRatio"),F("PanicRatio")),
            "F03"=>CombatFormulas.StressDamage(F("HealthDamage"),F("MaximumHealth"),F("MaximumStress"),F("DirectStress")),
            "F04"=>CombatFormulas.StressToTarget(F("Current"),F("Maximum"),F("Target")),
            "F05"=>CombatFormulas.RecoverStressTick(F("Current"),F("TimeAfterDamage"),F("StressRecoveryMultiplier")),
            "F06"=>new{Level=CombatFormulas.Level(F("Current"),new(F("Shock"),F("Panic"))).ToString()},
            "F07"=>CombatFormulas.KineticDamage(F("Damage"),F("Penetration"),F("Armor"),F("Effectiveness"),F("MinimumCoefficient")),
            "F08"=>CombatFormulas.DamageWithThreshold(F("Damage"),F("Threshold"),B("IsHeat")),
            "F09"=>CombatFormulas.Penetration(I("ArmorType"),F("MinimumPenetration"),F("EffectivePenetration"),F("MinimumRange"),F("EffectiveRange"),F("ActualRange")),
            "F10"=>CombatFormulas.HeatDetails(F("Damage"),F("Penetration"),F("Armor"),F("Effectiveness"),F("Curve")),
            "F11"=>CombatFormulas.Countermeasures(F("Resistivity"),F("AccuracyMultiplier"),I("Count")),
            "F12"=>CombatFormulas.MissileHitChance(F("Accuracy"),F("ECM"),F("CM"),F("Stress"),F("Veterancy"),F("Critical")),
            "F13"=>CombatFormulas.AimTimes(F("WeaponMinimum"),F("WeaponMaximum"),(F("AmmoMinimum"),F("AmmoMaximum"))),
            "CF03"=>ExplosionMath.Evaluate(V("Center"),V("Hit"),F("RoughRadius"),F("Damage"),F("StressDamage"),F("HealthAOERadius"),F("StressAOERadius"),B("NoFalloff")),
            "CF12"=>InfantryMath.Count(F("HP"),F("ArmorMaxHealth"),I("InitialSquadMembers"),I("SoldierSlots")),
            "CF13"=>new{Death=InfantryMath.SelectDeath(Read<SoldierState[]>("Soldiers"),new SuppliedRandom(Read<int[]>("Indices"))),Heal=InfantryMath.SelectHeal(Read<SoldierState[]>("Soldiers"))},
            "P3-F01"=>InfantryMath.Ammunition(Read<SoldierState[]>("Soldiers"),I("SelectedAmmoId"),I("AmmoPerSoldier"),I("OldCurrent"),I("MagazineSize"),I("OldMagazine")),
            "CF09"=>CriticalMath.Attempts(F("Multiplier"),F("HpLossRate"),F("BaseProbability"),p["Roll"].GetDouble()),
            "CF09-selection"=>CriticalMath.Select(Read<CriticalLevels>("Levels"),I("Attempts"),I("RedThreshold"),new SuppliedRandom(Read<int[]>("Indices")),I("MaximumDraws")),
            "P3-F09"=>CriticalMath.Apply(Read<CriticalLevels>("Levels"),new CriticalTable(Read<CriticalRow[]>("Rows"),F("MobilityRotateSpeed")),Read<CombatModifiers>("Modifiers")),
            "P3-F02"=>AircraftMath.ShiftFinite(V("Current"),V("Target"),F("Agility"),F("DeltaTime")),
            "P3-F03"=>AircraftMath.ShiftTime(V("Current"),V("Target"),F("Agility")),
            "P3-F04"=>AircraftMath.Fuel(F("OldFuel"),F("DeltaTime"),F("AfterburnerMultiplier")),
            "CF14"=>VisibilityMath.Ground(Read<GroundVisionInput>("Ground")),
            "CF15"=>VisibilityMath.Air(F("SensorDistance"),F("RadarMultiplier"),F("WeaponFlash"),F("SpottedPenalty"),F("AntiVisible"),p.ContainsKey("Optics")?F("Optics"):null),
            "CF16"=>VisibilityMath.Terrain(Read<TerrainSample[]>("Samples"),B("IgnoreTerrain"),B("IgnoreForestOnly"),B("ForcedNear"),F("SmokeExtra"),F("MaxVision")),
            "P3-F05"=>VisibilityMath.WeaponFlashTimer(F("WeaponFlash"),F("FlashReduction"),B("Visible"),F("SpottedPenalty")),
            "P3-F07"=>VisibilityMath.Smoke(Read<SmokeInterval[]>("Intervals"),F("Multiplier")),
            "P3-F08"=>VisibilityMath.Sphere(V("Origin"),V("Target"),V("Center"),F("Radius")),
            _=>throw new ArgumentException("Unsupported formula; consult implementation matrix")
        };
        string source=id.StartsWith("P3-")?"combat_systems_audit/pass_03/COMBAT_FORMULAS_PASS03.md":id.StartsWith("CF")||id is "F10" or "F11" or "F12"?"combat_systems_audit/pass_02/COMBAT_FORMULAS_PASS02.md":"full_code_audit/FORMULAS.md";
        if(id=="CF03")source="combat_systems_audit/COMBAT_FORMULAS.md";
        string grade=id is "F01" or "F02" or "F03" or "F04" or "F05" or "F06" or "F07" or "F08" or "F09" or "F13" or "CF03" or "P3-F02"?"A":"B";
        string limits=id.StartsWith("CF1")&&id is not "CF12" and not "CF13"?"Prepared numeric inputs only; no raycast, team contact authorization or native RNG equivalence.":id.StartsWith("P3-F0")?"Recovered fragment only; no ECS writes, endpoint clipping, interval resolution or full flight model.":"Finite supported domain only; static reconstruction, not runtime validation. No implicit application to frontline or game state.";
        return new(id,grade,source,limits,profile,p,result);
    }
    private sealed class SuppliedRandom(int[] indices):ICombatRandom
    {private int cursor;public int NextIndex(int max)=>cursor<indices.Length?indices[cursor++]:throw new ArgumentException("Supplied RNG sequence exhausted");public double NextUnit()=>throw new NotSupportedException();}
}
