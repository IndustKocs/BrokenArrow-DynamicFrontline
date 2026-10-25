using System.Diagnostics;
using System.Text.Json;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

public sealed record BridgeFrame(InternalSimulationSnapshot Input,AuthoritativeFrame Result,TerritoryStateSnapshot Territory,
    double SnapshotMilliseconds,double CoreMilliseconds,int PendingRoutes,int CachedRoutes,HealthSnapshot Health);
public sealed class FrontlineSimulationBridge:IDisposable
{
    private readonly IntegrationSettings settings;private readonly Rules rules;private readonly IGameMatchContextProvider contexts;
    private readonly IReadOnlyGameDataAccess access;private readonly IGameTerritoryMapProvider maps;private readonly CoordinateTransform coordinates;
    private readonly Func<DatabaseIdentity,GameUnitProfileResolver?> profileFactory;private readonly GameNavigationAdapter navigation;
    private readonly NavigationConnectivityAdapter navLinks;private readonly bool runtimeAuthorized;
    private BrokenArrowUnitSnapshotProvider? provider;private FrontlineSimulation? simulation;private TeamRoot[] roots=Array.Empty<TeamRoot>();
    private TerritoryStateSnapshot? territory;
    private string? signature,contextSignature;private long logicalTick,nextDue,lastUpdate=-1;private bool coreFaulted;
    public GameIntegrationHealthMonitor Health {get;}=new();public BridgeFrame? LastGood {get;private set;}
    public InternalSimulationSnapshot? LastInspection {get;private set;}
    public FrontlineSimulationBridge(IntegrationSettings settings,Rules rules,IGameMatchContextProvider contexts,IReadOnlyGameDataAccess access,
        IGameTerritoryMapProvider maps,CoordinateTransform coordinates,Func<DatabaseIdentity,GameUnitProfileResolver?> profileFactory,
        GameNavigationAdapter navigation,NavigationSettings navigationSettings,bool runtimeAuthorized=false)
    {
        settings.Validate();rules.Validate();this.settings=settings;this.rules=rules;this.contexts=contexts;this.access=access;
        this.maps=maps;this.coordinates=coordinates;this.profileFactory=profileFactory;this.navigation=navigation;
        navLinks=new(navigation,navigationSettings);this.runtimeAuthorized=runtimeAuthorized;Health.State=IntegrationState.WaitingForMatch;
    }
    public bool Update(long elapsedMilliseconds)
    {
        if(Health.State==IntegrationState.Disposed)return false;
        if(elapsedMilliseconds<lastUpdate||elapsedMilliseconds<0){Health.Reject(FailureCode.InvalidRevision);return false;}
        lastUpdate=elapsedMilliseconds;
        if(!settings.EnableGameDataCollection){Health.State=IntegrationState.DiagnosticsOnly;return false;}
        if(elapsedMilliseconds<nextDue)return false;
        nextDue=checked(elapsedMilliseconds+rules.TickMilliseconds);
        try
        {
            var context=contexts.ReadContext();if(context==null){Health.State=IntegrationState.WaitingForMatch;Health.Reject(FailureCode.NoContext);return false;}
            if(Environment.CurrentManagedThreadId!=context.GameThreadId)throw new IntegrationValidationException(FailureCode.WrongThread);
            if(context.MatchId==Guid.Empty||!context.TrustedFullState||context.Origin==DataOrigin.LiveReadOnly&&!runtimeAuthorized)throw new IntegrationValidationException(FailureCode.NoContext);
            string contextKey=JsonSerializer.Serialize(context with{NavigationRevision=0});
            if(contextSignature!=null&&contextSignature!=contextKey)ResetSession();
            var profiles=profileFactory(context.Database)??throw new IntegrationValidationException(FailureCode.UnknownDatabase);
            string current=contextKey+profiles.Document.Version;
            if(signature!=current)
            {
                ResetSession();profiles.ValidateContext(context.Database);
                var map=maps.ReadMap(context)??throw new IntegrationValidationException(FailureCode.InvalidMap);var grid=GameTerritoryMapProvider.CreateGrid(map);
                roots=map.Roots.ToArray();provider=new(access,profiles,coordinates,grid,context);
                simulation=new(context.MatchId,rules with{LinkDistance=profiles.MaximumRange},grid,profiles.CoreCatalog,roots,navLinks,navLinks,navLinks);
                signature=current;contextSignature=contextKey;
            }
            if(coreFaulted){Health.State=IntegrationState.Unavailable;return false;}
            Health.State=IntegrationState.CollectingData;var watch=Stopwatch.StartNew();var attempt=provider!.Capture(context);
            double snapshotTime=watch.Elapsed.TotalMilliseconds;
            if(attempt.Snapshot is not{} snapshot){Health.Reject(attempt.Failure);Health.State=IntegrationState.Unavailable;return false;}
            LastInspection=snapshot;
            if(!settings.EnableFrontlineSimulation){Health.Accept();Health.State=IntegrationState.DiagnosticsOnly;return true;}
            navigation.BeginFrame(context.MatchId,context.NavigationRevision,logicalTick,snapshot.CoreUnits,roots);navLinks.SetFrame(snapshot,roots);
            watch.Restart();AuthoritativeFrame result;
            try{result=simulation!.Step(logicalTick,snapshot.CoreUnits);}catch{coreFaulted=true;throw new IntegrationValidationException(FailureCode.CoreFailed);}
            logicalTick++;double coreTime=watch.Elapsed.TotalMilliseconds;
            Health.Accept();Health.State=IntegrationState.SimulationReady;
            if(territory==null)territory=simulation!.Snapshot();
            else
            {
                // CellState records are immutable; share unchanged cells across published versions.
                var cells=territory.Cells;
                if(result.Delta.Cells.Length>0){cells=cells.ToArray();foreach(var changed in result.Delta.Cells)cells[changed.Index]=changed;}
                territory=territory with{Header=result.Header,Cells=cells};
            }
            LastGood=new(snapshot,result,territory,snapshotTime,coreTime,navigation.PendingCount,navigation.CachedCount,Health.Snapshot());
            return true;
        }
        catch(IntegrationValidationException e){Health.Reject(e.Code);Health.State=IntegrationState.Unavailable;return false;}
        catch{Health.Reject(FailureCode.ProviderUnavailable);Health.State=IntegrationState.Unavailable;return false;}
    }
    public void ResetSession(){navigation.Reset();provider=null;simulation=null;territory=null;signature=null;contextSignature=null;roots=Array.Empty<TeamRoot>();logicalTick=0;coreFaulted=false;LastGood=null;LastInspection=null;}
    public void Dispose(){ResetSession();Health.State=IntegrationState.Disposed;}
}
