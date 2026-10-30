using System.Reflection;
using DynamicFrontline.Poc030;
using HarmonyLib;
namespace DynamicFrontline.Steam.MelonLoader;
public sealed record NativeScenario(string Key,bool Host,bool InMatch,long FightId);
// Read-only postfixes on lobby state/lifecycle, never patch anti-cheat or native simulation.
public sealed class NativeContext : IDisposable
{
 const string LobbyType="BrokenArrow.Client.Ecs.GNetwork.Services.Lobby.NetworkLobbyService";
 static object? lobbyService;readonly HarmonyLib.Harmony harmony=new("DynamicFrontline.T18.LobbyObserver");NativeScenario? retained;
 public void Install(){var type=LiveReader.Resolve(LobbyType);harmony.Patch(type.GetMethod("get_State"),postfix:new HarmonyMethod(typeof(NativeContext).GetMethod(nameof(Capture),BindingFlags.Static|BindingFlags.NonPublic)));harmony.Patch(type.GetMethod("Dispose"),postfix:new HarmonyMethod(typeof(NativeContext).GetMethod(nameof(Closed),BindingFlags.Static|BindingFlags.NonPublic)));}
 static void Capture(object __instance)=>Interlocked.Exchange(ref lobbyService,__instance);
 static void Closed(object __instance){Interlocked.Exchange(ref lobbyService,null);}
 public NativeScenario? Read()
 {
  var gc=LiveReader.Resolve("BrokenArrow.Client.Ecs.Controllers.GameController");
  bool active=Convert.ToBoolean(gc.GetProperty("IsInstanceAlive")!.GetValue(null));
  if(active){var game=gc.GetProperty("Instance")!.GetValue(null)!;var room=LiveReader.Get(game,"_networkRoomService")!;var state=LiveReader.Get(room,"State")!;
   if(Convert.ToString(LiveReader.Get(state,"LobbyType"))!="Scenario"||Convert.ToString(LiveReader.Get(LiveReader.Get(state,"ScenarioMeta")!,"Name"))!="5454584")throw new InvalidDataException("OnlyScenario5454584");
   // Query existing service only. No BeginSession/EndSession/patch/config changes.
   if(Convert.ToBoolean(LiveReader.Get(LiveReader.Get(room,"_eosAntiCheatService")!,"IsActive")))throw new InvalidDataException("ProtectedSession_ModDisabled");
   if(retained==null)throw new InvalidDataException("PrivateLobbyNotWitnessed");
   return retained with{InMatch=true,FightId=Convert.ToInt64(LiveReader.Get(state,"FightId"))};
  }
  var lobby=Volatile.Read(ref lobbyService);if(lobby==null){retained=null;return null;}
  var ls=LiveReader.Get(lobby,"State");if(ls==null){retained=null;return null;}var opts=LiveReader.Get(ls,"Options")!;
  if(Convert.ToString(LiveReader.Get(opts,"LobbyType"))!="Scenario"||!Convert.ToBoolean(LiveReader.Get(opts,"IsPrivate"))||Convert.ToString(LiveReader.Get(LiveReader.Get(ls,"ScenarioMeta")!,"Name"))!="5454584"){retained=null;return null;}
  var id=Convert.ToInt32(LiveReader.Get(lobby,"LobbyId"));if(id<=0)return null;
  return retained=new(CanonicalCodec.Digest(new{Lobby=id,Host=Convert.ToInt32(LiveReader.Get(ls,"HostId")),Scenario="5454584"}),Convert.ToBoolean(LiveReader.Get(lobby,"IsHost")),false,0);
 }
 public static Action<string> Logger()
 {
  var type=LiveReader.Resolve("BrokenArrow.Client.Ecs.Utils.Log.LogManager");var instance=type.GetProperty("Instance")!.GetValue(null)!;
  var logger=instance.GetType().GetMethod("GetLogger",[typeof(string)])!.Invoke(instance,["DynamicFrontline"] )!;
  var method=logger.GetType().GetMethod("Info",[typeof(string)])??throw new MissingMethodException("ILogger.Info");return line=>method.Invoke(logger,[line]);
 }
 public void Dispose(){harmony.UnpatchSelf();lobbyService=null;retained=null;}
}
