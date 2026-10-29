using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
using static Fixtures023;
public static class GeometryOracleTests
{
    // Independent continuous point-in-polygon oracle. Probe points are >400m from the
    // prescribed separator; raster tolerance is one cell. Does not call detector code.
    static bool Inside(Point2 p,Point2[] polygon){bool result=false;for(int i=0,j=polygon.Length-1;i<polygon.Length;j=i++){var a=polygon[i];var b=polygon[j];if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)result=!result;}return result;}
    public static void Run(Action<string,Action> test,Action<bool> assert)
    {
        foreach(int cell in new[]{50,100})foreach(int turn in new[]{0,1,2,3})foreach(bool mirror in new[]{false,true})test($"Independent concave polygon oracle {cell}m rotation {turn} mirror {mirror}",()=>{
            Point2 Transform(Point2 p){double x=p.X-3200,y=p.Y-3200;if(mirror)x=-x;for(int i=0;i<turn;i++)(x,y)=(-y,x);return new(x+3200,y+3200);}
            Point2[] polygon=[new(1600,1600),new(4800,1600),new(4800,2800),new(2800,2800),new(2800,4800),new(1600,4800)];polygon=polygon.Select(Transform).ToArray();var units=new List<Unit023>();long id=100;
            for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];int n=(int)Math.Ceiling(Math.Sqrt(a.DistanceSquared(b))/250);for(int k=0;k<n;k++)units.Add(Unit(id++,a.X+(b.X-a.X)*k/n,a.Y+(b.Y-a.Y)*k/n,1,Tank));}
            var map=Map(6400/cell,cell);var z=CombinedArmsRadiusResolver.Resolve(units,map,new());var field=new ZocInfluenceField(map);field.Update(z);var detector=new EncirclementRegionDetector(map,new());
            foreach(var original in new[]{new Point2(2200,2200),new Point2(4200,4200)}){var p=Transform(original);var expected=Inside(p,polygon);var result=detector.Evaluate(Unit(1,p.X,p.Y,p:Infantry with{CanProjectZoc=false}),z,field);assert(expected?result.Enclosed==Evidence.True:result.Enclosed!=Evidence.True);}
        });
        test("Native missing topology is Unknown rather than blocked proof",()=>{var m=Map(8,50);m.NativeEdges=[[],[]];var u=Unit(1,225,225);var z=CombinedArmsRadiusResolver.Resolve([u],m,new());var field=new ZocInfluenceField(m);field.Update(z);assert(new EncirclementRegionDetector(m,new()).Evaluate(u,z,field).Enclosed!=Evidence.True);m.Passable[0][m.Grid.Index(u.Position)]=false;m.Revision++;assert(new EncirclementRegionDetector(m,new()).Evaluate(u,z,field).Corridor==CorridorState.Unknown);});
    }
}
