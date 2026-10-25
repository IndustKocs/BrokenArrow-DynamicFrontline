using System.Diagnostics;
using System.Text.Json;
using DynamicFrontline.Integration;

static class Benchmarks
{
    public static void Run()
    {
        var results=new List<object>();
        foreach(int size in new[]{128,256})foreach(int count in new[]{100,300,500,1000})foreach(int period in new[]{100,250,1000})
        {
            using var f=new Fixture(size,period);var random=new Random(82026);
            f.Access.Units=Enumerable.Range(0,count).Select(i=>f.Unit(i+1,i%4==0?3:1,i%2,5+random.NextDouble()*(size*10-10),5+random.NextDouble()*(size*10-10))).ToArray();
            var rows=new List<double[]>();long bytes=0,serializationBytes=0;int gc0=GC.CollectionCount(0);var projection=new TeamSafeProjection();
            for(int step=0;step<70;step++)
            {
                for(int i=step%10;i<count;i+=10){var u=f.Access.Units[i];f.Access.Units[i]=u with{WorldPosition=u.WorldPosition with{X=Math.Clamp(u.WorldPosition.X+(step%2==0?2:-2),5,size*10-5)}};}
                long startBytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();bool ok=f.Bridge.Update((long)step*period);double total=timer.Elapsed.TotalMilliseconds;
                long tickBytes=GC.GetAllocatedBytesForCurrentThread()-startBytes;if(!ok)throw new Exception("Benchmark frame rejected: "+f.Bridge.Health.LastFailure);
                var frame=f.Bridge.LastGood!;timer.Restart();var safe=projection.Project(frame.Input,0,step);long before=GC.GetAllocatedBytesForCurrentThread();_ = JsonSerializer.Serialize(safe);
                double serialize=timer.Elapsed.TotalMilliseconds;
                if(step>=10){bytes+=tickBytes;serializationBytes+=GC.GetAllocatedBytesForCurrentThread()-before;rows.Add(new[]{total,frame.SnapshotMilliseconds,frame.CoreMilliseconds,frame.Result.Metrics.ConnectivityMs,serialize});}
            }
            object Stats(int col){var a=rows.Select(r=>r[col]).OrderBy(x=>x).ToArray();return new{median=a[29],p95=a[56],p99=a[59]};}
            results.Add(new{size,count,period,total=Stats(0),snapshot=Stats(1),core=Stats(2),graph=Stats(3),teamSerialization=Stats(4),allocatedBytesPerTick=bytes/60,
                serializationBytesPerTick=serializationBytes/60,managedHeapBytes=GC.GetTotalMemory(false),workingSetBytes=Environment.WorkingSet,gen0=GC.CollectionCount(0)-gc0,
                navigationSubmitted=f.Navigation.Submitted,navigationCached=f.Navigation.CachedCount});
            Console.WriteLine($"PoC02 {size} count={count} period={period} median={rows.Select(r=>r[0]).OrderBy(x=>x).ElementAt(29):F3}ms bytes={bytes/60}");
        }
        File.WriteAllText("artifacts/poc02/benchmarks.json",JsonSerializer.Serialize(new{seed=82026,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,results},new JsonSerializerOptions{WriteIndented=true}));
        // Microbenchmarks include profile lookup, DTO inspection, nav cache hits and identity cleanup separately.
        using var fixture=new Fixture();var profile=fixture.Profiles;var unit=fixture.Unit();
        object Measure(string name,Action action,int iterations){for(int i=0;i<20;i++)action();long b=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();for(int i=0;i<iterations;i++)action();return new{name,iterations,totalMs=sw.Elapsed.TotalMilliseconds,bytes=GC.GetAllocatedBytesForCurrentThread()-b};}
        var provider=fixture.Provider();fixture.Access.Units=new[]{unit};var nav=new NavRig();nav.Backend.AutoAnswer=true;nav.Begin(0);nav.Adapter.Query(nav.Query);nav.Adapter.Poll();
        var identity=new GameUnitIdentityResolver();var micro=new[]{Measure("profile-resolution",()=>profile.Resolve(1,30),10000),Measure("single-unit-copy",()=>provider.Capture(),1000),
            Measure("navigation-cache-hit",()=>nav.Adapter.Query(nav.Query),10000),Measure("lifecycle-accept-and-cleanup",()=>{identity.AcceptComplete(fixture.Match,new[]{unit});identity.AcceptComplete(fixture.Match,Array.Empty<RawGameUnit>());},1000)};
        File.WriteAllText("artifacts/poc02/microbenchmarks.json",JsonSerializer.Serialize(micro,new JsonSerializerOptions{WriteIndented=true}));
    }
}
