using System.Text.Json;
static partial class Tests
{
    static readonly List<object> results=new();static int failed;
    static void Main(string[] args)
    {
        if(args.Contains("--benchmark")){Benchmark();return;}
        Artillery();Buffs();Smoke();Aircraft();Navigation();Explorer();Compatibility();
        Directory.CreateDirectory("artifacts/core022");
        File.WriteAllText("artifacts/core022/tests.json",JsonSerializer.Serialize(new{total=results.Count,failed,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"TOTAL {results.Count}; FAILED {failed}");Environment.ExitCode=failed==0?0:1;
    }
    static partial void Artillery();static partial void Buffs();static partial void Smoke();static partial void Aircraft();static partial void Navigation();static partial void Explorer();static partial void Compatibility();static partial void Benchmark();
    static void Test(string name,Action action){try{action();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Check(bool x){if(!x)throw new Exception("Assertion failed");}
    static void Eq(double a,double b,double tolerance=2e-5){if(!double.IsFinite(a)||Math.Abs(a-b)>tolerance*Math.Max(1,Math.Abs(b)))throw new Exception($"Expected {b}, got {a}");}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(ArithmeticException){return;}throw new Exception("Expected rejection");}
}
