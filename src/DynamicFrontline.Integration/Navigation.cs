using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

public enum RouteState { Reachable, Blocked, Unknown, Pending, Stale }
public readonly record struct RouteEndpoint(UnitKey? Unit,string? Root,Point2 Position,int Team);
public readonly record struct RouteQuery(Guid MatchId,RouteEndpoint A,RouteEndpoint B,NavigationChannel Channel,int MovementType,int Mask,long NavigationRevision,double MaximumCost);
public sealed record RouteRequest(long RequestId,RouteQuery Query);
public sealed record RouteAnswer(long RequestId,RouteState State,bool Complete,double Cost);
public readonly record struct RouteResult(RouteState State,double? Cost=null);
public interface IGameRouteBackend
{
    void Submit(RouteRequest request);
    IReadOnlyList<RouteAnswer> DrainAnswers();
}
public sealed record NavigationSettings(int MaximumQueriesPerTick=32,int MaximumPending=256,int CacheTicks=20,int TimeoutTicks=8,double MaximumCost=10000)
{
    public void Validate(){if(MaximumQueriesPerTick<1||MaximumPending<1||CacheTicks<1||TimeoutTicks<1||!double.IsFinite(MaximumCost)||MaximumCost<=0)throw new ArgumentException("Invalid navigation configuration");}
}
public sealed class GameNavigationAdapter
{
    private sealed record Entry(long RequestId,long Started,RouteResult Result,long Updated);
    private readonly IGameRouteBackend backend;private readonly NavigationSettings settings;
    private readonly Dictionary<RouteQuery,Entry> cache=new();private readonly Dictionary<long,RouteQuery> requests=new();
    private Dictionary<UnitKey,(Point2 Position,int Team)> units=new();private Dictionary<string,RouteEndpoint> roots=new();
    private long nextId,tick=-1,navRevision=-1;private Guid match;private int budget;
    public int PendingCount=>requests.Count;public int CachedCount=>cache.Count;public long Submitted {get;private set;}public long DiscardedAnswers {get;private set;}
    public GameNavigationAdapter(IGameRouteBackend backend,NavigationSettings settings){settings.Validate();this.backend=backend;this.settings=settings;}
    public static RouteEndpoint RootEndpoint(TeamRoot root)=>new(null,$"root:{root.Team}:{root.Position.X:R}:{root.Position.Y:R}",root.Position,root.Team);
    public void BeginFrame(Guid matchId,long navigationRevision,long logicalTick,IEnumerable<FrontlineUnit> current,IEnumerable<TeamRoot> rootAreas)
    {
        if(matchId!=match||navigationRevision!=navRevision){cache.Clear();requests.Clear();tick=-1;}
        if(logicalTick<=tick)throw new ArgumentException("Navigation ticks must increase");
        match=matchId;navRevision=navigationRevision;tick=logicalTick;budget=settings.MaximumQueriesPerTick;
        units=current.Where(u=>u.Alive).ToDictionary(u=>u.Key,u=>(u.Position,u.Team));
        roots=rootAreas.Select(RootEndpoint).ToDictionary(r=>r.Root!);
        foreach(var pair in cache.ToArray())
        {
            if(!Valid(pair.Key.A)||!Valid(pair.Key.B)){requests.Remove(pair.Value.RequestId);cache.Remove(pair.Key);}
            else if(pair.Value.Result.State==RouteState.Pending&&tick-pair.Value.Started>=settings.TimeoutTicks)
            {requests.Remove(pair.Value.RequestId);cache[pair.Key]=new(0,tick,new(RouteState.Unknown),tick);}
        }
        Poll();
    }
    private bool Valid(RouteEndpoint e)=>e.Position.IsFinite&&(e.Unit is {} key?
        key.MatchId==match&&units.TryGetValue(key,out var p)&&p.Position==e.Position&&p.Team==e.Team:e.Root!=null&&roots.TryGetValue(e.Root,out var r)&&r==e);
    public RouteResult Query(RouteQuery query)
    {
        if(query.MatchId!=match||query.NavigationRevision!=navRevision||!Valid(query.A)||!Valid(query.B)||query.A.Team!=query.B.Team)return new(RouteState.Stale);
        if(!double.IsFinite(query.MaximumCost)||query.MaximumCost<=0)return new(RouteState.Unknown);
        bool stale=false;
        if(cache.TryGetValue(query,out var existing))
        {
            if(existing.Result.State==RouteState.Pending)return existing.Result;
            if(tick-existing.Updated<settings.CacheTicks)return existing.Result;
            stale=true;cache.Remove(query);
        }
        if(budget==0||requests.Count>=settings.MaximumPending)return new(stale?RouteState.Stale:RouteState.Pending);
        long id=++nextId;budget--;Submitted++;
        cache[query]=new(id,tick,new(RouteState.Pending),tick);requests[id]=query;
        try{backend.Submit(new(id,query));}
        catch{requests.Remove(id);cache[query]=new(0,tick,new(RouteState.Unknown),tick);return new(RouteState.Unknown);}
        return new(stale?RouteState.Stale:RouteState.Pending);
    }
    public void Poll()
    {
        IReadOnlyList<RouteAnswer> answers;
        try{answers=backend.DrainAnswers();}catch{return;}
        foreach(var answer in answers)
        {
            if(!requests.Remove(answer.RequestId,out var query)||!cache.TryGetValue(query,out var entry)||entry.RequestId!=answer.RequestId||!Valid(query.A)||!Valid(query.B))
            {DiscardedAnswers++;continue;}
            var state=answer.State;
            if(!answer.Complete||!double.IsFinite(answer.Cost)||answer.Cost<0||state is not(RouteState.Reachable or RouteState.Blocked))state=RouteState.Unknown;
            // A route exceeding a policy budget is not proof of physical blockage.
            if(state==RouteState.Reachable&&answer.Cost>Math.Min(query.MaximumCost,settings.MaximumCost))state=RouteState.Unknown;
            cache[query]=new(0,tick,new(state,state==RouteState.Reachable?answer.Cost:null),tick);
        }
    }
    public void Reset(){cache.Clear();requests.Clear();units.Clear();roots.Clear();match=Guid.Empty;tick=-1;navRevision=-1;}
}
public sealed class NavigationConnectivityAdapter(GameNavigationAdapter navigation,NavigationSettings settings):IUnitLinkGeometry,IRetreatAccess,ILogisticsConnectivity
{
    private GameMatchContext? context;private Dictionary<UnitKey,AdaptedUnit> units=new();private TeamRoot[] roots=Array.Empty<TeamRoot>();
    public void SetFrame(InternalSimulationSnapshot snapshot,TeamRoot[] teamRoots)
    {context=snapshot.Context;units=snapshot.Units.ToDictionary(u=>u.Key);roots=teamRoots;}
    private RouteResult Query(RouteEndpoint a,RouteEndpoint b,GameProfile p,NavigationChannel channel)=>
        context==null?new(RouteState.Unknown):navigation.Query(new(context.MatchId,a,b,channel,p.MovementType,p.NavigationMask,context.NavigationRevision,settings.MaximumCost));
    private static RouteEndpoint Endpoint(AdaptedUnit u)=>new(u.Key,null,u.Position,u.Team);
    public bool CanLink(Point2 a,Point2 b,int team)=>false; // Position-only queries cannot establish identity/channel.
    public bool CanLink(FrontlineUnit a,FrontlineUnit b)
    {
        if(!units.TryGetValue(a.Key,out var x)||!units.TryGetValue(b.Key,out var y)||x.Profile is not{} p||y.Profile is not{} q||
            p.ConnectivityChannel!=q.ConnectivityChannel||p.MovementType!=q.MovementType||p.NavigationMask!=q.NavigationMask)return false;
        double distance=Math.Min(p.ConnectivityRange,q.ConnectivityRange);
        return a.Position.DistanceSquared(b.Position)<=distance*distance&&Query(Endpoint(x),Endpoint(y),p,p.ConnectivityChannel).State==RouteState.Reachable;
    }
    public bool CanLink(TeamRoot root,FrontlineUnit unit)
    {
        if(!units.TryGetValue(unit.Key,out var u)||u.Profile is not{} p||root.Position.DistanceSquared(u.Position)>p.ConnectivityRange*p.ConnectivityRange)return false;
        return Query(GameNavigationAdapter.RootEndpoint(root),Endpoint(u),p,p.ConnectivityChannel).State==RouteState.Reachable;
    }
    private RouteState ToRoot(FrontlineUnit unit,NavigationChannel channel)
    {
        if(!units.TryGetValue(unit.Key,out var u)||u.Profile is not{} p)return RouteState.Unknown;
        bool allBlocked=true;int count=0;
        foreach(var root in roots.Where(r=>r.Team==u.Team))
        {count++;var result=Query(Endpoint(u),GameNavigationAdapter.RootEndpoint(root),p,channel).State;if(result==RouteState.Reachable)return result;if(result!=RouteState.Blocked)allBlocked=false;}
        return count>0&&allBlocked?RouteState.Blocked:RouteState.Unknown;
    }
    public RouteAccess Get(FrontlineUnit unit)=>ToRoot(unit,NavigationChannel.GroundPassage) switch{RouteState.Reachable=>RouteAccess.Available,RouteState.Blocked=>RouteAccess.Blocked,_=>RouteAccess.Unknown};
    // A navigable logistics route does not prove that supplies exist; explicit inventory/provider remains required.
    public bool IsSupplied(FrontlineUnit unit)=>false;
}
