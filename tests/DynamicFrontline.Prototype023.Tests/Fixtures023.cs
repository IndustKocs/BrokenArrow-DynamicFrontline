using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
public static class Fixtures023
{
    public static readonly Guid Session = Guid.Parse("62b88734-f7c0-4000-8000-000000000023");
    public static Profile023 Infantry = new("line", true, true, true, true, false, Mobility.Infantry, 1, MoraleClass.Regular, true, 1000, .5f, .8f, "Synthetic S0; F02 ratios fixture only");
    public static Profile023 Tank = Infantry with { Id = "tank", IsInfantry = false, CanCaptureTerritory = false, CanCreateLocalArea = false, Mobility = Mobility.GroundVehicle, Strength = 10, Morale = MoraleClass.NativePassthrough };
    public static Profile023 Ifv = Tank with { Id = "ifv", IsApcOrIfv = true, Strength = 6 };
    public static Profile023 Recon = Infantry with { Id = "recon", CanCaptureTerritory = false, CanCreateLocalArea = false, Strength = .9 };
    public static Unit023 Unit(long id, double x = 1625, double y = 1625, int team = 0, Profile023? p = null) => new(new(Session, id, 0), team, team, (p ?? Infantry).IsInfantry ? 1 : 2, p ?? Infantry, new(x, y), 8, 100, 100);
    public static PreparedTerrain Map(int size = 64, double cell = 50) => new(new(size, size, cell), [new(0, new(cell / 2, size * cell / 2 + cell / 2)), new(1, new(size * cell - cell / 2, size * cell / 2 + cell / 2))]);
    public static Unit023[] Ring(bool filled = false, bool gap = false)
    {
        var units = new List<Unit023> { Unit(1, p: Infantry with { CanProjectZoc = false }) };
        for (int i = 0; i < 12; i++) if (!gap || i is < 4 or > 8) units.Add(Unit(100 + i, 1625 + 650 * Math.Cos(i * Math.PI / 6), 1625 + 650 * Math.Sin(i * Math.PI / 6), 1, Tank) with { Visible = false });
        if (filled) units.Add(Unit(200, 1625, 1625, 1, Tank) with { Visible = false }); return units.ToArray();
    }
    public static GeometryResult Geometry(Unit023[] units, PreparedTerrain? map = null, Rules023Config? config = null)
    {
        map ??= Map(); config ??= new(); var resolved = CombinedArmsRadiusResolver.Resolve(units, map, config); var field = new ZocInfluenceField(map); field.Update(resolved); return new EncirclementRegionDetector(map, config).Evaluate(units[0], resolved, field);
    }
    public static GeometryResult Closed = new(CorridorState.Blocked, Evidence.True, null);
    public static GeometryResult Open = new(CorridorState.Open, Evidence.False, null);
    public static GeometryResult Unknown = new(CorridorState.Unknown, Evidence.Unknown, null);
    public static ArmyValue Army(decimal a = 1000, decimal b = 1000, bool complete = true) => new([a, b], complete, "NominalActiveValueExperimental", "Synthetic");
    public static VictoryInterval Interval(long rev, double start, double end, double a = .6, double b = .3, ArmyValue? army = null, Evidence[]? breakthrough = null) => new(Session, "Zoc_023/1.2", rev, start, end, [a,b], true, army ?? Army(), breakthrough ?? [Evidence.False, Evidence.False], []);
    public static Simulation023 Simulation(PreparedTerrain map, VictoryConfig? victory = null) => new(Session, new(), victory ?? new(), map,
        new ProfileCatalog([new(1, UnitRole.Line, true, false, 1, 50), new(2, UnitRole.Vehicle, false, false, 0, 50, CanCreateLocalArea:false)]), new Rules { TickMilliseconds = 250 }, Enumerable.Range(0,map.Grid.Count).Select(map.Grid.Available).ToArray());
}
