using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline.Rules023;
using MelonLoader;
[assembly:MelonInfo(typeof(DynamicFrontline.Steam.MelonLoader.SteamMod),"DynamicFrontline T18 Steam Preview","0.4.0","DynamicFrontline")]
[assembly:MelonGame("Steel Balalaika Studio","Broken Arrow")]
[assembly:MelonOptionalDependencies("UnityEngine.CoreModule","UnityEngine.IMGUIModule","UnityEngine.InputLegacyModule")]
namespace DynamicFrontline.Steam.MelonLoader;
public sealed class SteamMod : MelonMod
{
 readonly Stopwatch clock=Stopwatch.StartNew();readonly DfEvents events=new();string dir="",status="Preparing bindings",lastStatus="",compatibility="",scope="",connection="";bool enabled=true,initialized,failed,roster;double nextRead,nextContext,nextRetry;int samples;
 GameCompatibilityDescriptor? descriptor;MapDescriptor? map;ProfileSet? profiles;NativeContext? context;NativeSteam? steam;SteamDiscovery? discovery;NativeSteam.Wire? wire;SteamWorldChannel? channel;RuntimeObserver? observer;LiveReader? reader;Observation? latest;SteamHud? hud;Action<string>? gameLog;StreamWriter? backup;Task? worker;long fight;
 T Read<T>(string name)=>JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(dir,name)))??throw new InvalidDataException(name);
 public override void OnInitializeMelon()
 {
  try{var root=Path.GetDirectoryName(Environment.ProcessPath)!;dir=Path.Combine(root,"UserData","DynamicFrontlineSteam");descriptor=Read<GameCompatibilityDescriptor>("compatibility.json");
   if(descriptor.BuildId!="26273833"||descriptor.Files.Count!=4||descriptor.Files.GetValueOrDefault("GameAssembly.dll")!="12E7088369539B029A7BF0064062CE030A53B5189E7067420903F47B4281FDDF"||descriptor.Files.GetValueOrDefault("BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat")!="A4EDE1B29664378A2FB9F952F65330A73DBFA115B205065D1831F420540AFE01"||descriptor.Files.GetValueOrDefault("UnityPlayer.dll")!="79A54AF1C83B5B3BFA34936BE18899DE49FA88EEA6A33BA7351C2163F95F6613"||descriptor.Files.GetValueOrDefault("BrokenArrow.exe")!="A0A2D82854AF8FB1718D70EC0BE0F4028A1332B13BAE89051EC8B368187F34B4")throw new InvalidDataException("UnsupportedGameBuild");
   descriptor.Verify(root);map=Read<MapDescriptor>("map.json");profiles=Read<ProfileSet>("profiles.json");map.VerifyLocalSources(root);
   string assembly=Path.GetDirectoryName(typeof(SteamMod).Assembly.Location)!;
   var hashes=new[]{"Steam.MelonLoader","Steam","Poc030","Core","Frontline","GameAdapter","Integration"}.Select(n=>new{Name=n,Hash=DevInstall.Hash(n=="Steam.MelonLoader"?typeof(SteamMod).Assembly.Location:Path.Combine(Path.GetDirectoryName(typeof(LiveReader).Assembly.Location)!,"DynamicFrontline."+n+".dll"))}).ToArray();
   compatibility=CanonicalCodec.Digest(new{Protocol="df18-world-1",Payload=hashes,Game=descriptor.Digest,Map=map.Hash,Profiles=profiles.Hash,Rules=new Rules023Config().Hash,Mode="ObserveOnly_TeamSafe"});
   Directory.CreateDirectory(Path.Combine(dir,"logs"));backup=new(Path.Combine(dir,"logs","DF-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".jsonl")){AutoFlush=true};events.Add("Build","T18 candidate; runtime validation pending; native writes OFF");
  }catch(Exception e){failed=true;status="Disabled: "+e.GetType().Name+" / "+e.Message;LoggerInstance.Error(status);}
 }
 void InitializeBindings()
 {
  InteropGate.VerifyLoaded(Path.Combine(dir,"interop-provenance.json"),descriptor!,global::MelonLoader.Utils.MelonEnvironment.Il2CppAssembliesDirectory);
  hud=new();context=new();context.Install();initialized=true;status="Waiting for private Scenario 5454584";
 }
 public override void OnUpdate()
 {
  if(failed)return;
  try{
   if(!initialized){if(clock.Elapsed.TotalSeconds<nextRetry)return;nextRetry=clock.Elapsed.TotalSeconds+1;try{InitializeBindings();}catch(Exception e){status="Bindings waiting: "+e.GetType().Name;if(clock.Elapsed.TotalSeconds>60)throw;}return;}
   if(!enabled){FlushLogs();return;}
   if(clock.Elapsed.TotalSeconds>=nextContext){nextContext=clock.Elapsed.TotalSeconds+.5;var native=context!.Read();
    if(native==null){ResetAll();status="Waiting for private Scenario 5454584 lobby";FlushLogs();return;}
    if(scope!=native.Key){ResetAll();scope=native.Key;steam??=new();discovery=new(steam,scope,native.Host,compatibility);events.Add("Lobby","Native private Scenario observed; discovering Steam companion");}
    if(fight!=native.FightId){ResetWorld();fight=native.FightId;events.Add("Match",native.InMatch?"Native match entered":"Returned to lobby");}
    if(!native.InMatch){latest=null;observer?.Fail(SourceState.NoSession,"InLobby");}
   }
   if(discovery==null)return;discovery.Pump();status=discovery.State;
   if(!discovery.PairReady){latest=null;observer?.Fail(SourceState.NoSession,"SteamPeerNotReady");FlushLogs();return;}
   string expected=discovery.Session+"/"+discovery.Peer+"/"+discovery.Mode+"/"+fight;
   // Native FightId is checked by the existing session binding; lobby peer readiness is independent of game reads.
   if(connection!=expected){if(clock.Elapsed.TotalSeconds<nextRetry)return;nextRetry=clock.Elapsed.TotalSeconds+2;ResetWorld();StartWorld(expected);}
   if(channel!.State.StartsWith("SteamDisconnected:")&&clock.Elapsed.TotalSeconds>=nextRetry){nextRetry=clock.Elapsed.TotalSeconds+3;channel.Restart();events.Add("Reconnect","Preserving observer/core; requesting checkpoint and full snapshots");}
   channel.Pump();status=discovery.State+" / "+channel.State;
   if(fight>0&&channel.Authenticated&&clock.Elapsed.TotalSeconds>=nextRead){nextRead=clock.Elapsed.TotalSeconds+.25;
    try{var guard=context!.Read();if(guard==null||!guard.InMatch||guard.FightId!=fight)throw new InvalidDataException("MatchChanged");latest=reader!.Read();samples++;observer!.Submit(latest);events.Add("Roster",$"own={latest.Owned.Length}; complete={latest.Complete}; quality={latest.Quality}; fight={latest.FightId}");}
    catch(Exception e){latest=null;observer!.Fail(SourceState.ReadUnavailable,e.GetType().Name);events.Add("ReadUnavailable",e.GetType().Name+":"+e.Message);}
   }
   FlushLogs();
  }catch(Exception e){ResetAll();enabled=false;status="Stopped: "+e.GetType().Name+" / "+e.Message;events.Add("Stopped",status);LoggerInstance.Warning(status);FlushLogs();}
 }
 void StartWorld(string id)
 {
  var d=discovery!;var session=Guid.Parse(d.Session);
  // New fight/mode => separate memory/nonce scope. Identical on both clients without manual config.
  byte[] bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(session+"/"+fight+"/"+d.Mode));var matchSession=new Guid(bytes.Take(16).ToArray()).ToString();
  var cfg=new ProbeConfig(true,true,d.Mode,matchSession,d.Role,true,"",0,"",Replication:"TeamSafe",RosterContractConfirmed:roster&&d.Mode=="ObserveTwoClient");
  var hello=new Hello("df18-world-1",cfg.SessionId,cfg.Role,descriptor!.Digest,compatibility,new Rules023Config().Hash,profiles!.Hash,map!.Hash,"ua01-rc2-read-contract","SteamReadOnly/"+cfg.Mode);
  wire=new(steam!,d.Peer);channel=new(wire,hello);observer=new(cfg,channel,descriptor.Digest,null,map,profiles,true);observer.Diagnostic+=item=>{if(item.GetType().GetProperty("Kind")?.GetValue(item)?.ToString() is string kind)events.Add(kind,JsonSerializer.Serialize(item));};reader=new(cfg.RosterContractConfirmed);worker=observer.Run();connection=id;events.Add("Connect",cfg.Role+" / "+cfg.Mode+"; Steam identity + ECDH/HMAC; no IP config");
 }
 void ResetWorld(){observer?.Dispose();channel?.Dispose();wire?.Dispose();observer=null;channel=null;wire=null;reader=null;latest=null;connection="";}
 void ResetAll(){ResetWorld();discovery?.Dispose();discovery=null;scope="";fight=0;roster=false;samples=0;}
 void FlushLogs(){if(status!=lastStatus){lastStatus=status;events.Add("Status",status);}if(gameLog==null)try{gameLog=NativeContext.Logger();}catch{}events.Flush(clock.Elapsed.TotalSeconds,gameLog,line=>{if(backup?.BaseStream.Length<16*1024*1024)backup.WriteLine(line);});}
 public override void OnGUI()
 {
  if(hud==null)return;
  try{
   var view=observer?.View;var own=latest?.Owned;var unit=own?.Length>0?own[hud.Selected%own.Length]:null;var prediction=unit==null?null:view?.Projection?.Own.FirstOrDefault(u=>u.Key.Uid==unit.NetworkId);var delta=unit==null?null:view?.Display?.Forecast.FirstOrDefault(u=>u.Unit==unit.NetworkId);
   hud.Box(16,16,820,176,$"Dynamic Frontline T18 | {status}\nPeer: {discovery?.PeerLabel??"None"} / roster {discovery?.PeerRosterConfirmed} | Sync: {view?.Status??"Waiting"} | Mode: {discovery?.Mode??"LoadReadProbe"} | native effects OFF\n"+
    (unit==null?"Own roster not yet available":$"Own count {own!.Length}; HUD selection #{unit.NetworkId} ({unit.Definition}): ACTUAL stress {unit.Stress:F1}/{unit.MaxStress:F1}; HP {unit.Hp:F1}/{unit.MaxHp:F1}")+"\n"+
    (prediction==null?"Encirclement forecast unavailable":$"FORECAST: {prediction.State}; tau {prediction.Tau:F1}s; penalty delta {(delta?.Delta is double value?value.ToString("F2"):"UNKNOWN")}")+"\n"+
    $"Notices: {string.Join(", ",view?.Projection?.Notices.Select(n=>n.Kind)??[])} | {string.Join("; ",view?.Blockers??[])}\nGameLog: {events.SinkState}; backup ON. Private Scenario 5454584 only.");
   if(!enabled){if(hud.Button(16,198,820,32,"Retry in private Scenario (Anti-Cheat Disabled)")){enabled=true;nextRetry=0;events.Add("Consent","Operator retried read-only private Scenario diagnostics");}return;}
   if(hud.Button(16,198,150,28,"Map on/off"))hud.MapVisible=!hud.MapVisible;
   if(hud.Button(172,198,150,28,"Own ZOC toggle"))hud.AllOwnZoc=!hud.AllOwnZoc;
   if(hud.Button(328,198,150,28,"Next own unit"))hud.Selected++;
   if(hud.Button(484,198,150,28,"Stop / disconnect")){enabled=false;ResetAll();status="Stopped by tester";}
   if(!roster&&discovery!=null&&samples>=20&&latest?.Owned.Length>0){if(hud.Button(16,234,820,42,"Confirm MY roster checked: count, identities, transport, death. Both confirmations enable calculation.")){roster=true;discovery.ConfirmRoster();events.Add("RosterAttestation","User confirms own roster; not an automated proof. Await peer confirmation.");}}
   else hud.Box(16,234,820,42,roster?"Own roster confirmed; waiting for peer / ObserveTwoClient":"First run: compare own units against HUD before confirming the roster.");
   hud.Render(view,latest);
  }catch(Exception e){events.Add("HudUnavailable",e.GetType().Name);}
 }
 public override void OnDeinitializeMelon(){enabled=false;ResetAll();context?.Dispose();events.Add("Shutdown","Read-only session ended");FlushLogs();backup?.Dispose();}
}
