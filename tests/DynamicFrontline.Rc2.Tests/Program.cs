using System.Text.Json;
using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
using DynamicFrontline.Core;
using static Fixtures023;
if(args.Length>0&&args[0] is "benchmark-full" or "benchmark-events"){FullPipelineBenchmark.Run(args[1],args[2],int.Parse(args[3]),int.Parse(args[4]),int.Parse(args[5]),args[0]=="benchmark-events");return 0;}
if(args.Length>0&&args[0]=="crash-install"){
 var crashFiles=JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(args[2]))!;int point=int.Parse(args[3]),n=0;
 using var installer=new DevInstall(args[1],true,true,_=>{if(++n==point)Environment.FailFast("Injected installer process termination");});installer.Apply(crashFiles);return 0;
}
if(args.Length>0&&args[0] is "runtime-peer-v2" or "runtime-resync"){
 string role=args[1];var config=new ProbeConfig(false,false,"ObserveTwoClient",Rc2Fixtures.Binding.ModSession.ToString(),role,true,"127.0.0.1",int.Parse(args[2]),args[3],args[4],args[5],"TrustedDiagnosticReplica",true);
 var hello=new Hello("df030-world-2",config.SessionId,role,"fixture-build","fixture-payload","fixture-rules","fixture-profile","fixture-map","fixture-ABI","NoWrites");
 var runtimeMap=args.Length>7?JsonSerializer.Deserialize<MapDescriptor>(File.ReadAllText(args[7]))!:Rc2Fixtures.Map;
 var runtimeProfiles=Rc2Fixtures.Profiles with{BuildFingerprint=runtimeMap.GameFingerprint};
 Observation Sample(string peer,long revision,double time){var o=Rc2Fixtures.Observation(peer,revision,time);if(runtimeMap.Scenario==null)return o;return o with{Map=runtimeMap.Map,Participants=Rc2Fixtures.Binding.Players.Select(p=>p with{TeamUid=p.Owner==1?1:7}).ToArray(),Owned=o.Owned.Select(u=>u with{TeamUid=u.Owner==1?1:7}).ToArray(),MapState=new(runtimeMap.Scenario,runtimeMap.Map,30,0,0,9000,9000,"Synthetic observations for real static Airbase map; not game")};}
 using var channel=new DevChannel(config,hello,File.ReadAllBytes(args[3]));using var observer=new RuntimeObserver(config,channel,runtimeMap.GameFingerprint,null,runtimeMap,runtimeProfiles);
 var events=new System.Collections.Concurrent.ConcurrentQueue<string>();observer.Diagnostic+=e=>events.Enqueue(JsonSerializer.Serialize(e));
 var transport=channel.RunWorld();var worker=observer.Run();var watch=System.Diagnostics.Stopwatch.StartNew();long revision=0;bool success=false,dropped=false;bool reconnect=args[0]=="runtime-resync";
 while(watch.Elapsed.TotalSeconds<12){double time=reconnect?(watch.Elapsed.TotalSeconds<4?.25:.5):.25;observer.Submit(Sample(role,++revision,time));if(reconnect&&role=="B"&&!dropped&&watch.Elapsed.TotalSeconds>2){channel.DisconnectForDiagnosticResync();dropped=true;}await Task.Delay(100);if(observer.View.InputHash.Length>0&&observer.View.Projection!=null&&(!reconnect||watch.Elapsed.TotalSeconds>7&&observer.View.Projection.Time==.5)){success=true;break;}}
 File.WriteAllLines(args[6]+".jsonl",events);
 await Task.Delay(300);File.WriteAllText(args[6],JsonSerializer.Serialize(new{Role=role,Success=success,observer.View.InputHash,observer.View.Status,observer.View.Blockers,Kind="IndependentSourceTransportCanonicalCoreProjection_NotGame",channel.State,channel.LastTransportError}));observer.Dispose();channel.Dispose();await Task.WhenAll(worker,transport);return success?0:1;
}
if(args.Length>0&&args[0]=="peer-v2"){
 string role=args[1];int port=int.Parse(args[2]);var secret=File.ReadAllBytes(args[3]);var config=new ProbeConfig(false,false,"OfflineReplay","rc2-two-process",role,true,"127.0.0.1",port,"",args[4],args[5],"TrustedDiagnosticReplica",true);
 var hello=new Hello("df030-world-2",config.SessionId,role,"fixture-build","fixture-payload","fixture-rules","fixture-profile","fixture-map","fixture-ABI","NoWrites");
 using var channel=new DevChannel(config,hello,secret);var bridge=Rc2Fixtures.Bridge();bool success=false;string? hash=null;StageDigests? own=null;
 channel.Received+=message=>{if(role=="B"&&message.Kind=="CanonicalInput"){var input=message.Payload.Deserialize<CanonicalInput>()!;own=bridge.Step(input,0);hash=input.Hash;success=true;channel.Send("ResultDigest",own);}else if(role=="A"&&message.Kind=="ResultDigest"){var result=message.Payload.Deserialize<StageDigests>()!;success=result==own;hash=result.Input;}};
 var run=channel.RunWorld();var watch=System.Diagnostics.Stopwatch.StartNew();bool sent=false;
 while(watch.Elapsed.TotalSeconds<12&&!success){if(role=="A"&&channel.State=="AuthenticatedWorldTls"&&!sent){var input=Rc2Fixtures.Input();own=bridge.Step(input,0);channel.Send("CanonicalInput",input);sent=true;}await Task.Delay(50);}
 await Task.Delay(500);File.WriteAllText(args[6],JsonSerializer.Serialize(new{Role=role,Success=success,InputHash=hash,Digests=own,Kind="IndependentTwoProcessCanonicalCore_NotGame",channel.State,channel.LastTransportError}));channel.Dispose();await run;return success?0:1;
}
var output=args.FirstOrDefault()??"artifacts/poc030_rc2/tests";Directory.CreateDirectory(output);var results=new List<object>();int failed=0;
void Test(string name,Action run){try{run();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}catch(Exception e){failed++;results.Add(new{name,passed=false,error=e.ToString()});Console.WriteLine("FAIL "+name+": "+e.Message);}}
void Assert(bool value){if(!value)throw new Exception("Assertion");}
void Reject(Action run){try{run();}catch(Exception e)when(e is InvalidDataException or IOException or ArgumentException or JsonException){return;}throw new Exception("Expected rejection");}
var sandbox=Path.Combine(Path.GetFullPath(output),"sandbox-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sandbox);
var source=Path.Combine(sandbox,"source");Directory.CreateDirectory(source);File.WriteAllText(Path.Combine(source,"owned.dll"),"synthetic binary fixture");File.WriteAllText(Path.Combine(source,"probe.json"),"{\"Mode\":\"LoadReadProbe\",\"Role\":\"A\"}");
InstallFile[] files=[new("UserLibs/DynamicFrontline.Poc030.dll",Path.Combine(source,"owned.dll"),DevInstall.Hash(Path.Combine(source,"owned.dll")),FileCategory.ImmutablePayload),new("UserData/DynamicFrontline030/probe.json",Path.Combine(source,"probe.json"),DevInstall.Hash(Path.Combine(source,"probe.json")),FileCategory.MutableUserConfig)];
Test("R1 configure remove preserve reinstall",()=>{var target=Path.Combine(sandbox,"кириллица пробел");using var i=new DevInstall(target,true,true);i.Apply(files);File.WriteAllText(Path.Combine(target,files[1].Relative),"{\"Mode\":\"LoadReadProbe\",\"Role\":\"B\"}");Assert(i.Verify().Conflicts.Length==0);i.Remove();Assert(!File.Exists(Path.Combine(target,files[0].Relative)));Assert(File.ReadAllText(Path.Combine(target,files[1].Relative)).Contains("B"));i.Apply(files);Assert(File.ReadAllText(Path.Combine(target,files[1].Relative)).Contains("B"));});
Test("Launcher explicit owned-settings removal preserves unrelated files",()=>{
 var target=Path.Combine(sandbox,"launcher-remove-settings");using var installer=new DevInstall(target,true,true);installer.Apply(files);var cfg=Path.Combine(target,files[1].Relative);File.WriteAllText(cfg,"{\"Mode\":\"LoadReadProbe\",\"Role\":\"B\"}");File.WriteAllText(Path.Combine(target,"unrelated.txt"),"foreign");installer.Remove(false);Assert(!File.Exists(cfg)&&File.ReadAllText(Path.Combine(target,"unrelated.txt"))=="foreign");
});
Test("R1 RC1 receipt migration preserves configured role without manual editing",()=>{
 var target=Path.Combine(sandbox,"legacy");foreach(var file in files){var dest=Path.Combine(target,file.Relative);Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(file.Source,dest);}
 File.WriteAllText(Path.Combine(target,"UserData/DynamicFrontline030/install-receipt.json"),JsonSerializer.Serialize(new{Schema=1,Files=files.Select(f=>new{f.Relative,f.Sha256})}));
 File.WriteAllText(Path.Combine(target,files[1].Relative),"{\"Mode\":\"LoadReadProbe\",\"Role\":\"B\"}");using var installer=new DevInstall(target,true,true);installer.Apply(files);Assert(installer.Verify().Conflicts.Length==0);installer.Remove();Assert(File.ReadAllText(Path.Combine(target,files[1].Relative)).Contains("B"));Assert(!File.Exists(Path.Combine(target,files[0].Relative)));
});
Test("R1 changed DLL conflict retained independently",()=>{var target=Path.Combine(sandbox,"changed");using var i=new DevInstall(target,true,true);i.Apply(files);File.WriteAllText(Path.Combine(target,files[0].Relative),"other content");Assert(i.Remove().Conflicts.Length==1);Assert(File.ReadAllText(Path.Combine(target,files[0].Relative))=="other content");});
Test("R2 injected exception all apply boundaries recover twice",()=>{for(int at=1;at<=13;at++){var target=Path.Combine(sandbox,"exception-"+at);int n=0;try{using var i=new DevInstall(target,true,true,_=>{if(++n==at)throw new IOException("Injected");});i.Apply(files);}catch(IOException){}using var recovery=new DevInstall(target,true,true);Assert(recovery.Recover().Conflicts.Length==0);Assert(recovery.Recover().State=="NoPendingOperation");}});
Test("R2 fresh process recovery after fail-fast at every apply boundary",()=>{
 var manifest=Path.Combine(source,"manifest.json");File.WriteAllText(manifest,JsonSerializer.Serialize(files));
 for(int at=1;at<=12;at++){
  var target=Path.Combine(sandbox,"killed-"+at);Directory.CreateDirectory(target);var foreign=Path.Combine(target,"foreign.txt");File.WriteAllText(foreign,"preserve");
  var start=new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
  foreach(var arg in new[]{System.Reflection.Assembly.GetExecutingAssembly().Location,"crash-install",target,manifest,at.ToString()})start.ArgumentList.Add(arg);
  using var child=System.Diagnostics.Process.Start(start)!;var error=child.StandardError.ReadToEndAsync();var stdout=child.StandardOutput.ReadToEndAsync();if(!child.WaitForExit(15000)){child.Kill();throw new Exception("Crash child timeout");}
  File.WriteAllText(Path.Combine(output,"crash-"+at+".log"),"Exit="+child.ExitCode+"\n"+error.GetAwaiter().GetResult());Assert(child.ExitCode!=0);
  using var recovery=new DevInstall(target,true,true);Assert(recovery.Recover().Conflicts.Length==0);Assert(recovery.Recover().State=="NoPendingOperation");Assert(File.ReadAllText(foreign)=="preserve");recovery.Apply(files);Assert(recovery.Verify().Conflicts.Length==0);
 }
});
Test("R2 interrupted upgrade and removal preserve original receipt and bytes",()=>{
 var updatePath=Path.Combine(source,"updated.dll");File.WriteAllText(updatePath,"updated fixture");var updated=files.Select(f=>f.Category==FileCategory.ImmutablePayload?f with{Source=updatePath,Sha256=DevInstall.Hash(updatePath)}:f).ToArray();
 for(int point=1;point<=12;point++){
  var target=Path.Combine(sandbox,"upgrade-"+point);using(var init=new DevInstall(target,true,true))init.Apply(files);int n=0;try{using var upgrade=new DevInstall(target,true,true,_=>{if(++n==point)throw new IOException("Upgrade crash");});upgrade.Apply(updated);}catch(IOException){}
  using(var recover=new DevInstall(target,true,true)){Assert(recover.Recover().Conflicts.Length==0);Assert(recover.Verify().Conflicts.Length==0);}
 }
 for(int point=1;point<=3;point++){
  var target=Path.Combine(sandbox,"remove-"+point);using(var init=new DevInstall(target,true,true))init.Apply(files);int n=0;try{using var removal=new DevInstall(target,true,true,_=>{if(++n==point)throw new IOException("Remove crash");});removal.Remove();}catch(IOException){}
  using var recover=new DevInstall(target,true,true);Assert(recover.Recover().Conflicts.Length==0);Assert(recover.Verify().Conflicts.Length==0);Assert(File.Exists(Path.Combine(target,files[0].Relative)));
 }
});
Test("R2 wrong package hash foreign same name and concurrent operations",()=>{var target=Path.Combine(sandbox,"foreign");using var i=new DevInstall(target,true,true);Reject(()=>i.Apply([files[0] with{Sha256="BAD"}]));Directory.CreateDirectory(Path.Combine(target,"UserLibs"));File.WriteAllText(Path.Combine(target,files[0].Relative),"foreign");Reject(()=>i.Apply(files));Reject(()=>{using var other=new DevInstall(target,true,true);});Assert(File.ReadAllText(Path.Combine(target,files[0].Relative))=="foreign");});
Test("R2 shared cache replacement removal restores original files",()=>{
 var target=Path.Combine(sandbox,"shared-cache");var relative="MelonLoader/Il2CppAssemblies/Il2CppFixture.dll";var path=Path.Combine(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"original foreign cache");
 var raw=new[]{new InstallFile(relative,files[0].Source,files[0].Sha256,FileCategory.SharedInteropCache),files[0]};var plan=InstallPlanning.Plan(target,raw);Assert(plan[0].ReplaceApprovedHash==DevInstall.Hash(path));
 using var installer=new DevInstall(target,true,true);installer.Apply(plan);Assert(DevInstall.Hash(path)==files[0].Sha256);installer.Remove();Assert(File.ReadAllText(path)=="original foreign cache");Assert(!File.Exists(Path.Combine(target,files[0].Relative)));
});
Test("R2 shared cache crash rollback and changed cache preserve group",()=>{
 for(int point=1;point<=8;point++){var target=Path.Combine(sandbox,"cache-crash-"+point);var relative="MelonLoader/Il2CppAssemblies/Il2CppFixture.dll";var path=Path.Combine(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,"original");var plan=InstallPlanning.Plan(target,[new(relative,files[0].Source,files[0].Sha256,FileCategory.SharedInteropCache)]);int n=0;try{using var installer=new DevInstall(target,true,true,_=>{if(++n==point)throw new IOException("Crash");});installer.Apply(plan);}catch(IOException){}using var recovery=new DevInstall(target,true,true);Assert(recovery.Recover().Conflicts.Length==0);if(File.ReadAllText(path)!="original")recovery.Remove();Assert(File.ReadAllText(path)=="original");}
 var root=Path.Combine(sandbox,"cache-modified");var rel="MelonLoader/Il2CppAssemblies/Il2CppFixture.dll";var raw=new[]{new InstallFile(rel,files[0].Source,files[0].Sha256,FileCategory.SharedInteropCache),files[0]};using var i=new DevInstall(root,true,true);i.Apply(raw);File.WriteAllText(Path.Combine(root,rel),"foreign update");Assert(i.Remove().Conflicts.Contains(rel));Assert(File.ReadAllText(Path.Combine(root,rel))=="foreign update");Assert(!File.Exists(Path.Combine(root,files[0].Relative)));
});
Test("R3 heartbeat cannot freshen failed source and pause is live",()=>{var h=new SourceHealthTracker();h.Success(new(1,1,10,true,1,2,"map","live",true,"Complete",[]),0);h.Success(new(2,1,10,true,1,2,"map","live",true,"Complete",[]),1);Assert(h.At(1).State==SourceState.LiveComplete);h.Failure(SourceState.ReadUnavailable,"reader error");Assert(h.At(5).ObservationRevision==2&&h.At(5).AgeSeconds==4&&h.At(5).State==SourceState.ReadUnavailable);h.Failure(SourceState.NoSession,"menu");Assert(h.At(6).State==SourceState.NoSession);});
Test("R5 observed 80 20 80 follows actual state and no backend feedback",()=>{var map=Map();var sim=new Simulation023(Session,new(),new(),map,new ProfileCatalog([new(1,UnitRole.Line,true,false,1,50)]),new Rules(),Enumerable.Repeat(true,map.Grid.Count).ToArray(),SimulationLane.Observe);var u=Unit(1) with{ObservedMaxStress=100};int n=0;foreach(var stress in new[]{80f,20f,80f}){var level=stress==80?StressLevel.Panicked:StressLevel.Calm;var f=sim.Step(new(Session,"Zoc_023/1.2",new Rules023Config().Hash,n,n*.25,(n+1)*.25,[u with{Stress=stress,Level=level}],true,[],[],[],false,false));Assert(f.Zoc[0].Unit.Level==level&&f.Stress[0].Stress==stress&&sim.Project(0).Own[0].Stress==stress);Assert(sim.Stress.Current().Length==0);n++;}});
Test("R6 independent core same canonical input no local handle hash",()=>{var input=Rc2Fixtures.Input();var a=Rc2Fixtures.Bridge();var b=Rc2Fixtures.Bridge();Assert(a.Step(input,0)==b.Step(input with{Units=input.Units.Reverse().ToArray()},0));Assert(a.Step(input,0)==b.Step(input,0));Reject(()=>a.Step(input with{End=.5},0));});
Test("R6 world partial stale wrong owner delta gap and conflict reject",()=>{var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());var o=Rc2Fixtures.Observation("A",1,.25);w.Full("A",o);w.Full("A",o);Reject(()=>w.Full("A",o with{GameTime=.5}));Reject(()=>w.Full("B",o));Reject(()=>w.Delta("A",new(0,o,[],[])));Assert(w.Build(new(SourceState.LiveComplete,1,1,.25,false,0,"fixture"),new(SourceState.Stale,1,1,.25,false,2,"fixture"))==null);});
Test("R6 duplicate delta is idempotent and conflicting duplicate rejected",()=>{var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());var o=Rc2Fixtures.Observation("A",1,.25);w.Full("A",o);var d=new OwnedDelta(1,o with{Revision=2,GameTime=.5},[],[]);w.Delta("A",d);w.Delta("A",d);var restored=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());restored.Restore(w.Export());restored.Delta("A",d);Reject(()=>w.Delta("A",d with{RemovedNetIds=[1]}));});
Test("R6 checkpoint restore preserves core and next canonical id",()=>{var input=Rc2Fixtures.Input();var a=Rc2Fixtures.Bridge();a.Step(input,0);var state=JsonSerializer.Deserialize<ObservationCheckpoint>(JsonSerializer.Serialize(a.Export()))!;var b=Rc2Fixtures.Bridge();b.Restore(state);var next=input with{InputId=1,Start=.25,End=.5,DataGap=false};Assert(a.Step(next,0)==b.Step(next,0));});
Test("R6 authenticated chunk roundtrip truncated and oversized reject",()=>{var h=new Hello("world","session","A","build","payload","rules","profile","map","abi","no writes","epoch");var key=new byte[32];var m=new WorldMessage("FullState",JsonSerializer.SerializeToElement(new{data=new string('x',100000)}));var envelope=WorldProtocol.Sign(h,1,m,key);using var stream=new MemoryStream();WorldProtocol.Write(stream,envelope,CancellationToken.None).GetAwaiter().GetResult();var bytes=stream.ToArray();stream.Position=0;var copy=WorldProtocol.Read(stream,CancellationToken.None).GetAwaiter().GetResult();WorldProtocol.Verify(copy,h with{Role="B"},0,key);Assert(copy.Digest==envelope.Digest);using var cut=new MemoryStream(bytes[..^3]);Reject(()=>WorldProtocol.Read(cut,CancellationToken.None).GetAwaiter().GetResult());using var huge=new MemoryStream(new byte[]{127,255,255,255});Reject(()=>WorldProtocol.Read(huge,CancellationToken.None).GetAwaiter().GetResult());});
Test("R5 live observer survives projected terminal time",()=>{var map=Map(8,50);var sim=new Simulation023(Session,new(),new(),map,new ProfileCatalog([new(1,UnitRole.Line,true,false,1,50)]),new Rules(),Enumerable.Repeat(true,64).ToArray(),SimulationLane.Observe);sim.ObservationGap(2699.75);var u=Unit(1,125,125);sim.Step(new(Session,"Zoc_023/1.2",new Rules023Config().Hash,1,2699.75,2700,[u],true,[],[],[],false,false));var f=sim.Step(new(Session,"Zoc_023/1.2",new Rules023Config().Hash,2,2700,2700.25,[u with{Stress=20}],true,[],[],[],false,false));Assert(f.Time==2700.25&&f.Stress[0].Stress==20);});
Test("R6 rejected profile leaves spawn registry transactional",()=>{var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());var a=Rc2Fixtures.Observation("A",1,.25);var b=Rc2Fixtures.Observation("B",1,.25);w.Full("A",a);w.Full("B",b with{Owned=[b.Owned[0] with{Definition=999}]});var h=new SourceHealth(SourceState.LiveComplete,1,1,.25,false,0,"fixture");Reject(()=>w.Build(h,h));Assert(w.Export().Spawns.Length==0&&w.Export().Next==0);});
Test("R6 lifecycle death disappearance reuse owner transfer and resync",()=>{var r=new SpawnRegistry();var u=Rc2Fixtures.Observation("A",1,.25).Owned[0];Assert(r.Resolve(u)==0);Assert(r.Resolve(u with{Alive=false})==0);Assert(r.Resolve(u)==1);r.RetireMissing([],new());Assert(r.Resolve(u)==2);Assert(r.Resolve(u with{Owner=2})==3);var restored=new SpawnRegistry();restored.Restore(r.Export());Assert(restored.Resolve(u with{Owner=2})==3);Assert(restored.Resolve(u with{Owner=2,Local=u.Local with{Version=2}})==4);});
Test("R5 active profile mismatch never gets a fallback",()=>{var u=Rc2Fixtures.Observation("A",1,.25).Owned[0];foreach(var bad in new[]{u with{LoadedDatabaseMaxStress=0},u with{Definition=710},u with{Role=31},u with{ShockThreshold=null}})Reject(()=>Rc2Fixtures.Profiles.Resolve(bad));});
Test("Map runtime bounds scenario and root teams are mandatory",()=>{var m=Rc2Fixtures.Map with{Map="Airport",Scenario="Airport_network"};var o=Rc2Fixtures.Observation("A",1,.25);Reject(()=>m.CheckObserved(o));var state=new NativeMapSample("Airport_network","Airport",23,0,0,1600,1600,"fixture");o=o with{MapState=state,Participants=[new(1,1,101,false),new(2,4,102,false)]};m.CheckObserved(o);Reject(()=>m.CheckObserved(o with{MapState=state with{SizeX=9000}}));Reject(()=>m.CheckObserved(o with{MapState=state with{Scenario="other"}}));});
Test("Two independent pocket certificates do not merge interiors",()=>{var m=Map(140,50);var units=new List<Unit023>();long id=100;foreach(double cx in new[]{1800d,5000d}){units.Add(Unit((long)cx,cx,3500,p:Infantry with{CanProjectZoc=false}));for(int i=0;i<12;i++)units.Add(Unit(id++,cx+650*Math.Cos(i*Math.PI/6),3500+650*Math.Sin(i*Math.PI/6),1,Tank));}var z=CombinedArmsRadiusResolver.Resolve(units.ToArray(),m,new());var f=new ZocInfluenceField(m);f.Update(z);var d=new EncirclementRegionDetector(m,new());var a=d.Evaluate(units[0],z,f);var b=d.Evaluate(units[13],z,f);Assert(a.Enclosed==Evidence.True&&b.Enclosed==Evidence.True);Assert(!a.Certificate!.Interior.Intersect(b.Certificate!.Interior).Any());});
Test("Persistent breakthrough survives conflicting local score projection",()=>{var m=Map(20,50);for(int x=0;x<16;x++)for(int y=8;y<13;y++)m.Grid.Seed(y*20+x,0);var score=new TerritoryScoringProjection(m.Grid,Enumerable.Repeat(true,400).ToArray());score.Update([new(Unit(999).Key,1,new(450,525),400,0)]);Assert(score.EffectiveOwner.Any(o=>o<0));Assert(new BreakthroughEvaluator(m,new(){RequiredWidth=250}).Evaluate(score,true)[0]==Evidence.True);});
Test("Diagnostics replay and comparison use actual matching inputs",()=>{var input=Rc2Fixtures.Input();var result=Rc2Fixtures.Bridge().Step(input,0);var path=Path.Combine(output,"replay.jsonl");File.WriteAllLines(path,new[]{JsonSerializer.Serialize(new{Kind="ReplayContext",Binding=Rc2Fixtures.Binding,Map=Rc2Fixtures.Map,Profiles=Rc2Fixtures.Profiles,Fingerprint="fixture-build"}),JsonSerializer.Serialize(new{Kind="ReplayInput",Input=input}),JsonSerializer.Serialize(new{Kind="Result",input.InputId,Digests=result})});Assert(JsonSerializer.SerializeToElement(DiagnosticTools.Replay(path)).GetProperty("Passed").GetBoolean());Assert(JsonSerializer.SerializeToElement(DiagnosticTools.Compare(path,path)).GetProperty("Compared").GetInt32()==1);var collected=Path.Combine(Path.GetDirectoryName(output)!,"collected-"+Guid.NewGuid().ToString("N"));DiagnosticTools.Collect(output,collected);Assert(Directory.GetFiles(collected,"*.jsonl").All(f=>!File.ReadAllText(f).Contains("ReplayInput")));Assert(!File.Exists(Path.Combine(collected,"replay.jsonl")));});

Test("Airbase actual descriptor roots and observed source contract",()=>{
 var m=JsonSerializer.Deserialize<MapDescriptor>(File.ReadAllText("config/poc030-rc2/maps/Airbase-5454584-100.json"))!;
 var t=m.Create("Airbase",m.GameFingerprint);Assert(m.Width==90&&m.Height==90&&m.CellSize==100&&m.Origin==new Point2(0,0)&&m.ActiveZoneId==null);
 foreach(var root in m.Roots)Assert(t.Passable[0][t.Grid.Index(root.Position)]);
 var a=Rc2Fixtures.Observation("A",1,.25) with{Map="Airbase",Participants=[new(1,1,101,false),new(2,7,102,false)],MapState=new("5454584","Airbase",30,0,0,9000,9000,"Fixture native sample")};
 m.CheckObserved(a);
 foreach(var n in new[]{a.MapState! with{Scenario="Airport_network"},a.MapState! with{NavigationName="Airport"},a.MapState! with{NavigationCellSize=23},a.MapState! with{MinX=1},a.MapState! with{SizeX=6500}})Reject(()=>m.CheckObserved(a with{MapState=n}));
 Reject(()=>m.CheckObserved(a with{Participants=[new(1,1,101,false),new(2,4,102,false)]}));
 Reject(()=>(m with{Scenario="unknown"}).CheckObserved(a));
});
Test("Airbase source hash covers scenario settings and metadata",()=>{
 var m=JsonSerializer.Deserialize<MapDescriptor>(File.ReadAllText("config/poc030-rc2/maps/Airbase-5454584-100.json"))!;var root=Path.Combine(sandbox,"airbase-sources");
 string[] paths=["BrokenArrow_Data/StreamingAssets/Navigation/Airbase.ng","Scenarios/5454584/5454584.bascr","BrokenArrow_Data/StreamingAssets/aa/PC/mapsettings_assets_mapsettingsairbase_f28604c0f1829a35d50f99e2b6dcdef2.bundle","Scenarios/5454584/meta.json"];
 foreach(var path in paths){var full=Path.Combine(root,path);Directory.CreateDirectory(Path.GetDirectoryName(full)!);File.WriteAllText(full,path);}
 var bytes=paths.SelectMany(p=>File.ReadAllBytes(Path.Combine(root,p))).ToArray();m=m with{SourceSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))};m.VerifyLocalSources(root);
 foreach(var path in paths){var full=Path.Combine(root,path);File.WriteAllText(full,"changed");Reject(()=>m.VerifyLocalSources(root));File.WriteAllText(full,path);}
});


Test("Radmin requires opt-in and exact coordinator and role",()=>{
 var c=new ProbeConfig(false,false,"NetworkProbe","fixture","A",true,EndpointPolicy.RadminA,47617,"");Reject(()=>EndpointPolicy.Validate(c));
 foreach(var role in new[]{"A","B"})Assert(EndpointPolicy.Validate(c with{Role=role,RadminPair=true}).ToString()==EndpointPolicy.RadminA);
 foreach(var host in new[]{EndpointPolicy.RadminB,"26.1.2.3","0.0.0.0","8.8.8.8","127.0.0.1"})Reject(()=>EndpointPolicy.Validate(c with{Host=host,RadminPair=true}));
 Reject(()=>EndpointPolicy.Validate(c with{RadminPair=true,Role="C"}));Reject(()=>EndpointPolicy.Validate(c with{RadminPair=true,Port=80}));
 Assert(EndpointPolicy.Validate(c with{Host="192.168.1.2"}).ToString()=="192.168.1.2");
});
Test("Radmin peers are pinned independently of HMAC identity",()=>{
 EndpointPolicy.VerifyRadminPeer("A",System.Net.IPAddress.Parse(EndpointPolicy.RadminB));EndpointPolicy.VerifyRadminPeer("B",System.Net.IPAddress.Parse(EndpointPolicy.RadminA));
 foreach(var host in new[]{"26.1.2.3","127.0.0.1","192.168.1.2"}){Reject(()=>EndpointPolicy.VerifyRadminPeer("A",System.Net.IPAddress.Parse(host)));Reject(()=>EndpointPolicy.VerifyRadminPeer("B",System.Net.IPAddress.Parse(host)));}
 Reject(()=>EndpointPolicy.VerifyRadminPeer("A",System.Net.IPAddress.Parse(EndpointPolicy.RadminA)));
});

Test("Loader cfg formatting requires original receipt hash and exact schema",()=>{
 string cfg="[Il2CppAssemblyGenerator]\nGameAssemblyHash = \"ABCD\"\nDeobfuscationRegex = \"\"\nUnityVersion = \"6000.3.24\"\nDumperVersion = \"2022.1.0-pre-release.21\"\nDumperSCRSVersion = \"2022.1.0-pre-release.21\"\nOldFiles = [\"A.dll\",\"B.dll\"]";
 string file=Path.Combine(sandbox,"cfg-original.txt");File.WriteAllText(file,cfg);string hash=DevInstall.Hash(file);
 string formatted=cfg.Replace("[\"A.dll\",\"B.dll\"]","[ \"A.dll\", \"B.dll\", ]").Replace("\n","\r\n")+"\r\n\r\n";
 Assert(LoaderConfigFormatting.Matches(formatted,hash));Assert(!LoaderConfigFormatting.Matches(formatted.Replace("ABCD","ABCE"),hash));Assert(!LoaderConfigFormatting.Matches(formatted+"Unknown = true",hash));Assert(!LoaderConfigFormatting.Matches(formatted.Replace("B.dll","A.dll"),hash));
 foreach(bool crash in new[]{false,true}){
  string target=Path.Combine(sandbox,"cfg-format-"+crash);var entry=new InstallFile(LoaderConfigFormatting.Relative,file,hash,FileCategory.SharedLoaderConfig);string dest=Path.Combine(target,entry.Relative);
  using(var init=new DevInstall(target,true,true))init.Apply([entry]);File.WriteAllText(dest,formatted);
  using(var verify=new DevInstall(target,true,true))Assert(verify.Verify().Conflicts.Length==0);
  if(crash){try{using var update=new DevInstall(target,true,true,phase=>{if(phase=="before-receipt")throw new IOException("fixture");});update.Apply([entry]);}catch(IOException){}using var recover=new DevInstall(target,true,true);Assert(recover.Recover().Conflicts.Length==0);Assert(File.ReadAllText(dest)==formatted);}
  else{using var update=new DevInstall(target,true,true);update.Apply([entry]);Assert(update.Verify().Conflicts.Length==0);}
  File.WriteAllText(dest,formatted);using var remove=new DevInstall(target,true,true);Assert(remove.Remove().Conflicts.Length==0);Assert(!File.Exists(dest));
 }
});

AdditionalRc2Tests.Run(Test,Assert,Reject,output,files,sandbox);
GeometryOracleTests.Run(Test,Assert);
File.WriteAllText(Path.Combine(output,"results.json"),JsonSerializer.Serialize(new{count=results.Count,failed,results},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine($"TOTAL {results.Count}; FAILED {failed}");return failed==0?0:1;
