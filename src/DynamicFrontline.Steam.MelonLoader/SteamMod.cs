using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline.Rules023;
using MelonLoader;
[assembly:MelonInfo(typeof(DynamicFrontline.Steam.MelonLoader.SteamMod),"DynamicFrontline T18 Steam Preview","0.4.0","DynamicFrontline")]
[assembly:MelonGame("SteelBalalaikaStudio","BrokenArrow")]
[assembly:MelonOptionalDependencies("UnityEngine.CoreModule","UnityEngine.IMGUIModule","UnityEngine.InputLegacyModule")]
namespace DynamicFrontline.Steam.MelonLoader;
public sealed class SteamMod : MelonMod
{
 readonly Stopwatch clock=Stopwatch.StartNew();readonly DfEvents events=new();string dir="",status="Preparing bindings",lastStatus="",compatibility="",scope="",connection="";bool enabled=true,initialized,failed,roster;double nextRead,nextContext,nextRetry,nextFault;int samples;bool interopReady,hudCallback,hudDrawn;string phase="Bootstrap";
 GameCompatibilityDescriptor? descriptor;MapDescriptor? map;ProfileSet? profiles;NativeContext? context;NativeSteam? steam;SteamDiscovery? discovery;NativeSteam.Wire? wire;SteamWorldChannel? channel;RuntimeObserver? observer;LiveReader? reader;Observation? latest;SteamHud? hud;UnityGamelog? gameLog;StreamWriter? backup;Task? worker;long fight;
 T Read<T>(string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir,name)))??throw new InvalidDataException(name);
 public override void OnInitializeMelon()
 {
  try{var root=Path.GetDirectoryName(Environment.ProcessPath)!;dir=Path.Combine(root,"UserData","DynamicFrontlineSteam");descriptor=Read<GameCompatibilityDescriptor>("compatibility.json");
   if(descriptor.BuildId!="26273833"||descriptor.Files.Count!=4||descriptor.Files.GetValueOrDefault("GameAssembly.dll")!="12E7088369539B029A7BF0064062CE030A53B5189E7067420903F47B4281FDDF"||descriptor.Files.GetValueOrDefault("BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat")!="A4EDE1B29664378A2FB9F952F65330A73DBFA115B205065D1831F420540AFE01"||descriptor.Files.GetValueOrDefault("UnityPlayer.dll")!="79A54AF1C83B5B3BFA34936BE18899DE49FA88EEA6A33BA7351C2163F95F6613"||descriptor.Files.GetValueOrDefault("BrokenArrow.exe")!="A0A2D82854AF8FB1718D70EC0BE0F4028A1332B13BAE89051EC8B368187F34B4")throw new InvalidDataException("UnsupportedGameBuild");
   descriptor.Verify(root);map=Read<MapDescriptor>("map.json");profiles=Read<ProfileSet>("profiles.json");map.VerifyLocalSources(root);
   string assembly=Path.GetDirectoryName(typeof(SteamMod).Assembly.Location)!;
   var hashes=new[]{"Steam.MelonLoader","Steam","Poc030","Core","Frontline","GameAdapter","Integration"}.Select(n=>new{Name=n,Hash=DevInstall.Hash(n=="Steam.MelonLoader"?typeof(SteamMod).Assembly.Location:Path.Combine(Path.GetDirectoryName(typeof(LiveReader).Assembly.Location)!,"DynamicFrontline."+n+".dll"))}).ToArray();
   compatibility=CanonicalCodec.Digest(new{Protocol="df18-world-4",Relay="RelayOnly_ICE0",Payload=hashes,Game=descriptor.Digest,Map=map.Hash,Profiles=profiles.Hash,Rules=new Rules023Config().Hash,Mode="ObserveOnly_TeamSafe"});
   Directory.CreateDirectory(Path.Combine(dir,"logs"));backup=new(Path.Combine(dir,"logs","DF-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".jsonl")){AutoFlush=true};events.Add("Build","T18 candidate; runtime validation pending; native writes OFF");events.Flush(clock.Elapsed.TotalSeconds,null,WriteBackup);LoggerInstance.Msg("DF bootstrap ready; awaiting Unity callbacks");
  }catch(Exception e){failed=true;status="Disabled: "+RuntimeStartup.Fault(phase,e);LoggerInstance.Error(status);}
 }
 void InitializeBindings()
 {
  phase="InteropGate";InteropGate.VerifyLoaded(Path.Combine(dir,"interop-provenance.json"),descriptor!,global::MelonLoader.Utils.MelonEnvironment.Il2CppAssembliesDirectory);interopReady=true;
  phase="LobbyHooks";context?.Dispose();context=new();try{context.Install();}catch{context.Dispose();context=null;throw;}
  initialized=true;phase="Waiting";status="Waiting for private Scenario 5454584";LoggerInstance.Msg("DF interop verified; Connect/Dispose handlers registered; native lobby observation pending");

 }
 public override void OnUpdate()
 {
  if(failed){FlushLogs();return;}
  try{
   if(interopReady&&!hudCallback&&clock.Elapsed.TotalSeconds>30&&clock.Elapsed.TotalSeconds>=nextFault){nextFault=clock.Elapsed.TotalSeconds+15;LoggerInstance.Warning("DF HudCallbackMissing: Melon OnGUI has not been received");events.Add("HudUnavailable","HudCallbackMissing");}
   if(!initialized){if(clock.Elapsed.TotalSeconds<nextRetry)return;nextRetry=clock.Elapsed.TotalSeconds+1;try{InitializeBindings();}catch(Exception e){status="Bindings waiting: "+phase;ReportFault(phase,e);FlushLogs();if(clock.Elapsed.TotalSeconds>60)throw;}return;}
   if(!enabled){FlushLogs();return;}
   if(clock.Elapsed.TotalSeconds>=nextContext){nextContext=clock.Elapsed.TotalSeconds+.5;phase="NativeContext.Read";var native=context!.Read();
    if(native==null){ResetAll();status=context.HasCapturedLobby?"Lobby service observed; waiting for private Scenario 5454584":"Waiting for native lobby Connect event; create or join private Scenario 5454584";FlushLogs();return;}
    if(scope!=native.Key){LoggerInstance.Msg("DF private Scenario lobby observed and validated");ResetAll();scope=native.Key;steam??=new();discovery=new(steam,scope,native.Host,compatibility);events.Add("Lobby","Native private Scenario observed; discovering Steam companion");}
    if(fight!=native.FightId){ResetWorld();fight=native.FightId;events.Add("Match",native.InMatch?"Native match entered":"Returned to lobby");}
    if(!native.InMatch){latest=null;observer?.Fail(SourceState.NoSession,"InLobby");}
   }
   if(discovery==null){FlushLogs();return;}phase="SteamDiscovery.Pump";discovery.Pump();status=discovery.State;
   if(!discovery.PairReady){latest=null;observer?.Fail(SourceState.NoSession,"SteamPeerNotReady");FlushLogs();return;}
   if(fight<=0){status="Steam companion ready; awaiting verified Scenario room";FlushLogs();return;}
   string expected=discovery.Session+"/"+discovery.Peer+"/"+discovery.Mode+"/"+fight+"/"+discovery.FullDebug+"/"+discovery.ConsentEpoch;
   // Native FightId is checked by the existing session binding; lobby peer readiness is independent of game reads.
   if(connection!=expected){if(clock.Elapsed.TotalSeconds<nextRetry)return;nextRetry=clock.Elapsed.TotalSeconds+2;ResetWorld();phase="StartWorld";StartWorld(expected);}
   if(channel!.State.StartsWith("SteamDisconnected:")&&clock.Elapsed.TotalSeconds>=nextRetry){nextRetry=clock.Elapsed.TotalSeconds+3;channel.Restart();events.Add("Reconnect","Preserving observer/core; requesting checkpoint and full snapshots");}
   channel.Pump();if(channel.State.StartsWith("SteamDisconnected:")&&(channel.State.Contains("Relay",StringComparison.Ordinal)||channel.State.Contains("PayloadBeforeRelayProof",StringComparison.Ordinal)))throw new InvalidDataException("RelayFailure_StopDF_RestartGameForDefaults");status=discovery.State+" / "+channel.State;
   if(fight>0&&channel.Authenticated&&clock.Elapsed.TotalSeconds>=nextRead){nextRead=clock.Elapsed.TotalSeconds+.25;
    try{var guard=context!.Read();if(guard==null||!guard.InMatch||guard.FightId!=fight)throw new InvalidDataException("MatchChanged");latest=reader!.Read();samples++;observer!.Submit(latest);events.Add("Roster",$"own={latest.Owned.Length}; complete={latest.Complete}; quality={latest.Quality}; fight={latest.FightId}");}
    catch(Exception e){latest=null;observer!.Fail(SourceState.ReadUnavailable,e.GetType().Name);events.Add("ReadUnavailable",e.GetType().Name+":"+e.Message);}
   }
   FlushLogs();if(events.Faulted)throw new InvalidDataException("ResultLogOverflow_Stop");
  }catch(Exception e){enabled=false;status="Stopped: "+RuntimeStartup.Fault(phase,e);ReportFault(phase,e,true);try{ResetAll();}catch(Exception cleanup){ReportFault("ResetAll",cleanup,true);}events.Add("Stopped",status);FlushLogs();}
 }
 void StartWorld(string id)
 {
  var d=discovery!;var session=Guid.Parse(d.Session);
  // New fight/mode => separate memory/nonce scope. Identical on both clients without manual config.
  byte[] bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(session+"/"+fight+"/"+d.Mode+"/"+d.FullDebug+"/"+d.ConsentEpoch));var matchSession=new Guid(bytes.Take(16).ToArray()).ToString();
  var cfg=new ProbeConfig(true,true,d.Mode,matchSession,d.Role,true,"",0,"",Replication:d.FullDebug?"TrustedDiagnosticReplica":"TeamSafe",TrustedReplicaConsent:d.FullDebug,FullDebugView:d.FullDebug,RosterContractConfirmed:roster&&d.Mode=="ObserveTwoClient");
  var hello=new Hello("df18-world-4",cfg.SessionId,cfg.Role,descriptor!.Digest,compatibility,new Rules023Config().Hash,profiles!.Hash,map!.Hash,"ua01-rc2-read-contract","SteamReadOnly/"+cfg.Mode+"/"+cfg.Replication);
  steam!.ActivateRelay();
  wire=new(steam!,d.Peer);channel=new(wire,hello);observer=new(cfg,channel,descriptor.Digest,null,map,profiles,true);observer.Diagnostic+=events.Structured;reader=new(cfg.RosterContractConfirmed);worker=Task.Run(observer.Run);connection=id;events.Add("Connect",cfg.Role+" / "+cfg.Mode+"; Steam identity + ECDH/HMAC; no IP config");
 }
 void ResetWorld(){observer?.Dispose();try{if(worker!=null&&!worker.Wait(2000))events.MarkLoss();}catch(AggregateException){events.MarkLoss();}if(observer!=null)observer.Diagnostic-=events.Structured;worker=null;channel?.Dispose();wire?.Dispose();observer=null;channel=null;wire=null;reader=null;latest=null;connection="";}
 void ResetAll(){ResetWorld();discovery?.Dispose();discovery=null;scope="";fight=0;roster=false;samples=0;}
 void WriteBackup(string line){if(backup==null)return;if(backup.BaseStream.Length>=16*1024*1024){backup.Dispose();backup=new(Path.Combine(dir,"logs","DF-"+Guid.NewGuid().ToString("N")+".jsonl")){AutoFlush=true};}backup.WriteLine(line);}
 void ReportFault(string stage,Exception e,bool force=false){if(!force&&clock.Elapsed.TotalSeconds<nextFault)return;nextFault=clock.Elapsed.TotalSeconds+5;string detail=RuntimeStartup.Fault(stage,e);LoggerInstance.Warning(detail);events.Add(stage.StartsWith("Hud",StringComparison.Ordinal)?"HudUnavailable":"Stopped",detail);}
 void FlushLogs(){
  if(status!=lastStatus){lastStatus=status;events.Add("Status",status);}
  // Persist pending events before attempting any native Unity/FileLog binding.
  try{events.Flush(clock.Elapsed.TotalSeconds,gameLog==null?null:gameLog.Write,WriteBackup);}catch(Exception e){LoggerInstance.Warning(RuntimeStartup.Fault("Backup",e));enabled=false;}
  if(!interopReady)return;
  try{gameLog??=new UnityGamelog();gameLog.Tick(clock.Elapsed.TotalSeconds,code=>events.Add("GamelogReceipt",code));}catch(Exception e){ReportFault("GamelogBinding",e);}
 }
 public override void OnGUI()
 {
  hudCallback=true;if(!interopReady)return;
  try{
   hud??=new();using var frame=hud.BeginFrame();
   hud.FullDebug=discovery?.FullDebug==true;
   var view=observer?.View;var own=latest?.Owned;var unit=own?.Length>0?own[hud.Selected%own.Length]:null;var prediction=unit==null?null:view?.Projection?.Own.FirstOrDefault(u=>u.Key.Uid==unit.NetworkId);var delta=unit==null?null:view?.Display?.Forecast.FirstOrDefault(u=>u.Unit==unit.NetworkId);
   hud.Box(16,16,820,176,$"Dynamic Frontline full-debug6 | {(hud.FullDebug?"FULL DEBUG — HIDDEN DATA SHARED":"TEAM SAFE")} | {status}\nPeer: {discovery?.PeerLabel??"None"} / roster {discovery?.PeerRosterConfirmed} | Sync: {view?.Status??"Waiting"} | Mode: {discovery?.Mode??"LoadReadProbe"} | native effects OFF | Relay-only (ICE disabled)\n"+
    (unit==null?"Own roster not yet available":$"Own count {own!.Length}; HUD selection #{unit.NetworkId} ({unit.Definition}): ACTUAL stress {unit.Stress:F1}/{unit.MaxStress:F1}; HP {unit.Hp:F1}/{unit.MaxHp:F1}")+"\n"+
    (prediction==null?"Encirclement forecast unavailable":$"FORECAST: {prediction.State}; tau {prediction.Tau:F1}s; penalty delta {(delta?.Delta is double value?value.ToString("F2"):"UNKNOWN")}")+"\n"+
    $"Notices: {string.Join(", ",view?.Projection?.Notices.Select(n=>n.Kind)??[])} | {string.Join("; ",view?.Blockers??[])}\nGameLog: {gameLog?.State??"Unity binding unavailable"} / {events.SinkState}; backup ON. Private Scenario 5454584 only.");
   if(!hudDrawn){hudDrawn=true;LoggerInstance.Msg("DF HUD first status panel draw completed");events.Add("Status","HudDrawSucceeded");}
   if(!enabled){if(hud.Button(16,198,820,32,"Retry in private Scenario (Anti-Cheat Disabled)")){enabled=true;nextRetry=0;events.Add("Consent","Operator retried read-only private Scenario diagnostics");}return;}
   if(hud.Button(16,198,150,28,"Map on/off"))hud.MapVisible=!hud.MapVisible;
   if(hud.Button(172,198,150,28,"Own ZOC toggle"))hud.AllOwnZoc=!hud.AllOwnZoc;
   if(hud.Button(328,198,150,28,"Next own unit"))hud.Selected++;
   if(hud.Button(484,198,150,28,"Stop / disconnect")){enabled=false;ResetAll();status="Stopped by tester";}
   if(!roster&&discovery!=null&&samples>=20&&latest?.Owned.Length>0){if(hud.Button(16,234,820,42,"Confirm MY roster checked: count, identities, transport, death. Both confirmations enable calculation.")){roster=true;discovery.ConfirmRoster();events.Add("RosterAttestation","User confirms own roster; not an automated proof. Await peer confirmation.");}}
   else hud.Box(16,234,820,42,roster?"Own roster confirmed; waiting for peer / ObserveTwoClient":"First run: compare own units against HUD before confirming the roster.");
   if(discovery!=null&&hud.Button(850,16,310,52,discovery.LocalDebugConsent?"Revoke FullDebug consent / TeamSafe":"I CONSENT to sharing HIDDEN DATA\nFullDebug: both testers must consent")){discovery.SetDebugConsent(!discovery.LocalDebugConsent);ResetWorld();events.Add("Consent","FullDebug consent changed; clear old session");}
   hud.Render(view,latest);
  }catch(Exception e){ReportFault("Hud.OnGUI",e);}
 }
 public override void OnDeinitializeMelon(){enabled=false;ResetAll();context?.Dispose();events.Add("Shutdown","Read-only session ended");do{FlushLogs();}while(events.PendingResults>0);backup?.Dispose();}
}
