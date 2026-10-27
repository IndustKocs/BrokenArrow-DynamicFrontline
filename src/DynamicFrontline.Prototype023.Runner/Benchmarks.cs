using System.Diagnostics;
using System.Text.Json;
using System.IO.Compression;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
public static class Benchmarks
{
    public static void Run(string output,int updates,int repeats)
    {
        Check.That(updates>=10000&&repeats>=3,"Long evidence requires >=10000 updates and >=3 repeats");Directory.CreateDirectory(output);
        var results=new List<object>();var session=Guid.Parse("62b88734-f7c0-4000-8000-000000000023");var rules=new Rules023Config();
        using var raw=new StreamWriter(new GZipStream(File.Create(Path.Combine(output,"samples.csv.gz")),CompressionLevel.Fastest));
        raw.WriteLine("units,size,cell,repeat,tick,moving,resolver_ms,field_ms,area_ms,army_ms,victory_ms,without_victory_ms,with_victory_ms");
        foreach(int count in new[]{100,300,500,1000})foreach(int size in new[]{128,256})foreach(double cell in new[]{50d,100d})for(int rep=0;rep<repeats;rep++)
        {
            var map=new PreparedTerrain(new(size,size,cell),[new(0,new(cell/2,cell/2)),new(1,new((size-.5)*cell,(size-.5)*cell))]);
            var profile=new Profile023("synthetic-tank",false,false,true,false,false,Mobility.GroundVehicle,10,MoraleClass.NativePassthrough,true,1000,.5f,.8f,"Synthetic benchmark");
            var rng=new Random(2300+rep);var units=Enumerable.Range(0,count).Select(i=>new Unit023(new(session,i,0),i%2,i%2,2,profile,new(cell+rng.NextDouble()*(size-2)*cell,cell+rng.NextDouble()*(size-2)*cell),0,100,100)).ToArray();
            var resolved=CombinedArmsRadiusResolver.Resolve(units,map,rules);var field=new ZocInfluenceField(map);field.Update(resolved);
            var area=new TerritoryScoringProjection(map.Grid,Enumerable.Repeat(true,map.Grid.Count).ToArray());area.Update([]);var victory=new VictorySimulation(session,new());
            var durations=Enumerable.Range(0,7).Select(_=>new double[updates]).ToArray();var initial=GC.GetTotalAllocatedBytes(true);var gc=Enumerable.Range(0,3).Select(GC.CollectionCount).ToArray();
            for(int tick=0;tick<updates;tick++)
            {
                // 4 Hz host; 1 Hz influence updates. Alternate 10% and 100% moving source batches.
                int moving=tick<updates/2?Math.Max(1,count/10):count;var t=Stopwatch.GetTimestamp();
                if(tick%4==0){for(int i=0;i<moving;i++){var u=units[i];units[i]=u with{Position=new(Math.Clamp(u.Position.X+(tick%8==0?3:-3),cell,(size-1)*cell),u.Position.Y)};}resolved=CombinedArmsRadiusResolver.Resolve(units,map,rules);}
                var r=Stopwatch.GetTimestamp();field.Update(resolved);var f=Stopwatch.GetTimestamp();area.Update([]);var a=Stopwatch.GetTimestamp();var army=ActiveArmyValueProvider.Evaluate(units,true,new());var ar=Stopwatch.GetTimestamp();
                victory.Advance(new(session,rules.Version,tick,tick*.25,(tick+1)*.25,[0,0],true,army,[Evidence.False,Evidence.False],[]));var v=Stopwatch.GetTimestamp();
                double Ms(long x,long y)=>(y-x)*1000d/Stopwatch.Frequency;
                var values=new[]{Ms(t,r),Ms(r,f),Ms(f,a),Ms(a,ar),Ms(ar,v),Ms(t,ar),Ms(t,v)};
                for(int k=0;k<values.Length;k++)durations[k][tick]=values[k];
                raw.WriteLine($"{count},{size},{cell},{rep},{tick},{moving},"+string.Join(",",values.Select(x=>x.ToString("R"))));
            }
            var allocated=GC.GetTotalAllocatedBytes(false)-initial;var collections=Enumerable.Range(0,3).Select(i=>GC.CollectionCount(i)-gc[i]).ToArray();
            // Cold geometry/serialization is measured independently, never hidden in hot-cache averages.
            var geometry=new double[128];var serialize=new double[128];int unknown=0;
            for(int n=0;n<128;n++){var sw=Stopwatch.StartNew();var answer=new EncirclementRegionDetector(map,rules).Evaluate(units[0],resolved,field);geometry[n]=sw.Elapsed.TotalMilliseconds;if(answer.Enclosed==Evidence.Unknown)unknown++;sw.Restart();_ = JsonSerializer.Serialize(new{answer.Corridor,answer.Enclosed,victory= new{victory.State.Time,victory.State.U}});serialize[n]=sw.Elapsed.TotalMilliseconds;}
            var reference=ZocInfluenceField.Reference(map,resolved);double error=0;for(int team=0;team<2;team++)for(int i=0;i<map.Grid.Count;i++)error=Math.Max(error,Math.Abs(reference[team][i]-field.Values[team][i]));Check.That(error<1e-7,"Benchmark field/reference mismatch");
            var summary=new{count,size,cell,repeat=rep,updates,allocatedBytes=allocated,gc=collections,referenceMaxError=error,stages=durations.Select(Stats).ToArray(),coldGeometry=Stats(geometry),projectionSerialization=Stats(serialize),geometryUnknown=unknown};results.Add(summary);
            File.WriteAllText(Path.Combine(output,$"geometry-{count}-{size}-{cell}-{rep}.json"),JsonSerializer.Serialize(new{geometry,serialize}));
            Console.WriteLine($"{count} units {size}x{size} {cell}m repeat {rep}: {updates} updates; p95={Stats(durations[6]).P95:F3}ms; geometry={Stats(geometry).P95:F3}ms");
            File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(new{environment=new{runtime=Environment.Version.ToString(),os=Environment.OSVersion.ToString(),cpu=Environment.ProcessorCount},stageOrder=new[]{"resolver","field","area","army","victory","withoutVictory","withVictory"},results},Formats.Json));
        }
    }
    public sealed record Distribution(int N,double Mean,double P50,double P95,double P99,double Max);
    static Distribution Stats(double[] samples){var a=samples.OrderBy(x=>x).ToArray();return new(a.Length,a.Average(),a[(int)((a.Length-1)*.5)],a[(int)((a.Length-1)*.95)],a[(int)((a.Length-1)*.99)],a[^1]);}
}
