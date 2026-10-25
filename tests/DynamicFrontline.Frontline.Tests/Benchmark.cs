using System.Diagnostics;
using System.Text.Json;
using DynamicFrontline.Frontline;

static class Benchmark
{
    public static void Run()
    {
        var results=new List<object>();
        foreach(int size in new[]{128,256})foreach(int count in new[]{100,300,500,1000})foreach(int ms in new[]{100,250,1000})
        {
            var f=new Fixture(Fixture.DefaultRules with{TickMilliseconds=ms,LinkDistance=45},size:size);
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)if(x<size/2-2||x>size/2+2)f.Grid.Seed(y*size+x,x<size/2?0:1);
            var random=new Random(62026);var units=Enumerable.Range(0,count).Select(i=>f.Unit(i+1,i%4==0?4:1,
                random.NextDouble()*(size*10-10)+5,random.NextDouble()*(size*10-10)+5,i%2)).ToArray();
            var samples=new List<UpdateMetrics>();var total=new List<double>();long allocated=0;int[] collections={GC.CollectionCount(0),GC.CollectionCount(1),GC.CollectionCount(2)};
            for(int step=0;step<70;step++)
            {
                for(int i=step%10;i<count;i+=10)
                {var p=units[i].Position;units[i]=units[i] with {Position=new(Math.Clamp(p.X+(step%2==0?3:-3),5,size*10-5),p.Y)};}
                long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();var frame=f.Step(units);sw.Stop();
                if(step>=10){allocated+=GC.GetAllocatedBytesForCurrentThread()-before;samples.Add(frame.Metrics);total.Add(sw.Elapsed.TotalMilliseconds);}
            }
            object Stats(IEnumerable<double> values){var a=values.OrderBy(x=>x).ToArray();double At(double q)=>a[(int)Math.Ceiling(q*a.Length)-1];return new{median=At(.5),p95=At(.95),p99=At(.99)};}
            results.Add(new{size,units=count,tickMilliseconds=ms,samples=samples.Count,
                influence=Stats(samples.Select(x=>x.InfluenceMs)),capture=Stats(samples.Select(x=>x.CaptureMs)),connectivity=Stats(samples.Select(x=>x.ConnectivityMs)),
                boundary=Stats(samples.Select(x=>x.BoundaryMs)),other=Stats(samples.Select(x=>x.OtherMs)),total=Stats(total),
                allocatedBytesPerTick=allocated/samples.Count,managedHeapBytes=GC.GetTotalMemory(false),workingSetBytes=Environment.WorkingSet,
                gen0=GC.CollectionCount(0)-collections[0],gen1=GC.CollectionCount(1)-collections[1],gen2=GC.CollectionCount(2)-collections[2],
                meanCpuMillisecondsPerSimulatedSecond=total.Average()*1000/ms});
            Console.WriteLine($"BENCH {size}x{size} units={count} tick={ms}ms median={total.OrderBy(x=>x).ElementAt(29):F3}ms allocated={allocated/samples.Count} bytes/tick");
        }
        // Forced geometry rebuild, separate from the production cache-hit measurements above.
        var boundaryCases=new List<object>();foreach(int size in new[]{128,256})
        {
            var grid=new TerritoryGrid(size,size,10);for(int i=0;i<grid.Count;i++)grid.Seed(i,((i%size)/8+(i/size)/8)%2);
            var extractor=new FrontlineBoundaryExtractor();var times=new List<double>();long bytes=0;int paths=0;
            for(int i=0;i<35;i++){long before=GC.GetAllocatedBytesForCurrentThread();var sw=Stopwatch.StartNew();paths=extractor.Extract(grid).Length;sw.Stop();if(i>=5){times.Add(sw.Elapsed.TotalMilliseconds);bytes+=GC.GetAllocatedBytesForCurrentThread()-before;}}
            times.Sort();boundaryCases.Add(new{size,paths,median=times[14],p95=times[28],p99=times[29],allocatedBytesPerBuild=bytes/30});
        }
        File.WriteAllText("artifacts/benchmarks.json",JsonSerializer.Serialize(new{seed=62026,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            operatingSystem=Environment.OSVersion.ToString(),processorCount=Environment.ProcessorCount,results,boundaryCases},new JsonSerializerOptions{WriteIndented=true}));
    }
}
