using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Poc030;
using MelonLoader;
[assembly:MelonInfo(typeof(DynamicFrontline.Poc030.MelonLoader.ProbeMod),"DynamicFrontline PoC030 Diagnostics","0.3.0","DynamicFrontline")]
[assembly:MelonGame("Steel Balalaika Studio","Broken Arrow")]
[assembly:MelonOptionalDependencies("UnityEngine.CoreModule","UnityEngine.IMGUIModule")]
namespace DynamicFrontline.Poc030.MelonLoader;
public sealed class ProbeMod : MelonMod
{
    readonly Stopwatch timer=Stopwatch.StartNew(); readonly LiveReader reader=new();
    double next; long rows; StreamWriter? log; DevChannel? channel; string status="Disabled: authorization required"; bool enabled; bool hudFailure;
    public override void OnInitializeMelon()
    {
        try
        {
            var root=Path.GetDirectoryName(Environment.ProcessPath)??throw new InvalidDataException("NoGameRoot");
            var dir=Path.Combine(root,"UserData","DynamicFrontline030");
            var config=JsonSerializer.Deserialize<ProbeConfig>(File.ReadAllText(Path.Combine(dir,"probe.json")))??throw new InvalidDataException("MissingConfig");
            if(!config.RuntimeReadAuthorized||!config.PrivateSessionConfirmed||config.Mode!="ObserveTwoClient"||config.Role is not ("A" or "B")||!Guid.TryParse(config.SessionId,out _))return;
            var descriptor=JsonSerializer.Deserialize<GameCompatibilityDescriptor>(File.ReadAllText(Path.Combine(dir,"compatibility.json")))??throw new InvalidDataException("MissingDescriptor");
            descriptor.Verify(root);
            // A copied descriptor is not sufficient: pin the audited pair in compiled code as well.
            if(descriptor.Files.GetValueOrDefault("GameAssembly.dll")!="12E7088369539B029A7BF0064062CE030A53B5189E7067420903F47B4281FDDF" || descriptor.Files.GetValueOrDefault("BrokenArrow_Data/il2cpp_data/Metadata/global-metadata.dat")!="A4EDE1B29664378A2FB9F952F65330A73DBFA115B205065D1831F420540AFE01" || descriptor.Files.GetValueOrDefault("UnityPlayer.dll")!="79A54AF1C83B5B3BFA34936BE18899DE49FA88EEA6A33BA7351C2163F95F6613" || descriptor.Files.GetValueOrDefault("BrokenArrow.exe")!="A0A2D82854AF8FB1718D70EC0BE0F4028A1332B13BAE89051EC8B368187F34B4" || descriptor.UnityVersion!="6000.3.24f1" || descriptor.MetadataVersion!=39 || descriptor.BindingSchema!="ua01-member-v1" || descriptor.Files.Count!=4 || descriptor.BuildId!="26273833")throw new InvalidDataException("UnauditedDescriptor");
            InteropGate.VerifyLoaded(Path.Combine(dir,"interop-provenance.json"),descriptor);
            Directory.CreateDirectory(Path.Combine(dir,"logs"));
            log=new StreamWriter(Path.Combine(dir,"logs",$"{config.SessionId}-{config.Role}-{DateTime.UtcNow:yyyyMMddHHmmss}.jsonl")){AutoFlush=true};
            string payload=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ProbeMod).Assembly.Location).Concat(File.ReadAllBytes(typeof(LiveReader).Assembly.Location)).ToArray()));
            string abi=typeof(MelonMod).Assembly.GetName().Version+"/"+(AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name=="Il2CppInterop.Runtime")?.GetName().Version?.ToString()??"MissingInterop");
            log.WriteLine(JsonSerializer.Serialize(new {Kind="Start",config.SessionId,config.Role,Compatibility=descriptor.Digest,Payload=payload,Abi=abi,Mode=config.Mode,RuntimeValidated=false}));
            if(config.NetworkEnabled)
            {
                channel=new(config,new("df030-status-1",config.SessionId,config.Role,descriptor.Digest,payload,"Legacy022+023-unmodified","Unknown","Unknown",abi,"Clock+OwnedReadCandidate;NoWrites"),File.ReadAllBytes(config.SecretFile));
                _=channel.Run();
            }
            enabled=true;status="Waiting for real session; writes disabled";
        }
        catch(Exception ex){status="Disabled:"+ex.GetType().Name+(ex is InvalidDataException?":"+ex.Message:"");LoggerInstance.Warning(status);}
    }
    public override void OnUpdate()
    {
        if(!enabled||timer.Elapsed.TotalSeconds<next)return;next=timer.Elapsed.TotalSeconds+.25;
        if(rows>=43200 || log?.BaseStream.Length>128*1024*1024){enabled=false;status="Stopped: 3-hour sample cap";return;}
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        try
        {
            var sample=reader.Read();channel?.Publish(sample);
            // Only local owner's observations; no remote world trace and no hidden enemy DTO.
            log?.WriteLine(JsonSerializer.Serialize(new {Kind="Observation",Sample=sample,Link=channel?.State??"Disabled",Peer=channel?.Peer}));
            status=$"ObserveTwoClient | owner {sample.Owner} | own {sample.Owned.Length}\n{sample.Quality} | {channel?.State??"Link disabled"}\nPrediction blocked: map/roster/identity not validated | native writes OFF"+"\n"+string.Join("\n",sample.Owned.Take(5).Select(u=>$"Own {u.Definition}/{u.Local.EntityId}: HP {u.Hp}/{u.MaxHp}; actual stress {u.Stress}/{u.MaxStress}; prediction unavailable"));
        }
        catch(Exception ex){status="ReadUnavailable:"+ex.GetType().Name;log?.WriteLine(JsonSerializer.Serialize(new {Kind="ReadUnavailable",Reason=ex.GetType().Name}));}
        finally{rows++;log?.WriteLine(JsonSerializer.Serialize(new {Kind="SamplerMetric",Milliseconds=watch.Elapsed.TotalMilliseconds,AllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocated}));}
    }
    public override void OnGUI()
    {
        if(!enabled)return;
        try
        {
            var rect=LiveReader.Resolve("UnityEngine.Rect");var gui=LiveReader.Resolve("UnityEngine.GUI");
            var box=Activator.CreateInstance(rect,new object[]{16f,16f,900f,220f});
            (gui.GetMethod("Box",new[]{rect,typeof(string)}) ?? throw new MissingMethodException("GUI.Box")).Invoke(null,new[]{box,status});
        }
        catch(Exception ex){if(!hudFailure){hudFailure=true;log?.WriteLine(JsonSerializer.Serialize(new {Kind="HudUnavailable",Reason=ex.GetType().Name}));}}
    }
    public override void OnDeinitializeMelon(){channel?.Dispose();log?.Dispose();enabled=false;}
}
