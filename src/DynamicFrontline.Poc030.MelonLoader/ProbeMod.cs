using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using DynamicFrontline.Poc030;
using MelonLoader;
[assembly:MelonInfo(typeof(DynamicFrontline.Poc030.MelonLoader.ProbeMod),"DynamicFrontline PoC030 RC2 Diagnostics","0.3.2","DynamicFrontline")]
[assembly:MelonGame("Steel Balalaika Studio","Broken Arrow")]
[assembly:MelonOptionalDependencies("UnityEngine.CoreModule","UnityEngine.IMGUIModule","UnityEngine.InputLegacyModule")]
namespace DynamicFrontline.Poc030.MelonLoader;
public sealed class ProbeMod : MelonMod
{
 readonly Stopwatch timer=Stopwatch.StartNew();readonly Channel<string> records=Channel.CreateBounded<string>(256);readonly CancellationTokenSource stopping=new();
 LiveReader? reader;DevChannel? channel;RuntimeObserver? observer;ProbeConfig? config;GameCompatibilityDescriptor? descriptor;string dir="",status="Disabled: separate authorization required",lastLogged="";double next;bool authorized,ready,circles;int selected;Observation? latest;
 Type? rectType,vectorType;MethodInfo? boxMethod,cameraProject,keyMethod;object? camera;long samples;
 public override void OnInitializeMelon()
 {
  try
  {
   var root=Path.GetDirectoryName(Environment.ProcessPath)??throw new InvalidDataException("NoGameRoot");dir=Path.Combine(root,"UserData","DynamicFrontline030");
   config=JsonSerializer.Deserialize<ProbeConfig>(File.ReadAllText(Path.Combine(dir,"probe.json")))??throw new InvalidDataException("Config");
   if(!config.RuntimeReadAuthorized||!config.PrivateSessionConfirmed||config.Mode is not ("LoadReadProbe" or "ObserveTwoClient")||config.Role is not ("A" or "B")||!Guid.TryParse(config.SessionId,out _)||!config.NetworkEnabled)return;
   descriptor=JsonSerializer.Deserialize<GameCompatibilityDescriptor>(File.ReadAllText(Path.Combine(dir,"compatibility.json")))??throw new InvalidDataException("Descriptor");descriptor.Verify(root);
        // A copied descriptor is not sufficient: pin the audited pair in compiled code as well.
        if(descriptor.Files.GetValueOrDefault("GameAssembly.dll")!="12E7088369539B029A7BF0064062CE030A53B5189E7067420903F47B4281FDDF" || descriptor.Files.GetValueOrDefault("BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat")!="A4EDE1B29664378A2FB9F952F65330A73DBFA115B205065D1831F420540AFE01" || descriptor.Files.GetValueOrDefault("UnityPlayer.dll")!="79A54AF1C83B5B3BFA34936BE18899DE49FA88EEA6A33BA7351C2163F95F6613" || descriptor.Files.GetValueOrDefault("BrokenArrow.exe")!="A0A2D82854AF8FB1718D70EC0BE0F4028A1332B13BAE89051EC8B368187F34B4" || descriptor.UnityVersion!="6000.3.24f1" || descriptor.MetadataVersion!=39 || descriptor.BindingSchema!="ua01-member-v1" || descriptor.Files.Count!=4 || descriptor.BuildId!="26273833")throw new InvalidDataException("UnauditedDescriptor");

   Directory.CreateDirectory(Path.Combine(dir,"logs"));_=WriteLogs(Path.Combine(dir,"logs",$"RC2-{config.Role}-{DateTime.UtcNow:yyyyMMddHHmmss}.jsonl"));authorized=true;status="Waiting for loaded interop (bounded 45 seconds); writes OFF";
  }
  catch(Exception ex){status="Disabled:"+ex.GetType().Name+":"+ex.Message;LoggerInstance.Warning(status);}
 }
 void StartReader()
 {
  InteropGate.VerifyLoaded(Path.Combine(dir,"interop-provenance.json"),descriptor!);
  reader=new(config!.RosterContractConfirmed);
  T? ReadOptional<T>(string path)=>string.IsNullOrWhiteSpace(path)?default:JsonSerializer.Deserialize<T>(File.ReadAllText(path));
  var binding=ReadOptional<MatchBinding>(config.BindingFile);var map=ReadOptional<MapDescriptor>(config.MapFile);var profiles=ReadOptional<ProfileSet>(config.ProfilesFile);
  var assemblyPaths=new[]{typeof(ProbeMod).Assembly.Location,typeof(LiveReader).Assembly.Location,typeof(DynamicFrontline.Frontline.Rules023.Simulation023).Assembly.Location,typeof(DynamicFrontline.Core.CombatFormulas).Assembly.Location};
  var payload=CanonicalCodec.Digest(assemblyPaths.Select(p=>new{Name=Path.GetFileName(p),Hash=DevInstall.Hash(p)}).OrderBy(p=>p.Name,StringComparer.Ordinal));
  channel=new(config,new("df030-world-2",config.SessionId,config.Role,descriptor!.Digest,payload,new DynamicFrontline.Frontline.Rules023.Rules023Config().Hash,profiles?.Hash??"Unknown",map?.Hash??"Unknown","ua01-rc2-read-contract","ReadOnlyRC2"),File.ReadAllBytes(config.SecretFile));
  observer=new(config,channel,descriptor.Digest,binding,map,profiles);_=channel.RunWorld();_=observer.Run();ready=true;
  records.Writer.TryWrite(JsonSerializer.Serialize(new{Kind="Start",Mode=config.Mode,Role=config.Role,Payload=payload,Compatibility=descriptor.Digest,RuntimeValidated=false}));
 }
 public override void OnUpdate()
 {
  if(!authorized)return;
  if(!ready){if(timer.Elapsed.TotalSeconds>45){authorized=false;status="Interop not ready within45s; readers never enabled";LoggerInstance.Warning(status);return;}if(timer.Elapsed.TotalSeconds<next)return;next=timer.Elapsed.TotalSeconds+.5;try{StartReader();}catch(Exception e){status="InteropWaiting:"+e.GetType().Name+":"+e.Message;}return;}
  try
  {
   var input=LiveReader.Resolve("UnityEngine.Input");var keyType=LiveReader.Resolve("UnityEngine.KeyCode");keyMethod??=input.GetMethod("GetKeyDown",[keyType]);
   if(Convert.ToBoolean(keyMethod?.Invoke(null,[Enum.Parse(keyType,config!.CircleKey)])))circles=!circles;
   if(Convert.ToBoolean(keyMethod?.Invoke(null,[Enum.Parse(keyType,config!.SelectOwnKey)])))selected++;
  }catch{ /* Missing optional key binding cannot enable gameplay effects or block reading. */ }
  if(timer.Elapsed.TotalSeconds<next)return;next=timer.Elapsed.TotalSeconds+.25;
  var watch=Stopwatch.StartNew();long bytes=GC.GetAllocatedBytesForCurrentThread();
  try{latest=reader!.Read();observer!.Submit(latest);status=config!.Mode+" / "+latest.Quality;}
  catch(Exception ex){var state=ex.Message=="NoSession"?SourceState.NoSession:SourceState.ReadUnavailable;observer!.Fail(state,ex.GetType().Name);latest=null;status=state+":"+ex.GetType().Name;}
  if(++samples%20==0||status!=lastLogged){lastLogged=status;records.Writer.TryWrite(JsonSerializer.Serialize(new{Kind="SourceMetric",State=status,Revision=latest?.Revision,GameTime=latest?.GameTime,ReadMilliseconds=watch.Elapsed.TotalMilliseconds,ReadBytes=GC.GetAllocatedBytesForCurrentThread()-bytes,Core=observer!.View.Status,InputHash=observer.View.InputHash,Blockers=observer.View.Blockers}));}
 }
 async Task WriteLogs(string path)
 {
  using var stream=new StreamWriter(path);try{await foreach(var line in records.Reader.ReadAllAsync(stopping.Token)){if(stream.BaseStream.Length>32*1024*1024)break;await stream.WriteLineAsync(line);if(samples%20==0)await stream.FlushAsync();}}catch(OperationCanceledException){}await stream.FlushAsync();
 }
 public override void OnGUI()
 {
  if(!authorized)return;
  try
  {
   rectType??=LiveReader.Resolve("UnityEngine.Rect");var gui=LiveReader.Resolve("UnityEngine.GUI");boxMethod??=gui.GetMethod("Box",[rectType,typeof(string)]);
   var view=observer?.View;var own=latest?.Owned;var chosen=own?.Length>0?own[selected%own.Length]:null;
   var predicted=chosen?.NetworkId is int id?view?.Projection?.Own.FirstOrDefault(u=>u.Key.Uid==id):null;
   string text=$"{config?.Mode} | {status}\n{view?.Status} | {channel?.State} | age {view?.Local?.AgeSeconds:F2}s / peer {view?.Peer?.AgeSeconds:F2}s\n"+
    (chosen==null?"Own diagnostic selection unavailable":$"Own #{chosen.Definition}/{chosen.NetworkId}: HP {chosen.Hp}/{chosen.MaxHp}; soldiers {chosen.Soldiers}; ACTUAL stress {chosen.Stress}/{chosen.MaxStress} level {chosen.ObservedLevel}")+"\n"+
    (predicted==null?"Prediction unavailable":$"FORECAST {predicted.State}; relief {predicted.Relief}; tau {predicted.Tau:F2}")+"\n"+
    $"{string.Join("; ",view?.Blockers??[])}\nForecast only — game effects OFF. {config?.CircleKey}: circles; {config?.SelectOwnKey}: own diagnostic selection";
   boxMethod?.Invoke(null,[Activator.CreateInstance(rectType,[16f,16f,780f,180f]),text]);
   if(circles&&chosen!=null&&view?.Projection!=null)DrawRadius(chosen,view.Projection.Circles.FirstOrDefault(c=>c.Key.Uid==chosen.NetworkId)?.Radius);
  }catch(Exception e){if(status!="HudUnavailable"){status="HudUnavailable";records.Writer.TryWrite(JsonSerializer.Serialize(new{Kind="HudUnavailable",Reason=e.GetType().Name}));}}
 }
 void DrawRadius(OwnedSample unit,double? radius)
 {
  if(radius==null)return;var cameraType=LiveReader.Resolve("UnityEngine.Camera");camera=cameraType.GetProperty("main")?.GetValue(null);if(camera==null)return;
  vectorType??=LiveReader.Resolve("UnityEngine.Vector3");cameraProject??=cameraType.GetMethod("WorldToScreenPoint",[vectorType]);var screen=LiveReader.Resolve("UnityEngine.Screen");float height=Convert.ToSingle(screen.GetProperty("height")!.GetValue(null));
  for(int i=0;i<48;i++){double angle=i*Math.PI/24;var point=Activator.CreateInstance(vectorType,[(float)(unit.X+radius*Math.Cos(angle)),unit.Y,(float)(unit.Z+radius*Math.Sin(angle))]);var pixel=cameraProject?.Invoke(camera,[point]);if(pixel==null||Convert.ToSingle(LiveReader.Get(pixel,"z"))<=0)continue;float x=Convert.ToSingle(LiveReader.Get(pixel,"x")),y=height-Convert.ToSingle(LiveReader.Get(pixel,"y"));boxMethod?.Invoke(null,[Activator.CreateInstance(rectType!,[x,y,3f,3f]),""]);}
 }
 public override void OnDeinitializeMelon(){authorized=false;observer?.Dispose();channel?.Dispose();records.Writer.TryComplete();stopping.Cancel();latest=null;camera=null;}
}
