using System.Diagnostics;
using DynamicFrontline.Core;
using DynamicFrontline.GameAdapter;
using MelonLoader;

[assembly: MelonInfo(typeof(DynamicFrontline.Loader.Mod), "Dynamic Frontline PoC", "0.1.0", "Dynamic Frontline")]
[assembly: MelonGame("SteelBalalaikaStudio", "BrokenArrow")]
[assembly: HarmonyDontPatchAll]

namespace DynamicFrontline.Loader;

// MelonMod (Mods), not early MelonPlugin (Plugins): scene lifecycle is required.
public sealed class Mod : MelonMod
{
    private readonly Stopwatch clock=Stopwatch.StartNew();
    private MelonLog? log;
    private bool compatible;
    private MelonPreferences_Entry<bool>? debug;
    private DesertionController? controller = null;
    private UnitInspector? inspector;
    private Func<UnitKey?>? selectedUnit;
    private double nextInspection;
    public override void OnInitializeMelon()
    {
        log=new MelonLog(LoggerInstance);
        try
        {
            debug=MelonPreferences.CreateCategory("DynamicFrontline").CreateEntry("EnableDebugDesertion",false);
            using Stream manifest=typeof(Mod).Assembly.GetManifestResourceStream("DynamicFrontline.compatibility.json")!;
            // Reflection avoids linking dummy Unity DLLs into a deployable mod.
            Type? app=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("UnityEngine.Application")).FirstOrDefault(t=>t!=null);
            string? version=app?.GetProperty("unityVersion")?.GetValue(null) as string;
            string? executable=Environment.ProcessPath;
            string? root=executable == null ? null : Path.GetDirectoryName(executable);
            if(root == null || version == null) { log.Error("Disabled: game identity unavailable"); return; }
            compatible=Compatibility.Verify(root,version,Compatibility.Load(manifest),out string reason);
            log.Info(reason);
            log.Info("PoC 0.1: gameplay adapter and Harmony hooks are unvalidated and disabled; debug="+debug.Value);
        }
        catch(Exception e) { Disable("initialization:"+e.GetType().Name); }
    }
    public override void OnSceneWasInitialized(int buildIndex,string sceneName)
    {
        // Never carry UID/controller state across scenes or matches.
        controller?.Disable(clock.Elapsed.TotalSeconds,"scene-changed"); controller=null;
        inspector=null; selectedUnit=null;
        log?.Info("Scene initialized; no game binding attached");
    }
    public override void OnUpdate()
    {
        try
        {
            double now=clock.Elapsed.TotalSeconds;
            controller?.Tick(now);
            if(debug?.Value == true && now >= nextInspection)
            {
                nextInspection=now+2;
                if(selectedUnit?.Invoke() is { } selected) inspector?.Inspect(selected);
            }
        }
        catch(Exception e) { Disable("update:"+e.GetType().Name); }
    }
    public override void OnDeinitializeMelon() => Disable("shutdown");
    // Prepared debugger/API trigger, no key binding and no automatic activation.
    public bool DebugActivate(UnitKey selected) => compatible && debug?.Value == true && controller?.Activate(selected,clock.Elapsed.TotalSeconds)==true;
    // Integration seam for a future verified scene resolver. This only enables read-only diagnostics.
    // No provider is automatically discovered or installed by PoC 0.1.
    public bool AttachReadOnlySession(Guid match,int localOwner,IUnitBindings bindings,Func<UnitKey?> selected)
    {
        if(!compatible || log == null || match == Guid.Empty) return false;
        controller?.Disable(clock.Elapsed.TotalSeconds,"session-replaced");
        var reader=new ReflectionUnitReader(name=>AppDomain.CurrentDomain.GetAssemblies()
            .Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null));
        var port=new ExperimentalGamePort(match,localOwner,bindings,reader,log);
        controller=new(port,new TeamSpawnPlanner(),new(debug?.Value==true),log);
        inspector=new(port,log); selectedUnit=selected;
        return true;
    }
    private void Disable(string reason)
    {
        compatible=false;
        controller?.Disable(clock.Elapsed.TotalSeconds,reason); controller=null;
        inspector=null; selectedUnit=null;
        log?.Error("Dynamic Frontline disabled: "+reason);
    }
}
internal sealed class MelonLog(MelonLogger.Instance logger) : ILog
{
    public void Info(string message) => logger.Msg("[DF] "+message);
    public void Error(string message) => logger.Error("[DF] "+message);
}
