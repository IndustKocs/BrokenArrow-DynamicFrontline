using System.Diagnostics;
namespace DynamicFrontline.Frontline;

public sealed record UpdateMetrics(double InfluenceMs,double CaptureMs,double ConnectivityMs,double BoundaryMs,double OtherMs,int DirtyCells);
// Internal authoritative diagnostic frame. Never send this object to an untrusted peer.
public sealed record AuthoritativeFrame(SnapshotHeader Header,TerritoryDelta Delta,FrontlineSnapshot Frontline,
    LocalControlArea[] LocalAreas,OperationalStateSnapshot Operational,IsolationBatch Isolation,FrontlineUnit[] Units,UpdateMetrics Metrics);

public sealed class FrontlineSimulation
{
    public Guid MatchId {get;}public Rules Rules {get;}public TerritoryGrid Grid {get;}
    public TerritorialConnectivity Territory {get;}
    private readonly TeamRoot[] roots;private readonly TerritoryCaptureSystem capture;private readonly LocalControlAreaSystem local;
    private readonly OperationalConnectivitySystem connectivity;private readonly ConnectivityEvaluator evaluator;private readonly EncirclementEvaluator encirclement;
    private readonly FrontlineBoundaryExtractor boundaries=new();private readonly IRetreatAccess routes;private readonly ILogisticsConnectivity logistics;
    private BoundaryPath[] cachedBoundary=Array.Empty<BoundaryPath>();private long boundaryRevision=-1,tick=-1,revision;
    public FrontlineSimulation(Guid matchId,Rules rules,TerritoryGrid grid,ProfileCatalog profiles,IEnumerable<TeamRoot> roots,
        ILinkGeometry geometry,IRetreatAccess routes,ILogisticsConnectivity logistics)
    {
        rules.Validate();if(matchId==Guid.Empty)throw new ArgumentException("Match ID required");
        this.roots=roots.OrderBy(r=>r.Team).ThenBy(r=>r.Position.X).ThenBy(r=>r.Position.Y).ToArray();
        foreach(var r in this.roots)if(r.Team is <0 or >1||!grid.Available(grid.Index(r.Position)))throw new ArgumentException("Invalid root");
        MatchId=matchId;Rules=rules;Grid=grid;this.routes=routes;this.logistics=logistics;
        Territory=new(grid,this.roots);capture=new(grid,profiles,rules);local=new(profiles,rules);
        connectivity=new(profiles,rules,geometry);evaluator=new(rules);encirclement=new(rules);
    }
    public AuthoritativeFrame Step(long nextTick,IEnumerable<FrontlineUnit> input)
    {
        if(nextTick!=tick+1)throw new ArgumentException("Supply consecutive ticks starting at zero");
        var units=input.OrderBy(u=>u.Key.Uid).ThenBy(u=>u.Key.Generation).ToArray();var keys=new HashSet<Core.UnitKey>();
        foreach(var u in units)
        {
            if(u.Key.MatchId!=MatchId||u.Team is <0 or >1||!u.Position.IsFinite||!keys.Add(u.Key)||!Enum.IsDefined(typeof(Core.StressLevel),u.Stress))throw new ArgumentException("Invalid authoritative input");
            Rules.Nonnegative(u.StatusCoefficient,nameof(u.StatusCoefficient));
        }
        var watch=Stopwatch.StartNew();Territory.Refresh();capture.UpdateInfluence(units,Territory);double influence=watch.Elapsed.TotalMilliseconds;
        watch.Restart();var changes=capture.Advance();Territory.Refresh();double cap=watch.Elapsed.TotalMilliseconds;
        var header=new SnapshotHeader(MatchId,Rules.Version,revision+1,nextTick);
        watch.Restart();var graph=connectivity.Build(units.Where(u=>Grid.Available(Grid.Index(u.Position))).ToArray(),roots);
        var operational=evaluator.Evaluate(header,graph);double conn=watch.Elapsed.TotalMilliseconds;
        watch.Restart();if(boundaryRevision!=Grid.OwnershipRevision){cachedBoundary=boundaries.Extract(Grid);boundaryRevision=Grid.OwnershipRevision;}
        double boundary=watch.Elapsed.TotalMilliseconds;watch.Restart();
        var areas=local.Evaluate(units,Grid,Territory);var isolation=encirclement.Evaluate(header,units,Territory,operational,routes,logistics);
        tick=nextTick;var delta=new TerritoryDelta(header,revision,changes);revision++;
        return new(header,delta,new(header,cachedBoundary),areas,operational,isolation,units,
            new(influence,cap,conn,boundary,watch.Elapsed.TotalMilliseconds,capture.LastDirtyCells));
    }
    public TerritoryStateSnapshot Snapshot()=>Grid.Snapshot(new(MatchId,Rules.Version,revision,tick));
}

public interface ITeamKnowledgePolicy
{
    bool CanShowCell(int team,CellState cell,long sourceTick,long currentTick);
    bool CanShowBoundary(int team,BoundaryPath path,long sourceTick,long currentTick);
}
// Until fog/frontline disclosure rules are approved, no territorial geometry leaves this filter.
public sealed class DenyTerritoryKnowledge : ITeamKnowledgePolicy
{
    public bool CanShowCell(int team,CellState cell,long sourceTick,long currentTick)=>false;
    public bool CanShowBoundary(int team,BoundaryPath path,long sourceTick,long currentTick)=>false;
}
public sealed record TeamPresentationState(SnapshotHeader Header,int RecipientTeam,CellState[] Cells,BoundaryPath[] Boundaries,
    LocalControlArea[] LocalAreas,OperationalNode[] Nodes,OperationalEdge[] Edges,IsolationSnapshot[] Isolation,FrontlineUnit[] FriendlyUnits);
public static class TeamPresentationFilter
{
    public static TeamPresentationState Project(int team,AuthoritativeFrame frame,TerritoryStateSnapshot grid,long currentTick,ITeamKnowledgePolicy policy)
    {
        if(team is <0 or >1||grid.Header!=frame.Header||currentTick<frame.Header.Tick)throw new ArgumentException("Invalid presentation context");
        var nodes=frame.Operational.Nodes.Where(n=>n.Team==team).ToArray();var allowed=nodes.Select(n=>n.Unit).ToHashSet();
        var components=nodes.Select(n=>n.Component).Distinct().OrderBy(x=>x).Select((c,i)=>(c,i)).ToDictionary(x=>x.c,x=>x.i);
        nodes=nodes.Select(n=>n with {Component=components[n.Component]}).ToArray();
        return new(frame.Header,team,grid.Cells.Where(c=>policy.CanShowCell(team,c,frame.Header.Tick,currentTick)).ToArray(),
            frame.Frontline.Paths.Where(p=>policy.CanShowBoundary(team,p,frame.Header.Tick,currentTick)).ToArray(),
            frame.LocalAreas.Where(a=>a.Team==team).ToArray(),nodes,
            frame.Operational.Edges.Where(e=>allowed.Contains(e.A)&&allowed.Contains(e.B)).ToArray(),
            frame.Isolation.Units.Where(u=>u.Team==team).ToArray(),frame.Units.Where(u=>u.Team==team).ToArray());
    }
    public static void ValidateRecipient(TeamPresentationState state,int authenticatedTeam,Guid match,string version)
    {
        if(state.RecipientTeam!=authenticatedTeam||state.Header.MatchId!=match||state.Header.RulesVersion!=version||
            state.FriendlyUnits.Any(u=>u.Team!=authenticatedTeam)||state.Nodes.Any(n=>n.Team!=authenticatedTeam)||
            state.LocalAreas.Any(a=>a.Team!=authenticatedTeam)||state.Isolation.Any(i=>i.Team!=authenticatedTeam))
            throw new InvalidOperationException("Presentation recipient mismatch");
        var keys=state.Nodes.Select(n=>n.Unit).ToHashSet();
        if(state.Edges.Any(e=>!keys.Contains(e.A)||!keys.Contains(e.B)))throw new InvalidOperationException("Foreign edge");
    }
}

// Authoritative replication helper; never a public fog-of-war message.
public sealed class TerritoryReplica
{
    public TerritoryStateSnapshot State {get;private set;}
    public TerritoryReplica(TerritoryStateSnapshot initial)=>State=initial with {Cells=initial.Cells.ToArray()};
    public void Apply(TerritoryDelta delta)
    {
        if(delta.Header.MatchId!=State.Header.MatchId||delta.Header.RulesVersion!=State.Header.RulesVersion||
            delta.BaseRevision!=State.Header.Revision||delta.Header.Revision!=delta.BaseRevision+1||delta.Header.Tick!=State.Header.Tick+1)
            throw new InvalidOperationException("Stale, reordered, foreign, or incompatible delta");
        var indices=new HashSet<int>();foreach(var c in delta.Cells)
            if(c.Index<0||c.Index>=State.Cells.Length||!indices.Add(c.Index)||c.Owner is <-1 or >1||c.Progress<0||c.Challenger is <-1 or >1||c.Available!=State.Cells[c.Index].Available)
                throw new InvalidOperationException("Invalid delta cell");
        var cells=State.Cells.ToArray();foreach(var c in delta.Cells)cells[c.Index]=c;
        State=State with {Header=delta.Header,Cells=cells};
    }
}
