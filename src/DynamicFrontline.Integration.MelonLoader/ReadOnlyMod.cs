using System.Diagnostics;
using DynamicFrontline.Frontline;
using DynamicFrontline.GameAdapter;
using DynamicFrontline.Integration;
using MelonLoader;

[assembly: MelonInfo(typeof(DynamicFrontline.IntegrationLoader.ReadOnlyMod),"Dynamic Frontline Read-only PoC","0.2.0","Dynamic Frontline")]
[assembly: MelonGame("SteelBalalaikaStudio","BrokenArrow")]
[assembly: HarmonyDontPatchAll]
namespace DynamicFrontline.IntegrationLoader;

// Separate component: no DesertionController lifecycle, no Harmony hooks, no game writes.
public sealed class ReadOnlyMod:MelonMod
{
    private readonly Stopwatch clock=Stopwatch.StartNew();private bool compatible;
    private MelonPreferences_Entry<bool>? collect,simulate,export,modify;
    private FrontlineSimulationBridge? bridge;private IntegrationSettings? attachedSettings;
    private IntegrationState? reported;
    private IntegrationSettings Settings()=>new(collect?.Value==true,simulate?.Value==true,export?.Value==true,modify?.Value==true);
    public override void OnInitializeMelon()
    {
        try
        {
            var category=MelonPreferences.CreateCategory("DynamicFrontline.ReadOnly.Poc02");
            collect=category.CreateEntry("EnableGameDataCollection",false);simulate=category.CreateEntry("EnableFrontlineSimulation",false);
            export=category.CreateEntry("EnableDebugExport",false);modify=category.CreateEntry("EnableGameStateModification",false);
            Settings().Validate();
            var app=Resolve("UnityEngine.Application");string? version=app?.GetProperty("unityVersion")?.GetValue(null) as string;
            string? root=Path.GetDirectoryName(Environment.ProcessPath);
            using var manifest=typeof(ReadOnlyMod).Assembly.GetManifestResourceStream("DynamicFrontline.Poc02.compatibility.json")!;
            compatible=root!=null&&version!=null&&Compatibility.Verify(root,version,Compatibility.Load(manifest),out _);
            if(ReadOnlyComponentInspector.RequiredTypes.Any(name=>Resolve(name)==null))compatible=false;
            LoggerInstance.Msg(compatible?"PoC02 read-only types available; no provider attached":"PoC02 unavailable: runtime compatibility/types not confirmed");
        }
        catch{compatible=false;LoggerInstance.Warning("PoC02 initialization unavailable");}
    }
    private static Type? Resolve(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).FirstOrDefault(t=>t!=null);
    public override void OnSceneWasInitialized(int buildIndex,string sceneName)=>Detach();
    public override void OnDeinitializeMelon()=>Detach();
    public override void OnUpdate()
    {
        if(bridge==null)return;
        try
        {
            var current=Settings();current.Validate();if(current!=attachedSettings){Detach();LoggerInstance.Msg("PoC02 options changed; explicit reattach required");return;}
            bridge.Update(clock.ElapsedMilliseconds);
            if(reported!=bridge.Health.State){reported=bridge.Health.State;LoggerInstance.Msg("PoC02: "+reported);}
        }
        catch{Detach();LoggerInstance.Warning("PoC02 read-only session stopped");}
    }
    // Approved bootstrap must provide the actual scoped services. This PoC never guesses a global game singleton.
    public bool AttachReadOnlySession(IGameMatchContextProvider context,IReadOnlyGameDataAccess access,IGameTerritoryMapProvider map,
        CoordinateTransform coordinates,Func<DatabaseIdentity,GameUnitProfileResolver?> profiles,IGameRouteBackend routes,
        Rules rules,NavigationSettings navigationSettings,bool separatelyAuthorizedRuntimeRead=false)
    {
        if(!compatible||!Settings().EnableGameDataCollection)return false;
        try
        {
            Detach();attachedSettings=Settings();attachedSettings.Validate();
            bridge=new(attachedSettings,rules,context,access,map,coordinates,profiles,new GameNavigationAdapter(routes,navigationSettings),navigationSettings,separatelyAuthorizedRuntimeRead);
            return true;
        }
        catch{Detach();return false;}
    }
    private void Detach(){bridge?.Dispose();bridge=null;attachedSettings=null;reported=null;}
}
