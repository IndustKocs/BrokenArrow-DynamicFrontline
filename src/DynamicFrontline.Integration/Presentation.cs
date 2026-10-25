using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

// Deliberately no global revision, enemy IDs, route counters, graph, isolation, territory, or rejection reasons.
public sealed record TeamUnitView(long LocalHandle,int DefinitionId,Point2 Position,bool Alive,uint? AliveSoldierCount,bool Loaded,bool Loading);
public sealed record TeamSafeDiagnostic(Guid MatchId,int RecipientTeam,long PresentationTick,TeamUnitView[] FriendlyUnits);
public sealed class GameVisibilityAdapter
{
    public bool HasConfirmedContact(AdaptedUnit unit,int team)=>unit.Visibility.ConfirmedVisibleTeams.Contains(team);
    // Contact alone does not authorize this prototype to export enemy positions. PotentialSee/Shootable/history never authorize it.
    public bool MayExport(AdaptedUnit unit,int authenticatedTeam)=>unit.Team==authenticatedTeam;
}
public sealed class TeamSafeProjection
{
    private readonly Dictionary<int,Dictionary<UnitKey,long>> handles=new();private readonly long[] next=new long[2];private Guid match;
    private readonly GameVisibilityAdapter visibility=new();
    public TeamSafeDiagnostic Project(InternalSimulationSnapshot snapshot,int authenticatedTeam,long publicClockTick)
    {
        if(authenticatedTeam is <0 or >1||publicClockTick<0)throw new ArgumentException("Invalid team context");
        if(match!=snapshot.Context.MatchId){handles.Clear();Array.Clear(next,0,next.Length);match=snapshot.Context.MatchId;}
        if(!handles.TryGetValue(authenticatedTeam,out var map))handles[authenticatedTeam]=map=new();
        var output=new List<TeamUnitView>();foreach(var u in snapshot.Units.Where(u=>visibility.MayExport(u,authenticatedTeam)).OrderBy(u=>u.Key.Uid).ThenBy(u=>u.Key.Generation))
        {
            if(!map.TryGetValue(u.Key,out long handle))map[u.Key]=handle=++next[authenticatedTeam];
            output.Add(new(handle,u.DefinitionId,u.Position,u.Alive,u.AliveSoldierCount,u.Loaded,u.Loading));
        }
        return new(match,authenticatedTeam,publicClockTick,output.ToArray());
    }
    public static void ValidateRecipient(TeamSafeDiagnostic dto,Guid match,int authenticatedTeam)
    {if(dto.MatchId!=match||dto.RecipientTeam!=authenticatedTeam)throw new InvalidOperationException("Recipient mismatch");}
}
public sealed class FrontlineDiagnosticsService(IntegrationSettings settings)
{
    private static readonly JsonSerializerOptions options=new(){WriteIndented=false};
    public string ExportTeam(TeamSafeDiagnostic state,int authenticatedTeam,Guid match)
    {TeamSafeProjection.ValidateRecipient(state,match,authenticatedTeam);return JsonSerializer.Serialize(state,options);}
    public string ExportDeveloper(BridgeFrame frame)
    {
        if(!settings.EnableDebugExport||frame.Input.Context.Origin==DataOrigin.LiveReadOnly)throw new InvalidOperationException("Full debug export is restricted to explicitly enabled offline data");
        return JsonSerializer.Serialize(frame,options);
    }
    public string PublicStatus()=>"Read-only diagnostics"; // No hidden-dependent counters or exception strings.
}
