using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Poc030;
if(args.Length>0 && args[0]=="benchmark"){PipelineBench.Run(args[1]);return 0;}
if(args.Length>0 && args[0]=="peer")
{
    string role=args[1];int port=int.Parse(args[2]);var key=File.ReadAllBytes(args[3]);
    var h=new Hello("df030-status-1","offline-loopback-test",role,"fixture-build","fixture-payload","fixture-rules","Unknown","Unknown","fixture-abi","NoWrites");
    using var channel=new DevChannel(new(false,false,"OfflineReplay",h.Session,role,true,"127.0.0.1",port,""),h,key);
    var run=channel.Run();await Task.Delay(1500);bool success=channel.State=="AuthenticatedStatusOnly"&&channel.Peer!=null;
    File.WriteAllText(args[4],JsonSerializer.Serialize(new {Role=role,channel.State,Peer=channel.Peer,Success=success,Kind="TwoProcessLoopback_NotGame"}));await Task.Delay(2000);channel.Dispose();await run;return success?0:1;
}
int passed=0;
void Check(string name,bool value){if(!value)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
void Reject(string name,Action action){try{action();}catch(InvalidDataException){Check(name,true);return;}throw new Exception("Expected rejection: "+name);}
var secret=RandomNumberGenerator.GetBytes(32);
var a=new Hello("df030-status-1","session","A","build","payload","rules","profile","map","abi","caps");var b=a with {Role="B"};
var frame=new StatusFrame(1,1,1,"Incomplete",2,"hash");var signed=Protocol.Sign(b,frame,secret);
Protocol.Verify(signed,a,secret,0);Check("Valid authenticated peer",true);
Reject("Wrong secret",()=>Protocol.Verify(signed,a,RandomNumberGenerator.GetBytes(32),0));
Reject("Replay",()=>Protocol.Verify(signed,a,secret,1));
Reject("Gap",()=>Protocol.Verify(Protocol.Sign(b,frame with {Sequence=3},secret),a,secret,0));
foreach(var mismatch in new[]{b with {Session="other"},b with {Role="A"},b with {Compatibility="other"},b with {Payload="other"},b with {Rules="other"},b with {Profile="other"},b with {Map="other"},b with {Abi="other"},b with {Capabilities="other"}})Reject("Handshake "+mismatch,()=>Protocol.Verify(Protocol.Sign(mismatch,frame,secret),a,secret,0));
Reject("Tampered payload",()=>Protocol.Verify(signed with {Status=frame with {OwnedCount=100}},a,secret,0));
using(var stream=new MemoryStream()){await Protocol.Write(stream,signed,CancellationToken.None);stream.Position=0;Check("Framed roundtrip",await Protocol.Read(stream,CancellationToken.None)==signed);}
using(var stream=new MemoryStream(new byte[]{127,255,255,255})){try{await Protocol.Read(stream,CancellationToken.None);throw new Exception("Oversize accepted");}catch(InvalidDataException){Check("Size before allocation",true);}}
foreach(var mode in Enum.GetValues<RuntimeMode>())Check("No native writer "+mode,!Gates.CanWrite(mode,new("ClassStressApply",true,true,true,true,true,"Even forged flags cannot add a writer")));
Check("Line explicit",Gates.CaptureEligible(30,1,new HashSet<int>{1}));Check("Assault explicit",Gates.CaptureEligible(31,1,new HashSet<int>{1}));Check("Assault unreviewed",!Gates.CaptureEligible(31,1,new HashSet<int>()));
foreach(int id in new[]{710,711,713})Check("Unreviewed new unit "+id,!Gates.CaptureEligible(30,id,new HashSet<int>{id}));
var temp=Path.Combine(Path.GetTempPath(),"df030-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try{File.WriteAllText(Path.Combine(temp,"pair.bin"),"old");var d=new GameCompatibilityDescriptor("build","unity",39,"schema",new(){["pair.bin"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(temp,"pair.bin"))))});d.Verify(temp);Check("Pinned file",true);File.WriteAllText(Path.Combine(temp,"pair.bin"),"new");Reject("Changed pair",()=>d.Verify(temp));}
finally{File.Delete(Path.Combine(temp,"pair.bin"));Directory.Delete(temp);}
var relief=new StreamingRelief();
relief.Accept(new("unit","repair",0,ReceiptStage.Begin,0,0,"fixture"),0);Check("Begin alone is not relief",relief.At("unit",0)=="NoRelief");
relief.Accept(new("unit","repair",1,ReceiptStage.PositiveRecipientReceipt,1,10,"fixture"),1);Check("Positive receipt starts relief",relief.At("unit",1)=="BeingSupplied");Check("Lost service source is unknown",relief.At("unit",3)=="Unknown_ServiceStreamStale");
relief.Accept(new("unit","repair",2,ReceiptStage.End,11,0,"fixture"),11);Check("Grace capped at 20",relief.At("unit",30)=="PostSupplyGrace"&&relief.At("unit",31)=="NoRelief");
relief.Accept(new("unit","heal",0,ReceiptStage.Begin,12,0,"fixture"),12);relief.Accept(new("unit","heal",1,ReceiptStage.PositiveRecipientReceipt,12,1,"fixture"),12);relief.Accept(new("unit","heal",2,ReceiptStage.End,13,0,"fixture"),13);Check("Short service cannot shorten grace",relief.At("unit",30)=="PostSupplyGrace");
Reject("Future service rejected",()=>relief.Accept(new("unit","future",0,ReceiptStage.Begin,100,0,"fixture"),1));
var journal=new EffectJournal(1);Check("First effect reserved",journal.Reserve("e","h")==ApplyDisposition.New);Check("Ambiguous write not retried",journal.Reserve("e","h")==ApplyDisposition.Ambiguous);journal.Confirm("e","h");Check("Duplicate no-op",journal.Reserve("e","h")==ApplyDisposition.Duplicate);Check("Conflict blocked",journal.Reserve("e","other")==ApplyDisposition.Conflict);Check("Bounded journal fails closed",journal.Reserve("other","h")==ApplyDisposition.BudgetExceeded);
var identities=new IdentityBindings();var canonical=new CanonicalIdentity("m",7,1);var binding=new Binding(new(1,2,3),1,1,canonical);identities.Bind(binding,false);Reject("Unknown rebind resync",()=>identities.Bind(binding with {Epoch=2,Owner=2},false));identities.Bind(binding with {Epoch=2,Owner=2},true);Check("Ownership is not new generation",identities.Find(canonical)?.Canonical==canonical);
Check("Descriptor digest does not recurse",new GameCompatibilityDescriptor("b","u",39,"s",new(){["x"]="h"}).Digest.Length==64);
Reject("Connection replay nonce",()=>Protocol.Verify(Protocol.Sign(b with {Connection="old"},frame,secret),a with {Connection="new"},secret,0));
Reject("Malformed envelope",()=>Protocol.Verify(signed with {Hello=null!},a,secret,0));
Console.WriteLine($"RESULT {passed}/{passed}; Offline only; no IL2CPP/runtime claims");return 0;
