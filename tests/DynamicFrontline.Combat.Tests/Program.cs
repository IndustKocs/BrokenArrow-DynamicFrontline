using System.Text.Json;
using DynamicFrontline.Core;

static partial class Tests
{
    static readonly List<object> Results=new();
    static int failures;
    public static void Main(string[] args)
    {
        Run();
        Directory.CreateDirectory("artifacts/core021");
        File.WriteAllText("artifacts/core021/tests.json",JsonSerializer.Serialize(new{total=Results.Count,failed=failures,results=Results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"TOTAL {Results.Count}; FAILED {failures}");
        Environment.ExitCode=failures==0?0:1;
    }
    static void Run(){ Test("baseline F07 F08 F09 preserved",()=>{Eq(CombatFormulas.KineticDamage(10,100,100,1,.1f),10);Eq(CombatFormulas.DamageWithThreshold(2,2,false),0);Eq(CombatFormulas.Penetration(1,100,50,10,110,20),90);}); Stages(); }
    static partial void Stages();
    static void Test(string name,Action action){try{action();Results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
    static void Check(bool condition){if(!condition)throw new Exception("Assertion failed");}
    static void Eq(double actual,double expected,double tolerance=1e-5){if(!double.IsFinite(actual)||Math.Abs(actual-expected)>tolerance*Math.Max(1,Math.Abs(expected)))throw new Exception($"Expected {expected}, got {actual}");}
    static void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(ArithmeticException){return;}throw new Exception("Expected domain rejection");}
}
