using DynamicFrontline.Core;

namespace DynamicFrontline.Frontline;

public readonly record struct Point2(double X, double Y)
{
    public double DistanceSquared(Point2 b) => (X-b.X)*(X-b.X)+(Y-b.Y)*(Y-b.Y);
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}
public enum UnitRole { Line, Reserve, Airborne, SpecialForces, Recon, Sniper, Vehicle, Other }
public sealed record UnitTerritoryProfile(int UnitId, UnitRole Role, bool CanCaptureTerritory,
    bool CanRelay, double TypeCoefficient, double InfluenceRadius, uint MinimumRelaySoldiers = 1,bool CanCreateLocalArea = true)
{
    public void Validate()
    {
        Rules.Nonnegative(TypeCoefficient, nameof(TypeCoefficient)); Rules.Positive(InfluenceRadius, nameof(InfluenceRadius));
        if (!Enum.IsDefined(typeof(UnitRole),Role)) throw new ArgumentException("Unknown role");
        if (CanCaptureTerritory && Role is not (UnitRole.Line or UnitRole.Reserve or UnitRole.Airborne))
            throw new ArgumentException("Only explicit line infantry profiles may capture.");
    }
}
public sealed class ProfileCatalog
{
    private readonly Dictionary<int,UnitTerritoryProfile> profiles;
    public ProfileCatalog(IEnumerable<UnitTerritoryProfile> entries)
    { profiles=entries.ToDictionary(x=>x.UnitId); foreach(var p in profiles.Values) p.Validate(); }
    public UnitTerritoryProfile? Find(int id)=>profiles.GetValueOrDefault(id);
}
public sealed record FrontlineUnit(UnitKey Key, int UnitId, int Team, Point2 Position, uint AliveSoldierCount,
    bool Alive=true, bool Loaded=false, bool Deserting=false, StressLevel Stress=StressLevel.Calm, double StatusCoefficient=1,
    bool HasInfantry=true,bool OperationallyActive=true);
public sealed record TeamRoot(int Team, Point2 Position);
public sealed record Rules
{
    public string Version {get;init;}="frontline-poc-0.1";
    public int TickMilliseconds {get;init;}=250;
    public double CalmCoefficient {get;init;}=1;
    public double ShockCoefficient {get;init;}=.5;
    public double PanicCoefficient {get;init;}=0;
    public double LocalRadiusPerSqrtInfluence {get;init;}=4;
    public double MaximumLocalRadius {get;init;}=30;
    public long CaptureThreshold {get;init;}=16000;
    public long DominanceMargin {get;init;}=1000;
    public long ProgressDecayPerSecond {get;init;}=2000;
    public double LinkDistance {get;init;}=60;
    public int MaximumHops {get;init;}=8;
    public bool ShockCanRelay {get;init;}=true;
    public bool PanicCanRelay {get;init;}=false;
    public int IsolationDelayTicks {get;init;}=12;
    public int RecoveryDelayTicks {get;init;}=8;
    public double SeverityPerSecond {get;init;}=.03;
    public double SupportedSeverityMultiplier {get;init;}=.25;
    public double SuppliedSeverityMultiplier {get;init;}=.5;
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Version) || TickMilliseconds is <1 or >60000 || CaptureThreshold<=0 ||
            CaptureThreshold>long.MaxValue/1000 || DominanceMargin<0 || ProgressDecayPerSecond<0 ||
            ProgressDecayPerSecond>long.MaxValue/60000 || MaximumHops<1 || IsolationDelayTicks<1 || RecoveryDelayTicks<1)
            throw new ArgumentException("Invalid rules domain");
        foreach(double v in new[]{CalmCoefficient,ShockCoefficient,PanicCoefficient,LocalRadiusPerSqrtInfluence,SeverityPerSecond}) Nonnegative(v,"coefficient");
        Positive(MaximumLocalRadius,nameof(MaximumLocalRadius)); Positive(LinkDistance,nameof(LinkDistance));
        foreach(double v in new[]{SupportedSeverityMultiplier,SuppliedSeverityMultiplier})
        { Nonnegative(v,"severity multiplier"); if(v>1) throw new ArgumentException("Multiplier exceeds one"); }
    }
    internal static void Nonnegative(double x,string name) { if(!double.IsFinite(x)||x<0) throw new ArgumentOutOfRangeException(name); }
    internal static void Positive(double x,string name) { Nonnegative(x,name); if(x==0)throw new ArgumentOutOfRangeException(name); }
}
public sealed record SnapshotHeader(Guid MatchId,string RulesVersion,long Revision,long Tick);
public sealed record CellState(int Index,bool Available,int Owner,int Challenger,long Progress,bool Contested);
public sealed record TerritoryStateSnapshot(SnapshotHeader Header,int Width,int Height,double CellSize,Point2 Origin,CellState[] Cells);
public sealed record TerritoryDelta(SnapshotHeader Header,long BaseRevision,CellState[] Cells);
public sealed record BoundaryPath(int Owner,int Neighbor,Point2[] Points);
public sealed record FrontlineSnapshot(SnapshotHeader Header,BoundaryPath[] Paths);
public sealed record LocalControlArea(UnitKey Unit,int Team,Point2 Center,double Radius,int Enclave);
public sealed record OperationalNode(UnitKey Unit,int Team,int Component,bool Connected,int Hops);
public sealed record OperationalEdge(UnitKey A,UnitKey B);
public sealed record OperationalStateSnapshot(SnapshotHeader Header,OperationalNode[] Nodes,OperationalEdge[] Edges);
public enum RouteAccess { Unknown, Available, Blocked }
public enum IsolationState { Connected, Autonomous, Encircled, Reconnecting }
public sealed record IsolationSnapshot(UnitKey Unit,int Team,IsolationState State,bool TerritorialConnectivity,
    bool OperationalConnectivity,bool LogisticsConnectivity,RouteAccess RetreatRoute,long StateSinceTick,
    long? IsolatedSinceTick,long? RecoverySinceTick,long? EncirclementSinceTick,double Severity,bool CanStartRecovery);
public sealed record IsolationBatch(SnapshotHeader Header,IsolationSnapshot[] Units);
public interface ILinkGeometry { bool CanLink(Point2 a,Point2 b,int team); }
public interface IUnitLinkGeometry : ILinkGeometry
{
    bool CanLink(FrontlineUnit a,FrontlineUnit b);
    bool CanLink(TeamRoot root,FrontlineUnit unit);
}
public interface IRetreatAccess { RouteAccess Get(FrontlineUnit unit); }
public interface ILogisticsConnectivity { bool IsSupplied(FrontlineUnit unit); }
public interface IIsolationEffectSink { void Observe(IsolationBatch state); }
public sealed class TestLogistics : ILogisticsConnectivity
{
    public HashSet<UnitKey> Supplied {get;}=new();
    public bool IsSupplied(FrontlineUnit unit)=>Supplied.Contains(unit.Key);
}
public sealed class TestRetreatAccess : IRetreatAccess
{
    public Dictionary<UnitKey,RouteAccess> Routes {get;}=new();
    public RouteAccess Get(FrontlineUnit unit)=>Routes.GetValueOrDefault(unit.Key,RouteAccess.Unknown);
}
public sealed class UnrestrictedGeometry : ILinkGeometry { public bool CanLink(Point2 a,Point2 b,int team)=>true; }

public static class DesertionStateAdapter
{
    public static FrontlineUnit Apply(FrontlineUnit unit,DesertionController controller)=>
        unit with {Deserting=controller.ActiveUnit==unit.Key && controller.Locked};
    public static FrontlineUnit FromSnapshot(UnitSnapshot unit,int team,bool loaded,StressLevel stress,DesertionController controller)=>
        Apply(new(unit.Key,unit.UnitId,team,new(unit.Position.X,unit.Position.Z),unit.AliveSoldierCount,
            unit.IsAlive,loaded,false,stress),controller);
}
