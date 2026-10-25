using System.Diagnostics;
using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
static class Microbenchmarks
{
    static float sink;
    public static void Run()
    {
        var rows=new List<object>();
        void Measure(string name,Action<int> action,int iterations=10000)
        {
            for(int i=0;i<1000;i++)action(i);var times=new double[15];long allocated=0;
            for(int batch=0;batch<15;batch++){long bytes=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();for(int i=0;i<iterations;i++)action(i);times[batch]=(Stopwatch.GetTimestamp()-start)*1e9/Stopwatch.Frequency/iterations;allocated+=GC.GetAllocatedBytesForCurrentThread()-bytes;}
            Array.Sort(times);rows.Add(new{name,iterations,batches=15,medianNanoseconds=times[7],p95BatchNanoseconds=times[14],allocatedBytesPerCall=(double)allocated/(15*iterations)});
        }
        Measure("HEAT",i=>sink=CombatFormulas.HeatDamage(10,100+i%100,100,1,2));
        Measure("Kinetic",i=>sink=CombatFormulas.KineticDamage(10,100+i%100,150,1,.1f));
        Measure("AoE",i=>sink=ExplosionMath.Falloff(i%100,45));
        Measure("Infantry count",i=>sink=InfantryMath.Count(i%80,80,8,8).Desired);
        Measure("Countermeasures",i=>sink=CombatFormulas.Countermeasures(.2f,.5f,i%5));
        Measure("Missile composition",i=>sink=CombatFormulas.MissileHitChance(.8f,.9f,.16f,1,1,1+i%2));
        var soldiers=Enumerable.Repeat(new SoldierState(true,1,4),8).ToArray();
        Measure("Ammunition",i=>sink=InfantryMath.Ammunition(soldiers,4,10,i%100,20,18).CurrentAmmo);
        Measure("Critical attempts",i=>sink=CriticalMath.Attempts(i%300,.1f,.02f,.5).Attempts);
        var rng=new CyclicRandom(21021,7);Measure("CF18 next",i=>sink=rng.NextFloat(0,1));
        Measure("Helicopter",i=>sink=AircraftMath.ShiftFinite(default,new(10,0,0),8,(i%5)*.1f).Result.X);
        Measure("Fuel requested",i=>sink=AircraftMath.Fuel(100,i%10,.5f).Requested);
        Measure("Air range",i=>sink=VisibilityMath.Air(2000,1.5f,.5f,.5f,.8f));
        var intervals=new[]{new SmokeInterval(0,2,3),new SmokeInterval(4,5,3)};
        Measure("Smoke intervals",i=>sink=VisibilityMath.Smoke(intervals,.5f).Cost);
        var sr=new ConstantRandom();Measure("Critical selection with trace",i=>sink=CriticalMath.Select(new(0,0,0,0),3,0,sr).Levels.Optics,1000);
        using var f=new Fixture(256,250);var random=new Random(82026);
        f.Access.Units=Enumerable.Range(0,1000).Select(i=>f.Unit(i+1,i%4==0?3:1,i%2,5+random.NextDouble()*2550,5+random.NextDouble()*2550)).ToArray();
        var totals=new List<double>();long bytesSum=0;
        for(int step=0;step<70;step++)
        {
            for(int i=step%10;i<1000;i+=10){var u=f.Access.Units[i];f.Access.Units[i]=u with{WorldPosition=u.WorldPosition with{X=Math.Clamp(u.WorldPosition.X+(step%2==0?2:-2),5,2555)}};}
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();if(!f.Bridge.Update(step*250))throw new Exception("Rejected benchmark");double ms=watch.Elapsed.TotalMilliseconds;long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
            // Same own-only projection/serialization between steps as PoC02 benchmark.
            _=JsonSerializer.Serialize(projection.Project(f.Bridge.LastGood!.Input,0,step));
            if(step>=10){totals.Add(ms);bytesSum+=bytes;}
        }
        totals.Sort();Directory.CreateDirectory("artifacts/core021");
#if DEBUG
        string configuration="Debug";
#else
        string configuration="Release";
#endif
        File.WriteAllText($"artifacts/core021/benchmarks-{configuration}.json",JsonSerializer.Serialize(new{configuration,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,microbenchmarks=rows,integration=new{size=256,count=1000,period=250,median=totals[29],p95=totals[56],p99=totals[59],allocatedBytesPerTick=bytesSum/60},sink},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{configuration}: {rows.Count} microbenchmarks; integration median {totals[29]:F3} ms, {bytesSum/60} bytes/tick");
    }
    static readonly DynamicFrontline.Integration.TeamSafeProjection projection=new();
    sealed class ConstantRandom:ICombatRandom{public int NextIndex(int max)=>0;public double NextUnit()=>.5;}
}
