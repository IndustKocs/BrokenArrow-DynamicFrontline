namespace DynamicFrontline.Frontline;

public sealed class OperationalConnectivityGraph
{
    public FrontlineUnit[] Units {get;}
    public List<int>[] Neighbors {get;}
    public bool[] RootLinks {get;}
    public OperationalConnectivityGraph(FrontlineUnit[] units,List<int>[] neighbors,bool[] rootLinks)
    {Units=units;Neighbors=neighbors;RootLinks=rootLinks;}
}
public sealed class OperationalConnectivitySystem(ProfileCatalog profiles,Rules rules,ILinkGeometry geometry)
{
    private bool Eligible(FrontlineUnit u)
    {
        var p=profiles.Find(u.UnitId);
        return p is {CanRelay:true}&&u.Alive&&!u.Loaded&&!u.Deserting&&
            (u.HasInfantry?u.AliveSoldierCount>0&&u.AliveSoldierCount>=p.MinimumRelaySoldiers:u.OperationallyActive)&&
            u.StatusCoefficient>0&&(u.Stress!=Core.StressLevel.Shocked||rules.ShockCanRelay)&&
            (u.Stress!=Core.StressLevel.Panicked||rules.PanicCanRelay);
    }
    public OperationalConnectivityGraph Build(IReadOnlyList<FrontlineUnit> source,IReadOnlyList<TeamRoot> roots)
    {
        var units=source.Where(Eligible).ToArray();var edges=units.Select(_=>new List<int>()).ToArray();
        var rootLinks=new bool[units.Length];var spatial=new SpatialIndex(rules.LinkDistance);
        double squared=rules.LinkDistance*rules.LinkDistance;
        for(int i=0;i<units.Length;i++)
        {
            var u=units[i];foreach(int j in spatial.Near(u.Position))
            {
                var v=units[j];if(u.Team==v.Team&&u.Position.DistanceSquared(v.Position)<=squared&&
                    (geometry is IUnitLinkGeometry typed?typed.CanLink(u,v):geometry.CanLink(u.Position,v.Position,u.Team)))
                {edges[i].Add(j);edges[j].Add(i);}
            }
            spatial.Add(u.Position,i);
            rootLinks[i]=roots.Any(r=>r.Team==u.Team&&r.Position.DistanceSquared(u.Position)<=squared&&
                (geometry is IUnitLinkGeometry typed?typed.CanLink(r,u):geometry.CanLink(r.Position,u.Position,u.Team)));
        }
        return new(units,edges,rootLinks);
    }
}
public sealed class ConnectivityEvaluator(Rules rules)
{
    public OperationalStateSnapshot Evaluate(SnapshotHeader header,OperationalConnectivityGraph graph)
    {
        int n=graph.Units.Length;var components=Enumerable.Repeat(-1,n).ToArray();var hops=Enumerable.Repeat(-1,n).ToArray();
        var q=new Queue<int>();
        for(int i=0;i<n;i++)if(components[i]<0)
        {
            components[i]=i;q.Enqueue(i);
            while(q.TryDequeue(out int v))foreach(int j in graph.Neighbors[v])if(components[j]<0){components[j]=i;q.Enqueue(j);}
        }
        for(int i=0;i<n;i++)if(graph.RootLinks[i]){hops[i]=1;q.Enqueue(i);}
        while(q.TryDequeue(out int v))
        {if(hops[v]>=rules.MaximumHops)continue;foreach(int j in graph.Neighbors[v])if(hops[j]<0){hops[j]=hops[v]+1;q.Enqueue(j);}}
        var nodes=graph.Units.Select((u,i)=>new OperationalNode(u.Key,u.Team,components[i],hops[i]>=0,hops[i])).ToArray();
        var edges=new List<OperationalEdge>();for(int i=0;i<n;i++)foreach(int j in graph.Neighbors[i])if(i<j)edges.Add(new(graph.Units[i].Key,graph.Units[j].Key));
        return new(header,nodes,edges.ToArray());
    }
}

// Conservative straight-line supercover on the supplied terrain mask. Not NavMesh pathfinding.
public sealed class GridLinkGeometry(TerritoryGrid grid) : ILinkGeometry
{
    public bool CanLink(Point2 a,Point2 b,int team)
    {
        int ai=grid.Index(a),bi=grid.Index(b);if(!grid.Available(ai)||!grid.Available(bi))return false;
        int x=ai%grid.Width,y=ai/grid.Width,tx=bi%grid.Width,ty=bi/grid.Width;
        double dx=b.X-a.X,dy=b.Y-a.Y;int sx=Math.Sign(dx),sy=Math.Sign(dy);
        double mx=sx==0?double.PositiveInfinity:((grid.Origin.X+(x+(sx>0?1:0))*grid.CellSize)-a.X)/dx;
        double my=sy==0?double.PositiveInfinity:((grid.Origin.Y+(y+(sy>0?1:0))*grid.CellSize)-a.Y)/dy;
        double stepX=sx==0?double.PositiveInfinity:grid.CellSize/Math.Abs(dx),stepY=sy==0?double.PositiveInfinity:grid.CellSize/Math.Abs(dy);
        bool Open(int cx,int cy)=>cx>=0&&cy>=0&&cx<grid.Width&&cy<grid.Height&&grid.Available(cy*grid.Width+cx);
        while(x!=tx||y!=ty)
        {
            if(Math.Abs(mx-my)<1e-10){if(!Open(x+sx,y)||!Open(x,y+sy))return false;x+=sx;y+=sy;mx+=stepX;my+=stepY;}
            else if(mx<my){x+=sx;mx+=stepX;}else{y+=sy;my+=stepY;}
            if(!Open(x,y))return false;
        }
        return true;
    }
}

public sealed class EncirclementEvaluator(Rules rules)
{
    private readonly Dictionary<Core.UnitKey,IsolationSnapshot> previous=new();
    public IsolationBatch Evaluate(SnapshotHeader header,IReadOnlyList<FrontlineUnit> units,TerritorialConnectivity territory,
        OperationalStateSnapshot operational,IRetreatAccess routes,ILogisticsConnectivity logistics)
    {
        var support=operational.Nodes.Where(n=>n.Connected).Select(n=>n.Unit).ToHashSet();
        var output=new List<IsolationSnapshot>();var live=new HashSet<Core.UnitKey>();
        foreach(var u in units.Where(u=>u.Alive&&(u.HasInfantry?u.AliveSoldierCount>0:u.OperationallyActive)))
        {
            live.Add(u.Key);bool t=territory.IsConnected(u),o=support.Contains(u.Key),l=logistics.IsSupplied(u);
            RouteAccess path=routes.Get(u);bool connected=t&&o;
            // Physical closure is evidence distinct from operational support; unknown is never closure.
            bool severe=!t&&path==RouteAccess.Blocked;
            bool canRecover=connected||(!severe&&(o||path==RouteAccess.Available));
            previous.TryGetValue(u.Key,out var old);
            long? severeSince=severe?old?.EncirclementSinceTick??header.Tick:null;
            long? isolated=connected?null:old?.IsolatedSinceTick??header.Tick;
            long? recovery=null;IsolationState state;long since=old?.StateSinceTick??header.Tick;
            if(old is null)state=connected?IsolationState.Connected:IsolationState.Autonomous;
            else state=old.State;
            bool wasIsolated=old!=null&&old.State!=IsolationState.Connected;
            if(canRecover&&wasIsolated)
            {
                recovery=old!.RecoverySinceTick??header.Tick;state=IsolationState.Reconnecting;
                if(header.Tick-recovery>=rules.RecoveryDelayTicks)
                {state=connected?IsolationState.Connected:IsolationState.Autonomous;recovery=null;}
            }
            else if(connected)state=IsolationState.Connected;
            else if(severe&&header.Tick-severeSince>=rules.IsolationDelayTicks)state=IsolationState.Encircled;
            else state=IsolationState.Autonomous;
            // An autonomous supported unit already settled after recovery must not oscillate back.
            if(old?.State==IsolationState.Autonomous&&canRecover&&!connected&&old.CanStartRecovery) {state=IsolationState.Autonomous;recovery=null;}
            if(old?.State!=state)since=header.Tick;
            double seconds=severeSince.HasValue?Math.Max(0,header.Tick-severeSince.Value-rules.IsolationDelayTicks)*rules.TickMilliseconds/1000d:0;
            double severity=state is IsolationState.Encircled or IsolationState.Reconnecting
                ?Math.Min(1,seconds*rules.SeverityPerSecond)*(o?rules.SupportedSeverityMultiplier:1)*(l?rules.SuppliedSeverityMultiplier:1):0;
            if(state==IsolationState.Reconnecting&&old!=null)
                severity=old.Severity*Math.Max(0,1-1d/Math.Max(1,rules.RecoveryDelayTicks-(header.Tick-recovery!.Value)+1));
            var item=new IsolationSnapshot(u.Key,u.Team,state,t,o,l,path,since,isolated,recovery,severeSince,severity,canRecover);
            previous[u.Key]=item;output.Add(item);
        }
        foreach(var k in previous.Keys.Where(k=>!live.Contains(k)).ToArray())previous.Remove(k);
        return new(header,output.ToArray());
    }
}
