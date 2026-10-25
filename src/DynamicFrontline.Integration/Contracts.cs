using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

public enum DataOrigin { Synthetic, RecordedFixture, LiveReadOnly }
public enum ApiEvidence { StaticConfirmed, Partial, RuntimeRequired, Unsupported, Unknown }
public enum IntegrationState { NotInitialized, WaitingForMatch, CollectingData, SimulationReady, DiagnosticsOnly, Unavailable, Disposed }
public enum FailureCode { None, NoContext, WrongThread, ProviderUnavailable, ReadFailed, Incomplete, InvalidIdentity, InvalidPosition, InvalidHealth, InvalidStress, UnknownTeam, InconsistentTeam, UnknownDatabase, ProfileMismatch, InvalidRevision, ContextChanged, InvalidMap, CoreFailed }
public enum CaptureExclusion { None, UnknownProfile, NonCaptureRole, Loaded, Loading, Deserting, NoAliveSoldiers, Dead, InvalidPosition, StressPolicy, MissingInfantry }
public enum StressPolicy { Standard, ExcludePanicked, ExcludeShockedAndPanicked }
public enum UnknownUnitPolicy { Exclude, RejectSnapshot }
public enum NavigationChannel { GroundPassage, OperationalRelay, LogisticsRoute }
public sealed record IntegrationSettings(bool EnableGameDataCollection=false,bool EnableFrontlineSimulation=false,
    bool EnableDebugExport=false,bool EnableGameStateModification=false)
{
    public void Validate(){if(EnableGameStateModification)throw new NotSupportedException("Game state modification is not implemented");}
}
public sealed record DatabaseIdentity(string ScenarioId,string ProfileId,string ProfileVersion,string GameRulesVersion,
    string Source,string? Sha256,bool ActiveVerified);
public sealed record TeamBinding(int TeamUid,int TeamSide,int[] PlayerUids);
public sealed record GameMatchContext(Guid MatchId,DatabaseIdentity Database,long MapRevision,long NavigationRevision,
    TeamBinding[] Teams,DataOrigin Origin,bool TrustedFullState,int GameThreadId);
public interface IGameMatchContextProvider { GameMatchContext? ReadContext(); }
public readonly record struct WorldPoint(double X,double Y,double Z)
{public bool Finite=>double.IsFinite(X)&&double.IsFinite(Y)&&double.IsFinite(Z);}
public sealed record RawVisibility(bool WasDetectedOnce,bool PotentialSee,bool Shootable,bool VisibleMarker,int[] ConfirmedVisibleTeams);
// LifecycleStamp is supplied by an explicit integration lifecycle registry, not a purported game generation.
public sealed record RawGameUnit(long Uid,string LifecycleStamp,int DefinitionId,int GameRole,int PlayerUid,int? TeamUid,int TeamSide,
    WorldPoint WorldPosition,bool Alive,uint? AliveSoldierCount,float? Hp,float? MaxHp,float? Stress,float? MaxStress,
    StressLevel? StressLevel,bool Loaded,bool Loading,bool Deserting,double StatusMultiplier,RawVisibility Visibility);
public sealed record RawGameFrame(long Revision,long CapturedMilliseconds,bool Complete,RawGameUnit[] Units);
public interface IReadOnlyGameDataAccess { RawGameFrame Capture(GameMatchContext context); }
public sealed record GameProfile(int UnitDefinitionId,int GameRole,bool CanCaptureTerritory,bool CanCreateLocalArea,
    bool CanRelayConnectivity,double CaptureTypeMultiplier,double InfluenceRadius,double ConnectivityRange,
    NavigationChannel ConnectivityChannel,int MovementType,int NavigationMask,StressPolicy StressPolicy,
    bool AllowLoading=false,bool AllowNonInfantryRelay=false);
public sealed record ProfileDocument(DatabaseIdentity Database,string Version,UnknownUnitPolicy UnknownUnitPolicy,GameProfile[] Units);
public sealed record AdaptedUnit(UnitKey Key,int DefinitionId,int GameRole,int PlayerUid,int Team,WorldPoint WorldPosition,
    Point2 Position,bool Alive,uint? AliveSoldierCount,float? Hp,float? MaxHp,float? Stress,float? MaxStress,StressLevel? StressLevel,
    bool Loaded,bool Loading,bool Deserting,double StatusMultiplier,CaptureExclusion CaptureReason,GameProfile? Profile,RawVisibility Visibility);
public sealed record InternalSimulationSnapshot(GameMatchContext Context,long SourceRevision,long CapturedMilliseconds,AdaptedUnit[] Units,
    FrontlineUnit[] CoreUnits,int UnknownProfiles);
public sealed record SnapshotAttempt(InternalSimulationSnapshot? Snapshot,FailureCode Failure);
public sealed record HealthSnapshot(IntegrationState State,FailureCode LastFailure,long RejectedSnapshots,long AcceptedSnapshots);
public sealed class GameIntegrationHealthMonitor
{
    public IntegrationState State {get;internal set;}=IntegrationState.NotInitialized;
    public FailureCode LastFailure {get;private set;}public long Rejected {get;private set;}public long Accepted {get;private set;}
    public void Reject(FailureCode reason){LastFailure=reason;Rejected++;}
    public void Accept(){LastFailure=FailureCode.None;Accepted++;}
    public HealthSnapshot Snapshot()=>new(State,LastFailure,Rejected,Accepted);
}
public sealed class IntegrationValidationException(FailureCode code):Exception(code.ToString()) { public FailureCode Code=>code; }
