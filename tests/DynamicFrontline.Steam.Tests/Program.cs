using System.Text.Json;
using System.Collections.Concurrent;
using DynamicFrontline.Poc030;
using DynamicFrontline.Steam;
using DynamicFrontline.Frontline;
if(args.Length==3&&args[0]=="loader-recovery"){HardeningTests.LoaderRecovery(args[1],args[2]);return;}
int passed=0;
void Check(bool b,string text){if(!b)throw new Exception(text);Console.WriteLine("PASS "+text);passed++;}
Hello H(string role)=>new("df18-world-1",Rc2Fixtures.Binding.ModSession.ToString(),role,"fixture-build","payload","rules","profile","map","abi","SteamReadOnly");
(FakeWire a,FakeWire b) W(){var a=new FakeWire();var b=new FakeWire();a.Other=b;b.Other=a;return(a,b);}
void Pump(SteamWorldChannel a,SteamWorldChannel b,int n=20){for(int i=0;i<n;i++){a.Pump();b.Pump();}}
{
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));using var b=new SteamWorldChannel(w2,H("B"));Pump(a,b);
 Check(a.Authenticated&&b.Authenticated&&a.ConnectionEpoch==b.ConnectionEpoch,"ephemeral Steam channel authentication / equal epoch");
 WorldMessage? received=null;b.Received+=m=>{if(m.Kind=="Blob")received=m;};a.Send("Blob",new{Text=new string('x',160000)});Pump(a,b,40);
 Check(received?.Payload.GetProperty("Text").GetString()?.Length==160000,"fragmented payload >48KB arrives intact");
 string old=a.ConnectionEpoch;w1.In.Clear();w2.In.Clear();a.Restart();b.Restart();Pump(a,b);Check(a.Authenticated&&b.Authenticated&&old!=a.ConnectionEpoch,"reconnect rotates ECDH nonce/epoch");
 w1.In.Clear();w2.In.Clear();a.Restart();Pump(a,b,60);Check(a.Authenticated&&b.Authenticated,"one-sided restart negotiates peer resync");
 var tampered=JsonSerializer.SerializeToUtf8Bytes(new SteamPacket("Chunk",JsonSerializer.SerializeToElement(new WireChunk("bad",1,1,1,"00",[1]))));w2.In.Enqueue(tampered);b.Pump();Check(!b.Authenticated,"out-of-order fragment stops world");
}
{
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));using var b=new SteamWorldChannel(w2,H("B") with{Payload="different"});Pump(a,b);Check(!a.Authenticated&&!b.Authenticated,"mismatched package cannot become ready");
}
{
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));using var b=new SteamWorldChannel(w2,H("A"));Pump(a,b);Check(!a.Authenticated&&!b.Authenticated,"same role rejected");
}
{
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));for(int i=0;i<32;i++)CheckSend(a.Send("x",new{i}));Check(!a.Send("overflow",0),"outgoing queue bounded at32");
}
{
 var bridge=Rc2Fixtures.Bridge();var input=Rc2Fixtures.Input();bridge.Step(input,0);var display=bridge.Display(0);display.Validate();var cells=bridge.Export().Core.Territory.Cells;
 Check(display.OwnCells.All(i=>cells.Single(c=>c.Index==i).Owner==0),"territory projection excludes foreign ownership");
 Check(display.PocketCells.Length==0,"open fixture invents no pocket");
 Check(display.Forecast.All(p=>p.Delta==null),"unknown supply does not invent a stress penalty");
 Check(bridge.Project(0).Own.Single().Stress==80,"observed stress remains actual game value");
 bool failed=false;try{(display with{PocketCells=[int.MaxValue]}).Validate();}catch(InvalidDataException){failed=true;}Check(failed,"invalid display index rejected");
}
{
 var e=new DfEvents();var log=new List<string>();for(int i=0;i<500;i++)e.Add("Repeat","state");e.Flush(1,null,log.Add);Check(log.Count==1&&e.Dropped>0&&log[0].StartsWith("[DF] "),"event storm bounded and duplicates rate-limited");e.Add("Second","ok");e.Flush(4,_=>throw new IOException(),log.Add);Check(e.SinkState.Contains("Fallback")&&e.Snapshot().Length>0,"native logger failure retains backup");
}
// Real RuntimeObserver + Simulation023, with synthetic observations over the new serialized wire.
{
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));using var b=new SteamWorldChannel(w2,H("B"));
 ProbeConfig C(string role)=>new(false,false,"ObserveTwoClient",Rc2Fixtures.Binding.ModSession.ToString(),role,true,"",0,"",Replication:"TeamSafe");
 using var oa=new RuntimeObserver(C("A"),a,"fixture-build",null,Rc2Fixtures.Map,Rc2Fixtures.Profiles,true);using var ob=new RuntimeObserver(C("B"),b,"fixture-build",null,Rc2Fixtures.Map,Rc2Fixtures.Profiles,true);
 var observedA=new ConcurrentQueue<object>();var observedB=new ConcurrentQueue<object>();oa.Diagnostic+=observedA.Enqueue;ob.Diagnostic+=observedB.Enqueue;
 var ta=oa.Run();var tb=ob.Run();long rev=0;bool complete=false;double prior=0;string priorEpoch="";
 for(int i=0;i<200;i++){if(i%10==0){rev++;oa.Submit(Rc2Fixtures.Observation("A",rev,rev*.25));ob.Submit(Rc2Fixtures.Observation("B",rev,rev*.25));}Pump(a,b,2);await Task.Delay(10);if(oa.View.Display!=null&&ob.View.Display!=null){complete=true;prior=oa.View.Display.Time;priorEpoch=a.ConnectionEpoch;break;}}
 Check(complete,"Steam wire -> owned observations -> canonical world -> both graphical projections");
 Check(observedA.Any(r=>JsonSerializer.SerializeToElement(r).GetProperty("Kind").GetString()=="Result")&&observedB.Any(r=>JsonSerializer.SerializeToElement(r).GetProperty("Kind").GetString()=="Result"),"both real observers emit structured coordinator/result receipts");
 w1.In.Clear();w2.In.Clear();a.Restart();b.Restart();bool resumed=false;
 for(int i=0;i<200;i++){if(i%10==0){rev++;oa.Submit(Rc2Fixtures.Observation("A",rev,rev*.25));ob.Submit(Rc2Fixtures.Observation("B",rev,rev*.25));}Pump(a,b,2);await Task.Delay(10);if(a.ConnectionEpoch!=priorEpoch&&oa.View.Display?.Time>prior&&ob.View.Display?.Time>prior){resumed=true;break;}}
 Check(resumed,"existing core survives reconnect and resumes paired projection");
 oa.Fail(SourceState.LivePartial,"unvalidated roster");ob.Fail(SourceState.LivePartial,"unvalidated roster");await Task.Delay(100);Check(oa.View.Projection==null&&oa.View.Display==null&&ob.View.Display==null,"incomplete source clears graphics");
 oa.Dispose();ob.Dispose();await Task.WhenAll(ta,tb);
}
// Explicit trusted FullDebug pair reuses the canonical replica protocol and existing core.
{
 Check(DebugViewConsent.Epoch("a","b",true)==DebugViewConsent.Epoch("b","a",false),"both clients derive identical consent epoch");
 Check(DebugViewConsent.Epoch("a","b",true)!=DebugViewConsent.Epoch("new","b",true),"reconsent creates fresh diagnostic session scope");
 for(int mask=0;mask<16;mask++)Check(DebugViewConsent.Enabled((mask&1)!=0,(mask&2)!=0,(mask&4)!=0,(mask&8)!=0)==(mask==15),"FullDebug requires both consents and both rosters "+mask);
 var(w1,w2)=W();using var a=new SteamWorldChannel(w1,H("A"));using var b=new SteamWorldChannel(w2,H("B"));
 ProbeConfig C(string role)=>new(false,false,"ObserveTwoClient",Rc2Fixtures.Binding.ModSession.ToString(),role,true,"",0,"",Replication:"TrustedDiagnosticReplica",TrustedReplicaConsent:true,FullDebugView:true);
 using var oa=new RuntimeObserver(C("A"),a,"fixture-build",null,Rc2Fixtures.Map,Rc2Fixtures.Profiles,true);using var ob=new RuntimeObserver(C("B"),b,"fixture-build",null,Rc2Fixtures.Map,Rc2Fixtures.Profiles,true);
 var ta=oa.Run();var tb=ob.Run();bool complete=false;long rev=0;
 for(int i=0;i<300;i++){if(i%10==0){rev++;oa.Submit(Rc2Fixtures.Observation("A",rev,rev*.25));ob.Submit(Rc2Fixtures.Observation("B",rev,rev*.25));}Pump(a,b,2);await Task.Delay(10);if(oa.View.Display?.FullDebug==true&&ob.View.Display?.FullDebug==true&&oa.View.Projection?.Own.Length==2&&ob.View.Projection?.Own.Length==2&&oa.View.Display.Time==ob.View.Display.Time){complete=true;break;}}
 Check(complete,"FullDebug two observers independently render both teams from canonical input");
 if(complete){oa.View.Display!.Validate(true);ob.View.Display!.Validate(true);Check(CanonicalCodec.Digest(oa.View.Projection!.Circles)==CanonicalCodec.Digest(ob.View.Projection!.Circles),"FullDebug synchronized ZOC geometry");Check(oa.View.Display.OwnCells.Concat(oa.View.Display.OtherCells??[]).OrderBy(i=>i).SequenceEqual(ob.View.Display.OwnCells.Concat(ob.View.Display.OtherCells??[]).OrderBy(i=>i)),"FullDebug synchronized two-team territory geometry");}
 oa.Fail(SourceState.NoSession,"ConsentRevoked");ob.Fail(SourceState.NoSession,"ConsentRevoked");Check(oa.View.Display==null&&ob.View.Display==null,"consent withdrawal clears full view before new TeamSafe session");
 oa.Dispose();ob.Dispose();await Task.WhenAll(ta,tb);
 bool denied=false;try{using var invalid=new RuntimeObserver(C("A") with{TrustedReplicaConsent=false},a,"fixture");}catch(InvalidDataException){denied=true;}Check(denied,"FullDebug observer cannot be created without explicit replica consent");
 var bridge=Rc2Fixtures.Bridge();bridge.Step(Rc2Fixtures.Input(),0);var frame=bridge.Last! with{Geometry=[new(DynamicFrontline.Frontline.Rules023.CorridorState.Blocked,DynamicFrontline.Frontline.Rules023.Evidence.True,new(0,DynamicFrontline.Frontline.Rules023.Mobility.Infantry,1,[1,2,3],[4],"hidden","fixture",true,false))]};
 var full=DisplayProjection.Build(bridge.Export(),frame,0,true);Check(full.PocketCells.SequenceEqual(new[]{1,2,3}),"consented full pocket geometry retained exactly");
 denied=false;try{full.Validate();}catch(InvalidDataException){denied=true;}Check(denied,"TeamSafe rejects a full debug payload even with a FullDebug flag");
}
{
 var bridge=Rc2Fixtures.Bridge();var input=Rc2Fixtures.Input();var units=Enumerable.Range(0,100).Select(i=>input.Units[i%2] with{Key=input.Units[i%2].Key with{Uid=100+i},Visible=false,Position=new DynamicFrontline.Frontline.Point2(50+(i%10)*140,50+(i/10)*140)}).ToArray();bridge.Step(input with{Units=units},0);
 var timer=System.Diagnostics.Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++){bridge.Display(0,true).Validate(true);bridge.DebugProject();}timer.Stop();
 Check(bridge.DebugProject().Own.Length==100&&bridge.DebugProject().Circles.Length<=100,"FullDebug complete 100-unit projection within canonical budget");Console.WriteLine($"METRIC FullDebug 100 units / 100 projections: {timer.Elapsed.TotalMilliseconds:F2} ms, {GC.GetAllocatedBytesForCurrentThread()-bytes} bytes; managed projection only, NOT Unity FPS");
}
HardeningTests.Run(Check);
FinalPreflightTests.Run(Check);
{
 int reads=0;object? ReadMissing(object o,string name){reads++;return null;}
 Check(RuntimeStartup.RoomState(null,ReadMissing)==null&&reads==0,"menu without controller does not dereference native state");
 Check(RuntimeStartup.RoomState(new object(),ReadMissing)==null&&reads==1,"controller without room remains outside match");
 var controller=new object();var room=new object();var state=new object();
 Check(RuntimeStartup.RoomState(controller,(o,n)=>n=="_networkRoomService"?room:null)==null,"room without state remains outside match");
 Check(ReferenceEquals(RuntimeStartup.RoomState(controller,(o,n)=>n=="_networkRoomService"?room:state),state),"room-state identity preserved for existing policy gate");
 bool throws=false;try{RuntimeStartup.RoomState(controller,(_,_)=>throw new MissingMemberException("fixture"));}catch(MissingMemberException){throws=true;}
 Check(throws,"missing binding is not silently treated as an allowed session");
 var detail=RuntimeStartup.Fault("Hud.OnGUI",new System.Reflection.TargetInvocationException(new NullReferenceException("SECRET_123 C:/private/user.txt 26.1.2.3")));
 Check(detail.Contains("NullReferenceException")&&!detail.Contains("SECRET")&&!detail.Contains("private")&&!detail.Contains("26.1"),"fault diagnostic unwraps reflection and excludes messages/paths/identifiers");
 var clean=SafeDiagnostics.Sanitize(JsonSerializer.SerializeToElement(new{kind="HudUnavailable",detail}));
 Check(clean!.Value.GetRawText().Contains("Hud.OnGUI")&&clean.Value.GetRawText().Contains("NullReferenceException"),"safe export preserves bounded HUD error codes");
 var boot=new DfEvents();var lines=new List<string>();boot.Add("Build","Bootstrap");boot.Flush(0,null,lines.Add);
 Check(lines.Count==1,"bootstrap JSONL does not depend on native logger initialization");
}
Console.WriteLine($"T18: {passed} checks passed; synthetic transport, NOT real Steam or game validation.");
void CheckSend(bool value){if(!value)throw new Exception("Unexpected queue failure");}
sealed class FakeWire : ISteamWire {public FakeWire Other=null!;public ConcurrentQueue<byte[]> In=new();public void Send(byte[] packet)=>Other.In.Enqueue(packet);public bool TryReceive(out byte[] packet)=>In.TryDequeue(out packet!);}
