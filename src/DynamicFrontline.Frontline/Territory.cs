namespace DynamicFrontline.Frontline;

public sealed class TerritoryGrid
{
    public int Width {get;} public int Height {get;} public int Count=>owners.Length;
    public double CellSize {get;} public Point2 Origin {get;}
    private readonly bool[] mask;
    private readonly int[] owners,challengers;
    private readonly long[] progress;
    private readonly bool[] contested;
    internal long OwnershipRevision {get;private set;}
    public TerritoryGrid(int width,int height,double cellSize,bool[]? available=null,Point2 origin=default)
    {
        Rules.Positive(cellSize,nameof(cellSize));
        if(width<=0||height<=0||!origin.IsFinite|| (long)width*height>4_000_000)throw new ArgumentException("Invalid map");
        Width=width;Height=height;CellSize=cellSize;Origin=origin;
        if(available!=null&&available.Length!=width*height)throw new ArgumentException("Mask size mismatch");
        mask=available?.ToArray()??Enumerable.Repeat(true,width*height).ToArray();
        owners=Enumerable.Repeat(-1,width*height).ToArray(); challengers=owners.ToArray();
        progress=new long[Count];contested=new bool[Count];
    }
    public bool Available(int i)=>i>=0&&i<Count&&mask[i];
    public int Owner(int i)=>Available(i)?owners[i]:-2;
    public int Index(Point2 p)
    {
        if(!p.IsFinite)return -1;
        double x=Math.Floor((p.X-Origin.X)/CellSize),y=Math.Floor((p.Y-Origin.Y)/CellSize);
        return x<0||y<0||x>=Width||y>=Height?-1:(int)y*Width+(int)x;
    }
    public Point2 Center(int i)=>new(Origin.X+(i%Width+.5)*CellSize,Origin.Y+(i/Width+.5)*CellSize);
    public IEnumerable<int> Neighbors(int i)
    { if(i%Width>0)yield return i-1;if(i%Width<Width-1)yield return i+1;if(i>=Width)yield return i-Width;if(i<Count-Width)yield return i+Width; }
    public CellState Cell(int i)=>new(i,mask[i],owners[i],challengers[i],progress[i],contested[i]);
    internal void Set(CellState c)
    { if(owners[c.Index]!=c.Owner)OwnershipRevision++;owners[c.Index]=c.Owner;challengers[c.Index]=c.Challenger;progress[c.Index]=c.Progress;contested[c.Index]=c.Contested; }
    // Initialization only; the simulation freezes its own root list.
    public void Seed(int index,int team)
    { if(!Available(index)||team is <0 or >1)throw new ArgumentException("Invalid seed");Set(new(index,true,team,-1,0,false)); }
    public TerritoryStateSnapshot Snapshot(SnapshotHeader header)=>new(header,Width,Height,CellSize,Origin,Enumerable.Range(0,Count).Select(Cell).ToArray());
    public IEnumerable<int> Within(Point2 center,double radius)
    {
        if(Index(center)<0)yield break;
        int x0=(int)Math.Max(0,Math.Floor((center.X-radius-Origin.X)/CellSize));
        int x1=(int)Math.Min(Width-1,Math.Floor((center.X+radius-Origin.X)/CellSize));
        int y0=(int)Math.Max(0,Math.Floor((center.Y-radius-Origin.Y)/CellSize));
        int y1=(int)Math.Min(Height-1,Math.Floor((center.Y+radius-Origin.Y)/CellSize));
        for(int y=y0;y<=y1;y++)for(int x=x0;x<=x1;x++)
        {int i=y*Width+x;if(mask[i]&&Center(i).DistanceSquared(center)<=radius*radius)yield return i;}
    }
}

public sealed class TerritorialInfluenceCalculator(ProfileCatalog catalog,Rules rules)
{
    public double Calculate(FrontlineUnit unit)
    {
        var profile=catalog.Find(unit.UnitId);
        if(profile is not {CanCaptureTerritory:true}||!unit.Alive||unit.Loaded||unit.Deserting||unit.AliveSoldierCount==0)return 0;
        double state=unit.Stress switch {Core.StressLevel.Calm=>rules.CalmCoefficient,Core.StressLevel.Shocked=>rules.ShockCoefficient,_=>rules.PanicCoefficient};
        double value=unit.AliveSoldierCount*profile.TypeCoefficient*state*unit.StatusCoefficient;
        Rules.Nonnegative(value,nameof(value));return value;
    }
}

public sealed class TerritorialConnectivity
{
    private readonly TerritoryGrid grid;private readonly TeamRoot[] roots;private readonly bool[][] connected;
    private long revision=-1;
    private readonly Queue<int> queue=new();
    public TerritorialConnectivity(TerritoryGrid grid,IEnumerable<TeamRoot> roots)
    {this.grid=grid;this.roots=roots.ToArray();connected=new[]{new bool[grid.Count],new bool[grid.Count]};}
    public void Refresh()
    {
        if(revision==grid.OwnershipRevision)return;
        foreach(var a in connected)Array.Clear(a,0,a.Length);
        var q=queue;q.Clear();
        for(int team=0;team<2;team++)
        {
            foreach(var root in roots.Where(x=>x.Team==team))
            {int i=grid.Index(root.Position);if(grid.Owner(i)==team&&!connected[team][i]){connected[team][i]=true;q.Enqueue(i);}}
            void Visit(int j){if(grid.Owner(j)==team&&!connected[team][j]){connected[team][j]=true;q.Enqueue(j);}}
            while(q.TryDequeue(out int i))
            {if(i%grid.Width>0)Visit(i-1);if(i%grid.Width<grid.Width-1)Visit(i+1);if(i>=grid.Width)Visit(i-grid.Width);if(i<grid.Count-grid.Width)Visit(i+grid.Width);}
        }
        revision=grid.OwnershipRevision;
    }
    public bool IsConnected(FrontlineUnit u)=>IsConnected(u.Team,grid.Index(u.Position));
    public bool IsConnected(int team,int index)=>grid.Available(index)&&connected[team][index];
    public bool CanAdvance(FrontlineUnit u)
    {
        int i=grid.Index(u.Position);
        if(!grid.Available(i))return false;var cells=connected[u.Team];
        return cells[i]||(i%grid.Width>0&&cells[i-1])||(i%grid.Width<grid.Width-1&&cells[i+1])||
            (i>=grid.Width&&cells[i-grid.Width])||(i<grid.Count-grid.Width&&cells[i+grid.Width]);
    }
    public int AreaCells(int team)=>connected[team].Count(x=>x);
}

public sealed class TerritoryCaptureSystem
{
    private sealed record Footprint(int Team,Point2 Position,double Radius,long Power,int[] Cells);
    private readonly TerritoryGrid grid;private readonly ProfileCatalog profiles;private readonly Rules rules;
    private readonly TerritorialInfluenceCalculator calculator;
    private readonly Dictionary<Core.UnitKey,Footprint> footprints=new();
    private readonly long[][] influence;private readonly SortedSet<int> active=new();
    public int LastDirtyCells {get;private set;}
    public TerritoryCaptureSystem(TerritoryGrid grid,ProfileCatalog profiles,Rules rules)
    {this.grid=grid;this.profiles=profiles;this.rules=rules;calculator=new(profiles,rules);influence=new[]{new long[grid.Count],new long[grid.Count]};}
    public void UpdateInfluence(IReadOnlyList<FrontlineUnit> units,TerritorialConnectivity territory)
    {
        var seen=new HashSet<Core.UnitKey>();var dirty=new HashSet<int>();
        foreach(var u in units)
        {
            double power=calculator.Calculate(u);
            if(power<=0||!territory.CanAdvance(u))continue;
            double radius=profiles.Find(u.UnitId)!.InfluenceRadius;
            long quantized=checked((long)Math.Round(power*1000,MidpointRounding.AwayFromZero));
            if(quantized==0)continue;seen.Add(u.Key);
            if(footprints.TryGetValue(u.Key,out var old)&&old.Team==u.Team&&old.Position==u.Position&&old.Radius==radius&&old.Power==quantized)continue;
            if(old!=null)Apply(old,-1,dirty);
            var next=new Footprint(u.Team,u.Position,radius,quantized,grid.Within(u.Position,radius).ToArray());
            footprints[u.Key]=next;Apply(next,1,dirty);
        }
        foreach(var key in footprints.Keys.Where(k=>!seen.Contains(k)).ToArray())
        {Apply(footprints[key],-1,dirty);footprints.Remove(key);}
        LastDirtyCells=dirty.Count;
    }
    private void Apply(Footprint footprint,int sign,HashSet<int> dirty)
    {foreach(int i in footprint.Cells){influence[footprint.Team][i]=checked(influence[footprint.Team][i]+sign*footprint.Power);active.Add(i);dirty.Add(i);}}
    public CellState[] Advance()
    {
        var changes=new List<CellState>();var remove=new List<int>();
        foreach(int i in active)
        {
            var old=grid.Cell(i);long a=influence[0][i],b=influence[1][i];
            bool contested=a>0&&b>0;
            long difference=Math.Abs(a-b);int winner=difference>rules.DominanceMargin?(a>b?0:1):-1;
            int challenger=old.Challenger,owner=old.Owner;long progress=old.Progress;
            // Progress is influence-milliseconds; threshold is influence-seconds * 1000.
            if(winner>=0&&winner!=owner)
            {
                if(challenger!=winner){challenger=winner;progress=0;}
                progress=checked(progress+checked(difference*rules.TickMilliseconds));
                if(progress>=rules.CaptureThreshold*1000){owner=winner;challenger=-1;progress=0;}
            }
            else
            {progress=Math.Max(0,progress-checked(rules.ProgressDecayPerSecond*rules.TickMilliseconds));if(progress==0)challenger=-1;}
            var next=new CellState(i,true,owner,challenger,progress,contested);
            if(next!=old){grid.Set(next);changes.Add(next);}
            if(a==0&&b==0&&progress==0)remove.Add(i);
        }
        foreach(int i in remove)active.Remove(i);
        return changes.ToArray();
    }
}

public sealed class LocalControlAreaSystem(ProfileCatalog profiles,Rules rules)
{
    public LocalControlArea[] Evaluate(IReadOnlyList<FrontlineUnit> units,TerritoryGrid grid,TerritorialConnectivity territory)
    {
        var calculator=new TerritorialInfluenceCalculator(profiles,rules);var areas=new List<LocalControlArea>();
        foreach(var u in units)
        {
            double power=calculator.Calculate(u);
            if(power<=0||!grid.Available(grid.Index(u.Position))||territory.CanAdvance(u))continue;
            double radius=Math.Min(rules.MaximumLocalRadius,Math.Sqrt(power)*rules.LocalRadiusPerSqrtInfluence);
            if(radius>0)areas.Add(new(u.Key,u.Team,u.Position,radius,areas.Count));
        }
        // Spatial buckets avoid an all-pairs enclave merge.
        var buckets=new SpatialIndex(rules.MaximumLocalRadius*2);
        int[] parent=Enumerable.Range(0,areas.Count).ToArray();
        int Find(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}
        for(int i=0;i<areas.Count;i++)
        {
            var area=areas[i];foreach(int j in buckets.Near(area.Center))
            {var other=areas[j];double radius=area.Radius+other.Radius;
                if(area.Team==other.Team&&area.Center.DistanceSquared(other.Center)<=radius*radius)
                {int x=Find(i),y=Find(j);parent[Math.Max(x,y)]=Math.Min(x,y);}}
            buckets.Add(area.Center,i);
        }
        return areas.Select((a,i)=>a with {Enclave=Find(i)}).ToArray();
    }
}

internal sealed class SpatialIndex(double size)
{
    private readonly Dictionary<(long,long),List<int>> buckets=new();
    private (long X,long Y) Key(Point2 p)=>((long)Math.Floor(p.X/size),(long)Math.Floor(p.Y/size));
    public void Add(Point2 p,int index){var k=Key(p);if(!buckets.TryGetValue(k,out var list))buckets[k]=list=new();list.Add(index);}
    public IEnumerable<int> Near(Point2 p)
    {var k=Key(p);for(long y=k.Y-1;y<=k.Y+1;y++)for(long x=k.X-1;x<=k.X+1;x++)
        if(buckets.TryGetValue((x,y),out var list))foreach(int i in list)yield return i;}
}
