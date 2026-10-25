using System.Diagnostics;
using System.Text.Json;
using DynamicFrontline.Integration;
using System.Security.Cryptography;

// Same harness binary is run with baseline and current DLL sets in separate local directories.
var rows=new List<object>();
foreach(int size in new[]{128,256})foreach(int count in new[]{100,300,500,1000})foreach(int period in new[]{100,250,1000})
{
    using var f=new Fixture(size,period);var rng=new Random(82026);var projection=new TeamSafeProjection();
    f.Access.Units=Enumerable.Range(0,count).Select(i=>f.Unit(i+1,i%4==0?3:1,i%2,5+rng.NextDouble()*(size*10-10),5+rng.NextDouble()*(size*10-10))).ToArray();
    var time=new List<double>();long allocations=0;int[] beforeGc=new int[3];
    for(int step=0;step<70;step++)
    {
        for(int i=step%10;i<count;i+=10){var u=f.Access.Units[i];f.Access.Units[i]=u with{WorldPosition=u.WorldPosition with{X=Math.Clamp(u.WorldPosition.X+(step%2==0?2:-2),5,size*10-5)}};}
        if(step==10)for(int g=0;g<3;g++)beforeGc[g]=GC.CollectionCount(g);
        long before=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();if(!f.Bridge.Update((long)step*period))throw new Exception("Rejected benchmark frame");double ms=timer.Elapsed.TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        _=JsonSerializer.Serialize(projection.Project(f.Bridge.LastGood!.Input,0,step));
        if(step>=10){time.Add(ms);allocations+=bytes;}
    }
    time.Sort();rows.Add(new{size,count,period,median=time[29],p95=time[56],p99=time[59],bytesPerTick=allocations/60,gc=Enumerable.Range(0,3).Select(g=>GC.CollectionCount(g)-beforeGc[g]).ToArray(),workingSet=Environment.WorkingSet,managedHeap=GC.GetTotalMemory(false)});
}
var hashes=new Dictionary<string,string>();foreach(var name in new[]{"DynamicFrontline.Core.dll","DynamicFrontline.Frontline.dll","DynamicFrontline.GameAdapter.dll","DynamicFrontline.Integration.dll"})hashes[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,name))));
File.WriteAllText(args[0],JsonSerializer.Serialize(new{seed=82026,warmup=10,samples=60,gcScope="60 measured iterations including own-only serialization between steps",hashes,rows},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("24 scenarios completed: "+args[0]);
