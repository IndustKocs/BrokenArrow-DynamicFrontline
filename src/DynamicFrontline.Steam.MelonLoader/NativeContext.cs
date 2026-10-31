using System.Reflection;
using DynamicFrontline.Poc030;
using HarmonyLib;
namespace DynamicFrontline.Steam.MelonLoader;
public sealed record NativeScenario(string Key,bool Host,bool InMatch,long FightId);
// Read-only postfixes on lobby state/lifecycle, never patch anti-cheat or native simulation.
public sealed class NativeContext : IDisposable
{
 const string LobbyType="BrokenArrow.Client.Ecs.GNetwork.Services.Lobby.NetworkLobbyService";
 static object? lobbyService;readonly HarmonyLib.Harmony harmony=new("DynamicFrontline.T18.LobbyObserver");NativeScenario? retained;int retainedLobby,retainedHost;
 public void Install(){var type=LiveReader.Resolve(LobbyType);harmony.Patch(type.GetMethod("get_State"),postfix:new HarmonyMethod(typeof(NativeContext).GetMethod(nameof(Capture),BindingFlags.Static|BindingFlags.NonPublic)));harmony.Patch(type.GetMethod("Dispose"),postfix:new HarmonyMethod(typeof(NativeContext).GetMethod(nameof(Closed),BindingFlags.Static|BindingFlags.NonPublic)));}
 static void Capture(object __instance)=>Interlocked.Exchange(ref lobbyService,__instance);
 static void Closed(object __instance){Interlocked.Exchange(ref lobbyService,null);}
 public NativeScenario? Read()
 {
  var gc=LiveReader.Resolve("BrokenArrow.Client.Ecs.Controllers.GameController");
  bool active=Convert.ToBoolean(gc.GetProperty("IsInstanceAlive")!.GetValue(null));
  if(active){var game=gc.GetProperty("Instance")!.GetValue(null);var state=RuntimeStartup.RoomState(game,LiveReader.Get);
   if(state!=null){
   var room=LiveReader.Get(game!,"_networkRoomService")??throw new InvalidDataException("ScenarioSessionNotReady");
   var scenario=LiveReader.Get(state,"ScenarioMeta");if(scenario==null){retained=null;return null;}
   if(Convert.ToString(LiveReader.Get(state,"LobbyType"))!="Scenario"||Convert.ToString(LiveReader.Get(scenario,"Name"))!="5454584")throw new InvalidDataException("OnlyScenario5454584");
   // Read effective server policy and actual EOS session bookkeeping; no protection/auth calls or hooks.
   var eos=LiveReader.Get(room,"_eosAntiCheatService")??throw new InvalidDataException("SessionPolicyUnknown");
   var openId=LiveReader.Get(eos,"_eosOpenIdAuthService")??throw new InvalidDataException("SessionPolicyUnknown");
   var auth=LiveReader.Get(openId,"_authorizationService")??throw new InvalidDataException("SessionPolicyUnknown");
   var connection=LiveReader.Get(room,"Connection")??throw new InvalidDataException("ScenarioSessionNotReady");
   var api=LiveReader.Get(connection,"Api")??throw new InvalidDataException("ScenarioSessionNotReady");
   var transport=LiveReader.Get(api,"Transport")??throw new InvalidDataException("ScenarioSessionNotReady");
   bool bound=retained!=null&&Convert.ToInt32(LiveReader.Get(state,"WaitingRoomId"))==retainedLobby&&Convert.ToInt32(LiveReader.Get(state,"HostId"))==retainedHost;
   var verdict=ScenarioGate.Evaluate(new(true,ProtectedScenario(),Convert.ToBoolean(LiveReader.Get(auth,"IsLogged")),ContainsScenario(LiveReader.Get(auth,"ActiveLobbyTypes")!),bound,
    Convert.ToBoolean(LiveReader.Get(transport,"IsConnected")),Convert.ToBoolean(LiveReader.Get(connection,"IsRecordPlaying")),
    LiveReader.Get(eos,"_roomService")!=null,Convert.ToBoolean(LiveReader.Get(eos,"IsIntegrityViolated")),Convert.ToString(LiveReader.Get(state,"LobbyType"))!,
    Convert.ToString(LiveReader.Get(LiveReader.Get(state,"ScenarioMeta")!,"Name"))!,Convert.ToInt32(LiveReader.Get(state,"Status")),Convert.ToInt64(LiveReader.Get(state,"FightId"))));
   if(verdict!="AllowedScenario")throw new InvalidDataException(verdict);

   return retained! with{InMatch=true,FightId=Convert.ToInt64(LiveReader.Get(state,"FightId"))};
   }
  }
  var lobby=Volatile.Read(ref lobbyService);if(lobby==null){retained=null;return null;}
  var ls=LiveReader.Get(lobby,"State");if(ls==null){retained=null;return null;}var opts=LiveReader.Get(ls,"Options");var scenarioMeta=LiveReader.Get(ls,"ScenarioMeta");if(opts==null||scenarioMeta==null){retained=null;return null;}
  if(Convert.ToString(LiveReader.Get(opts,"LobbyType"))!="Scenario"||!Convert.ToBoolean(LiveReader.Get(opts,"IsPrivate"))||Convert.ToString(LiveReader.Get(scenarioMeta,"Name"))!="5454584"){retained=null;return null;}
  var id=Convert.ToInt32(LiveReader.Get(lobby,"LobbyId"));if(id<=0)return null;
  if(ProtectedScenario())throw new InvalidDataException("ProtectedSession_ModDisabled");
  retainedLobby=id;retainedHost=Convert.ToInt32(LiveReader.Get(ls,"HostId"));
  return retained=new(CanonicalCodec.Digest(new{Lobby=id,Host=Convert.ToInt32(LiveReader.Get(ls,"HostId")),Scenario="5454584"}),Convert.ToBoolean(LiveReader.Get(lobby,"IsHost")),false,0);
 }
 static bool ProtectedScenario(){var type=LiveReader.Resolve("BrokenArrow.BrokenArrow.Client.Ecs.GNetwork.Services.EosAntiCheatService");const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
  var list=type.GetProperty("ProtectedLobbyTypes",flags)?.GetValue(null)??type.GetField("ProtectedLobbyTypes",flags)?.GetValue(null)??throw new InvalidDataException("SessionPolicyUnknown");return ContainsScenario(list);
 }
 static bool ContainsScenario(object list){
  // Generated IL2CPP collections need not implement managed IEnumerable.
  var item=list.GetType().GetProperty("Item");
  if(item!=null){int n=Convert.ToInt32(LiveReader.Get(list,"Count"));if(n<0||n>16)throw new InvalidDataException("SessionPolicyUnknown");bool protectedScenario=false;for(int i=0;i<n;i++){string? value=Convert.ToString(item.GetValue(list,[i]));if(value is not("Rating" or "Custom" or "Scenario" or "Skirmish"))throw new InvalidDataException("SessionPolicyUnknown");protectedScenario|=value=="Scenario";}return protectedScenario;}
  var contains=list.GetType().GetMethods().FirstOrDefault(m=>m.Name=="Contains"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.IsEnum)??throw new InvalidDataException("SessionPolicyUnknown");
  return Convert.ToBoolean(contains.Invoke(list,[Enum.Parse(contains.GetParameters()[0].ParameterType,"Scenario")]));
 }
 public void Dispose(){harmony.UnpatchSelf();lobbyService=null;retained=null;}
}
