using DynamicFrontline.Core;
namespace DynamicFrontline.Frontline.Rules023;

public static class CombinedArmsRadiusResolver
{
    public static ResolvedZoc[] Resolve(IReadOnlyList<Unit023> input, PreparedTerrain terrain, Rules023Config config)
    {
        var units = input.OrderBy(u => u.Key.Uid).ThenBy(u => u.Key.Generation).ToArray();
        var hash = new SpatialIndex(config.PartnerDistance);
        for (int i = 0; i < units.Length; i++) if (units[i].Active && units[i].Profile.CanProjectZoc) hash.Add(units[i].Position, i);
        var result = new ResolvedZoc[units.Length];
        for (int i = 0; i < units.Length; i++)
        {
            var u = units[i]; bool boosted = false, complete = true;
            if (u.Active && u.Profile.CanProjectZoc)
            foreach (var j in hash.Near(u.Position))
            {
                var v = units[j]; if (i == j || u.Team != v.Team || u.Position.DistanceSquared(v.Position) > config.PartnerDistance * config.PartnerDistance) continue;
                if (!(u.Profile.IsInfantry && v.Profile.IsApcOrIfv || v.Profile.IsInfantry && u.Profile.IsApcOrIfv)) continue;
                var evidence = terrain.Local(u.Position, v.Position, Mobility.Infantry);
                if (evidence == Evidence.Unknown) complete = false;
                if (evidence == Evidence.True) boosted = true;
            }
            result[i] = new(u, boosted ? config.CombinedRadius : config.BaseRadius, ZocStrengthCalculator.Power(u), complete);
        }
        return result;
    }
}
public static class ZocStrengthCalculator
{
    public static double Power(Unit023 u) => !u.Active || !u.Profile.CanProjectZoc ? 0 :
        (u.Profile.IsInfantry ? u.Soldiers * u.Profile.Strength : u.Profile.Strength * (.5 + .5 * u.Hp / u.MaxHp)) *
        (u.Level switch { StressLevel.Calm => 1, StressLevel.Shocked => .6, StressLevel.Panicked => .15, _ => 0 });
    public static double Falloff(double distance, double radius) => distance <= radius / 2 ? 1 : distance >= radius ? 0 : 2 * (1 - distance / radius);
}
public sealed class ZocInfluenceField
{
    private sealed record Footprint(ResolvedZoc Source, long TerrainRevision, (int Cell, double Value)[] Values);
    private readonly TerritoryGrid grid;
    private readonly PreparedTerrain terrain;
    private readonly Dictionary<UnitKey, Footprint> previous = new();
    private readonly HashSet<UnitKey> keys = new();
    private readonly List<UnitKey> removed = new();
    public double[][] Values { get; }
    public bool Complete { get; private set; }
    public long Revision { get; private set; }
    public int ChangedSources { get; private set; }
    public ZocInfluenceField(PreparedTerrain terrain) { this.terrain = terrain; grid = terrain.Grid; Values = [new double[grid.Count], new double[grid.Count]]; }
    public void Update(ResolvedZoc[] units, bool rosterComplete = true)
    {
        Complete = rosterComplete && terrain.Complete && units.All(s => s.Complete);
        if (!Complete) return; // incomplete snapshots cannot remove a hidden part of a ring
        ChangedSources = 0;
        keys.Clear(); removed.Clear(); foreach (var u in units) keys.Add(u.Unit.Key);
        foreach (var key in previous.Keys) if (!keys.Contains(key)) removed.Add(key);
        foreach (var key in removed) { Apply(previous[key], -1); previous.Remove(key); ChangedSources++; }
        foreach (var u in units)
        {
            previous.TryGetValue(u.Unit.Key, out var old);
            if (old != null && old.Source.Unit.Profile.Mobility == u.Unit.Profile.Mobility && old.Source.Unit.Team == u.Unit.Team && old.Source.Unit.Position == u.Unit.Position && old.Source.Radius == u.Radius && old.Source.Power == u.Power && old.TerrainRevision == terrain.Revision) continue;
            if (old != null) Apply(old, -1);
            var values = new List<(int, double)>();
            if (u.Power > 0)
            foreach (var i in grid.Within(u.Unit.Position, u.Radius))
            {
                var point = grid.Center(i);
                if (terrain.Local(u.Unit.Position, point, u.Unit.Profile.Mobility) != Evidence.True) continue;
                values.Add((i, u.Power * ZocStrengthCalculator.Falloff(Math.Sqrt(point.DistanceSquared(u.Unit.Position)), u.Radius)));
            }
            var next = new Footprint(u, terrain.Revision, values.ToArray()); previous[u.Unit.Key] = next; Apply(next, 1); ChangedSources++;
        }
        if (ChangedSources > 0) Revision++;
    }
    private void Apply(Footprint f, int sign) { var a = Values[f.Source.Unit.Team]; foreach (var (i, value) in f.Values) { a[i] += sign * value; if (Math.Abs(a[i]) < 1e-10) a[i] = 0; } }
    public int Control(int i, Rules023Config c)
    {
        var a = Values[0][i]; var b = Values[1][i];
        return a >= c.MinimumStrength && a >= c.Dominance * b ? 0 : b >= c.MinimumStrength && b >= c.Dominance * a ? 1 : a >= c.MinimumStrength || b >= c.MinimumStrength ? -1 : -2;
    }
    public static double[][] Reference(PreparedTerrain terrain, ResolvedZoc[] units)
    {
        var g = terrain.Grid; var r = new[] { new double[g.Count], new double[g.Count] };
        foreach (var u in units.OrderBy(u => u.Unit.Key.Uid).ThenBy(u => u.Unit.Key.Generation))
        for (int i = 0; i < g.Count; i++) if (g.Available(i) && u.Power > 0 && terrain.Local(u.Unit.Position, g.Center(i), u.Unit.Profile.Mobility) == Evidence.True)
            r[u.Unit.Team][i] += u.Power * ZocStrengthCalculator.Falloff(Math.Sqrt(g.Center(i).DistanceSquared(u.Unit.Position)), u.Radius);
        return r;
    }
}
