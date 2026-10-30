using System.Diagnostics;
using System.Runtime.InteropServices;
namespace DynamicFrontline.Steam;
// Windows x64 Steam flat ABI. Reuse the game's already loaded steam_api64.dll; never Init/Shutdown/RunCallbacks.
public sealed class NativeSteam
{
 const string Dll="DF_EXISTING_STEAM";
 public const int Channel=18230;
 public IntPtr Matchmaking {get;} public IntPtr Utils {get;} public IntPtr Messages {get;} public ulong User {get;}
 static NativeSteam(){NativeLibrary.SetDllImportResolver(typeof(NativeSteam).Assembly,(name,_,_)=>{
  if(name!=Dll)return IntPtr.Zero;
  var m=Process.GetCurrentProcess().Modules.Cast<ProcessModule>().SingleOrDefault(m=>string.Equals(m.ModuleName,"steam_api64.dll",StringComparison.OrdinalIgnoreCase));
  return m?.BaseAddress??throw new DllNotFoundException("Game Steam library not loaded");});}
 public NativeSteam(){if(IntPtr.Size!=8)throw new PlatformNotSupportedException("x64 only");Matchmaking=SteamAPI_SteamMatchmaking_v009();Utils=SteamAPI_SteamUtils_v010();Messages=SteamAPI_SteamNetworkingMessages_SteamAPI_v002();var u=SteamAPI_SteamUser_v023();if(new[]{Matchmaking,Utils,Messages,u}.Any(p=>p==IntPtr.Zero))throw new InvalidOperationException("SteamNotInitialized");User=SteamAPI_ISteamUser_GetSteamID(u);if(User==0)throw new InvalidOperationException("SteamUserUnavailable");}
 public void MemberSet(ulong lobby,string key,string value)=>SteamAPI_ISteamMatchmaking_SetLobbyMemberData(Matchmaking,lobby,key,value);
 public string MemberGet(ulong lobby,ulong member,string key)=>Marshal.PtrToStringUTF8(SteamAPI_ISteamMatchmaking_GetLobbyMemberData(Matchmaking,lobby,member,key))??"";
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void SteamAPI_ISteamMatchmaking_SetLobbyMemberData(IntPtr self,ulong lobby,[MarshalAs(UnmanagedType.LPUTF8Str)] string key,[MarshalAs(UnmanagedType.LPUTF8Str)] string value);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SteamAPI_ISteamMatchmaking_GetLobbyMemberData(IntPtr self,ulong lobby,ulong member,[MarshalAs(UnmanagedType.LPUTF8Str)] string key);
 public bool IsFriend(ulong id)=>SteamAPI_ISteamFriends_GetFriendRelationship(SteamAPI_SteamFriends_v017(),id)==3;
 public string FriendName(ulong id)=>Marshal.PtrToStringUTF8(SteamAPI_ISteamFriends_GetFriendPersonaName(SteamAPI_SteamFriends_v017(),id))??"Steam peer";
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SteamAPI_SteamFriends_v017();
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int SteamAPI_ISteamFriends_GetFriendRelationship(IntPtr self,ulong id);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr SteamAPI_ISteamFriends_GetFriendPersonaName(IntPtr self,ulong id);
 public ulong Create()=>SteamAPI_ISteamMatchmaking_CreateLobby(Matchmaking,3,2);
 public ulong Join(ulong id)=>SteamAPI_ISteamMatchmaking_JoinLobby(Matchmaking,id);
 public void Leave(ulong id){if(id!=0)SteamAPI_ISteamMatchmaking_LeaveLobby(Matchmaking,id);}
 public ulong Search(string nativeLobby){SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter(Matchmaking,"df.protocol","18.1",0);SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter(Matchmaking,"df.native",nativeLobby,0);SteamAPI_ISteamMatchmaking_AddRequestLobbyListDistanceFilter(Matchmaking,3);SteamAPI_ISteamMatchmaking_AddRequestLobbyListResultCountFilter(Matchmaking,8);return SteamAPI_ISteamMatchmaking_RequestLobbyList(Matchmaking);}
 public string Get(ulong lobby,string key)=>Marshal.PtrToStringUTF8(SteamAPI_ISteamMatchmaking_GetLobbyData(Matchmaking,lobby,key))??"";
 public void Set(ulong lobby,string key,string value){if(!SteamAPI_ISteamMatchmaking_SetLobbyData(Matchmaking,lobby,key,value))throw new IOException("SteamMetadataWrite");}
 public ulong Owner(ulong lobby)=>SteamAPI_ISteamMatchmaking_GetLobbyOwner(Matchmaking,lobby);
 public ulong[] Members(ulong lobby){int n=SteamAPI_ISteamMatchmaking_GetNumLobbyMembers(Matchmaking,lobby);if(n<0||n>2)throw new InvalidDataException("TwoTestersOnly");return Enumerable.Range(0,n).Select(i=>SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex(Matchmaking,lobby,i)).ToArray();}
 public ulong LobbyAt(int i)=>SteamAPI_ISteamMatchmaking_GetLobbyByIndex(Matchmaking,i);
 public byte[]? Poll(ulong call,int callback,int size){if(!SteamAPI_ISteamUtils_IsAPICallCompleted(Utils,call,out bool fail))return null;if(fail)throw new IOException("SteamCallFailed");var p=Marshal.AllocHGlobal(size);try{if(!SteamAPI_ISteamUtils_GetAPICallResult(Utils,call,p,size,callback,out fail)||fail)throw new IOException("SteamCallResultFailed");var bytes=new byte[size];Marshal.Copy(p,bytes,0,size);return bytes;}finally{Marshal.FreeHGlobal(p);}}
 [StructLayout(LayoutKind.Explicit,Size=136)] public struct Identity { [FieldOffset(0)] public int Type;[FieldOffset(4)] public int Size;[FieldOffset(8)] public ulong Id; public static Identity For(ulong id)=>new(){Type=16,Size=8,Id=id}; }
 public sealed class Wire : ISteamWire,IDisposable {
  readonly NativeSteam api; readonly ulong peer; public Wire(NativeSteam api,ulong peer){this.api=api;this.peer=peer;var id=Identity.For(peer);if(!SteamAPI_ISteamNetworkingMessages_AcceptSessionWithUser(api.Messages,ref id))throw new IOException("SteamAcceptFailed");}
  public void Send(byte[] packet){var id=Identity.For(peer);var p=Marshal.AllocHGlobal(packet.Length);try{Marshal.Copy(packet,0,p,packet.Length);int result=SteamAPI_ISteamNetworkingMessages_SendMessageToUser(api.Messages,ref id,p,(uint)packet.Length,8,Channel);if(result!=1)throw new IOException("SteamSend:"+result);}finally{Marshal.FreeHGlobal(p);}}
  public bool TryReceive(out byte[] packet){packet=[];var pointers=new IntPtr[1];int n=SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(api.Messages,Channel,pointers,1);if(n<0)throw new IOException("SteamReceive");if(n==0)return false;var p=pointers[0];try{
   int size=Marshal.ReadInt32(p,8);var id=Marshal.PtrToStructure<Identity>(IntPtr.Add(p,16));if(id.Type!=16||id.Size!=8||id.Id!=peer)throw new InvalidDataException("ForeignSteamSender");if(size<1||size>110000||Marshal.ReadInt32(p,192)!=Channel)throw new InvalidDataException("SteamMessageBudgetOrChannel");packet=new byte[size];Marshal.Copy(Marshal.ReadIntPtr(p),packet,0,size);return true;
  }finally{SteamAPI_SteamNetworkingMessage_t_Release(p);}}
  public void Dispose(){var id=Identity.For(peer);SteamAPI_ISteamNetworkingMessages_CloseChannelWithUser(api.Messages,ref id,Channel);}
 }
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern IntPtr SteamAPI_SteamMatchmaking_v009();
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern IntPtr SteamAPI_SteamUtils_v010();
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern IntPtr SteamAPI_SteamUser_v023();
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern IntPtr SteamAPI_SteamNetworkingMessages_SteamAPI_v002();
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamUser_GetSteamID(IntPtr self);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_CreateLobby(IntPtr self,int type,int max);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_JoinLobby(IntPtr self,ulong id);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern void SteamAPI_ISteamMatchmaking_LeaveLobby(IntPtr self,ulong id);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern void SteamAPI_ISteamMatchmaking_AddRequestLobbyListStringFilter(IntPtr self,[MarshalAs(UnmanagedType.LPUTF8Str)] string key,[MarshalAs(UnmanagedType.LPUTF8Str)] string value,int comparison);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern void SteamAPI_ISteamMatchmaking_AddRequestLobbyListDistanceFilter(IntPtr self,int distance);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern void SteamAPI_ISteamMatchmaking_AddRequestLobbyListResultCountFilter(IntPtr self,int count);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_RequestLobbyList(IntPtr self);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern IntPtr SteamAPI_ISteamMatchmaking_GetLobbyData(IntPtr self,ulong lobby,[MarshalAs(UnmanagedType.LPUTF8Str)] string key);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]
 static extern bool SteamAPI_ISteamMatchmaking_SetLobbyData(IntPtr self,ulong lobby,[MarshalAs(UnmanagedType.LPUTF8Str)] string key,[MarshalAs(UnmanagedType.LPUTF8Str)] string value);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_GetLobbyOwner(IntPtr self,ulong lobby);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern int SteamAPI_ISteamMatchmaking_GetNumLobbyMembers(IntPtr self,ulong lobby);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_GetLobbyMemberByIndex(IntPtr self,ulong lobby,int index);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern ulong SteamAPI_ISteamMatchmaking_GetLobbyByIndex(IntPtr self,int index);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]
 static extern bool SteamAPI_ISteamUtils_IsAPICallCompleted(IntPtr self,ulong call,[MarshalAs(UnmanagedType.I1)] out bool failed);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]
 static extern bool SteamAPI_ISteamUtils_GetAPICallResult(IntPtr self,ulong call,IntPtr result,int size,int callback,[MarshalAs(UnmanagedType.I1)] out bool failed);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]
 static extern bool SteamAPI_ISteamNetworkingMessages_AcceptSessionWithUser(IntPtr self,ref Identity identity);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern int SteamAPI_ISteamNetworkingMessages_SendMessageToUser(IntPtr self,ref Identity identity,IntPtr data,uint size,int flags,int channel);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern int SteamAPI_ISteamNetworkingMessages_ReceiveMessagesOnChannel(IntPtr self,int channel,[Out] IntPtr[] messages,int max);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)][return:MarshalAs(UnmanagedType.I1)]
 static extern bool SteamAPI_ISteamNetworkingMessages_CloseChannelWithUser(IntPtr self,ref Identity identity,int channel);
 [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)]
 static extern void SteamAPI_SteamNetworkingMessage_t_Release(IntPtr message);
}
