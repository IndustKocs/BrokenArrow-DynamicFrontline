using DynamicFrontline.Poc030;
using DynamicFrontline.Steam;
using System.Text.Json;
public static class HardeningTests
{
 public static void LoaderRecovery(string manifest,string target)
 {
  target=Path.GetFullPath(target);var allowed=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;if(!target.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||Directory.Exists(target))throw new InvalidDataException("Fresh artifact sandbox only");
  var files=JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(manifest))!;var proxy=files.Single(f=>f.Relative=="version.dll");Directory.CreateDirectory(target);File.Copy(proxy.Source,Path.Combine(target,"version.dll"));var plan=InstallPlanning.Plan(target,files);bool interrupted=false;
  try{using var installer=new DevInstall(target,true,true,phase=>{if(phase=="after-replace:version.dll")throw new IOException("Injected loader transaction interruption");});installer.Apply(plan);}catch(IOException){interrupted=true;}
  if(!interrupted)throw new Exception("Fault not reached");using(var recovered=new DevInstall(target,true,true))recovered.Recover();
  if(DevInstall.Hash(Path.Combine(target,"version.dll"))!=proxy.Sha256||File.Exists(Path.Combine(target,"Mods/DynamicFrontline.Steam.MelonLoader.dll")))throw new Exception("Loader rollback mismatch");
  Console.WriteLine("PASS interrupted complete loader/mod install restores original proxy and removes new plugin");
 }
 public static void Run(Action<bool,string> check)
 {
  string root=Path.Combine("artifacts","t18-hardening-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  string context=CanonicalCodec.Digest("test-session");var digests=Rc2Fixtures.Bridge().Step(Rc2Fixtures.Input(),0);
  object R(long id,string ctx,string source="Coordinator")=>new{Kind="Result",SessionHash=ctx,ResultSource=source,InputId=id,Digests=digests,Secret="PRIVATE_KEY_CANARY",SteamId="76561198000000001",Enemy=new{X=111,Z=222}};
  var events=new DfEvents();var lines=new List<string>();for(int i=0;i<400;i++)events.Structured(R(i,context));for(int i=0;i<4;i++)events.Flush(i,null,lines.Add);
  check(lines.Count==400&&lines.All(l=>l.Length>320),"all 400 complete Result records survive burst without truncation/throttle");
  string raw=Path.Combine(root,"source");Directory.CreateDirectory(raw);File.WriteAllLines(Path.Combine(raw,"user-76561198000000001-secret.jsonl"),lines);
  File.AppendAllLines(Path.Combine(raw,"user-76561198000000001-secret.jsonl"),new[]{"[DF] {\"kind\":\"Status\",\"detail\":\"C:\\\\Users\\\\PERSON_CANARY TOKEN_CANARY 26.137.77.181\"}",JsonSerializer.Serialize(new{Kind="ReplayInput",Secret="SECRET_CANARY",Units=new[]{new{X=12345}}}),JsonSerializer.Serialize(new{Kind="SourceMetric",Owner=76561198000000001,ReadMilliseconds=1.2,Participants=new[]{"HIDDEN_CANARY"}})});
  string export=Path.Combine(root,"export");DiagnosticTools.Collect(raw,export);string text=string.Join("",Directory.GetFiles(export).Select(File.ReadAllText));
  check(!new[]{"CANARY","76561198000000001","26.137.77.181","Participants","Enemy","ReplayInput","Secret"}.Any(text.Contains),"export strips secrets, identifiers, paths, unknown fields and hidden world");
  check(Directory.GetFiles(export).All(f=>!Path.GetFileName(f).Contains("7656")),"export anonymizes source filenames");
  var comparison=JsonSerializer.SerializeToElement(DiagnosticTools.Compare(raw,export));check(comparison.GetProperty("Passed").GetBoolean()&&comparison.GetProperty("Compared").GetInt32()==400,"raw DF and exported diagnostics compare losslessly");
  string receipt=Path.Combine(root,"receipt.jsonl");File.WriteAllLines(receipt,Enumerable.Range(0,400).Select(i=>JsonSerializer.Serialize(R(i,context,"CoordinatorReceipt"))));
  var received=JsonSerializer.SerializeToElement(DiagnosticTools.Compare(export,receipt));check(received.GetProperty("Passed").GetBoolean()&&!received.GetProperty("IndependentRecomputationVerified").GetBoolean(),"receipt match is never presented as independent recomputation");
  string other=Path.Combine(root,"other.jsonl");File.WriteAllText(other,JsonSerializer.Serialize(R(0,CanonicalCodec.Digest("other-session"))));check(!JsonSerializer.SerializeToElement(DiagnosticTools.Compare(receipt,other)).GetProperty("Passed").GetBoolean(),"reused input IDs from different sessions never match");
  string broken=Path.Combine(root,"broken");Directory.CreateDirectory(broken);File.WriteAllText(Path.Combine(broken,"x.jsonl"),"[DF] {\"kind\":\"Result\",\"detail\":\"old truncated data\"}\n{bad-json");var brokenExport=Path.Combine(root,"broken-export");var report=JsonSerializer.SerializeToElement(DiagnosticTools.Collect(broken,brokenExport));check(!report.GetProperty("Complete").GetBoolean()&&!JsonSerializer.SerializeToElement(DiagnosticTools.Compare(brokenExport,brokenExport)).GetProperty("Passed").GetBoolean(),"corrupt or old truncated Results mark export incomplete and comparison fails");
  var overflow=new DfEvents();for(int i=0;i<520;i++)overflow.Structured(R(i,context));check(overflow.Faulted,"Result queue overflow explicitly latches failure");
  int ice=-1;var policy=new RelayOnlyPolicy(v=>{ice=v;return true;},()=>ice);check(ice==0,"Relay-only disables ICE before sessions");
  ice=4;bool denied=false;try{policy.Verify();}catch(InvalidDataException){denied=true;}check(denied,"external direct-policy change fails closed");
  denied=false;try{new RelayOnlyPolicy(_=>false,()=>0);}catch(InvalidDataException){denied=true;}check(denied,"unsupported Relay policy cannot silently connect");
  denied=false;try{RelayOnlyPolicy.CheckConnectedRoute(3,0,0);}catch(InvalidDataException){denied=true;}check(denied,"connected direct route rejected");
  RelayOnlyPolicy.CheckConnectedRoute(3,16,123);check(true,"encrypted authenticated SDR route accepted by policy");
  denied=false;try{RelayOnlyPolicy.CheckConnectedRoute(3,18,123);}catch(InvalidDataException){denied=true;}check(denied,"unencrypted relayed connection rejected");
 }
}
