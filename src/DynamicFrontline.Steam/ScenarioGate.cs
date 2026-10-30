namespace DynamicFrontline.Steam;
// Values copied on the Unity thread. Unknown never authorizes observation or networking data.
public sealed record ScenarioEvidence(bool PolicyKnown,bool ScenarioProtected,bool LoggedIn,bool ScenarioEnabled,
 bool PrivateLobbyBound,bool Connected,bool Replay,bool EosSessionPresent,bool IntegrityViolated,
 string LobbyType,string Scenario,int RoomStatus,long FightId);
public static class ScenarioGate
{
 public static string Evaluate(ScenarioEvidence e){
  if(!e.PolicyKnown)return "SessionPolicyUnknown";
  if(e.ScenarioProtected||e.EosSessionPresent||e.IntegrityViolated)return "ProtectedSession_ModDisabled";
  if(!e.LoggedIn||!e.ScenarioEnabled||!e.PrivateLobbyBound||!e.Connected||e.Replay||e.LobbyType!="Scenario"||e.Scenario!="5454584"||e.FightId<=0)return "ScenarioSessionUnproven";
  return e.RoomStatus is 1 or 10 or 20?"AllowedScenario":"ScenarioSessionNotReady";
 }
}
