using DynamicFrontline.Core;
namespace DynamicFrontline.Frontline.Rules023;

public sealed record VictoryConfig
{
    public string Version { get; init; } = "Zoc_023/1.2";
    public double? TerritoryScale { get; init; } = 1;
    public double DepthThreshold { get; init; } = .75;
    public double RequiredWidth { get; init; } = 200;
    public string? FinalPolicy { get; init; } = null;
    public string AirPolicy { get; init; } = "StableAliveRosterExperimental";
    public string ValuationPolicy { get; init; } = "NominalActiveValueExperimental";
    public string ConflictPolicy { get; init; } = "NeutralConflictsExperimental";
    public string SimultaneousPolicy { get; init; } = "OutcomePendingPolicy";
    public string Provenance { get; init; } = "Synthetic / Experimental balance";
    public void Validate()
    {
        Check.That(Version == "Zoc_023/1.2" && DepthThreshold is .75 or .80 && RequiredWidth > 0 && double.IsFinite(RequiredWidth), "Invalid victory configuration");
        if (TerritoryScale != null) Check.Positive(TerritoryScale.Value);
        Check.That(FinalPolicy is null or "SyntheticSum" && ConflictPolicy == "NeutralConflictsExperimental" && SimultaneousPolicy == "OutcomePendingPolicy" && ValuationPolicy == "NominalActiveValueExperimental" && AirPolicy is "StableAliveRosterExperimental" or "GroundOnlyExperimental", "Unsupported policy");
    }
}
public sealed class TerritoryScoringProjection
{
    private sealed record LocalFootprint(int Team, Point2 Center, double Radius, int[] Cells);
    private readonly TerritoryGrid grid; private readonly bool[] mask; private readonly int[][] localCounts;
    private readonly Dictionary<UnitKey, LocalFootprint> local = new(); private long ownership = -1;
    private readonly HashSet<int> dirty = new();
    public int[] EffectiveOwner { get; }
    public double[] Area { get; } = new double[2];
    public double TotalArea { get; }
    public long Revision { get; private set; }
    public TerritoryScoringProjection(TerritoryGrid grid, bool[] scoringMask)
    {
        this.grid = grid; Check.That(scoringMask.Length == grid.Count, "Scoring mask size"); mask = scoringMask.ToArray();
        Check.That(Enumerable.Range(0, grid.Count).All(i => !mask[i] || grid.Available(i)), "Score mask includes unavailable cell");
        TotalArea = mask.Count(v => v) * grid.CellSize * grid.CellSize; Check.Positive(TotalArea);
        localCounts = [new int[grid.Count], new int[grid.Count]]; EffectiveOwner = Enumerable.Repeat(-1, grid.Count).ToArray();
    }
    public void Update(LocalControlArea[] areas)
    {
        dirty.Clear(); if (ownership != grid.OwnershipRevision) { for (int i = 0; i < grid.Count; i++) dirty.Add(i); ownership = grid.OwnershipRevision; }
        var seen = new HashSet<UnitKey>();
        void Apply(LocalFootprint f, int sign) { foreach (var i in f.Cells) { localCounts[f.Team][i] += sign; dirty.Add(i); } }
        foreach (var a in areas)
        {
            Check.That(seen.Add(a.Unit) && a.Team is 0 or 1, "Duplicate local area");
            local.TryGetValue(a.Unit, out var old); if (old != null && old.Center == a.Center && old.Radius == a.Radius && old.Team == a.Team) continue;
            if (old != null) Apply(old, -1);
            var next = new LocalFootprint(a.Team, a.Center, a.Radius, grid.Within(a.Center, a.Radius).ToArray()); local[a.Unit] = next; Apply(next, 1);
        }
        foreach (var key in local.Keys.Where(k => !seen.Contains(k)).ToArray()) { Apply(local[key], -1); local.Remove(key); }
        bool changed = false; double area = grid.CellSize * grid.CellSize;
        foreach (var i in dirty)
        {
            bool a = grid.Owner(i) == 0 || localCounts[0][i] > 0, b = grid.Owner(i) == 1 || localCounts[1][i] > 0;
            int next = !mask[i] || a == b ? -1 : a ? 0 : 1;
            int old = EffectiveOwner[i]; if (old == next) continue; if (old >= 0) Area[old] -= area; if (next >= 0) Area[next] += area; EffectiveOwner[i] = next; changed = true;
        }
        if (changed) Revision++;
    }
    public static double[] Reference(TerritoryGrid grid, bool[] mask, LocalControlArea[] areas)
    {
        var result = new double[2];
        for (int i = 0; i < grid.Count; i++) if (mask[i])
        {
            bool a = grid.Owner(i) == 0, b = grid.Owner(i) == 1;
            foreach (var local in areas) if (grid.Center(i).DistanceSquared(local.Center) <= local.Radius * local.Radius) { if (local.Team == 0) a = true; else b = true; }
            if (a != b) result[a ? 0 : 1] += grid.CellSize * grid.CellSize;
        }
        return result;
    }
}
public sealed class BreakthroughEvaluator(PreparedTerrain terrain, VictoryConfig config)
{
    private long revision = -1, terrainRevision = -1; private Evidence[] cached = [Evidence.Unknown, Evidence.Unknown];
    public Evidence[] Evaluate(TerritoryScoringProjection score, bool complete)
    {
        if (!complete || !terrain.Complete) return [Evidence.Unknown, Evidence.Unknown];
        if (revision == score.Revision && terrainRevision == terrain.Revision) return cached;
        var g = terrain.Grid; var result = new Evidence[2];
        for (int team = 0; team < 2; team++)
        {
            // Synthetic depth axis is X; width is the contiguous transverse Y section.
            var wide = new bool[g.Count];
            for (int x = 0; x < g.Width; x++)
            {
                int y = 0; while (y < g.Height) { int begin = y; while (y < g.Height && score.EffectiveOwner[y * g.Width + x] == team) y++;
                    if ((y - begin) * g.CellSize + 1e-9 >= config.RequiredWidth) for (int row = begin; row < y; row++) wide[row * g.Width + x] = true;
                    if (y == begin) y++;
                }
            }
            var visited = new bool[g.Count]; var queue = new Queue<int>();
            void Visit(int i) { if (i >= 0 && wide[i] && !visited[i]) { visited[i] = true; queue.Enqueue(i); } }
            foreach (var root in terrain.Roots.Where(r => r.Team == team)) Visit(g.Index(root.Position));
            double depth = 0;
            while (queue.TryDequeue(out var i)) { depth = Math.Max(depth, team == 0 ? (i % g.Width + 1.0) / g.Width : (g.Width - i % g.Width) / (double)g.Width); foreach (var j in g.Neighbors(i)) Visit(j); }
            result[team] = depth + 1e-10 >= config.DepthThreshold ? Evidence.True : Evidence.False;
        }
        revision = score.Revision; terrainRevision = terrain.Revision; return cached = result;
    }
}
public sealed record ArmyValue(decimal[] Values, bool Complete, string Policy, string Provenance);
public static class ActiveArmyValueProvider
{
    public static ArmyValue Evaluate(IEnumerable<Unit023> units, bool complete, VictoryConfig config)
    {
        var values = new decimal[2]; var seen = new HashSet<UnitKey>();
        foreach (var u in units.OrderBy(u => u.Key.Uid).ThenBy(u => u.Key.Generation))
        {
            Check.That(seen.Add(u.Key) && u.NominalValue >= 0, "Invalid army roster");
            if (!u.Alive || u.Air && config.AirPolicy == "GroundOnlyExperimental") continue;
            values[u.Team] = checked(values[u.Team] + u.NominalValue); // roster cost explicit per entity; HP/panic/FOW/on-map never valuation inputs
        }
        return new(values, complete, config.ValuationPolicy + "/" + config.AirPolicy, "Synthetic complete roster; costs must exclude embedded cargo duplication");
    }
}
public sealed record NativeScoreEvent(string Id, int Team, double Time, decimal Value, long Revision, string Mode, string Provenance);
public sealed record MatchEndIntent(Guid Session, string RulesVersion, double EffectiveTime, int? Winner, string[] Reasons, string Status);
public sealed record VictoryMemory(Guid Session, string Version, double Time, long Revision, double[][] U, decimal[] Native,
    double[] BreakthroughHeld, double[] CollapseHeld, string NativeMode, long[] NativeRevision, string[] NativeEvents,
    bool LedgerComplete, MatchEndIntent? End);
public sealed record VictoryInterval(Guid Session, string Version, long Revision, double Start, double End, double[] Fractions,
    bool AreaComplete, ArmyValue Army, Evidence[] Breakthrough, NativeScoreEvent[] NativeEvents, bool Paused = false, bool NativeComplete = true);
public sealed class VictorySimulation
{
    private readonly VictoryConfig config;
    public VictoryMemory State { get; private set; }
    public VictorySimulation(Guid session, VictoryConfig config, string nativeMode = "Delta")
    {
        config.Validate(); Check.That(nativeMode is "Delta" or "Absolute", "Unknown native feed mode"); this.config = config;
        State = new(session, config.Version, 0, -1, [new double[3], new double[3]], new decimal[2], new double[2], new double[2], nativeMode, [-1, -1], [], true, null);
    }
    public double Weighted(int team) => State.U[team][0] + 2 * State.U[team][1] + 3 * State.U[team][2];
    public VictoryMemory Export() => State with { U = State.U.Select(a => a.ToArray()).ToArray(), Native = State.Native.ToArray(), BreakthroughHeld = State.BreakthroughHeld.ToArray(), CollapseHeld = State.CollapseHeld.ToArray(), NativeRevision = State.NativeRevision.ToArray(), NativeEvents = State.NativeEvents.ToArray() };
    public void Restore(VictoryMemory memory) { Check.That(memory.Session == State.Session && memory.Version == config.Version && memory.NativeMode == State.NativeMode, "Replay incompatible"); State = memory; }
    public MatchEndIntent? Advance(VictoryInterval input)
    {
        Check.That(input.Session == State.Session && input.Version == config.Version, "Foreign victory context");
        if (State.End != null || input.Revision == State.Revision) return State.End;
        Check.That(input.Revision == State.Revision + 1 && Math.Abs(input.Start - State.Time) < 1e-8 && input.End >= input.Start && double.IsFinite(input.End), "Missing/stale victory interval");
        Check.That(input.Fractions.Length == 2 && input.Breakthrough.Length == 2 && input.Fractions.All(x => double.IsFinite(x) && x >= 0 && x <= 1) && input.Fractions.Sum() <= 1 + 1e-9, "Invalid scoring fractions");
        Check.That(input.Army.Values.Length == 2 && input.Army.Values.All(v => v >= 0), "Invalid army values");
        foreach (var e in input.NativeEvents) Check.That(e.Mode == State.NativeMode && e.Team is 0 or 1 && e.Value >= 0 && double.IsFinite(e.Time) && (e.Time >= input.Start || State.NativeEvents.Contains(e.Id)) && e.Time <= input.End && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Provenance), "Unconfirmed native score interval/source");
        if (input.Paused) { State = State with { Revision = input.Revision }; return null; }
        State = State with { Revision = input.Revision, LedgerComplete = State.LedgerComplete && input.AreaComplete };
        var events = input.NativeEvents.OrderBy(e => e.Time).ThenBy(e => e.Id).ToArray();
        var t = input.Start; var end = Math.Min(input.End, 2700);
        while (t < end - 1e-10)
        {
            Ingest(events.Where(e => e.Time <= t));
            int phase = Math.Min(2, (int)(t / 900)); double weight = phase + 1;
            double boundary = Math.Min(end, (phase + 1) * 900);
            var future = events.Where(e => e.Time > t && e.Time < boundary).Select(e => e.Time).ToArray(); if (future.Length > 0) boundary = future.Min();
            var diff = Weighted(0) - Weighted(1); var slope = input.AreaComplete ? weight * (input.Fractions[0] - input.Fractions[1]) : 0;
            if (diff * slope < 0) { var crossing = t - diff / slope; if (crossing > t + 1e-9 && crossing < boundary) boundary = crossing; }
            var breakthrough = new bool[2]; var collapse = new bool[2];
            for (int team = 0; team < 2; team++)
            {
                breakthrough[team] = input.AreaComplete && input.Breakthrough[team] == Evidence.True;
                var sign = team == 0 ? 1 : -1;
                var leading = sign * diff > 1e-10 || Math.Abs(diff) <= 1e-10 && sign * slope > 0;
                var weaker = input.Army.Values[1 - team];
                collapse[team] = State.LedgerComplete && input.Army.Complete && input.Army.Values[team] >= 2000 && weaker <= decimal.MaxValue / 4 && input.Army.Values[team] >= 4 * weaker && leading;
                if (!breakthrough[team]) State.BreakthroughHeld[team] = 0;
                if (!collapse[team]) State.CollapseHeld[team] = 0;
                if (breakthrough[team]) boundary = Math.Min(boundary, t + Math.Max(0, 120 - State.BreakthroughHeld[team]));
                if (collapse[team]) boundary = Math.Min(boundary, t + Math.Max(0, 420 - State.CollapseHeld[team]));
            }
            var dt = boundary - t;
            for (int team = 0; team < 2; team++)
            {
                if (input.AreaComplete) State.U[team][phase] += input.Fractions[team] * dt;
                if (breakthrough[team]) State.BreakthroughHeld[team] += dt;
                if (collapse[team]) State.CollapseHeld[team] += dt;
            }
            t = boundary; State = State with { Time = t }; Ingest(events.Where(e => e.Time <= t));
            var outcomes = new List<(int Team, string Reason)>();
            for (int team = 0; team < 2; team++)
            { if (breakthrough[team] && State.BreakthroughHeld[team] >= 120 - 1e-8) outcomes.Add((team, "Breakthrough")); if (collapse[team] && State.CollapseHeld[team] >= 420 - 1e-8) outcomes.Add((team, "ArmyCollapse")); }
            if (outcomes.Count > 0)
            {
                var winners = outcomes.Select(o => o.Team).Distinct().ToArray();
                State = State with { End = new(State.Session, config.Version, t, winners.Length == 1 && t < 2700 ? winners[0] : null, outcomes.Select(o => o.Reason).Distinct().ToArray(), winners.Length == 1 && t < 2700 ? "SyntheticEnded" : "OutcomePendingPolicy") }; return State.End;
            }
        }
        if (State.Time >= 2700)
        {
            int? winner = null; string status = "TimeLimitReached/OutcomePendingPolicy";
            if (config.FinalPolicy == "SyntheticSum" && config.TerritoryScale != null && State.LedgerComplete && input.NativeComplete)
            { double a = config.TerritoryScale.Value * Weighted(0) + (double)State.Native[0], b = config.TerritoryScale.Value * Weighted(1) + (double)State.Native[1]; if (a != b) { winner = a > b ? 0 : 1; status = "SyntheticEnded"; } }
            State = State with { End = new(State.Session, config.Version, 2700, winner, ["TimeLimit"], status) };
        }
        return State.End;
    }
    private void Ingest(IEnumerable<NativeScoreEvent> events)
    {
        var seen = State.NativeEvents.ToHashSet();
        foreach (var e in events)
        {
            Check.That(e.Mode == State.NativeMode && e.Team is 0 or 1 && e.Value >= 0 && !string.IsNullOrWhiteSpace(e.Provenance), "Unconfirmed native score source");
            if (!seen.Add(e.Id)) continue;
            if (e.Mode == "Absolute") { if (e.Revision <= State.NativeRevision[e.Team]) continue; State.Native[e.Team] = e.Value; State.NativeRevision[e.Team] = e.Revision; }
            else State.Native[e.Team] = checked(State.Native[e.Team] + e.Value);
        }
        State = State with { NativeEvents = seen.OrderBy(x => x).ToArray() };
    }
}
