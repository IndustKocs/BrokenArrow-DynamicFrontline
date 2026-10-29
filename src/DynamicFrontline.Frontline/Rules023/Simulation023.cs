using DynamicFrontline.Core;
namespace DynamicFrontline.Frontline.Rules023;

public sealed record Input023(Guid Session, string RulesVersion, string ConfigHash, long Revision, double Start, double End,
    Unit023[] Units, bool RosterComplete, ServiceInterval[] Services, CombatEvent[] Combat, NativeScoreEvent[] NativeScore, bool SupplyComplete = true, bool VictoryComplete = true);
public sealed record OwnEncirclement(UnitKey Key, EncirclementState State, ReliefState Relief, double Tau, double Stress, double Maximum, bool Loaded);
public sealed record OwnNotice(int Team, string Kind, UnitKey[] Units);
public sealed record VisibleZoc(UnitKey Key, int Team, Point2 Position, double Radius, bool IntelligenceEstimate);
public sealed record PublicVictory(double[]? TerritoryPoints, decimal[]? DestructionPoints, MatchEndIntent? End);
public sealed record Public023(Guid Session, string Version, double Time, OwnEncirclement[] Own, OwnNotice[] Notices, VisibleZoc[] Circles, PublicVictory Victory);
public sealed record Frame023(Guid Session, string Version, string ConfigHash, long Revision, double Time, ResolvedZoc[] Zoc,
    UnitEncirclementMemory[] Encirclement, StressMemory[] Stress, GeometryResult[] Geometry, LocalControlArea[] LocalAreas,
    double[] ScoreFractions, VictoryMemory Victory, OwnNotice[] Notices, StressEffectIntent[]? Predictions = null);
public sealed record Checkpoint023(Guid Session, string Version, string ConfigHash, string VictoryPolicy, long Revision, double Time,
    TerritoryStateSnapshot Territory, UnitEncirclementMemory[] Encirclement, StressMemory[] Stress, ServiceInterval[] Services, VictoryMemory Victory, SimulationLane Lane = SimulationLane.Autonomous);
public interface IGameRadialRulerBinding { void PreviewOwnRadius(UnitKey unit, double radius); } // Runtime/Binding Required; no implementation calls game APIs.

public sealed class Simulation023
{
    private readonly SimulationLane lane;
    private readonly Guid session; private readonly Rules023Config rules; private readonly PreparedTerrain terrain;
    private readonly TerritorialConnectivity territory; private readonly TerritoryCaptureSystem capture; private readonly LocalControlAreaSystem locals;
    private readonly EncirclementRegionDetector detector; private readonly BreakthroughEvaluator breakthrough;
    private readonly Dictionary<UnitKey, UnitEncirclementStateMachine> states = new();
    private readonly Dictionary<long, int> generations = new();
    public ZocInfluenceField Field { get; }
    public TerritoryScoringProjection Score { get; }
    public InMemoryStressBackend Stress { get; } = new();
    public SupplyReliefEvaluator Supply { get; } = new();
    public VictorySimulation Victory { get; }
    private readonly VictoryConfig victoryConfig; private long revision = -1; private double time;
    public Frame023? Last { get; private set; }
    public RulesMode Mode => RulesMode.Zoc_023;
    public Simulation023(Guid session, Rules023Config rules, VictoryConfig victoryConfig, PreparedTerrain terrain, ProfileCatalog profiles, Rules captureRules, bool[] scoringMask, SimulationLane lane = SimulationLane.Autonomous)
    {
        Check.That(session != Guid.Empty && captureRules.TickMilliseconds == 250, "Synthetic session/capture cadence 250 ms required");
        this.lane = lane; rules.Validate(); victoryConfig.Validate(); this.session = session; this.rules = rules; this.terrain = terrain; this.victoryConfig = victoryConfig;
        territory = new(terrain.Grid, terrain.Roots); capture = new(terrain.Grid, profiles, captureRules); locals = new(profiles, captureRules);
        Field = new(terrain); detector = new(terrain, rules); Score = new(terrain.Grid, scoringMask); breakthrough = new(terrain, victoryConfig); Victory = new(session, victoryConfig);
    }
    public Frame023 Step(Input023 input)
    {
        Check.That(input.Session == session && input.RulesVersion == rules.Version && input.ConfigHash == rules.Hash, "Incompatible input");
        if (input.Revision == revision && Last != null) return Last;
        Check.That(input.Revision == revision + 1 && input.Start == time && Math.Abs(input.End - input.Start - .25) < 1e-8, "Missing/stale tick or unsupported cadence");
        var keys = new HashSet<UnitKey>();
        foreach (var u in input.Units) { u.Validate(session); Check.That(keys.Add(u.Key) && (!generations.TryGetValue(u.Key.Uid, out var generation) || u.Key.Generation >= generation), "Duplicate/stale generation"); }
        if (lane == SimulationLane.Autonomous && Victory.State.End != null && Last != null) return Last;
        Check.That(input.Combat.All(c => keys.Contains(c.Unit) && c.Time == input.Start && c.Stage == "Raw" && !string.IsNullOrWhiteSpace(c.Provenance)), "Unproven combat input");
        foreach (var s in input.Services) { Check.That(keys.Contains(s.Recipient), "Unknown service recipient"); Supply.Add(s); }
        foreach (var u in input.Units) generations[u.Key.Uid] = u.Key.Generation;
        var units = input.Units.OrderBy(u => u.Key.Uid).ThenBy(u => u.Key.Generation).Select(u => { if(lane == SimulationLane.Observe) return u; Stress.Initialize(u, input.Start); return u with { Stress = (float)Stress.Get(u).Stress, Level = Stress.Level(u) }; }).ToArray();
        var projected = units.Select(u => u.TerritoryUnit).ToArray(); territory.Refresh();
        var local = input.RosterComplete ? locals.Evaluate(units.Where(u => u.Profile.CanCreateLocalArea).Select(u => u.TerritoryUnit).ToArray(), terrain.Grid, territory) : Last?.LocalAreas ?? []; if (input.RosterComplete) Score.Update(local);
        var zoc = CombinedArmsRadiusResolver.Resolve(units, terrain, rules); Field.Update(zoc, input.RosterComplete);
        var geometry = units.Select(u => detector.Evaluate(u, zoc, Field)).ToArray();
        var fractions = Score.Area.Select(x => x / Score.TotalArea).ToArray();
        var army = ActiveArmyValueProvider.Evaluate(units, input.RosterComplete && input.VictoryComplete, victoryConfig);
        Victory.Advance(new(session, rules.Version, input.Revision, input.Start, input.End, fractions, input.RosterComplete && input.VictoryComplete, army,
            breakthrough.Evaluate(Score, input.RosterComplete && input.VictoryComplete), input.NativeScore));
        var end = lane == SimulationLane.Observe ? input.End : Victory.State.End?.EffectiveTime ?? input.End;
        var predictions = new List<StressEffectIntent>();
        var notices = new List<(int Team, string Kind, UnitKey Key)>();
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i]; if (!states.TryGetValue(u.Key, out var machine)) states[u.Key] = machine = new(u.Key, u.Team, rules, input.Start);
            var before = machine.Memory;
            var effects = machine.Advance(u, geometry[i], end, Supply, lane == SimulationLane.Autonomous || input.SupplyComplete);
            if(lane == SimulationLane.Observe && input.SupplyComplete) predictions.AddRange(effects);
            var combat = input.Combat.Where(c => c.Unit == u.Key).ToArray();
            Check.That(combat.All(c => c.Time == input.Start), "Fixture combat events must be at tick start; split replay at events");
            if(lane == SimulationLane.Autonomous) { foreach (var e in combat) Stress.Combat(u, e); foreach (var effect in effects) Stress.Apply(u, effect); }
            if (machine.Memory.State != before.State || machine.Memory.Relief != before.Relief)
                notices.Add((u.Team, machine.Memory.State + "/" + machine.Memory.Relief, u.Key));
        }
        if (input.RosterComplete && end == input.End) { capture.UpdateInfluence(projected, territory); capture.Advance(); }
        time = end; revision = input.Revision;
        return Last = new(session, rules.Version, rules.Hash, revision, end, zoc, units.Select(u => states[u.Key].Memory).ToArray(), lane == SimulationLane.Autonomous ? Stress.Current() : units.Select(u=>new StressMemory(u.Key,u.Profile.Id,u.ObservedMaxStress ?? u.Profile.BaseMaxStress,u.Stress,0,end,end+1,[])).ToArray(), geometry, local, fractions, Victory.Export(),
            notices.GroupBy(n => (n.Team, n.Kind)).Select(g => new OwnNotice(g.Key.Team, g.Key.Kind, g.Select(n => n.Key).ToArray())).ToArray(), predictions.ToArray());
    }
    public void ObservationGap(double resumeAt)
    {
        Check.That(lane == SimulationLane.Observe && resumeAt >= time && double.IsFinite(resumeAt), "Invalid observation gap");
        foreach(var z in Last?.Zoc ?? []) states[z.Unit.Key].Advance(z.Unit,new(CorridorState.Unknown,Evidence.Unknown,null),resumeAt,Supply);
        if(Victory.State.End == null && resumeAt > Victory.State.Time) Victory.Advance(new(session,rules.Version,Victory.State.Revision+1,Victory.State.Time,resumeAt,[0,0],false,new([0,0],false,"Unknown","DataGap"),[Evidence.Unknown,Evidence.Unknown],[]));
        // Keep host/core revision aligned; gaps consume one canonical control revision.
        revision++; time=resumeAt;
    }
    public Checkpoint023 Checkpoint() => new(session, rules.Version, rules.Hash, System.Text.Json.JsonSerializer.Serialize(victoryConfig), revision, time,
        terrain.Grid.Snapshot(new(session, rules.Version, revision, revision)), states.Values.Select(s => s.Memory).ToArray(), Stress.Export(), Supply.Export(), Victory.Export(), lane);
    public void Restore(Checkpoint023 c)
    {
        Check.That(c.Lane == lane && revision == -1 && c.Session == session && c.Version == rules.Version && c.ConfigHash == rules.Hash && c.VictoryPolicy == System.Text.Json.JsonSerializer.Serialize(victoryConfig) && c.Territory.Cells.Length == terrain.Grid.Count && c.Territory.Width == terrain.Grid.Width && c.Territory.Height == terrain.Grid.Height && c.Territory.CellSize == terrain.Grid.CellSize && c.Territory.Origin == terrain.Grid.Origin, "Checkpoint incompatible");
        foreach (var cell in c.Territory.Cells) { Check.That(cell.Available == terrain.Grid.Available(cell.Index), "Map mask changed"); terrain.Grid.Set(cell); }
        foreach (var m in c.Encirclement) { var machine = new UnitEncirclementStateMachine(m.Key, m.Team, rules); machine.Restore(m); states.Add(m.Key, machine); generations[m.Key.Uid] = Math.Max(generations.GetValueOrDefault(m.Key.Uid), m.Key.Generation); }
        Stress.Restore(c.Stress); foreach (var s in c.Services) Supply.Add(s); Victory.Restore(c.Victory); revision = c.Revision; time = c.Time;
    }
    public Public023 Project(int team, bool showCircles = false, bool exposeAggregateScore = false)
    {
        Check.That(team is 0 or 1 && Last != null, "No frame"); var f = Last!;
        var own = f.Zoc.Where(z => z.Unit.Team == team).Select(z => { var m = states[z.Unit.Key].Memory; var s = lane == SimulationLane.Observe ? f.Stress.Single(x=>x.Key==z.Unit.Key) : Stress.Get(z.Unit); return new OwnEncirclement(z.Unit.Key, m.State, m.Relief, m.Tau, s.Stress, s.Maximum, z.Unit.Loaded); }).ToArray();
        VisibleZoc[] circles = [];
        if (showCircles)
        {
            var visible = f.Zoc.Where(z => z.Unit.Team == team || z.Unit.Visible).Select(z => z.Unit).ToArray();
            circles = CombinedArmsRadiusResolver.Resolve(visible, terrain, rules).Where(z => z.Power > 0).Select(z => new VisibleZoc(z.Unit.Key, z.Unit.Team, z.Unit.Position, z.Radius, z.Unit.Team != team)).ToArray();
        }
        return new(session, rules.Version, time, own, f.Notices.Where(n => n.Team == team).ToArray(), circles,
            new(exposeAggregateScore && victoryConfig.TerritoryScale != null ? Enumerable.Range(0, 2).Select(t => Victory.Weighted(t) * victoryConfig.TerritoryScale.Value).ToArray() : null,
                exposeAggregateScore ? Victory.State.Native.ToArray() : null, Victory.State.End));
    }
}
