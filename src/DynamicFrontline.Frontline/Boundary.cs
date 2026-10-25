namespace DynamicFrontline.Frontline;

public sealed class FrontlineBoundaryExtractor
{
    private readonly record struct Vertex(int X,int Y);
    private readonly record struct Edge(Vertex A,Vertex B);
    public BoundaryPath[] Extract(TerritoryGrid grid)
    {
        var groups=new SortedDictionary<(int Owner,int Neighbor),List<Edge>>();
        void Add(int owner,int neighbor,Vertex a,Vertex b)
        {
            if(owner<0||owner==neighbor||neighbor>=0&&owner>neighbor)return;
            var k=(owner,neighbor);if(!groups.TryGetValue(k,out var edges))groups[k]=edges=new();edges.Add(new(a,b));
        }
        for(int i=0;i<grid.Count;i++)
        {
            int owner=grid.Owner(i);if(owner<0)continue;int x=i%grid.Width,y=i/grid.Width;
            Add(owner,x==0?-2:grid.Owner(i-1),new(x,y),new(x,y+1));
            Add(owner,x==grid.Width-1?-2:grid.Owner(i+1),new(x+1,y),new(x+1,y+1));
            Add(owner,y==0?-2:grid.Owner(i-grid.Width),new(x,y),new(x+1,y));
            Add(owner,y==grid.Height-1?-2:grid.Owner(i+grid.Width),new(x,y+1),new(x+1,y+1));
        }
        var result=new List<BoundaryPath>();
        foreach(var group in groups)
        {
            var edges=group.Value;var adjacency=new Dictionary<Vertex,List<int>>();var used=new bool[edges.Count];
            for(int i=0;i<edges.Count;i++)foreach(var v in new[]{edges[i].A,edges[i].B})
            {if(!adjacency.TryGetValue(v,out var list))adjacency[v]=list=new();list.Add(i);}
            void Trace(int start,Vertex first)
            {
                var vertices=new List<Vertex>{first};int index=start;Vertex current=first;
                while(true)
                {
                    used[index]=true;var edge=edges[index];current=edge.A==current?edge.B:edge.A;vertices.Add(current);
                    if(current==first||adjacency[current].Count!=2)break;
                    int next=adjacency[current].FirstOrDefault(e=>!used[e],-1);if(next<0)break;index=next;
                }
                var simple=new List<Vertex>();foreach(var v in vertices)
                {
                    while(simple.Count>=2)
                    {
                        var a=simple[^2];var b=simple[^1];
                        if((long)(b.X-a.X)*(v.Y-b.Y)!=(long)(b.Y-a.Y)*(v.X-b.X))break;
                        simple.RemoveAt(simple.Count-1);
                    }
                    simple.Add(v);
                }
                result.Add(new(group.Key.Owner,group.Key.Neighbor,simple.Select(v=>new Point2(grid.Origin.X+v.X*grid.CellSize,grid.Origin.Y+v.Y*grid.CellSize)).ToArray()));
            }
            foreach(var entry in adjacency.OrderBy(x=>x.Key.Y).ThenBy(x=>x.Key.X))
                if(entry.Value.Count!=2)foreach(int i in entry.Value)if(!used[i])Trace(i,entry.Key);
            for(int i=0;i<edges.Count;i++)if(!used[i])Trace(i,edges[i].A);
        }
        return result.ToArray();
    }
}
