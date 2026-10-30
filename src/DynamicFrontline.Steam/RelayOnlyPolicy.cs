namespace DynamicFrontline.Steam;
// Steam Messages lacks a per-session pre-creation config argument. Its global ICE default
// is set before discovery/network sessions; retained until process exit (never persisted).
public sealed class RelayOnlyPolicy
{
 readonly Func<int> read;public const string Name="RelayOnly_ICE0";
 public RelayOnlyPolicy(Func<int,bool> write,Func<int> read){this.read=read;if(!write(0))throw new InvalidDataException("RelayPolicyWriteFailed");Verify();}
 public void Verify(){if(read()!=0)throw new InvalidDataException("RelayPolicyChanged_DirectForbidden");}
 public static bool PayloadReady(int state,int flags,uint relayPop){CheckConnectedRoute(state,flags,relayPop);return state==3;}
 public static void CheckConnectedRoute(int state,int flags,uint relayPop){if(state==3&&((flags&16)==0||(flags&3)!=0||relayPop==0))throw new InvalidDataException("RelayRouteNotVerified_DirectForbidden");}
}
