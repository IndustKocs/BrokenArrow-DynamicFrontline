using DynamicFrontline.Core;
using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.Frontline.Rules023;

public enum SimulationLane { Autonomous, Observe }
public enum RulesMode { Legacy_022, Zoc_023 }
public enum Evidence { Unknown, False, True }
public enum Mobility { Infantry, GroundVehicle }
public enum MoraleClass { Unmapped, Reserve, Regular, Special, NativePassthrough }
public enum EncirclementState { NotEncircled, EncirclementPending, Encircled, ReleasePending, DataUncertain }
public enum ReliefState { NoRelief, BeingSupplied, PostSupplyGrace }
public enum ReleasePolicy { PausePenalty, KeepPenaltyUntilConfirmed }
public enum CorridorState { Open, Contested, Blocked, Unknown }
public sealed record Profile023(string Id, bool CanCaptureTerritory, bool CanCreateLocalArea, bool CanProjectZoc,
    bool IsInfantry, bool IsApcOrIfv, Mobility Mobility, double Strength, MoraleClass Morale,
    bool CanReceiveEncirclementPenalty, float BaseMaxStress, float ShockRatio, float PanicRatio, string Provenance)
{
    public void Validate()
    {
        Check.That(!string.IsNullOrWhiteSpace(Id) && !string.IsNullOrWhiteSpace(Provenance), "Profile provenance required");
        Check.Positive(Strength); Check.Positive(BaseMaxStress);
        Check.That(Enum.IsDefined(typeof(MoraleClass), Morale) && Morale != MoraleClass.Unmapped, "Unmapped morale");
        Check.That(!CanCaptureTerritory || IsInfantry, "Only explicit infantry capture");
        Check.That(!CanCreateLocalArea || CanCaptureTerritory, "Local areas require capture permission");
        CombatFormulas.Thresholds(BaseMaxStress, ShockRatio, PanicRatio);
    }
}
public sealed record Unit023(UnitKey Key, int Team, int Owner, int TerritoryProfileId, Profile023 Profile,
    Point2 Position, uint Soldiers, float Hp, float MaxHp, float Stress = 0, StressLevel Level = StressLevel.Calm,
    bool Alive = true, bool Loaded = false, bool Deserting = false, bool Visible = true, bool OnMap = true,
    bool Air = false, decimal NominalValue = 0, string PositionProvenance = "Synthetic", double PositionAge = 0, float? ObservedMaxStress = null)
{
    public bool Active => Alive && !Loaded && !Deserting && OnMap && !Air && (!Profile.IsInfantry || Soldiers > 0);
    public void Validate(Guid session)
    {
        Profile.Validate(); Check.That(Key.MatchId == session && Key.Generation >= 0 && Team is 0 or 1 && Position.IsFinite && !string.IsNullOrWhiteSpace(PositionProvenance), "Invalid unit identity/position");
        Check.Positive(MaxHp); Check.Nonnegative(Hp); Check.That(Hp <= MaxHp && NominalValue >= 0, "Invalid health/value"); Check.Nonnegative(Stress); Check.Nonnegative(PositionAge);
    }
    public FrontlineUnit TerritoryUnit => new(Key, TerritoryProfileId, Team, Position, Soldiers, Alive, Loaded, Deserting, Level, Active ? 1 : 0, Profile.IsInfantry && Profile.CanCaptureTerritory, Active);
}
public sealed record Rules023Config
{
    public string Version { get; init; } = "Zoc_023/1.2";
    public double BaseRadius { get; init; } = 400;
    public double CombinedRadius { get; init; } = 600;
    public double PartnerDistance { get; init; } = 100;
    public double MinimumStrength { get; init; } = 2;
    public double Dominance { get; init; } = 1.25;
    public double CorridorWidth { get; init; } = 100;
    public double Confirmation { get; init; } = 10;
    public double ReleaseConfirmation { get; init; } = 5;
    public double A { get; init; } = .003;
    public double B { get; init; } = .01;
    public double Ramp { get; init; } = 60;
    public ReleasePolicy Release { get; init; } = ReleasePolicy.PausePenalty;
    public int GeometryBudget { get; init; } = 2000000;
    public string Provenance { get; init; } = "TZ13 v1.2 / Synthetic experimental parameters";
    [System.Text.Json.Serialization.JsonIgnore] public string Hash => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this)));
    public void Validate()
    {
        Check.That(Version == "Zoc_023/1.2", "Unsupported rules version");
        foreach (var x in new[] { BaseRadius, CombinedRadius, PartnerDistance, MinimumStrength, Dominance, CorridorWidth, Ramp }) Check.Positive(x);
        foreach (var x in new[] { Confirmation, ReleaseConfirmation, A, B }) Check.Nonnegative(x);
        Check.That(B >= A && Dominance > 1 && GeometryBudget > 0, "Invalid configuration");
    }
}
public static class Check
{
    public static void That(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
    public static void Nonnegative(double x) => That(double.IsFinite(x) && x >= 0, "Nonnegative finite value required");
    public static void Positive(double x) => That(double.IsFinite(x) && x > 0, "Positive finite value required");
}
public sealed record NavPortal(int A, int B, Mobility Mobility, double Width, bool Open, string Provenance);
public sealed class PreparedTerrain
{
    private long checkedRevision = -1;
    private readonly bool[] unrestricted = new bool[2];
    public TerritoryGrid Grid { get; }
    public bool[][] Passable { get; }
    public TeamRoot[] Roots { get; }
    public NavPortal[] Portals { get; set; } = [];
    public HashSet<long>[]? NativeEdges { get; set; }
    public bool EdgeAllowed(int a,int b,Mobility mobility)=>NativeEdges==null||NativeEdges[(int)mobility].Contains((long)Math.Min(a,b)*Grid.Count+Math.Max(a,b));
    public long Revision { get; set; } = 1;
    public bool Complete { get; set; } = true;
    public PreparedTerrain(TerritoryGrid grid, TeamRoot[] roots)
    {
        Grid = grid; Roots = roots.ToArray(); Passable = [Enumerable.Range(0, grid.Count).Select(grid.Available).ToArray(), Enumerable.Range(0, grid.Count).Select(grid.Available).ToArray()];
        Check.That(roots.All(r => r.Team is 0 or 1 && grid.Available(grid.Index(r.Position))), "Invalid fixed root");
    }
    public Evidence Local(Point2 a, Point2 b, Mobility mobility)
    {
        if (!Complete || Revision < 1) return Evidence.Unknown;
        if (checkedRevision != Revision) { for (int k = 0; k < 2; k++) unrestricted[k] = Passable[k].All(x => x); checkedRevision = Revision; }
        if (Grid.Index(a) < 0 || Grid.Index(b) < 0) return Evidence.False;
        if (unrestricted[(int)mobility]&&NativeEdges==null) return Evidence.True;
        var n = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(a.DistanceSquared(b)) / (Grid.CellSize / 3)));
        int previous=Grid.Index(a);
        for (int i = 0; i <= n; i++) { var p = new Point2(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n); var k = Grid.Index(p); if (k < 0 || !Passable[(int)mobility][k]) return Evidence.False;if(k!=previous&&!EdgeAllowed(previous,k,mobility)){int side=previous/Grid.Width*Grid.Width+k%Grid.Width;int other=k/Grid.Width*Grid.Width+previous%Grid.Width;if(!EdgeAllowed(previous,side,mobility)||!EdgeAllowed(side,k,mobility)||!EdgeAllowed(previous,other,mobility)||!EdgeAllowed(other,k,mobility))return Evidence.False;}previous=k; }
        return Evidence.True;
    }
}
public sealed record ResolvedZoc(Unit023 Unit, double Radius, double Power, bool Complete);
public sealed record EncirclementCertificate(int Team, Mobility Mobility, long GeometryRevision, int[] Interior,
    int[] Boundary, string Reason, string Quality, bool EnemyContribution, bool CounterfactualRoute);
public sealed record GeometryResult(CorridorState Corridor, Evidence Enclosed, EncirclementCertificate? Certificate);
