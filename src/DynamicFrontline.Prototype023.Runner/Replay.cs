using System.Text;
using System.Text.Json;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
public sealed record SeedRectangle(int Team,int X,int Y,int Width,int Height);
public sealed record UnitChange(double Time,long Uid,Point2? Position=null,bool? Alive=null,bool? Visible=null);
public sealed record Scenario(string Name,string Provenance,Guid Session,int Size,double CellSize,double Duration,Rules023Config Rules,VictoryConfig Victory,
    UnitTerritoryProfile[] TerritoryProfiles,Unit023[] Units,SeedRectangle[] Seeds,UnitChange[] Changes,ServiceInterval[] Services,NativeScoreEvent[] NativeEvents);
public sealed record DisplayUnit(long Id,int Team,double X,double Y,double Radius,double Power,bool Visible);
public sealed record DisplayFrame(double Time,DisplayUnit[] Units,OwnEncirclement[] Own,double[] Fractions,double[][] U,decimal[] Native,MatchEndIntent? End,string[] Geometry,string[] Notices,int[] Owners);
public static class Replay
{
    public static (Simulation023 Sim,PreparedTerrain Terrain) Create(Scenario s)
    {
        var g=new TerritoryGrid(s.Size,s.Size,s.CellSize);
        var t=new PreparedTerrain(g,[new(0,new(s.CellSize/2,(s.Size/2+.5)*s.CellSize)),new(1,new((s.Size-.5)*s.CellSize,(s.Size/2+.5)*s.CellSize))]);
        foreach(var r in s.Seeds) for(int y=r.Y;y<r.Y+r.Height;y++)for(int x=r.X;x<r.X+r.Width;x++)g.Seed(y*s.Size+x,r.Team);
        return(new(s.Session,s.Rules,s.Victory,t,new ProfileCatalog(s.TerritoryProfiles),new Rules{TickMilliseconds=250},Enumerable.Repeat(true,g.Count).ToArray()),t);
    }
    public static void Run(Scenario s,string output)
    {
        Check.That(!string.IsNullOrWhiteSpace(s.Provenance)&&s.Duration>0&&s.Duration<=2700&&s.Duration*4==Math.Floor(s.Duration*4),"Invalid scenario");
        Check.That(s.Changes.All(c=>c.Time>=0 && c.Time<s.Duration && c.Time*4==Math.Floor(c.Time*4)) && s.Services.All(e=>e.Start>=0 && e.Start<s.Duration && e.Start*4==Math.Floor(e.Start*4)) && s.NativeEvents.All(e=>e.Time>=0 && e.Time<=s.Duration),"Replay event outside supported cadence/range");
        Directory.CreateDirectory(output); var (sim,terrain)=Create(s);var units=s.Units.ToArray();
        var frames=new List<DisplayFrame>();var publics=new List<Public023>();var log=new StringBuilder("time,ownState,relief,tau,stress,fraction0,fraction1,weighted0,weighted1,end\n");
        Simulation023? resumed=null; Checkpoint023? checkpoint=null;int steps=0;
        for(int i=0;i<s.Duration*4;i++)
        {
            double start=i*.25,end=start+.25;
            foreach(var c in s.Changes.Where(c=>c.Time==start)){int index=Array.FindIndex(units,u=>u.Key.Uid==c.Uid);Check.That(index>=0,"Unknown change unit");var u=units[index];units[index]=u with{Position=c.Position??u.Position,Alive=c.Alive??u.Alive,Visible=c.Visible??u.Visible};}
            var input=new Input023(s.Session,s.Rules.Version,s.Rules.Hash,i,start,end,units,true,s.Services.Where(e=>e.Start==start).ToArray(),[],s.NativeEvents.Where(e=>e.Time>start&&e.Time<=end || start==0&&e.Time==0).ToArray());
            var frame=sim.Step(input);steps++;
            if(resumed!=null)resumed.Step(input);
            if(i==(int)(s.Duration*2)-1&&frame.Victory.End==null){checkpoint=JsonSerializer.Deserialize<Checkpoint023>(JsonSerializer.Serialize(sim.Checkpoint(),Formats.Json),Formats.Json)!;resumed=Create(s).Sim;resumed.Restore(checkpoint);}
            var own=sim.Project(0).Own.FirstOrDefault();
            log.AppendLine($"{frame.Time:R},{own?.State},{own?.Relief},{own?.Tau:R},{own?.Stress:R},{frame.ScoreFractions[0]:R},{frame.ScoreFractions[1]:R},{sim.Victory.Weighted(0):R},{sim.Victory.Weighted(1):R},{frame.Victory.End?.Status}");
            if(i==0||end%5==0||frame.Notices.Length>0||frame.Victory.End!=null)
            {
                frames.Add(new(frame.Time,frame.Zoc.Select(z=>new DisplayUnit(z.Unit.Key.Uid,z.Unit.Team,z.Unit.Position.X,z.Unit.Position.Y,z.Radius,z.Power,z.Unit.Visible)).ToArray(),sim.Project(0).Own,frame.ScoreFractions,frame.Victory.U,frame.Victory.Native,frame.Victory.End,frame.Geometry.Select(g=>g.Corridor+"/"+g.Enclosed).ToArray(),frame.Notices.Select(n=>n.Team+":"+n.Kind).ToArray(),sim.Score.EffectiveOwner.ToArray()));
                publics.Add(sim.Project(0));
            }
            if(frame.Victory.End!=null)break;
        }
        var final=sim.Checkpoint(); bool continued=resumed!=null&&JsonSerializer.Serialize(final,Formats.Json)==JsonSerializer.Serialize(resumed.Checkpoint(),Formats.Json);
        Check.That(continued,"Serialized checkpoint continuation differs");
        Write("scenario.json",s);Write("checkpoint.json",checkpoint);Write("final.json",final);Write("frames-debug.json",frames);Write("frames-player.json",publics);
        Write("result.json",new{s.Name,steps,checkpointResumeEqual=continued,final.Time,sim.Victory.State.End,weighted=new[]{sim.Victory.Weighted(0),sim.Victory.Weighted(1)},rulesHash=s.Rules.Hash,provenance=s.Provenance});
        File.WriteAllText(Path.Combine(output,"replay.csv"),log.ToString());
        Preview.Write(output,s,frames,publics);
        Console.WriteLine($"{s.Name}: {steps} ticks; t={final.Time}; checkpoint equal; {final.Victory.End?.Status??"running"}");
        void Write(string name,object? value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,Formats.Json));
    }
}
