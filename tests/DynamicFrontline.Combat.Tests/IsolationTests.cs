using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Core.VerifiedCombat;
using DynamicFrontline.Frontline;
static partial class Tests
{
    static void IntegrationIsolation()
    {
        Test("Integration territory graph isolation unchanged by combat calls",()=>{
            var match=Guid.Parse("00000000-0000-0000-0000-000000000021");using var a=new Fixture(match:match);using var b=new Fixture(match:match);
            for(int i=0;i<20;i++)
            {
                var units=new[]{a.Unit(),a.Unit(2,3,x:35),a.Unit(3,2,x:65),a.Unit(4,team:1,x:305)};
                Check(a.Step(units));
                for(int j=0;j<100;j++){_ = CombatFormulas.HeatDamage(10,100+j,100,1,2);_=CombatFormulas.MissileHitChance(2,2,1,1,1,1);_=AircraftMath.Fuel(100,2,3);}
                Check(b.Step(units));var left=a.Bridge.LastGood!;var right=b.Bridge.LastGood!;
                Check(JsonSerializer.Serialize(left.Territory)==JsonSerializer.Serialize(right.Territory));
                var zero=new UpdateMetrics(0,0,0,0,0,0);
                Check(JsonSerializer.Serialize(left.Result with{Metrics=zero})==JsonSerializer.Serialize(right.Result with{Metrics=zero}));
                Check(left.Input.Units[0].AliveSoldierCount==8&&left.Input.Units[0].StressLevel==StressLevel.Calm);
            }
        });
        Test("Mathematical assembly has no Unity loader or network references",()=>{
            var refs=typeof(CombatFormulas).Assembly.GetReferencedAssemblies().Select(a=>a.Name!).ToArray();
            Check(!refs.Any(n=>n.Contains("Unity")||n.Contains("Melon")||n.Contains("BrokenArrow")||n.Contains("Integration")));
        });
    }
    static void Differential()
    {
        Test("Independent Python double reference fixtures finite domain",()=>{
            using var doc=JsonDocument.Parse(File.ReadAllText("config/core021/reference-values.json"));
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                var p=r.GetProperty("inputs").EnumerateArray().Select(v=>v.GetSingle()).ToArray();
                float result=r.GetProperty("formula").GetString() switch{
                    "F10"=>CombatFormulas.HeatDamage(p[0],p[1],p[2],p[3],p[4]),
                    "F11"=>CombatFormulas.Countermeasures(p[0],p[1],(int)p[2]),
                    "F12"=>CombatFormulas.MissileHitChance(p[0],p[1],p[2],p[3],p[4],p[5]),
                    "CF12"=>InfantryMath.Count(p[0],p[1],(int)p[2],(int)p[3]).Desired,
                    "CF03"=>ExplosionMath.Falloff(p[0],p[1]),
                    _=>throw new Exception("Unknown reference")};
                Eq(result,r.GetProperty("expected").GetDouble(),2e-5);
            }
        });
    }
}
