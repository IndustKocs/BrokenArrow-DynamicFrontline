using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Frontline;

Directory.CreateDirectory("artifacts");
if(args.Contains("--benchmark")){Benchmark.Run();return 0;}
var results=new List<object>();int failed=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);results.Add(new{name,passed=true,error=""});}
    catch(Exception e){failed++;Console.WriteLine("FAIL "+name+": "+e.Message);results.Add(new{name,passed=false,error=e.ToString()});}}
void Check(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(InvalidOperationException){return;}throw new Exception("Expected rejection");}
string Cells(Fixture f)=>JsonSerializer.Serialize(f.Sim.Snapshot().Cells);

Test("neutral capture and regular advance require rooted territory",()=>{
    var f=new Fixture();var u=f.Unit(1,1,15,5);var frame=f.Step(u);Check(f.Grid.Owner(1)==0);Check(frame.Delta.Cells.Length>0);
    u=u with {Position=new(25,5)};f.Step(u);Check(f.Grid.Owner(2)==0);
});
Test("regular withdrawal retains last stable control",()=>{var f=new Fixture();f.Step(f.Unit(1,1,15,5));f.Step(f.Unit(1,1,5,5));Check(f.Grid.Owner(1)==0);});
Test("hysteresis requires accumulated capture and decays after death",()=>{
    var f=new Fixture(new Rules{TickMilliseconds=1000,CaptureThreshold=32000,DominanceMargin=0});var u=f.Unit(1,1,15,5);
    f.Step(u);Check(f.Grid.Owner(1)==-1&&f.Grid.Cell(1).Progress>0);long before=f.Grid.Cell(1).Progress;
    f.Step(u with {Alive=false});Check(f.Grid.Owner(1)==-1&&f.Grid.Cell(1).Progress<before);
});
Test("capture after four equal ticks",()=>{var f=new Fixture(new Rules{TickMilliseconds=1000,CaptureThreshold=32000,DominanceMargin=0});var u=f.Unit(1,1,15,5);for(int i=0;i<4;i++)f.Step(u);Check(f.Grid.Owner(1)==0);});
Test("equal opposing influence contested without ownership flicker",()=>{
    var f=new Fixture();f.Grid.Seed(2,1);var a=f.Unit(1,1,15,5);var b=f.Unit(2,1,15,5,1);
    // Add an explicit continuous team-one corridor to its static root.
    for(int x=2;x<f.Grid.Width;x++)f.Grid.Seed(x,1);
    for(int i=0;i<8;i++)f.Step(a,b);Check(f.Grid.Cell(1).Contested&&f.Grid.Owner(1)==-1);
});
Test("enemy capture changes stable owner only after threshold",()=>{
    var f=new Fixture();for(int x=2;x<f.Grid.Width;x++)f.Grid.Seed(x,1);f.Grid.Seed(1,0);
    f.Step(f.Unit(2,1,15,5,1));Check(f.Grid.Owner(1)==1);
});
Test("soldier count and shock affect influence independently of HP",()=>{
    var r=new Rules();var c=new TerritorialInfluenceCalculator(Fixture.Profiles,r);var f=new Fixture();var u=f.Unit(1,1,15,5);
    Check(c.Calculate(u)==8);Check(c.Calculate(u with {AliveSoldierCount=4})==4);Check(c.Calculate(u with {Stress=StressLevel.Shocked})==4);
});
foreach(var mode in new[]{"dead","zero","panic","deserting","loaded"})Test("territory excludes "+mode,()=>{
    var f=new Fixture();var u=f.Unit(1,2,15,5);u=mode switch{"dead"=>u with{Alive=false},"zero"=>u with{AliveSoldierCount=0},"panic"=>u with{Stress=StressLevel.Panicked},"deserting"=>u with{Deserting=true},_=>u with{Loaded=true}};
    string before=Cells(f);var frame=f.Step(u);Check(Cells(f)==before&&frame.LocalAreas.Length==0);
});
foreach(int id in new[]{3,4,5})Test("noncapture role "+id+" never changes grid even in rear",()=>{
    var f=new Fixture();string before=Cells(f);for(int i=0;i<10;i++){var frame=f.Step(f.Unit(1,id,55+i*10,45));Check(frame.LocalAreas.Length==0&&frame.Delta.Cells.Length==0);}
    Check(Cells(f)==before);
});
Test("invalid capture role rejected and unknown IDs default deny",()=>{
    Reject(()=>new ProfileCatalog(new[]{new UnitTerritoryProfile(7,UnitRole.Recon,true,true,1,20)}));
    var f=new Fixture();var frame=f.Step(f.Unit(1,999,15,5));Check(frame.Delta.Cells.Length==0&&frame.Operational.Nodes.Length==0);
});
Test("local area moves without territorial trail",()=>{
    var f=new Fixture();string before=Cells(f);var a=f.Step(f.Unit(1,1,105,105));var b=f.Step(f.Unit(1,1,155,105));
    Check(a.LocalAreas.Single().Center!=b.LocalAreas.Single().Center&&Cells(f)==before&&f.Sim.Territory.AreaCells(0)==1);
});
Test("separate autonomous groups and overlapping enclave union",()=>{
    var f=new Fixture();var a=f.Unit(1,1,105,105);var b=f.Unit(2,1,205,105);var frame=f.Step(a,b);
    Check(frame.LocalAreas.Select(x=>x.Enclave).Distinct().Count()==2);
    frame=f.Step(a,b with {Position=new(115,105)});Check(frame.LocalAreas.Select(x=>x.Enclave).Distinct().Count()==1);
});
Test("local radius tracks soldiers and last soldier removes enclave",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);double r=f.Step(u).LocalAreas.Single().Radius;
    Check(f.Step(u with {AliveSoldierCount=2}).LocalAreas.Single().Radius<r);Check(f.Step(u with {AliveSoldierCount=0}).LocalAreas.Length==0);
});
Test("restoring territorial corridor absorbs local presence",()=>{
    var f=new Fixture();var u=f.Unit(1,1,55,5);Check(f.Step(u).LocalAreas.Length==1);
    for(int x=1;x<=4;x++)f.Grid.Seed(x,0);var frame=f.Step(u);Check(frame.LocalAreas.Length==0&&f.Grid.Owner(5)==0);
});
Test("out of map and masked units cannot capture or relay",()=>{
    bool[] mask=Enumerable.Repeat(true,32*32).ToArray();mask[1]=false;var f=new Fixture(mask:mask);
    var a=f.Step(f.Unit(1,1,-1,5),f.Unit(2,1,15,5));Check(a.Delta.Cells.Length==0&&a.LocalAreas.Length==0&&a.Operational.Nodes.Length==0);
});
Test("only changed footprints are updated",()=>{
    var f=new Fixture();var u=f.Unit(1,1,5,5);f.Step(u);Check(f.Step(u).Metrics.DirtyCells==0);
    Check(f.Step(u with{Position=new(15,5)}).Metrics.DirtyCells>0);
});
Test("direct and intermediate operational support",()=>{
    var f=new Fixture();var frame=f.Step(f.Unit(1,1,25,5),f.Unit(2,1,45,5),f.Unit(3,1,65,5));
    Check(frame.Operational.Nodes.All(n=>n.Connected)&&frame.Operational.Nodes.Last().Hops==3);
});
Test("regular recon special regular chain supports without painting corridor",()=>{
    var f=new Fixture();var units=new[]{f.Unit(1,1,5,5),f.Unit(2,4,25,5),f.Unit(3,3,45,5),f.Unit(4,1,65,5)};
    var frame=f.Step(units);Check(frame.Operational.Nodes.All(n=>n.Connected));Check(f.Grid.Owner(4)==-1&&frame.LocalAreas.Length==1);
    string before=Cells(f);units[1]=units[1] with{Position=new(25,15)};units[2]=units[2] with{Position=new(45,15)};
    f.Step(units);Check(before==Cells(f));
});
Test("multiple reconnaissance relays connect to root",()=>{
    var f=new Fixture();var frame=f.Step(f.Unit(1,4,25,5),f.Unit(2,4,45,5),f.Unit(3,4,65,5),f.Unit(4,1,85,5));
    Check(frame.Operational.Nodes.All(n=>n.Connected));
});
Test("destroyed relay breaks chain and replacement restores it",()=>{
    var f=new Fixture();var a=f.Unit(1,4,25,5);var b=f.Unit(2,3,45,5);var c=f.Unit(3,1,65,5);
    Check(f.Step(a,b,c).Operational.Nodes.Last().Connected);
    Check(!f.Step(a,b with{Alive=false},c).Operational.Nodes.Last().Connected);
    Check(f.Step(a,b with{Key=b.Key with{Uid=99}},c).Operational.Nodes.Single(n=>n.Unit==c.Key).Connected);
});
Test("too distant and unrooted chain remain disconnected",()=>{
    var f=new Fixture();var frame=f.Step(f.Unit(1,4,105,105),f.Unit(2,3,125,105));Check(frame.Operational.Nodes.All(n=>!n.Connected));
    Check(frame.Operational.Nodes.Select(n=>n.Component).Distinct().Count()==1);
});
Test("hop limit stops artificial long chains",()=>{
    var f=new Fixture(Fixture.DefaultRules with{MaximumHops=2});var frame=f.Step(f.Unit(1,4,25,5),f.Unit(2,4,45,5),f.Unit(3,1,65,5));
    Check(frame.Operational.Nodes[1].Connected&&!frame.Operational.Nodes[2].Connected);
});
foreach(var mode in new[]{"panic","deserting","loaded","zero"})Test("relay exclusion "+mode,()=>{
    var f=new Fixture();var a=f.Unit(1,4,25,5);a=mode switch{"panic"=>a with{Stress=StressLevel.Panicked},"deserting"=>a with{Deserting=true},"loaded"=>a with{Loaded=true},_=>a with{AliveSoldierCount=0}};
    Check(!f.Step(a,f.Unit(2,1,45,5)).Operational.Nodes.Single().Connected);
});
Test("shock participation configured independently of capture",()=>{
    var f=new Fixture(Fixture.DefaultRules with{ShockCanRelay=false});Check(f.Step(f.Unit(1,4,25,5) with{Stress=StressLevel.Shocked}).Operational.Nodes.Length==0);
});
Test("opponent nodes never link across teams",()=>{var f=new Fixture();var x=f.Step(f.Unit(1,4,25,5),f.Unit(2,4,25,5,1));Check(x.Operational.Edges.Length==0&&!x.Operational.Nodes[1].Connected);});
Test("terrain barrier blocks links including diagonal corner cutting",()=>{
    var mask=Enumerable.Repeat(true,32*32).ToArray();for(int y=0;y<32;y++)mask[y*32+1]=false;
    var f=new Fixture(mask:mask);Check(!f.Step(f.Unit(1,4,25,5)).Operational.Nodes.Single().Connected);
    Check(!new GridLinkGeometry(f.Grid).CanLink(new(5,5),new(25,25),0));
});
Test("operational support does not imply logistics or territory",()=>{
    var f=new Fixture();var u=f.Unit(2,1,45,5);var frame=f.Step(f.Unit(1,4,25,5),u);var state=frame.Isolation.Units.Single(s=>s.Unit==u.Key);
    Check(state.OperationalConnectivity&&!state.TerritorialConnectivity&&!state.LogisticsConnectivity);
});
Test("logistics can exist without territorial or operational continuity",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Logistics.Supplied.Add(u.Key);var s=f.Step(u).Isolation.Units.Single();
    Check(s.LogisticsConnectivity&&!s.TerritorialConnectivity&&!s.OperationalConnectivity);
});
Test("distance and unknown retreat are not proof of encirclement",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);AuthoritativeFrame frame=f.Step(u);for(int i=0;i<10;i++)frame=f.Step(u);
    Check(frame.Isolation.Units.Single().State==IsolationState.Autonomous);
});
Test("safe retreat without support remains autonomous",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Routes.Routes[u.Key]=RouteAccess.Available;
    for(int i=0;i<10;i++)Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Autonomous);
});
Test("confirmed closed route waits then produces configured severity",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Routes.Routes[u.Key]=RouteAccess.Blocked;
    Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Autonomous);f.Step(u);
    Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Encircled);Check(f.Step(u).Isolation.Units.Single().Severity>0);
});
Test("physical closure with operational support lowers severity but opens no route",()=>{
    var f=new Fixture();var u=f.Unit(2,1,45,5);f.Routes.Routes[u.Key]=RouteAccess.Blocked;AuthoritativeFrame x=f.Step(f.Unit(1,4,25,5),u);
    for(int i=0;i<4;i++)x=f.Step(f.Unit(1,4,25,5),u);var s=x.Isolation.Units.Single(s=>s.Unit==u.Key);
    Check(s.State==IsolationState.Encircled&&s.OperationalConnectivity&&s.RetreatRoute==RouteAccess.Blocked&&s.Severity>0&&s.Severity<.1);
});
Test("recovery before penalties and repeated isolation reset confirmation timer",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Routes.Routes[u.Key]=RouteAccess.Blocked;f.Step(u);
    f.Routes.Routes[u.Key]=RouteAccess.Available;Check(f.Step(u).Isolation.Units.Single().Severity==0);
    f.Routes.Routes[u.Key]=RouteAccess.Blocked;Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Autonomous);
    f.Step(u);Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Encircled);
});
Test("recovery after penalties holds reconnecting then settles",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Routes.Routes[u.Key]=RouteAccess.Blocked;for(int i=0;i<5;i++)f.Step(u);
    f.Routes.Routes[u.Key]=RouteAccess.Available;Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Reconnecting);
    f.Step(u);Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Autonomous);
    Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Autonomous);
});
Test("reconnected territory and support wait before Connected",()=>{
    var f=new Fixture();var u=f.Unit(1,1,105,105);f.Step(u);u=u with{Position=new(5,5)};
    Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Reconnecting);f.Step(u);
    Check(f.Step(u).Isolation.Units.Single().State==IsolationState.Connected);
});
Test("boundary extracts stable simplified straight front",()=>{
    var g=new TerritoryGrid(8,8,10);for(int i=0;i<g.Count;i++)g.Seed(i,i%8<4?0:1);
    var b=new FrontlineBoundaryExtractor().Extract(g);var front=b.Single(p=>p.Owner==0&&p.Neighbor==1);
    Check(front.Points.Length==2&&front.Points.All(p=>p.X==40));
});
Test("boundary supports closed islands and concave contours",()=>{
    var g=new TerritoryGrid(8,8,10);foreach(int i in new[]{9,10,17,45})g.Seed(i,0);
    var b=new FrontlineBoundaryExtractor().Extract(g).Where(p=>p.Neighbor==-1).ToArray();
    Check(b.Length==2&&b.All(p=>p.Points[0]==p.Points[^1])&&b.Any(p=>p.Points.Length>5));
});
Test("masked terrain boundary remains distinct from enemy front",()=>{
    var mask=Enumerable.Repeat(true,9).ToArray();mask[4]=false;var g=new TerritoryGrid(3,3,10,mask);g.Seed(3,0);
    var b=new FrontlineBoundaryExtractor().Extract(g);Check(b.Any(p=>p.Neighbor==-2)&&!b.Any(p=>p.Neighbor==1));
});
Test("input order does not change authoritative outputs",()=>{
    var f=new Fixture();var g=new Fixture(match:f.Match);var units=new[]{f.Unit(5,1,15,5),f.Unit(3,4,35,5),f.Unit(7,1,85,5)};
    for(int i=0;i<5;i++){
        var a=f.Step(units);var b=g.Step(units.Reverse().ToArray());
        Check(JsonSerializer.Serialize(a.Delta)==JsonSerializer.Serialize(b.Delta));Check(JsonSerializer.Serialize(a.Operational)==JsonSerializer.Serialize(b.Operational));Check(JsonSerializer.Serialize(a.Isolation)==JsonSerializer.Serialize(b.Isolation));
    }
});
Test("JSON snapshots and ordered deltas reconstruct authority",()=>{
    var f=new Fixture();var replica=new TerritoryReplica(f.Sim.Snapshot());
    for(int i=0;i<4;i++){var frame=f.Step(f.Unit(1,1,5+10*i,5));var json=JsonSerializer.Serialize(frame.Delta);replica.Apply(JsonSerializer.Deserialize<TerritoryDelta>(json)!);}
    Check(JsonSerializer.Serialize(replica.State)==JsonSerializer.Serialize(f.Sim.Snapshot()));
});
Test("foreign duplicate reordered delta rejected",()=>{
    var f=new Fixture();var replica=new TerritoryReplica(f.Sim.Snapshot());var a=f.Step(f.Unit(1,1,15,5));
    Reject(()=>replica.Apply(a.Delta with{Header=a.Header with{MatchId=Guid.NewGuid()}}));replica.Apply(a.Delta);Reject(()=>replica.Apply(a.Delta));
});
Test("presentation default denies territory and every enemy coordinate or key",()=>{
    var f=new Fixture();var enemy=f.Unit(999,3,205,205,1);var frame=f.Step(f.Unit(1,4,25,5),enemy);
    var view=TeamPresentationFilter.Project(0,frame,f.Sim.Snapshot(),0,new DenyTerritoryKnowledge());
    Check(view.Cells.Length==0&&view.Boundaries.Length==0&&view.FriendlyUnits.All(u=>u.Team==0)&&view.Nodes.All(n=>n.Team==0));
    Check(!JsonSerializer.Serialize(view).Contains("\"Uid\":999"));TeamPresentationFilter.ValidateRecipient(view,0,f.Match,f.Sim.Rules.Version);
    Reject(()=>TeamPresentationFilter.ValidateRecipient(view,1,f.Match,f.Sim.Rules.Version));
});
Test("invalid tick duplicate UID match and nonfinite data fail before mutation",()=>{
    var f=new Fixture();var u=f.Unit(1,1,15,5);string before=Cells(f);
    Reject(()=>f.Sim.Step(2,new[]{u}));Reject(()=>f.Sim.Step(0,new[]{u,u}));Reject(()=>f.Sim.Step(0,new[]{u with{Position=new(double.NaN,0)}}));
    Reject(()=>f.Sim.Step(0,new[]{u with{Key=u.Key with{MatchId=Guid.NewGuid()}}}));Check(Cells(f)==before);
});
Test("configuration rejects invalid domains",()=>{Reject(()=>new Rules{LinkDistance=0}.Validate());Reject(()=>new Rules{IsolationDelayTicks=0}.Validate());Reject(()=>new TerritoryGrid(0,2,10));});
Test("configuration file loads explicit synthetic profiles",()=>{
    var config=FrontlineConfiguration.Load(File.ReadAllText("config/frontline.example.json"));
    Check(config.Profiles.Single(p=>p.Role==UnitRole.Recon).CanRelay&&!config.Profiles.Single(p=>p.Role==UnitRole.Recon).CanCaptureTerritory);
});
Test("synthetic retreat routing detects enclosing terrain and neutral escape",()=>{
    var mask=Enumerable.Repeat(true,81).ToArray();foreach(int i in new[]{30,31,32,39,41,48,49,50})mask[i]=false;
    var g=new TerritoryGrid(9,9,10,mask);g.Seed(0,0);var roots=new[]{new TeamRoot(0,new(5,5))};
    var route=new GridRetreatAccess(g,roots,true);var f=new Fixture();var u=f.Unit(1,1,45,45);
    Check(route.Get(u)==RouteAccess.Blocked);Check(route.Get(u with{Position=new(15,5)})==RouteAccess.Available);
});
Test("enemy territory retreat rule is independent configurable policy",()=>{
    var g=new TerritoryGrid(9,9,10);g.Seed(0,0);for(int y=0;y<9;y++)g.Seed(y*9+3,1);
    var roots=new[]{new TeamRoot(0,new(5,5))};var f=new Fixture();var u=f.Unit(1,1,65,45);
    Check(new GridRetreatAccess(g,roots,true).Get(u)==RouteAccess.Blocked);
    Check(new GridRetreatAccess(g,roots,false).Get(u)==RouteAccess.Available);
});
Test("desertion adapter suppresses territory relay and local area until restored",()=>{
    var f=new Fixture();var u=f.Unit(1,2,105,105);var port=new RetreatPort(u.Key);var c=new DesertionController(port,new FixedPointPlanner(new(105,0,105)),new(true),port);
    Check(c.Activate(u.Key,0));var adapted=DesertionStateAdapter.Apply(u,c);Check(adapted.Deserting&&adapted.Alive);
    var frame=f.Step(adapted);Check(frame.LocalAreas.Length==0&&frame.Operational.Nodes.Length==0);
    c.Tick(1);c.Tick(11);adapted=DesertionStateAdapter.Apply(u,c);Check(!adapted.Deserting);
    Check(f.Step(adapted).LocalAreas.Length==1&&port.Moves==1);
});
Test("configurable panic coefficient and type multiplier apply",()=>{
    var profiles=new ProfileCatalog(new[]{new UnitTerritoryProfile(1,UnitRole.Line,true,true,2,10)});
    var f=new Fixture();var calc=new TerritorialInfluenceCalculator(profiles,new Rules{PanicCoefficient=.25});
    Check(calc.Calculate(f.Unit(1,1,5,5) with{Stress=StressLevel.Panicked,StatusCoefficient=.5})==2);
});
Test("vehicle relay opt in does not enable territorial capture",()=>{
    var profiles=new ProfileCatalog(new[]{new UnitTerritoryProfile(8,UnitRole.Vehicle,false,true,1,10)});var f=new Fixture();var u=f.Unit(1,8,5,5);
    Check(new TerritorialInfluenceCalculator(profiles,Fixture.DefaultRules).Calculate(u)==0);
    Check(new OperationalConnectivitySystem(profiles,Fixture.DefaultRules,new UnrestrictedGeometry()).Build(new[]{u},new[]{new TeamRoot(0,new(5,5))}).RootLinks.Single());
});

File.WriteAllText("artifacts/frontline-test-results.json",JsonSerializer.Serialize(new{total=results.Count,failed,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"TOTAL {results.Count}; FAILED {failed}");
Diagnostic.WriteDemo();
return failed==0?0:1;

internal sealed class Fixture
{
    public static readonly Rules DefaultRules=new(){TickMilliseconds=1000,CaptureThreshold=8000,DominanceMargin=0,LinkDistance=25,IsolationDelayTicks=2,RecoveryDelayTicks=2};
    public static readonly ProfileCatalog Profiles=new(new[]{
        new UnitTerritoryProfile(1,UnitRole.Line,true,true,1,10),new UnitTerritoryProfile(2,UnitRole.Reserve,true,true,1,10),
        new UnitTerritoryProfile(3,UnitRole.SpecialForces,false,true,1,10),new UnitTerritoryProfile(4,UnitRole.Recon,false,true,1,10),
        new UnitTerritoryProfile(5,UnitRole.Sniper,false,false,1,10)});
    public Guid Match {get;}public TerritoryGrid Grid {get;}public FrontlineSimulation Sim {get;}
    public TestRetreatAccess Routes {get;}=new();public TestLogistics Logistics {get;}=new();private long tick;
    public Fixture(Rules? rules=null,bool[]? mask=null,Guid? match=null,int size=32)
    {
        Match=match??Guid.NewGuid();Grid=new(size,size,10,mask);Grid.Seed(0,0);Grid.Seed(size-1,1);
        Sim=new(Match,rules??DefaultRules,Grid,Profiles,new[]{new TeamRoot(0,new(5,5)),new TeamRoot(1,new(size*10-5,5))},new GridLinkGeometry(Grid),Routes,Logistics);
    }
    public FrontlineUnit Unit(long id,int profile,double x,double y,int team=0)=>new(new(Match,id,1),profile,team,new(x,y),8);
    public AuthoritativeFrame Step(params FrontlineUnit[] units)=>Sim.Step(tick++,units);
}

sealed class RetreatPort(UnitKey key):IGamePort,ILog
{
    public int Moves;public Guid MatchId=>key.MatchId;public int LocalOwnerId=>0;public bool ReadyForControl=>true;public Position? TeamSpawn=>new(105,0,105);
    public UnitRead ReadOwned(UnitKey k)=>new(ReadStatus.Available,new(k,2,0,new(105,0,105),true,true,8,0,100,"Calm","Move"));
    public bool CancelOrders(UnitKey k)=>true;public bool MoveTo(UnitKey k,Position p){Moves++;return true;}public PathStatus ReadPath(UnitKey k)=>PathStatus.Arrived;
    public void Info(string s){}public void Error(string s){}
}
