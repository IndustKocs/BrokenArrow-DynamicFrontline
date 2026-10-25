using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
using DynamicFrontline.Integration;

Directory.CreateDirectory("artifacts/poc02");
if(args.Contains("--benchmark")){Benchmarks.Run();return 0;}
var results=new List<object>();int failures=0;
void Check(bool value){if(!value)throw new Exception("Assertion failed");}
void Reject(Action action){try{action();}catch(ArgumentException){return;}catch(IntegrationValidationException){return;}catch(InvalidOperationException){return;}catch(NotSupportedException){return;}throw new Exception("Expected rejection");}
void Test(string name,Action action){try{action();results.Add(new{name,passed=true,error=""});Console.WriteLine("PASS "+name);}catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
string State(Fixture f)=>JsonSerializer.Serialize(f.Bridge.LastGood!.Territory);

foreach(int id in Enumerable.Range(1,9))Test("snapshot profile "+id,()=>{
    using var f=new Fixture();f.Access.Units=new[]{f.Unit(id:id)};var a=f.Provider().Capture();Check(a.Failure==FailureCode.None&&a.Snapshot!.Units.Length==1);
    var snapshot=a.Snapshot!;Check(snapshot.Units[0].Hp==100&&snapshot.Units[0].Team==0);
    if(id is 4 or 5)Check(snapshot.Units[0].AliveSoldierCount==null&&!snapshot.CoreUnits[0].HasInfantry);
    if(id is 2 or 3)Check(snapshot.Units[0].Profile!.CanRelayConnectivity&&!snapshot.Units[0].Profile!.CanCaptureTerritory);
});
foreach(var mode in new[]{"dead","loaded","loading","deserting","zero","missing-infantry"})Test("snapshot state "+mode,()=>{
    using var f=new Fixture();var u=f.Unit();u=mode switch{"dead"=>u with{Alive=false,Hp=0},"loaded"=>u with{Loaded=true},"loading"=>u with{Loading=true},"deserting"=>u with{Deserting=true},"zero"=>u with{AliveSoldierCount=0},_=>u with{AliveSoldierCount=null}};
    f.Access.Units=new[]{u};var a=f.Provider().Capture();if(mode=="missing-infantry")Check(a.Failure==FailureCode.Incomplete);else Check(a.Snapshot!.Units[0].CaptureReason!=CaptureExclusion.None);
});
Test("unknown definition is diagnosed and excluded",()=>{using var f=new Fixture();f.Access.Units=new[]{f.Unit() with{DefinitionId=999}};var a=f.Provider().Capture();Check(a.Snapshot!.UnknownProfiles==1&&a.Snapshot.CoreUnits.Single().StatusCoefficient==0);});
Test("UID reuse after absence receives a new generation",()=>{
    using var f=new Fixture();var p=f.Provider();f.Access.Units=new[]{f.Unit()};int first=p.Capture().Snapshot!.Units[0].Key.Generation;
    f.Access.Units=Array.Empty<RawGameUnit>();p.Capture();f.Access.Units=new[]{f.Unit()};Check(p.Capture().Snapshot!.Units[0].Key.Generation==first+1);
});
Test("replacement lifecycle stamp differentiates same UID immediately",()=>{using var f=new Fixture();var p=f.Provider();f.Access.Units=new[]{f.Unit()};p.Capture();f.Access.Units=new[]{f.Unit() with{LifecycleStamp="replacement"}};Check(p.Capture().Snapshot!.Units[0].Key.Generation==2);});
Test("incomplete frame neither despawns nor advances identity",()=>{using var f=new Fixture();var p=f.Provider();f.Access.Units=new[]{f.Unit()};p.Capture();f.Access.Complete=false;f.Access.Units=Array.Empty<RawGameUnit>();Check(p.Capture().Failure==FailureCode.Incomplete);f.Access.Complete=true;f.Access.Units=new[]{f.Unit()};Check(p.Capture().Snapshot!.Units[0].Key.Generation==1);});
Test("death repeated is stable and revival gets generation",()=>{using var f=new Fixture();var p=f.Provider();f.Access.Units=new[]{f.Unit() with{Alive=false}};p.Capture();Check(p.Capture().Snapshot!.Units[0].Key.Generation==1);f.Access.Units=new[]{f.Unit()};Check(p.Capture().Snapshot!.Units[0].Key.Generation==2);});
foreach(var mode in new[]{"position","team-missing","team-mismatch","hp","stress","duplicate","revision","read-failure"})Test("invalid input preserves stable core "+mode,()=>{
    using var f=new Fixture();Check(f.Step(f.Unit()));string before=State(f);var u=f.Unit();
    switch(mode){case "position":u=u with{WorldPosition=new(double.NaN,0,0)};break;case "team-missing":u=u with{TeamUid=null};break;case "team-mismatch":u=u with{TeamUid=87};break;
        case "hp":u=u with{Hp=float.NaN};break;case "stress":u=u with{Stress=-1};break;case "read-failure":f.Access.Throw=true;break;case "revision":f.Access.Override=new(1,0,true,new[]{u});break;}
    Check(!f.Step(mode=="duplicate"?new[]{u,u}:new[]{u}));Check(State(f)==before);
});
Test("provider copies visibility arrays",()=>{using var f=new Fixture();int[] visible={0};f.Access.Units=new[]{f.Unit() with{Visibility=new(false,false,false,false,visible)}};var s=f.Provider().Capture().Snapshot!;visible[0]=1;Check(s.Units[0].Visibility.ConfirmedVisibleTeams[0]==0);});
Test("provider rejects worker thread before source inspection",()=>{using var f=new Fixture();var p=f.Provider();Check(Task.Run(()=>p.Capture().Failure).Result==FailureCode.WrongThread);Check(f.Access.Revision==0);});
Test("assault capture needs explicit profile; role alone insufficient",()=>{using var f=new Fixture();Check(!f.Profiles.Resolve(6,31)!.CanCaptureTerritory&&f.Profiles.Resolve(7,31)!.CanCaptureTerritory);});
Test("scout cannot be granted capture or local area",()=>{using var f=new Fixture();var p=f.Profiles.Document.Units.Single(x=>x.UnitDefinitionId==3);Reject(()=>new GameUnitProfileResolver(f.Profiles.Document with{Units=new[]{p with{CanCaptureTerritory=true}}}));});
Test("different active scenario database cannot silently reuse profile",()=>{using var f=new Fixture();Reject(()=>f.Profiles.ValidateContext(f.Context.Value!.Database with{ScenarioId="other"}));});
Test("unknown active database is explicit failure",()=>{using var f=new Fixture();f.Context.Value=f.Context.Value! with{Database=f.Context.Value!.Database with{ActiveVerified=false}};Check(!f.Step(f.Unit())&&f.Bridge.Health.LastFailure==FailureCode.UnknownDatabase);});
Test("configuration digest and malformed JSON rejected",()=>{
    using var f=new Fixture();string json=JsonSerializer.Serialize(f.Profiles.Document);string hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    Check(GameUnitProfileResolver.Load(json,hash).Document.Version==f.Profiles.Document.Version);Reject(()=>GameUnitProfileResolver.Load(json,new string('0',64)));
    string bad="{";Reject(()=>GameUnitProfileResolver.Load(bad,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bad)))));
});
Test("5v5 player roster maps separately from team and network identities",()=>{using var f=new Fixture();var r=new GameTeamResolver(f.Context.Value!.Teams);for(int i=0;i<5;i++){Check(r.Resolve(f.Unit() with{PlayerUid=10+i})==0);Check(r.Resolve(f.Unit(team:1) with{PlayerUid=20+i})==1);}Check(r.Resolve(f.Unit() with{TeamSide=101})==null);});
Test("inconsistent team roster is rejected",()=>{using var f=new Fixture();Reject(()=>new GameTeamResolver(new[]{f.Context.Value!.Teams[0],f.Context.Value.Teams[0]}));});
Test("world XZ coordinates round trip independently of world Y",()=>{var c=new CoordinateTransform(new(-200,7,300),.5);var p=new WorldPoint(-180,77,340);Check(c.ToPlane(p)==new Point2(10,20));Check(c.ToWorld(c.ToPlane(p),77)==p);});
Test("map has symmetric roots and cannot be assumed available",()=>{using var f=new Fixture();var grid=GameTerritoryMapProvider.CreateGrid(f.Map);Check(grid.Owner(0)==0&&grid.Owner(31)==1);Reject(()=>GameTerritoryMapProvider.CreateGrid(f.Map with{Available=Array.Empty<bool>()}));});
Test("protected spawn area cannot be contested or captured",()=>{using var f=new Fixture();for(int i=0;i<6;i++)Check(f.Step(f.Unit(team:1,x:5)));Check(f.Bridge.LastGood!.Territory.Cells[0].Owner==0&&!f.Bridge.LastGood.Territory.Cells[0].Contested);});
Test("symmetric teams advance equally with independent graphs",()=>{using var f=new Fixture();for(int i=0;i<4;i++)Check(f.Step(f.Unit(x:25),f.Unit(2,team:1,x:295)));var s=f.Bridge.LastGood!;Check(s.Territory.Cells[2].Owner==0&&s.Territory.Cells[29].Owner==1);Check(s.Result.Operational.Edges.Length==0);Check(s.Result.Operational.Nodes.All(n=>n.Connected));});

foreach(var expected in new[]{RouteState.Reachable,RouteState.Blocked,RouteState.Unknown})Test("navigation result "+expected,()=>{
    var n=new NavRig();n.Begin(0);Check(n.Adapter.Query(n.Query).State==RouteState.Pending);n.Backend.Answers.Enqueue(new(n.Backend.Requests[0].RequestId,expected,true,10));n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==expected);
});
Test("partial path is Unknown, never Blocked",()=>{var n=new NavRig();n.Begin(0);n.Adapter.Query(n.Query);n.Backend.Answers.Enqueue(new(1,RouteState.Blocked,false,0));n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==RouteState.Unknown);});
Test("route timeout is Unknown and late response cannot revive it",()=>{var n=new NavRig();n.Begin(0);n.Adapter.Query(n.Query);n.Begin(2);n.Backend.Answers.Enqueue(new(1,RouteState.Reachable,true,10));n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==RouteState.Unknown&&n.Adapter.DiscardedAnswers==1);});
Test("expired cache is Stale before requery",()=>{var n=new NavRig();n.Backend.AutoAnswer=true;n.Begin(0);n.Adapter.Query(n.Query);n.Adapter.Poll();n.Begin(2);Check(n.Adapter.Query(n.Query).State==RouteState.Stale);});
Test("removed endpoint invalidates pending route",()=>{var n=new NavRig();n.Begin(0);var q=n.Query;n.Adapter.Query(q);n.Units=n.Units.Take(1).ToArray();n.Begin(1);n.Backend.Answers.Enqueue(new(1,RouteState.Reachable,true,10));n.Adapter.Poll();Check(n.Adapter.Query(q).State==RouteState.Stale&&n.Adapter.DiscardedAnswers==1);});
Test("new generation never reuses old endpoint response",()=>{var n=new NavRig();n.Begin(0);n.Adapter.Query(n.Query);n.Units[1]=n.Units[1] with{Key=n.Units[1].Key with{Generation=2}};n.Begin(1);n.Backend.Answers.Enqueue(new(1,RouteState.Reachable,true,10));n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==RouteState.Pending&&n.Adapter.DiscardedAnswers==1);});
Test("dynamic bridge and navigation revision invalidate cached reachability",()=>{var n=new NavRig();n.Backend.AutoAnswer=true;n.Begin(0);n.Adapter.Query(n.Query);n.Adapter.Poll();n.Revision=2;n.Backend.State=RouteState.Blocked;n.Begin(1);Check(n.Adapter.Query(n.Query).State==RouteState.Pending);n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==RouteState.Blocked);});
Test("masks channels and movement types are separate cache keys",()=>{var n=new NavRig(new(10));n.Begin(0);n.Adapter.Query(n.Query);n.Adapter.Query(n.Query with{Mask=2});n.Adapter.Query(n.Query with{Channel=NavigationChannel.GroundPassage});n.Adapter.Query(n.Query with{MovementType=3});Check(n.Backend.Requests.Count==4);});
Test("route budget limits submissions and pending queue",()=>{var n=new NavRig(new(1,1));n.Begin(0);n.Adapter.Query(n.Query);Check(n.Adapter.Query(n.Query with{Mask=2}).State==RouteState.Pending);Check(n.Backend.Requests.Count==1&&n.Adapter.PendingCount==1);});
Test("too long route is Unknown rather than physical Blocked",()=>{var n=new NavRig();n.Begin(0);n.Adapter.Query(n.Query);n.Backend.Answers.Enqueue(new(1,RouteState.Reachable,true,101));n.Adapter.Poll();Check(n.Adapter.Query(n.Query).State==RouteState.Unknown);});
Test("backend exception is Unknown",()=>{var n=new NavRig();n.Backend.Throw=true;n.Begin(0);Check(n.Adapter.Query(n.Query).State==RouteState.Unknown);});
Test("Unknown navigation never confirms encirclement",()=>{using var f=new Fixture();f.Routes.AutoAnswer=false;for(int i=0;i<15;i++)Check(f.Step(f.Unit(x:155,z:155)));Check(f.Bridge.LastGood!.Result.Isolation.Units.Single().State!=IsolationState.Encircled);});
Test("per-profile channel and range affect links, not territory",()=>{using var f=new Fixture();var p=f.Profiles.Document.Units.Select(p=>p.UnitDefinitionId==3?p with{ConnectivityChannel=NavigationChannel.GroundPassage}:p).ToArray();f.Profiles=new(f.Profiles.Document with{Units=p});for(int i=0;i<3;i++)f.Step(f.Unit(),f.Unit(2,3,x:45),f.Unit(3,2,x:75));Check(f.Bridge.LastGood!.Result.Operational.Edges.Length==0);});

Test("default settings perform no collection and no mutation option exists",()=>{using var f=new Fixture(settings:new());Check(!f.Step(f.Unit())&&f.Access.Revision==0);Reject(()=>new IntegrationSettings(EnableGameStateModification:true).Validate());});
Test("render updates do not repeat logical tick",()=>{using var f=new Fixture();Check(f.Step(f.Unit()));Check(!f.Bridge.Update(1)&&!f.Bridge.Update(249));Check(f.Bridge.LastGood!.Result.Header.Tick==0);Check(f.Step(f.Unit()));Check(f.Bridge.LastGood.Result.Header.Tick==1);});
Test("provider loss preserves previous accepted snapshot",()=>{using var f=new Fixture();f.Step(f.Unit());var old=f.Bridge.LastGood;f.Access.Throw=true;Check(!f.Step(f.Unit())&&ReferenceEquals(old,f.Bridge.LastGood));});
Test("absent match context waits safely",()=>{using var f=new Fixture();f.Context.Value=null;Check(!f.Bridge.Update(0)&&f.Bridge.Health.State==IntegrationState.WaitingForMatch);});
Test("scenario switch to unknown database clears old match result",()=>{using var f=new Fixture();f.Step(f.Unit());f.Context.Value=f.Context.Value! with{Database=f.Context.Value!.Database with{ScenarioId="different"}};Check(!f.Step(f.Unit())&&f.Bridge.LastGood==null);});
Test("new match starts tick and generations from scratch",()=>{using var f=new Fixture();f.Step(f.Unit());f.Context.Value=f.Context.Value! with{MatchId=Guid.NewGuid()};Check(f.Step(f.Unit())&&f.Bridge.LastGood!.Result.Header.Tick==0&&f.Bridge.LastGood.Input.Units[0].Key.Generation==1);});
Test("navigation revision changes preserve territorial history",()=>{using var f=new Fixture();f.Step(f.Unit());f.Context.Value=f.Context.Value! with{NavigationRevision=2};Check(f.Step(f.Unit())&&f.Bridge.LastGood!.Result.Header.Tick==1);});
Test("profile version change restarts cleanly",()=>{using var f=new Fixture();f.Step(f.Unit());f.Profiles=new(f.Profiles.Document with{Version="v2"});Check(f.Step(f.Unit())&&f.Bridge.LastGood!.Result.Header.Tick==0);});
Test("live source cannot be enabled by ordinary settings",()=>{using var f=new Fixture();f.Context.Value=f.Context.Value! with{Origin=DataOrigin.LiveReadOnly};Check(!f.Step(f.Unit())&&f.Access.Revision==0);});
Test("disposed bridge never calls provider",()=>{using var f=new Fixture();f.Bridge.Dispose();Check(!f.Bridge.Update(0)&&f.Access.Revision==0&&f.Bridge.Health.State==IntegrationState.Disposed);});

Test("hidden entities cannot change public DTO bytes or handles or counts",()=>{
    using var a=new Fixture();using var b=new Fixture(match:a.Match);var own=a.Unit();var hidden=b.Unit(999,2,1,235,95);
    a.Step(own);b.Step(own,hidden);var va=new TeamSafeProjection();var vb=new TeamSafeProjection();
    var ja=JsonSerializer.Serialize(va.Project(a.Bridge.LastGood!.Input,0,0));var jb=JsonSerializer.Serialize(vb.Project(b.Bridge.LastGood!.Input,0,0));Check(ja==jb);
    a.Step(own);b.Step(own,hidden with{WorldPosition=new(305,0,205),LifecycleStamp="other"},b.Unit(555,3,1,275,75));
    Check(JsonSerializer.Serialize(va.Project(a.Bridge.LastGood!.Input,0,1))==JsonSerializer.Serialize(vb.Project(b.Bridge.LastGood!.Input,0,1)));
});
foreach(var flag in new[]{"history","potential","shootable","marker","confirmed"})Test("FOW evidence never silently authorizes public enemy export "+flag,()=>{
    using var f=new Fixture();var v=flag switch{"history"=>new RawVisibility(true,false,false,false,Array.Empty<int>()),"potential"=>new(false,true,false,false,Array.Empty<int>()),"shootable"=>new(false,false,true,false,Array.Empty<int>()),"marker"=>new(false,false,false,true,Array.Empty<int>()),_=>new(false,false,false,false,new[]{0})};
    f.Step(f.Unit(),f.Unit(2,2,1,215,55) with{Visibility=v});var safe=new TeamSafeProjection().Project(f.Bridge.LastGood!.Input,0,0);Check(safe.FriendlyUnits.Length==1);
});
Test("team-safe diagnostic has no graph territory route or rejected-counter metadata",()=>{using var f=new Fixture();f.Step(f.Unit());var dto=new TeamSafeProjection().Project(f.Bridge.LastGood!.Input,0,0);string json=new FrontlineDiagnosticsService(new()).ExportTeam(dto,0,f.Match);foreach(string field in new[]{"Graph","Revision","UnknownProfiles","PendingRoutes","Severity","CaptureReason","Uid","Generation"})Check(!json.Contains(field));Reject(()=>new FrontlineDiagnosticsService(new()).ExportTeam(dto,1,f.Match));});
Test("developer full export requires explicit offline flag",()=>{using var f=new Fixture();f.Step(f.Unit());Reject(()=>new FrontlineDiagnosticsService(new()).ExportDeveloper(f.Bridge.LastGood!));var live=f.Bridge.LastGood! with{Input=f.Bridge.LastGood!.Input with{Context=f.Context.Value! with{Origin=DataOrigin.LiveReadOnly}}};Reject(()=>new FrontlineDiagnosticsService(new(true,true,true)).ExportDeveloper(live));});
Test("internal exceptions never appear in public status",()=>{using var f=new Fixture();f.Access.Throw=true;f.Step(f.Unit());Check(new FrontlineDiagnosticsService(new()).PublicStatus()=="Read-only diagnostics");});
Test("moving hidden recon and special forces produces no territorial delta",()=>{using var f=new Fixture();for(int i=0;i<5;i++){f.Step(f.Unit(1,2,1,205+i*10,105),f.Unit(2,3,1,205,125+i*10));Check(f.Bridge.LastGood!.Result.Delta.Cells.Length==0);}});

ReflectionTests.Run(Test,Check);
Test("collection-only mode retains copied DTO without starting core",()=>{using var f=new Fixture(settings:new(true,false));Check(f.Step(f.Unit()));Check(f.Bridge.LastGood==null&&f.Bridge.LastInspection!.Units.Length==1&&f.Bridge.Health.State==IntegrationState.DiagnosticsOnly);});
Test("snapshot roster is not a borrowed mutable array",()=>{using var f=new Fixture();f.Step(f.Unit());var snapshot=f.Bridge.LastInspection!;f.Context.Value!.Teams[0].PlayerUids[0]=999;Check(snapshot.Context.Teams[0].PlayerUids[0]==10);});
Test("profile may disable local circles without disabling capture",()=>{using var f=new Fixture();f.Profiles=new(f.Profiles.Document with{Units=f.Profiles.Document.Units.Select(p=>p.UnitDefinitionId==1?p with{CanCreateLocalArea=false}:p).ToArray()});f.Step(f.Unit(x:155));Check(f.Bridge.LastGood!.Result.LocalAreas.Length==0);});
Test("vehicle relay has no fabricated soldiers and never captures",()=>{using var f=new Fixture();for(int i=0;i<3;i++)f.Step(f.Unit(id:4));var s=f.Bridge.LastGood!;Check(s.Input.Units.Single().AliveSoldierCount==null&&s.Result.Operational.Nodes.Single().Connected&&s.Result.Delta.Cells.Length==0);});
Test("Pass03 unknown death lifecycle rejects complete-looking snapshot",()=>{using var f=new Fixture();Check(!f.Step(f.Unit() with{DeathState=GameDeathState.Unknown})&&f.Bridge.Health.LastFailure==FailureCode.Incomplete);});
Test("Pass03 processing death is excluded before entity disposal",()=>{using var f=new Fixture();Check(f.Step(f.Unit() with{DeathState=GameDeathState.DeathProcessing}));Check(f.Bridge.LastGood!.Result.Operational.Nodes.Length==0&&f.Bridge.LastGood.Input.Units.Single().CaptureReason==CaptureExclusion.Dead);});
Test("Pass03 one-way passage does not create an undirected relay edge",()=>{using var f=new Fixture();f.Routes.CustomAnswer=r=>r.Query.A.Position.X>r.Query.B.Position.X?RouteState.Blocked:RouteState.Reachable;for(int i=0;i<3;i++)f.Step(f.Unit(),f.Unit(2,3,x:35));Check(f.Bridge.LastGood!.Result.Operational.Edges.Length==0&&f.Bridge.LastGood.Result.Operational.Nodes.All(n=>!n.Connected));});
Test("Pass03 native partial endpoint and entry-exit codes stay Unknown",()=>{foreach(byte s in new byte[]{0,2,3,5,6,7})Check(NativeNavigationStatusPolicy.Path(s,true,true)==RouteState.Unknown);Check(NativeNavigationStatusPolicy.Path(1)==RouteState.Unknown&&NativeNavigationStatusPolicy.Path(4)==RouteState.Unknown);Check(NativeNavigationStatusPolicy.Passage(3,true)==RouteState.Unknown&&NativeNavigationStatusPolicy.Passage(4,true)==RouteState.Unknown);});
Test("ownership transfer invalidates an outstanding route",()=>{var n=new NavRig();n.Begin(0);var q=n.Query;n.Adapter.Query(q);n.Units[1]=n.Units[1] with{Team=1};n.Begin(1);n.Backend.Answers.Enqueue(new(1,RouteState.Reachable,true,10));n.Adapter.Poll();Check(n.Adapter.Query(q).State==RouteState.Stale&&n.Adapter.DiscardedAnswers==1);});
File.WriteAllText("artifacts/poc02/integration-tests.json",JsonSerializer.Serialize(new{total=results.Count,failed=failures,results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"TOTAL {results.Count}; FAILED {failures}");
if(failures==0)Diagnostics.Build();
return failures==0?0:1;
