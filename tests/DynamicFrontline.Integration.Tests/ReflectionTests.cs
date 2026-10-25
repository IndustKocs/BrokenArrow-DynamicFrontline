using DynamicFrontline.Core;
using DynamicFrontline.Integration;

static class ReflectionTests
{
    public static void Run(Action<string,Action> test,Action<bool> check)
    {
        test("mock ECS inspection copies audited fields without setters",()=>{
            var lua=new MockLua();var inspector=Inspector();var raw=inspector.Inspect(new(lua,72,"registry-1"));
            check(raw.Uid==72&&raw.DefinitionId==1&&raw.TeamUid==42&&raw.PlayerUid==10&&raw.AliveSoldierCount==8&&raw.Hp==100);
            check(MockHealth.Writes==0);lua.Entity.Components[typeof(MockInfantry)]=new MockInfantry{AliveSoldierCount=2};
            check(raw.AliveSoldierCount==8&&inspector.Inspect(new(lua,72,"registry-1")).AliveSoldierCount==2);
        });
        test("mock transport flags and noninfantry absence handled independently",()=>{
            var lua=new MockLua();lua.Entity.Components.Remove(typeof(MockInfantry));lua.Entity.Components[typeof(MockLoaded)]=new MockLoaded();lua.Entity.Components[typeof(MockLoading)]=new MockLoading();
            var raw=Inspector().Inspect(new(lua,1,"x"));check(raw.AliveSoldierCount==null&&raw.Loaded&&raw.Loading);
        });
        test("reflection provider thread gate prevents any feed access",()=>{
            using var f=new Fixture();var feed=new MockFeed();var access=new ReflectionGameDataAccess(feed,Inspector());
            bool rejected=Task.Run(()=>{try{access.Capture(f.Context.Value!);return false;}catch(IntegrationValidationException){return true;}}).Result;
            check(rejected&&feed.Calls==0);
        });
        test("recorded fixture is tagged and cannot pose as live",()=>{
            using var f=new Fixture();var source=new RecordedFixtureAccess(new[]{new RawGameFrame(1,0,true,new[]{f.Unit()})});
            bool failed=false;try{source.Capture(f.Context.Value!);}catch(IntegrationValidationException){failed=true;}check(failed);
            check(source.Capture(f.Context.Value! with{Origin=DataOrigin.RecordedFixture}).Complete);
        });
    }
    static ReadOnlyComponentInspector Inspector()=>new(name=>name switch{
        ReadOnlyComponentInspector.UnitType=>typeof(MockUnit),ReadOnlyComponentInspector.InfantryType=>typeof(MockInfantry),
        ReadOnlyComponentInspector.HealthType=>typeof(MockHealth),ReadOnlyComponentInspector.StressType=>typeof(MockStress),
        ReadOnlyComponentInspector.LoadedType=>typeof(MockLoaded),ReadOnlyComponentInspector.LoadingType=>typeof(MockLoading),_=>null},
        _=>new(false,false,false,false,Array.Empty<int>()));
}
public sealed class MockFeed:IGameObjectFeed {public int Calls;public GameObjectBatch ReadObjects(){Calls++;return new(1,0,true,new[]{new GameObjectHandle(new MockLua(),1,"s1")});}}
public sealed class MockLua
{
    public MockEntity Entity=new();public int GetOwnerPlayerUID()=>10;public bool IsAlive()=>true;public MockPosition GetPosition()=>new();
}
public sealed class MockEntity
{
    public bool IsAliveVersion=>true;
    public Dictionary<Type,object> Components=new(){{typeof(MockUnit),new MockUnit()},{typeof(MockInfantry),new MockInfantry{AliveSoldierCount=8}},
        {typeof(MockHealth),new MockHealth()},{typeof(MockStress),new MockStress()}};
    public bool Has<T>()=>Components.ContainsKey(typeof(T));public T Get<T>()=>(T)Components[typeof(T)];
}
public sealed class MockUnit {public MockOwner Owner=new();public MockData UnitData=new();}
public sealed class MockOwner {public int UID=>10;public int TeamUid=>42;public int TeamSide=>0;}
public sealed class MockData {public int Id=>1;public int Role=>30;}
public struct MockInfantry {public uint AliveSoldierCount;}
public sealed class MockHealth {public static int Writes;public float CurrentHealth{get=>100;set{Writes++;throw new Exception("Forbidden write");}}public float MaxHealth=>100;}
public sealed class MockStress {public float CurrentStressValue=>0;public float MaxStress=>100;public StressLevel CurrentStressLevel=>StressLevel.Calm;}
public sealed class MockPosition {public float x=>15;public float y=>5;public float z=>5;}
public struct MockLoaded{}public struct MockLoading{}
