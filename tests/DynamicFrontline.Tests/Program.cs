using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.GameAdapter;

Console.WriteLine("Runtime: "+System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
var results=new List<object>(); int failed=0;
void Check(bool value,string message="assertion failed") { if(!value) throw new Exception(message); }
void Test(string name,Action run)
{
    try { run(); Console.WriteLine("PASS "+name); results.Add(new {name,passed=true,error=""}); }
    catch(Exception e) { failed++; Console.WriteLine("FAIL "+name+": "+e.Message); results.Add(new {name,passed=false,error=e.ToString()}); }
}
Test("manual activation cancels once and issues one physical move",()=>{
    var f=new Fixture(); Check(f.C.Activate(f.Key,0)); Check(f.C.State==DesertionState.Retreating);
    Check(f.Game.Calls.SequenceEqual(new[]{"cancel:1","move:1"})); Check(f.Game.Destination==f.Target);
    Check(f.Game.Units[f.Key].OwnerId==7); Check(f.C.DrainEvents().Select(x=>x.State).SequenceEqual(new[]{DesertionState.Deserting,DesertionState.Retreating}));
});
Test("debug is disabled by default",()=>{ var f=new Fixture(debug:false); Check(!f.C.Activate(f.Key,0)); Check(f.Game.Calls.Count==0); });
Test("team spawn planner and missing position",()=>{
    var f=new Fixture(planner:new TeamSpawnPlanner()); Check(f.C.Activate(f.Key,0)); Check(f.Game.Destination==f.Game.TeamSpawn);
    var g=new Fixture(planner:new TeamSpawnPlanner()); g.Game.TeamSpawn=null; Check(!g.C.Activate(g.Key,0)); Check(g.Game.Calls.Count==0);
});
Test("user rejected, controller movement allowed, group preserves others",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); var gate=new UserCommandGate(f.Game,f.C);
    Check(!gate.Allows(f.Key,7)); Check(gate.Allows(f.Other,7)); Check(!gate.Allows(f.Other,99));
    Check(gate.FilterGroup(new[]{f.Key,f.Other,f.Key},7).SequenceEqual(new[]{f.Other})); Check(f.Game.Calls.Count==2);
});
Test("death during retreat releases lock without issuing commands",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Game.Units[f.Key]=f.Game.Units[f.Key] with {IsAlive=false}; f.C.Tick(1);
    Check(f.C.State==DesertionState.Destroyed && !f.C.Locked); Check(f.Game.Calls.Count==2);
});
Test("unreachable path fails safely and does not retry",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Game.Path=PathStatus.Unreachable; f.C.Tick(1);
    for(int i=2;i<50;i++) f.C.Tick(i);
    Check(f.C.State==DesertionState.Failed && !f.C.Locked); Check(f.Game.Calls.Count==2);
});
Test("arrival then recovery restores same owner control",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Arrive(); f.C.Tick(2); Check(f.C.State==DesertionState.Recovering && f.C.Locked);
    f.C.Tick(11); Check(f.C.Locked); f.C.Tick(12); Check(f.C.State==DesertionState.Controlled && !f.C.Locked);
    Check(new UserCommandGate(f.Game,f.C).Allows(f.Key,7)); Check(f.Game.Units[f.Key].OwnerId==7);
    var ev=f.C.DrainEvents(); Check(ev.Length==4 && ev.Select(x=>x.Revision).SequenceEqual(new long[]{1,2,3,4}));
});
Test("repeat activation rejected during and after recovery",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); Check(!f.C.Activate(f.Key,1)); f.Arrive(); f.C.Tick(2); f.C.Tick(12);
    Check(!f.C.Activate(f.Key,13)); Check(f.Game.Calls.Count==2);
});
Test("second squad unchanged and activation cannot replace active squad",()=>{
    var f=new Fixture(); var before=f.Game.Units[f.Other]; f.C.Activate(f.Key,0); Check(!f.C.Activate(f.Other,1));
    Check(before==f.Game.Units[f.Other] && !f.C.BlocksUser(f.Other));
});
Test("unknown path times out; no command loop",()=>{
    var f=new Fixture(); f.Game.Path=PathStatus.Unknown; f.C.Activate(f.Key,0); f.C.Tick(120);
    Check(f.C.LastReason=="retreat-timeout" && !f.C.Locked && f.Game.Calls.Count==2);
});
Test("arrival status alone cannot fake safe position",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Game.Path=PathStatus.Arrived; f.C.Tick(1); Check(f.C.State==DesertionState.Retreating);
});
Test("movement rejection releases user lock",()=>{
    var f=new Fixture(); f.Game.MoveAccepted=false; Check(!f.C.Activate(f.Key,0)); Check(!f.C.Locked && f.C.State==DesertionState.Failed);
});
Test("cancel failure never issues movement",()=>{
    var f=new Fixture(); f.Game.CancelAccepted=false; Check(!f.C.Activate(f.Key,0)); Check(f.Game.Calls.SequenceEqual(new[]{"cancel:1"}));
});
Test("adapter exception disables functionality and releases lock",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Game.Throw=true; f.C.Tick(1); Check(f.C.Disabled && !f.C.Locked);
});
Test("owner changed during retreat is never reassigned",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Game.Units[f.Key]=f.Game.Units[f.Key] with {OwnerId=8}; f.C.Tick(1);
    Check(!f.C.Locked && f.Game.Units[f.Key].OwnerId==8 && f.Game.Calls.Count==2);
});
Test("dead noninfantry and foreign units cannot activate",()=>{
    foreach(int mode in new[]{0,1,2,3}) { var f=new Fixture(); var s=f.Game.Units[f.Key];
      f.Game.Units[f.Key]=mode switch {0=>s with {IsAlive=false},1=>s with {IsInfantry=false},2=>s with {OwnerId=8},_=>s with {AliveSoldierCount=0}};
      Check(!f.C.Activate(f.Key,0)); Check(f.Game.Calls.Count==0); }
});
Test("wrong match and recycled generation rejected",()=>{
    var f=new Fixture(); Check(!f.C.Activate(f.Key with {MatchId=Guid.NewGuid()},0)); Check(!f.C.Activate(f.Key with {Generation=2},1));
});
Test("invalid destination and backwards clock rejected",()=>{
    var f=new Fixture(planner:new FixedPointPlanner(new(float.NaN,0,0))); Check(!f.C.Activate(f.Key,5));
    bool thrown=false; try { f.C.Tick(4); } catch(ArgumentOutOfRangeException) {thrown=true;} Check(thrown);
});
Test("diagnostics own units only",()=>{
    var f=new Fixture(); var inspector=new UnitInspector(f.Game,f.Log); Check(inspector.Inspect(f.Key));
    Check(f.Log.Lines.Single().Contains("aliveSoldiers=8") && f.Log.Lines.Single().Contains("stress=20/100"));
    f.Game.Units[f.Other]=f.Game.Units[f.Other] with {OwnerId=8}; Check(!inspector.Inspect(f.Other)); Check(f.Log.Lines.Count==1);
});
Test("reflection reader rejects enemy before reading any coordinates or ECS",()=>{
    var reader=new ReflectionUnitReader(_=>throw new Exception("must not resolve ECS"));
    Check(reader.Read(new EnemyUnit(),new(Guid.NewGuid(),1,1),7).Status==ReadStatus.NotOwned);
});
Test("reflection reads fresh struct snapshots without writes",()=>{
    var unit=new SampleUnit(); var key=new UnitKey(Guid.NewGuid(),1,1); var reader=new ReflectionUnitReader(SampleTypes.Resolve);
    var first=reader.Read(unit,key,7); Check(first.Snapshot?.AliveSoldierCount==8 && first.Snapshot.CurrentStressValue==20);
    unit.Entity.Values[typeof(SampleStress)]=new SampleStress{CurrentStressValue=30,MaxStress=100,CurrentStressLevel="Panicked"};
    Check(reader.Read(unit,key,7).Snapshot?.CurrentStressValue==30); Check(first.Snapshot?.CurrentStressValue==20);
});
Test("unsupported wrappers disable adapter without commands",()=>{
    var match=Guid.NewGuid(); var log=new MemoryLog(); var port=new ExperimentalGamePort(match,7,new BrokenBindings(),new ReflectionUnitReader(_=>null),log);
    Check(port.ReadOwned(new(match,1,1)).Status==ReadStatus.Unavailable); Check(!port.ReadyForControl); Check(!port.CancelOrders(new(match,1,1)));
    Check(log.Lines.Count==1);
});
Test("compatibility accepts exact hash rejects mismatch version and traversal",()=>{
    string root=Path.Combine(Environment.CurrentDirectory,"artifacts","compatibility-test"); Directory.CreateDirectory(root);
    File.WriteAllText(Path.Combine(root,"sample"),"example"); string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"sample"))));
    var m=new CompatibilityManifest("test",new(){{"sample",hash}});
    Check(Compatibility.Verify(root,"test",m,out _)); Check(!Compatibility.Verify(root,"wrong",m,out _));
    File.WriteAllText(Path.Combine(root,"sample"),"changed"); Check(!Compatibility.Verify(root,"test",m,out _));
    Check(!Compatibility.Verify(root,"test",new("test",new(){{"../outside",hash}}),out _));
});
Test("disabled adapter prevents activation",()=>{var f=new Fixture(); f.Game.ReadyForControl=false; Check(!f.C.Activate(f.Key,0)); Check(f.Game.Calls.Count==0);});
Test("leaving recovery area releases lock instead of silently recovering",()=>{
    var f=new Fixture(); f.C.Activate(f.Key,0); f.Arrive(); f.C.Tick(1); f.Game.Units[f.Key]=f.Game.Units[f.Key] with {Position=new(0,0,0)}; f.C.Tick(2);
    Check(f.C.State==DesertionState.Failed && !f.C.Locked);
});
CombatFormulaTests.Run(Test);
Directory.CreateDirectory("artifacts");
File.WriteAllText("artifacts/test-results.json",JsonSerializer.Serialize(new {runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,total=results.Count,failed,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"TOTAL {results.Count}; PASSED {results.Count-failed}; FAILED {failed}");
return failed==0 ? 0 : 1;

sealed class MemoryLog : ILog { public List<string> Lines=new(); public void Info(string s)=>Lines.Add(s); public void Error(string s)=>Lines.Add(s); }
sealed class FakeGame : IGamePort
{
    public Guid MatchId {get;}=Guid.NewGuid(); public int LocalOwnerId=>7; public bool ReadyForControl {get;set;}=true;
    public Dictionary<UnitKey,UnitSnapshot> Units=new(); public List<string> Calls=new(); public Position? Destination;
    public Position? TeamSpawn {get;set;}=new(50,0,50); public PathStatus Path=PathStatus.Moving;
    public bool MoveAccepted=true,CancelAccepted=true,Throw;
    public UnitRead ReadOwned(UnitKey key) {if(Throw)throw new Exception(); return Units.TryGetValue(key,out var s)?new(ReadStatus.Available,s):new(ReadStatus.Missing);}
    public bool CancelOrders(UnitKey k){Calls.Add("cancel:"+k.Uid);return CancelAccepted;}
    public bool MoveTo(UnitKey k,Position p){Calls.Add("move:"+k.Uid);Destination=p;return MoveAccepted;}
    public PathStatus ReadPath(UnitKey k)=>Path;
}
sealed class Fixture
{
    public FakeGame Game=new(); public MemoryLog Log=new(); public UnitKey Key,Other; public Position Target=new(100,0,100); public DesertionController C;
    public Fixture(bool debug=true,IRetreatPlanner? planner=null,CalmStressRecoveryCondition? recoveryCondition=null)
    {
        Key=new(Game.MatchId,1,1); Other=new(Game.MatchId,2,1);
        Game.Units[Key]=new(Key,10,7,new(0,0,0),true,true,8,20,100,"Shocked","Attack"); Game.Units[Other]=Game.Units[Key] with {Key=Other};
        C=new(Game,planner??new FixedPointPlanner(Target),new(debug),Log,recoveryCondition);
    }
    public void Arrive(){Game.Units[Key]=Game.Units[Key] with {Position=Target}; Game.Path=PathStatus.Arrived;}
}
public sealed class EnemyUnit { public int GetOwnerPlayerUID()=>99; public object GetPosition()=>throw new Exception("enemy position read"); }
public struct SampleStress {public float CurrentStressValue;public float MaxStress;public string CurrentStressLevel;}
public struct SampleInfantry {public uint AliveSoldierCount;}
public struct SampleComponent {public SampleData UnitData;public SampleOwner Owner;}
public sealed class SampleOwner {public int UID=7;}
public sealed class SampleData {public int Id=10;}
public sealed class SampleCommands {public object? CurrentCommand=>null;}
public sealed class SampleEntity
{
    public bool IsAliveVersion=>true;
    public Dictionary<Type,object> Values=new(){{typeof(SampleStress),new SampleStress{CurrentStressValue=20,MaxStress=100,CurrentStressLevel="Shocked"}},
        {typeof(SampleInfantry),new SampleInfantry{AliveSoldierCount=8}},{typeof(SampleComponent),new SampleComponent{UnitData=new(),Owner=new()}},{typeof(SampleCommands),new SampleCommands()}};
    public bool Has<T>()=>Values.ContainsKey(typeof(T)); public T Get<T>()=>(T)Values[typeof(T)];
}
public sealed class SampleUnit
{
    public int UID=1; public SampleEntity Entity=new(); public int GetOwnerPlayerUID()=>7; public bool IsAlive()=>true;
    public SampleVector GetPosition()=>new();
}
public sealed class SampleVector {public float x=1,y=2,z=3;}
static class SampleTypes
{
    public static Type? Resolve(string s)=>s.Split('.').Last() switch {"StressComponent"=>typeof(SampleStress),"InfantryUnitComponent"=>typeof(SampleInfantry),"UnitComponent"=>typeof(SampleComponent),"CommandsComponent"=>typeof(SampleCommands),_=>null};
}
sealed class BrokenBindings : IUnitBindings
{
    public Position? TeamSpawn=>null;public object? ResolveOwnedLuaUnit(UnitKey k)=>new object();public PathStatus ReadPath(UnitKey k)=>PathStatus.Unknown;
}
