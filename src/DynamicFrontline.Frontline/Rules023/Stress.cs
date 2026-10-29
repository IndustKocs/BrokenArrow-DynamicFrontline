using DynamicFrontline.Core;
namespace DynamicFrontline.Frontline.Rules023;

public static class ClassStressPolicy
{
    public static (double M, double Q) Factors(MoraleClass c) => c switch { MoraleClass.Reserve => (.8, 1.1), MoraleClass.Regular => (1, 1), MoraleClass.Special => (1.2, .9), MoraleClass.NativePassthrough => (1, 1), _ => throw new ArgumentException("Unmapped class") };
    public static double Maximum(Profile023 p) { p.Validate(); return p.BaseMaxStress * Factors(p.Morale).M; }
    public static double Combat(Profile023 p, float damage, float maxHp, float direct, bool enabled = true) => CombatFormulas.StressDamage(damage, maxHp, p.BaseMaxStress, direct) * (enabled ? Factors(p.Morale).Q : 1);
}
public sealed record ServiceInterval(UnitKey Recipient, string EventId, string ServiceId, double Start, double End, double Amount, string Kind, string Provenance);
public sealed class SupplyReliefEvaluator
{
    private readonly Dictionary<string, ServiceInterval> events = new();
    public void Add(ServiceInterval s)
    {
        Check.That(s.End > s.Start && s.Start >= 0 && double.IsFinite(s.End) && s.Amount > 0 && double.IsFinite(s.Amount) && s.Kind is "Ammo" or "Repair" or "Heal" && !string.IsNullOrWhiteSpace(s.Provenance), "Unconfirmed service");
        var id = $"{s.Recipient}/{s.EventId}";
        if (events.TryGetValue(id, out var old)) { Check.That(old == s, "Conflicting duplicate service"); return; }
        events.Add(id, s);
    }
    public (double Start, double End)[] Union(UnitKey key)
    {
        var result = new List<(double Start, double End)>();
        foreach (var s in events.Values.Where(s => s.Recipient == key).OrderBy(s => s.Start).ThenBy(s => s.End))
        {
            if (result.Count == 0 || s.Start > result[^1].End) result.Add((s.Start, s.End));
            else result[^1] = (result[^1].Start, Math.Max(result[^1].End, s.End));
        }
        return result.ToArray();
    }
    public static double GraceEnd((double Start, double End) s) => s.End + Math.Min(20, 2 * (s.End - s.Start));
    public static ReliefState At((double Start, double End)[] union, double time)
    {
        if (union.Any(s => time >= s.Start && time < s.End)) return ReliefState.BeingSupplied;
        return union.Any(s => time >= s.End && time < GraceEnd(s)) ? ReliefState.PostSupplyGrace : ReliefState.NoRelief;
    }
    public ServiceInterval[] Export() => events.Values.OrderBy(e => e.Start).ThenBy(e => e.EventId).ToArray();
}
public sealed record StressEffectIntent(UnitKey Unit, string EventId, double Start, double End, double Delta, bool BlockRecovery, string Source, string RulesVersion);
public sealed record UnitEncirclementMemory(UnitKey Key, int Team, bool Confirmed, double Pending, double Releasing, double Tau, double Time, EncirclementState State, ReliefState Relief);
public sealed class UnitEncirclementStateMachine
{
    public UnitEncirclementMemory Memory { get; private set; }
    private readonly Rules023Config config;
    public UnitEncirclementStateMachine(UnitKey key, int team, Rules023Config config, double start = 0) { this.config = config; Memory = new(key, team, false, 0, 0, 0, start, EncirclementState.NotEncircled, ReliefState.NoRelief); }
    public void Restore(UnitEncirclementMemory memory) { Check.That(memory.Key == Memory.Key, "Foreign state"); Memory = memory; }
    public static double H(double tau, Rules023Config c) { var u = Math.Min(tau, c.Ramp); return c.A * u + (c.B - c.A) * u * u / (2 * c.Ramp) + c.B * Math.Max(0, tau - c.Ramp); }
    public StressEffectIntent[] Advance(Unit023 unit, GeometryResult geometry, double end, SupplyReliefEvaluator supply, bool penaltyKnown = true)
    {
        var m = Memory; Check.That(unit.Key == m.Key && end >= m.Time && double.IsFinite(end), "Stale or foreign state");
        if (unit.Team != m.Team) m = new(unit.Key, unit.Team, false, 0, 0, 0, m.Time, EncirclementState.NotEncircled, ReliefState.NoRelief);
        bool eligible = unit.Alive && !unit.Air && unit.Profile.CanReceiveEncirclementPenalty;
        bool uncertain = geometry.Corridor == CorridorState.Unknown;
        bool closed = geometry.Enclosed == Evidence.True;
        bool opened = geometry.Enclosed == Evidence.False && geometry.Corridor == CorridorState.Open;
        var intervals = supply.Union(unit.Key); var cuts = new SortedSet<double> { m.Time, end };
        foreach (var s in intervals) foreach (var x in new[] { s.Start, s.End, SupplyReliefEvaluator.GraceEnd(s) }) if (x > m.Time && x < end) cuts.Add(x);
        if (!m.Confirmed && closed && !unit.Loaded) cuts.Add(Math.Min(end, m.Time + Math.Max(0, config.Confirmation - m.Pending)));
        if (m.Confirmed && opened && !unit.Loaded) cuts.Add(Math.Min(end, m.Time + Math.Max(0, config.ReleaseConfirmation - m.Releasing)));
        var times = cuts.ToArray(); var effects = new List<StressEffectIntent>();
        for (int i = 1; i < times.Length; i++)
        {
            var start = times[i - 1]; var stop = times[i]; var dt = stop - start; if (dt <= 0) continue;
            var relief = SupplyReliefEvaluator.At(intervals, start + dt / 2); var active = false;
            if (!eligible) m = m with { Confirmed = false, Pending = 0, Releasing = 0, Tau = 0, State = EncirclementState.NotEncircled };
            else if (uncertain) m = m with { Pending = 0, Releasing = 0, State = EncirclementState.DataUncertain };
            else if (unit.Loaded) { /* Suspend history; embarking is not release. */ }
            else if (m.Confirmed)
            {
                if (opened)
                {
                    active = config.Release == ReleasePolicy.KeepPenaltyUntilConfirmed;
                    m = m with { Releasing = m.Releasing + dt, State = EncirclementState.ReleasePending };
                }
                else { m = m with { Releasing = 0, State = EncirclementState.Encircled }; active = true; }
            }
            else if (closed)
            {
                m = m with { Pending = m.Pending + dt, State = EncirclementState.EncirclementPending };
            }
            else m = m with { Pending = 0, State = EncirclementState.NotEncircled };
            active &= relief == ReliefState.NoRelief && penaltyKnown;
            var delta = active ? ClassStressPolicy.Maximum(unit.Profile) * (H(m.Tau + dt, config) - H(m.Tau, config)) : 0;
            if (active) m = m with { Tau = m.Tau + dt };
            effects.Add(new(unit.Key, $"encirclement/{start:R}/{stop:R}", start, stop, delta, active, "Encirclement", config.Version));
            if (m.Confirmed && opened && !unit.Loaded && !uncertain && m.Releasing + 1e-9 >= config.ReleaseConfirmation)
                m = m with { Confirmed = false, Pending = 0, Releasing = 0, Tau = 0, State = EncirclementState.NotEncircled };
            if (!m.Confirmed && closed && !unit.Loaded && !uncertain && eligible && m.Pending + 1e-9 >= config.Confirmation)
                m = m with { Confirmed = true, Pending = 0, State = EncirclementState.Encircled };
            m = m with { Time = stop, Relief = SupplyReliefEvaluator.At(intervals, stop) };
        }
        Memory = m; return effects.ToArray();
    }
}
public sealed record CombatEvent(UnitKey Unit, string EventId, string OriginalImpactId, double Time, float HpDamage, float DirectStress, string Stage = "Raw", string Provenance = "Synthetic");
public sealed record StressMemory(UnitKey Key, string ProfileId, double Maximum, double Stress, float NativeRecoveryTime, double Time, double NextRecovery, string[] Applied);
public sealed class InMemoryStressBackend
{
    private readonly Dictionary<UnitKey, StressMemory> states = new();
    private readonly Dictionary<UnitKey, HashSet<string>> seen = new();
    public void Initialize(Unit023 u, double time) { if (!states.ContainsKey(u.Key)) { var s = Get(u); states[u.Key] = s with { Time = time, NextRecovery = Math.Floor(time) + 1 }; } }
    public StressMemory Get(Unit023 u)
    {
        if (!states.TryGetValue(u.Key, out var s)) { states[u.Key] = s = new(u.Key, u.Profile.Id, ClassStressPolicy.Maximum(u.Profile), u.Stress, 0, 0, 1, []); seen[u.Key] = new(); }
        Check.That(s.ProfileId == u.Profile.Id && s.Maximum == ClassStressPolicy.Maximum(u.Profile), "Profile migration not supported"); return s;
    }
    public bool Combat(Unit023 u, CombatEvent e, bool policy = true)
    {
        var s = Get(u); Check.That(e.Unit == u.Key && e.Stage == "Raw" && !string.IsNullOrWhiteSpace(e.Provenance) && e.Time >= s.Time, "Unproven/already composed combat event");
        var id = "impact/" + e.OriginalImpactId; if (seen[u.Key].Contains(id)) return false;
        var delta = ClassStressPolicy.Combat(u.Profile, e.HpDamage, u.MaxHp, e.DirectStress, policy);
        seen[u.Key].Add(id); states[u.Key] = s with { Stress = Math.Min(s.Maximum, s.Stress + delta), NativeRecoveryTime = 0 }; return true;
    }
    // F04 is a separate unscaled source; raw unknown sources are rejected by the adapter.
    public bool NativeTarget(Unit023 u, string eventId, float target, string provenance)
    {
        Check.That(!string.IsNullOrWhiteSpace(eventId) && !string.IsNullOrWhiteSpace(provenance), "Native target provenance required");
        var s = Get(u); var id = "target/" + eventId; if (seen[u.Key].Contains(id)) return false;
        var delta = CombatFormulas.StressToTarget((float)s.Stress, (float)s.Maximum, target);
        seen[u.Key].Add(id); states[u.Key] = s with { Stress = s.Stress + delta, NativeRecoveryTime = delta > 0 ? 0 : s.NativeRecoveryTime }; return true;
    }
    public bool Apply(Unit023 u, StressEffectIntent intent, float recoveryMultiplier = 1)
    {
        var s = Get(u);
        Check.That(intent.Source == "Encirclement" && intent.Unit == u.Key && intent.RulesVersion == "Zoc_023/1.2", "Foreign intent");
        if (intent.End <= s.Time && intent.Start < intent.End) return false; // monotonic watermark: no per-tick event history
        Check.That(Math.Abs(intent.Start - s.Time) < 1e-8 && intent.End >= intent.Start, "Nonconsecutive effect interval"); Check.Nonnegative(intent.Delta);
        double stress = Math.Min(s.Maximum, s.Stress + intent.Delta); float timer = intent.Delta > 0 ? 0 : s.NativeRecoveryTime; double next = s.NextRecovery;
        while (next <= intent.End + 1e-9)
        {
            if (!intent.BlockRecovery) { var r = CombatFormulas.RecoverStressTick((float)stress, timer, recoveryMultiplier); stress = r.Stress; timer = r.TimeAfterLastDamage; }
            next += 1; // skipped ticks never become a catch-up recovery burst
        }
        states[u.Key] = s with { Stress = stress, NativeRecoveryTime = timer, Time = intent.End, NextRecovery = next }; return true;
    }
    public StressLevel Level(Unit023 u) { var s = Get(u); return CombatFormulas.Level((float)s.Stress, CombatFormulas.Thresholds((float)s.Maximum, u.Profile.ShockRatio, u.Profile.PanicRatio)); }
    public StressMemory[] Current() => states.Values.OrderBy(s => s.Key.Uid).ToArray();
    public StressMemory[] Export() => states.Values.OrderBy(s => s.Key.Uid).Select(s => s with { Applied = seen[s.Key].OrderBy(x => x).ToArray() }).ToArray();
    public void Restore(IEnumerable<StressMemory> memory) { states.Clear(); seen.Clear(); foreach (var s in memory) { states.Add(s.Key, s with { Applied = [] }); seen[s.Key] = s.Applied.ToHashSet(); } }
}
