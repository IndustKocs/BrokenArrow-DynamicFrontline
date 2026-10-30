using System.Diagnostics;
using DynamicFrontline.Poc030;
namespace DynamicFrontline.Steam;
// Companion lobby keyed to the native Scenario lobby. Never substitutes for the game's matchmaking.
public sealed class SteamDiscovery : IDisposable
{
 readonly NativeSteam api; readonly string nativeId,compatibility;readonly bool host;readonly Stopwatch clock=Stopwatch.StartNew();
 ulong consentPeer;ulong call;string operation="";double since,nextSearch;bool roster,debugConsent;string consentToken=Guid.NewGuid().ToString("N");string published="";
 public ulong Lobby {get;private set;} public ulong Peer {get;private set;} public string Session {get;private set;}="";
 public string State {get;private set;}="Discovering";public string Mode {get;private set;}="LoadReadProbe";
 public string PeerLabel {get;private set;}="None";public bool PeerRosterConfirmed {get;private set;}
 public bool PairReady {get;private set;} public string Role=>host?"A":"B";
 public SteamDiscovery(NativeSteam api,string nativeId,bool host,string compatibility){this.api=api;this.nativeId=nativeId;this.host=host;this.compatibility=compatibility;}
 void Begin(ulong id,string op){if(id==0)throw new IOException("SteamCallUnavailable");call=id;operation=op;since=clock.Elapsed.TotalSeconds;}
 public string ConsentEpoch {get;private set;}="";public bool FullDebug {get;private set;} public bool LocalDebugConsent=>debugConsent;
 public void SetDebugConsent(bool value){debugConsent=value;consentToken=Guid.NewGuid().ToString("N");FullDebug=false;}
 public void ConfirmRoster()=>roster=true;
 public void Pump()
 {
  PairReady=false;FullDebug=false;
  if(call!=0){if(clock.Elapsed.TotalSeconds-since>25)throw new IOException("SteamLobbyTimeout");
   var bytes=api.Poll(call,operation=="Create"?513:operation=="Join"?504:510,operation=="Create"?16:operation=="Join"?24:4);if(bytes==null)return;call=0;
   if(operation=="Create"){if(BitConverter.ToInt32(bytes,0)!=1)throw new IOException("SteamCreateRejected");Lobby=BitConverter.ToUInt64(bytes,8);Session=Guid.NewGuid().ToString();api.Set(Lobby,"df.native",nativeId);api.Set(Lobby,"df.session",Session);api.Set(Lobby,"df.compat",compatibility);api.Set(Lobby,"df.protocol","18.1");}
   else if(operation=="Join"){if(BitConverter.ToUInt32(bytes,16)!=1)throw new IOException("SteamJoinRejected");Lobby=BitConverter.ToUInt64(bytes,0);}
   else {uint count=BitConverter.ToUInt32(bytes,0);var candidates=Enumerable.Range(0,(int)Math.Min(count,8)).Select(api.LobbyAt).Where(l=>api.Get(l,"df.native")==nativeId&&api.Get(l,"df.compat")==compatibility&&api.IsFriend(api.Owner(l))).ToArray();
    if(candidates.Length>1)throw new InvalidDataException("AmbiguousCompanionLobby");if(candidates.Length==1){Begin(api.Join(candidates[0]),"Join");return;}State=count>0?"IncompatibleModInLobby":"WaitingForModHost";nextSearch=clock.Elapsed.TotalSeconds+3;
   }
  }
  if(Lobby==0){if(clock.Elapsed.TotalSeconds>=nextSearch)Begin(host?api.Create():api.Search(nativeId),host?"Create":"Search");return;}
  if(api.Get(Lobby,"df.native")!=nativeId||api.Get(Lobby,"df.compat")!=compatibility)throw new InvalidDataException("CompanionMetadataChanged");
  var session=api.Get(Lobby,"df.session");if(!Guid.TryParse(session,out _))throw new InvalidDataException("InvalidSteamSession");Session=session;
  if((api.Owner(Lobby)==api.User)!=host)throw new InvalidDataException("HostMigration_NewLobbyRequired");
  var members=api.Members(Lobby);if(!members.Contains(api.User))throw new InvalidDataException("LeftSteamLobby");
  string data=compatibility+"/"+(roster?"Validated":"Probe")+"/"+(debugConsent?"FullDebug":"TeamSafe")+"/"+consentToken;if(published!=data){api.MemberSet(Lobby,"df.ready",data);published=data;}
  if(members.Length!=2){Peer=0;State="WaitingForSecondTester";return;}Peer=members.Single(id=>id!=api.User);if(consentPeer!=0&&consentPeer!=Peer){debugConsent=false;roster=false;}consentPeer=Peer;if(!api.IsFriend(Peer))throw new InvalidDataException("SteamFriendRequired");PeerLabel=api.FriendName(Peer);if(PeerLabel.Length>40)PeerLabel=PeerLabel[..40];
  string remote=api.MemberGet(Lobby,Peer,"df.ready");if(!remote.StartsWith(compatibility+"/",StringComparison.Ordinal)){State="PeerCompatibilityPending";return;}
  var parts=remote.Split('/');if(parts.Length!=4||!Guid.TryParseExact(parts[3],"N",out _)||parts[1] is not("Validated" or "Probe")||parts[2] is not("FullDebug" or "TeamSafe")){State="PeerConsentPending";return;}
  ConsentEpoch=DebugViewConsent.Epoch(consentToken,parts[3],host);
  PeerRosterConfirmed=parts[1]=="Validated";FullDebug=DebugViewConsent.Enabled(debugConsent,parts[2]=="FullDebug",roster,PeerRosterConfirmed);Mode=roster&&PeerRosterConfirmed?"ObserveTwoClient":"LoadReadProbe";PairReady=true;State="SteamPairReady / "+Mode;
 }
 public void Dispose(){api.Leave(Lobby);Lobby=0;PairReady=false;}
}
