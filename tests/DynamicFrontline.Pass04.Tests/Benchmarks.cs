using System.Diagnostics;
using System.Numerics;
using System.Text.Json;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
static partial class Tests
{
    static double benchmarkSink;
    static partial void Benchmark()
    {
        var rows=new List<object>();
        void Measure(string name,Action<int> action,int count=10000,int warmup=1000)
        {
            for(int i=0;i<warmup;i++)action(i);var times=new double[15];long bytes=0;
            for(int j=0;j<15;j++){long b=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();for(int i=0;i<count;i++)action(i);times[j]=(Stopwatch.GetTimestamp()-start)*1e9/Stopwatch.Frequency/count;bytes+=GC.GetAllocatedBytesForCurrentThread()-b;}
            Array.Sort(times);rows.Add(new{name,count,batches=15,medianNs=times[7],maxBatchNs=times[14],bytesPerCall=(double)bytes/(15*count)});
        }
        var cdf=new LinearTestCurve(new[]{new CurveKey(0,0),new(1,1)});var factory=new LinearInverseFactory();var t=Table();var random=new CyclicRandom(22004,7);var crit=CritTable();var stress=new SuppliedStressModifiers(BuffAccumulators.Reset with{MovementSpeed=.5f,RotateSpeed=.6f},"synthetic");
        Measure("CDF build 10001 keys and 1000 samples",i=>benchmarkSink=ArtilleryDispersionMath.Build(Source,cdf,factory)[250],10,2);
        var cache=new CorrectionTableCache();Measure("CDF cache hit",i=>benchmarkSink=cache.Get(Source,cdf,factory)[250]);
        Measure("Polar confirmed draw",i=>benchmarkSink=ArtilleryDispersionMath.Polar(t,random).Radius);
        Measure("FastDimond",i=>benchmarkSink=ArtilleryDispersionMath.FastDimond(random).Result.X);
        Measure("Trajectory deviation",i=>benchmarkSink=ArtilleryDispersionMath.Apply(Vector3.UnitZ,10,1000,20,40,i%2*.5f,-.5f).DistanceDelta);
        Measure("Buff pipeline",i=>benchmarkSink=BuffEvaluationPipeline.Evaluate(stress,new(1,1,1,1),crit).Final.RotateSpeed);
        Measure("Single sphere smoke",i=>benchmarkSink=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(5,i%2,0),2).End);
        Measure("Aircraft interpolation",i=>benchmarkSink=AircraftMath.CalculateRate(i%400,new(100,200,300),new(10,20,15)).Rate);
        Measure("Astar priority",i=>benchmarkSink=NavigationMath.AstarPriority(i%100,4,10).Priority);
        var observation=new LifecycleObservation(new(Guid.NewGuid(),1,7,1),1,10,false,false,false,false,false,true);
        Measure("Identity validation",i=>benchmarkSink=(int)IdentityValidation.Validate(observation,observation));
        Measure("H1 finite research",i=>benchmarkSink=H1FiniteResearch.Evaluate(i%100).Result);
#if DEBUG
        string configuration="Debug";
#else
        string configuration="Release";
#endif
        Directory.CreateDirectory("artifacts/core022");File.WriteAllText($"artifacts/core022/micro-{configuration}.json",JsonSerializer.Serialize(new{configuration,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,rows,benchmarkSink},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"{configuration}: {rows.Count} microbenchmarks completed");
    }
}
