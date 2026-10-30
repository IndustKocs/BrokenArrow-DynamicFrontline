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
HardeningTests.Run(Check);
FinalPreflightTests.Run(Check);
Console.WriteLine($"T18: {passed} checks passed; synthetic transport, NOT real Steam or game validation.");
void CheckSend(bool value){if(!value)throw new Exception("Unexpected queue failure");}
sealed class FakeWire : ISteamWire {public FakeWire Other=null!;public ConcurrentQueue<byte[]> In=new();public void Send(byte[] packet)=>Other.In.Enqueue(packet);public bool TryReceive(out byte[] packet)=>In.TryDequeue(out packet!);}
