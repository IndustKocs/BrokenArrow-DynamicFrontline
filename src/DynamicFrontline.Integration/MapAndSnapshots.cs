using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

public sealed record GameMapDefinition(int Width,int Height,double CellSize,Point2 Origin,bool[] Available,int[] InitialOwners,
    int[] ProtectedOwners,TeamRoot[] Roots,long Revision,DataOrigin OriginKind);
public interface IGameTerritoryMapProvider { GameMapDefinition? ReadMap(GameMatchContext context); }
public sealed class GameTerritoryMapProvider(GameMapDefinition definition):IGameTerritoryMapProvider
{
    private readonly GameMapDefinition map=Clone(definition);
    private static GameMapDefinition Clone(GameMapDefinition d)=>d with{Available=d.Available.ToArray(),InitialOwners=d.InitialOwners.ToArray(),ProtectedOwners=d.ProtectedOwners.ToArray(),Roots=d.Roots.ToArray()};
    public GameMapDefinition? ReadMap(GameMatchContext context)=>context.MapRevision==map.Revision&&context.Origin==map.OriginKind?Clone(map):null;
    public static TerritoryGrid CreateGrid(GameMapDefinition d)
    {
        if(d.InitialOwners.Length!=(long)d.Width*d.Height||d.Available.Length!=d.InitialOwners.Length||d.ProtectedOwners.Length!=d.Available.Length||
            d.InitialOwners.Any(t=>t is <-1 or >1)||d.Roots.Length<2||!d.Roots.Any(r=>r.Team==0)||!d.Roots.Any(r=>r.Team==1))throw new IntegrationValidationException(FailureCode.InvalidMap);
        var grid=new TerritoryGrid(d.Width,d.Height,d.CellSize,d.Available,d.Origin,d.ProtectedOwners);
        for(int i=0;i<d.InitialOwners.Length;i++)
        {
            if(!d.Available[i]&&(d.InitialOwners[i]>=0||d.ProtectedOwners[i]>=0))throw new IntegrationValidationException(FailureCode.InvalidMap);
            if(d.ProtectedOwners[i]>=0&&d.ProtectedOwners[i]!=d.InitialOwners[i])throw new IntegrationValidationException(FailureCode.InvalidMap);
            if(d.InitialOwners[i]>=0)grid.Seed(i,d.InitialOwners[i]);
        }
        foreach(var root in d.Roots)if(root.Team is <0 or >1||grid.Owner(grid.Index(root.Position))!=root.Team)throw new IntegrationValidationException(FailureCode.InvalidMap);
        return grid;
    }
}
public sealed class BrokenArrowUnitSnapshotProvider
{
    private readonly GameUnitIdentityResolver identity=new();private readonly IReadOnlyGameDataAccess access;
    private readonly GameUnitProfileResolver profiles;private readonly CoordinateTransform coordinates;private readonly TerritoryGrid grid;
    private readonly GameTeamResolver teams;private readonly GameMatchContext context;
    private long lastRevision=-1,lastTime=-1;
    public BrokenArrowUnitSnapshotProvider(IReadOnlyGameDataAccess access,GameUnitProfileResolver profiles,CoordinateTransform coordinates,TerritoryGrid grid,GameMatchContext context)
    {this.access=access;this.profiles=profiles;this.coordinates=coordinates;this.grid=grid;this.context=context;teams=new(context.Teams);}
    public SnapshotAttempt Capture(GameMatchContext? currentContext=null)
    {
        var context=currentContext??this.context;
        try
        {
            if(Environment.CurrentManagedThreadId!=context.GameThreadId)throw new IntegrationValidationException(FailureCode.WrongThread);
            if(!context.TrustedFullState)throw new IntegrationValidationException(FailureCode.NoContext);
            profiles.ValidateContext(context.Database);var raw=access.Capture(context);
            if(!raw.Complete)throw new IntegrationValidationException(FailureCode.Incomplete);
            if(raw.Revision<=lastRevision||raw.CapturedMilliseconds<lastTime||raw.CapturedMilliseconds<0)throw new IntegrationValidationException(FailureCode.InvalidRevision);
            var seen=new HashSet<long>();var validated=new List<(RawGameUnit Raw,int Team,Point2 Position,GameProfile? Profile,CaptureExclusion Reason)>();int unknown=0;
            foreach(var u in raw.Units.OrderBy(u=>u.Uid))
            {
                if(string.IsNullOrWhiteSpace(u.LifecycleStamp)||!seen.Add(u.Uid)||u.Visibility==null||u.Visibility.ConfirmedVisibleTeams==null)throw new IntegrationValidationException(FailureCode.InvalidIdentity);
                int? team=teams.Resolve(u);if(team==null)continue;
                var position=coordinates.ToPlane(u.WorldPosition);if(!grid.Available(grid.Index(position)))throw new IntegrationValidationException(FailureCode.InvalidPosition);
                if((u.Hp.HasValue!=u.MaxHp.HasValue)||u.Hp is {} hp&&(!float.IsFinite(hp)||hp<0||!float.IsFinite(u.MaxHp!.Value)||u.MaxHp<=0||hp>u.MaxHp))throw new IntegrationValidationException(FailureCode.InvalidHealth);
                if((u.Stress.HasValue!=u.MaxStress.HasValue)||u.Stress is {} stress&&(!float.IsFinite(stress)||stress<0||!float.IsFinite(u.MaxStress!.Value)||u.MaxStress<=0)||
                    u.StressLevel.HasValue&&!Enum.IsDefined(typeof(StressLevel),u.StressLevel.Value))throw new IntegrationValidationException(FailureCode.InvalidStress);
                if(!double.IsFinite(u.StatusMultiplier)||u.StatusMultiplier<0||u.StatusMultiplier>100||u.AliveSoldierCount>10000)throw new IntegrationValidationException(FailureCode.Incomplete);
                var profile=profiles.Resolve(u.DefinitionId,u.GameRole);if(profile==null)unknown++;
                if(u.Alive&&u.GameRole is >=30 and <=36&&(!u.AliveSoldierCount.HasValue||!u.StressLevel.HasValue||!u.Stress.HasValue||!u.Hp.HasValue))throw new IntegrationValidationException(FailureCode.Incomplete);
                var reason=Exclusion(u,profile);validated.Add((u,team.Value,position,profile,reason));
            }
            var keys=identity.AcceptComplete(context.MatchId,validated.Select(v=>v.Raw).ToArray());var values=new AdaptedUnit[validated.Count];var core=new FrontlineUnit[validated.Count];
            for(int i=0;i<validated.Count;i++)
            {
                var v=validated[i];var u=v.Raw;bool alive=u.Alive&&u.Hp!=0;
                values[i]=new(keys[u.Uid],u.DefinitionId,u.GameRole,u.PlayerUid,v.Team,u.WorldPosition,v.Position,alive,u.AliveSoldierCount,u.Hp,u.MaxHp,u.Stress,u.MaxStress,u.StressLevel,
                    u.Loaded,u.Loading,u.Deserting,u.StatusMultiplier,v.Reason,v.Profile,u.Visibility with{ConfirmedVisibleTeams=u.Visibility.ConfirmedVisibleTeams.ToArray()});
                bool excludedState=v.Reason is CaptureExclusion.StressPolicy||v.Profile==null;
                core[i]=new(keys[u.Uid],u.DefinitionId,v.Team,v.Position,u.AliveSoldierCount??0,alive,
                    u.Loaded||(u.Loading&&v.Profile?.AllowLoading!=true),u.Deserting,u.StressLevel??StressLevel.Calm,excludedState?0:u.StatusMultiplier,
                    u.AliveSoldierCount.HasValue,v.Profile?.AllowNonInfantryRelay==true);
            }
            lastRevision=raw.Revision;lastTime=raw.CapturedMilliseconds;
            var copiedContext=context with{Teams=context.Teams.Select(t=>t with{PlayerUids=t.PlayerUids.ToArray()}).ToArray()};
            return new(new(copiedContext,raw.Revision,raw.CapturedMilliseconds,values,core,unknown),FailureCode.None);
        }
        catch(IntegrationValidationException e){return new(null,e.Code);}
        catch(Exception){return new(null,FailureCode.ReadFailed);}
    }
    private static CaptureExclusion Exclusion(RawGameUnit u,GameProfile? p)
    {
        if(p==null)return CaptureExclusion.UnknownProfile;
        if(!u.Alive||u.Hp==0)return CaptureExclusion.Dead;
        if(u.Loaded)return CaptureExclusion.Loaded;if(u.Loading&&!p.AllowLoading)return CaptureExclusion.Loading;if(u.Deserting)return CaptureExclusion.Deserting;
        if(p.StressPolicy==StressPolicy.ExcludePanicked&&u.StressLevel==Core.StressLevel.Panicked||
            p.StressPolicy==StressPolicy.ExcludeShockedAndPanicked&&u.StressLevel is Core.StressLevel.Shocked or Core.StressLevel.Panicked)return CaptureExclusion.StressPolicy;
        if(!p.CanCaptureTerritory)return CaptureExclusion.NonCaptureRole;
        if(!u.AliveSoldierCount.HasValue)return CaptureExclusion.MissingInfantry;
        if(u.AliveSoldierCount==0)return CaptureExclusion.NoAliveSoldiers;return CaptureExclusion.None;
    }
}
