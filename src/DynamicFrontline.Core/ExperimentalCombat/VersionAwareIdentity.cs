using System.Numerics;
using DynamicFrontline.Core.VerifiedCombat;
namespace DynamicFrontline.Core.ExperimentalCombat;
public readonly record struct VersionAwareIdentity(Guid SessionId,int WorldId,int EntityId,int EntityVersion);
public readonly record struct LifecycleObservation(VersionAwareIdentity Identity,long Revision,int OwnerId,bool Death,bool Dead,bool RemoveUnit,bool FogRemove,bool Disposed,bool SpatialPresent);
public enum IdentityDecision { Accepted,InvalidHandle,DifferentSession,DifferentWorld,DifferentEntity,StaleVersion,StaleSnapshot,Disposed }
public static class IdentityValidation
{
    // Proposed snapshot contract E, not a replacement for GameUnitIdentityResolver.
    public static IdentityDecision Validate(LifecycleObservation snapshot,LifecycleObservation current)
    {
        bool Valid(VersionAwareIdentity h)=>h.SessionId!=Guid.Empty&&h.WorldId>0&&h.EntityId>=0&&h.EntityVersion>=0;
        var a=snapshot.Identity;var b=current.Identity;
        if(!Valid(a)||!Valid(b)||snapshot.Revision<0||current.Revision<0)return IdentityDecision.InvalidHandle;
        if(a.SessionId!=b.SessionId)return IdentityDecision.DifferentSession;if(a.WorldId!=b.WorldId)return IdentityDecision.DifferentWorld;if(a.EntityId!=b.EntityId)return IdentityDecision.DifferentEntity;
        if(a.EntityVersion!=b.EntityVersion)return IdentityDecision.StaleVersion;
        if(current.Disposed||snapshot.Disposed)return IdentityDecision.Disposed;
        if(snapshot.Revision<current.Revision)return IdentityDecision.StaleSnapshot;
        return IdentityDecision.Accepted; // Owner change/spatial absence/death stages are not handle disposal.
    }
}
public readonly record struct LastKnownGateResult(float ActualDistanceSquared,float LastDistanceSquared,float ChosenPathDistanceSquared,bool WouldWriteActual,bool CompletionDistanceReached);
public static class LastKnownNavigationFragment
{
    // P4-F06 B: numeric diagnostic only. Returns NO target position or team contact.
    public static LastKnownGateResult Evaluate(bool hasPath,bool chaseInvisible,Vector3 self,Vector3 actual,Vector3 last,Vector3 pathTarget,float squaredThreshold)
    {
        VectorMath.Validate(self);VectorMath.Validate(actual);VectorMath.Validate(last);VectorMath.Validate(pathTarget);FormulaDomain.Nonnegative(squaredThreshold,nameof(squaredThreshold));
        float Distance(Vector3 a,Vector3 b){float x=a.X-b.X,z=a.Z-b.Z;return FormulaDomain.Result(x*x+z*z);}
        float da=Distance(actual,self),dl=Distance(last,self),dp=Distance(pathTarget,chaseInvisible?actual:last);
        return new(da,dl,dp,hasPath&&Math.Min(da,dl)<=640000&&dp>squaredThreshold,dl<625);
    }
}
