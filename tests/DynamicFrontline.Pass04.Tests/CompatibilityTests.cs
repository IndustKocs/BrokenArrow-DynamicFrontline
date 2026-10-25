using System.Numerics;
using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Core.ExperimentalCombat;
using DynamicFrontline.Core.Diagnostics;
using DynamicFrontline.Frontline;
using DynamicFrontline.Integration;
static partial class Tests
{
    static partial void Compatibility()
    {
        Test("3000 independent Python reference computations",()=>{
            using var doc=JsonDocument.Parse(File.ReadAllText("config/core022/reference.json"));foreach(var row in doc.RootElement.EnumerateArray())
            {
                var p=row.GetProperty("inputs").EnumerateArray().Select(x=>x.GetSingle()).ToArray();var expected=row.GetProperty("expected");
                switch(row.GetProperty("formula").GetString())
                {
                    case "astar":Eq(NavigationMath.AstarPriority(p[0],p[1],p[2]).Priority,expected.GetDouble(),1e-12);break;
                    case "aircraft":Eq(AircraftMath.CalculateRate(p[0],new(100,200,300),new(10,20,15)).Rate,expected.GetDouble());break;
                    case "diamond":var d=ArtilleryDispersionMath.FastDimond(new ScriptFloat(p[0],p[1]));Eq(d.Result.X,expected[0].GetDouble());Eq(d.Result.Y,expected[1].GetDouble());break;
                    case "smoke":var s=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(p[0],0,0),p[1]);Check(s.HasInterval==(expected.ValueKind!=JsonValueKind.Null));if(s.HasInterval){Eq(s.Start,expected[0].GetDouble());Eq(s.End,expected[1].GetDouble());}break;
                    case "trajectory":var t=ArtilleryDispersionMath.Apply(Vector3.UnitZ,10,1000,20,40,p[0],p[1]);Eq(t.Direction.X,expected[0].GetDouble());Eq(t.DistanceDelta,expected[1].GetDouble());break;
                    case "h1":Eq(H1FiniteResearch.Evaluate(p[0]).Result,expected.GetDouble(),1e-7);break;
                }
            }
        });
        Test("CDF valid monotone property across 12 synthetic curves",()=>{for(int n=1;n<=12;n++){float middle=n/16f;var t=ArtilleryDispersionMath.Build(Source,new LinearTestCurve(new[]{new CurveKey(0,0),new(.5f,middle),new(1,1)}),new LinearInverseFactory());Check(t.Count==1000);for(int i=1;i<1000;i++)Check(t[i]>=t[i-1]);}});
        Test("Pass04 twenty paired integration steps preserve all public and core outputs",()=>{
            var match=Guid.Parse("00000000-0000-0000-0000-000000000022");using var a=new Fixture(match:match);using var b=new Fixture(match:match);var pa=new TeamSafeProjection();var pb=new TeamSafeProjection();var table=Table();var rng=new CyclicRandom(22,4);var profiles=JsonSerializer.Serialize(a.Profiles.Document);
            for(int i=0;i<20;i++)
            {
                var units=new[]{a.Unit() with{Deserting=i>=7&&i<12},a.Unit(2,3,x:35),a.Unit(3,2,x:65),a.Unit(4,team:1,x:305)};
                Check(a.Step(units));_=ArtilleryDispersionMath.FastDimond(rng);_=ArtilleryDispersionMath.Polar(table,rng);_=ArtilleryDispersionMath.Apply(Vector3.UnitZ,10,1000,20,40,.5f,-.5f);_=BuffEvaluationPipeline.Evaluate(null,new(1,1,1,1),CritTable());_=VisibilityMath.GetSingleSphereSmokeInterval(Vector3.Zero,new(10,0,0),new(5,0,0),2);_=AircraftMath.CalculateRate(i*20,new(100,200,300),new(10,20,15));_=NavigationMath.AstarPriority(3,4,10);_=H1FiniteResearch.Evaluate(i);
                Check(b.Step(units));var l=a.Bridge.LastGood!;var r=b.Bridge.LastGood!;var zero=new UpdateMetrics(0,0,0,0,0,0);
                Check(JsonSerializer.Serialize(l.Territory)==JsonSerializer.Serialize(r.Territory));Check(JsonSerializer.Serialize(l.Result with{Metrics=zero})==JsonSerializer.Serialize(r.Result with{Metrics=zero}));
                Check(JsonSerializer.Serialize(pa.Project(l.Input,0,i))==JsonSerializer.Serialize(pb.Project(r.Input,0,i)));Check(JsonSerializer.Serialize(a.Profiles.Document)==profiles&&JsonSerializer.Serialize(b.Profiles.Document)==profiles);
            }
        });
        Test("Identity Explorer serialization preserves lifecycle distinctions",()=>{
            var c=new ResearchProfileCatalog(File.ReadAllText("config/core021/research-profiles.json"));var a=new LifecycleObservation(new(Guid.NewGuid(),1,7,2),1,10,false,false,false,false,false,false);
            var p=new Dictionary<string,JsonElement>{{"Snapshot",JsonSerializer.SerializeToElement(a)},{"Current",JsonSerializer.SerializeToElement(a)}};var r=FormulaExplorer.Evaluate(new("VersionAwareIdentity",null,p),c);Check(JsonSerializer.Serialize(r).Contains("Accepted"));Check(r.EquivalenceStatus=="Experimental");
        });
        Test("RNG finite domain errors and reproducible sequences",()=>{var q=new float[100];q[1]=1;var a=new CyclicRandom(q);var b=new CyclicRandom(q);Reject(()=>a.NextFloat(0,float.PositiveInfinity));Reject(()=>a.NextFloat(float.NegativeInfinity,1));Check(a.ValuesConsumed==0);for(int i=0;i<250;i++)Eq(a.NextFloat(-1,1),b.NextFloat(-1,1),0);});
        Test("Core assembly remains independent of game libraries",()=>{Check(!typeof(ArtilleryDispersionMath).Assembly.GetReferencedAssemblies().Any(a=>a.Name!.Contains("Unity")||a.Name.Contains("Melon")||a.Name.Contains("Integration")||a.Name.Contains("BrokenArrow")));});
    }
}
