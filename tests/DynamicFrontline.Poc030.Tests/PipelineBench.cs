using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
static class PipelineBench
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);var results=new List<object>();var geometric=new List<object>();
        foreach(double cell in new[]{50d,100d})
        {
            var map=new PreparedTerrain(new((int)(3200/cell),(int)(3200/cell),cell),[new(0,new(25,1625)),new(1,new(3175,1625))]);
            foreach(bool gap in new[]{false,true}){var answer=Fixtures023.Geometry(Fixtures023.Ring(gap:gap),map);geometric.Add(new{cell,gap,enclosed=answer.Enclosed.ToString(),corridor=answer.Corridor.ToString(),physicalMap="3200x3200; same roots and unit coordinates"});}
        }
        foreach(int count in new[]{100,500,1000})foreach(double cell in new[]{50d,100d})
        {
            var map=new PreparedTerrain(new((int)(3200/cell),(int)(3200/cell),cell),[new(0,new(25,1625)),new(1,new(3175,1625))]);
            var sim=Fixtures023.Simulation(map);var rules=new Rules023Config();
            var units=Enumerable.Range(0,count).Select(i=>Fixtures023.Unit(i+1,600+(i/2%20)*95,600+(i/40)*90,i/2%2,i%2==0?Fixtures023.Infantry:Fixtures023.Ifv)).ToArray();
            var durations=new List<double>();long allocated=GC.GetTotalAllocatedBytes(true);var beforeGc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();int maxBytes=0;
            for(int tick=0;tick<32;tick++)
            {
                var watch=Stopwatch.StartNew();
                units=units.Select((u,i)=>u with {Position=new(u.Position.X+(tick%2==0?1:-1),u.Position.Y),Alive=tick<24||i%20!=0}).ToArray();
                var input=new Input023(Fixtures023.Session,rules.Version,rules.Hash,tick,tick*.25,(tick+1)*.25,units,true,[],[],[]);
                _=SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input));sim.Step(input);
                var a=JsonSerializer.SerializeToUtf8Bytes(sim.Project(0,true));var b=JsonSerializer.SerializeToUtf8Bytes(sim.Project(1,true));maxBytes=Math.Max(maxBytes,a.Length+b.Length);
                _=SHA256.HashData(a);_=SHA256.HashData(b);durations.Add(watch.Elapsed.TotalMilliseconds);
            }
            var samples=durations.OrderBy(x=>x).ToArray();double P(double p)=>samples[(int)((samples.Length-1)*p)];
            var result=new{count,cell,physicalWidth=3200,physicalHeight=3200,samples=samples.Length,p50=P(.5),p95=P(.95),p99=P(.99),max=samples[^1],allocatedBytes=GC.GetTotalAllocatedBytes(false)-allocated,retainedBytes=GC.GetTotalMemory(true),gc=Enumerable.Range(0,3).Select(i=>GC.CollectionCount(i)-beforeGc[i]).ToArray(),maxProjectionBytes=maxBytes,scope="Offline full Step + canonical serialization/hash + both player projections; excludes live reader/HUD/socket/disk"};results.Add(result);
            Console.WriteLine(JsonSerializer.Serialize(result));File.WriteAllText(Path.Combine(output,"pipeline.json"),JsonSerializer.Serialize(new {Results=results,Geometry=geometric,RuntimePerformance="NotMeasured",Soak2700="NotMeasured_32ticksOnly"},new JsonSerializerOptions{WriteIndented=true}));
        }
    }
}
