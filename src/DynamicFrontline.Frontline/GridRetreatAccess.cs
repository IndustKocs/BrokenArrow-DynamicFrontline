namespace DynamicFrontline.Frontline;

// Synthetic conservative path model, independent of the operational graph and logistics.
// Production must replace this with authoritative validated route evidence.
public sealed class GridRetreatAccess : IRetreatAccess
{
    private readonly TerritoryGrid grid;private readonly TeamRoot[] roots;private readonly bool enemyControlBlocksRetreat;
    private readonly bool[][] reachable;private long revision=-1;
    public GridRetreatAccess(TerritoryGrid grid,IEnumerable<TeamRoot> roots,bool enemyControlBlocksRetreat)
    {this.grid=grid;this.roots=roots.ToArray();this.enemyControlBlocksRetreat=enemyControlBlocksRetreat;reachable=new[]{new bool[grid.Count],new bool[grid.Count]};}
    private void Refresh()
    {
        if(revision==grid.OwnershipRevision)return;
        foreach(var r in reachable)Array.Clear(r,0,r.Length);
        var q=new Queue<int>();for(int team=0;team<2;team++)
        {
            bool Open(int i)=>grid.Available(i)&&(!enemyControlBlocksRetreat||grid.Owner(i)<0||grid.Owner(i)==team);
            foreach(var root in roots.Where(r=>r.Team==team)){int i=grid.Index(root.Position);if(Open(i)&&!reachable[team][i]){reachable[team][i]=true;q.Enqueue(i);}}
            while(q.TryDequeue(out int i))foreach(int j in grid.Neighbors(i))if(Open(j)&&!reachable[team][j]){reachable[team][j]=true;q.Enqueue(j);}
        }
        revision=grid.OwnershipRevision;
    }
    public RouteAccess Get(FrontlineUnit unit)
    {
        int i=grid.Index(unit.Position);if(unit.Team is <0 or >1||!grid.Available(i)||!roots.Any(r=>r.Team==unit.Team))return RouteAccess.Unknown;
        Refresh();return reachable[unit.Team][i]?RouteAccess.Available:RouteAccess.Blocked;
    }
}
