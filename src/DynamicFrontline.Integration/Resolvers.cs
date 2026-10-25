using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
namespace DynamicFrontline.Integration;

public sealed class GameTeamResolver
{
    private readonly Dictionary<int,(int Team,int Uid,int Side)> players=new();
    public GameTeamResolver(TeamBinding[] bindings)
    {
        if(bindings.Length!=2||bindings.Select(x=>x.TeamUid).Distinct().Count()!=2||bindings.Select(x=>x.TeamSide).Distinct().Count()!=2)
            throw new IntegrationValidationException(FailureCode.InconsistentTeam);
        for(int i=0;i<2;i++)
        {
            var b=bindings[i];if(b.TeamSide is not (0 or 1)||b.PlayerUids.Length>5||b.PlayerUids.Length==0)throw new IntegrationValidationException(FailureCode.InconsistentTeam);
            foreach(int player in b.PlayerUids)if(!players.TryAdd(player,(i,b.TeamUid,b.TeamSide)))throw new IntegrationValidationException(FailureCode.InconsistentTeam);
        }
    }
    public int? Resolve(RawGameUnit u)
    {
        if(u.TeamSide is 100 or 101 or 255)return null;
        if(u.TeamUid==null||!players.TryGetValue(u.PlayerUid,out var binding))throw new IntegrationValidationException(FailureCode.UnknownTeam);
        if(binding.Uid!=u.TeamUid||binding.Side!=u.TeamSide)throw new IntegrationValidationException(FailureCode.InconsistentTeam);
        return binding.Team;
    }
}
public sealed class CoordinateTransform(WorldPoint origin,double scale)
{
    public Point2 ToPlane(WorldPoint p)
    {Validate();if(!p.Finite)throw new IntegrationValidationException(FailureCode.InvalidPosition);return new((p.X-origin.X)*scale,(p.Z-origin.Z)*scale);}
    public WorldPoint ToWorld(Point2 p,double height)
    {Validate();if(!p.IsFinite||!double.IsFinite(height))throw new IntegrationValidationException(FailureCode.InvalidPosition);return new(p.X/scale+origin.X,height,p.Y/scale+origin.Z);}
    private void Validate(){if(!origin.Finite||!double.IsFinite(scale)||scale<=0)throw new IntegrationValidationException(FailureCode.InvalidMap);}
}
public sealed class GameUnitProfileResolver
{
    private readonly Dictionary<int,GameProfile> profiles;
    public ProfileDocument Document {get;}
    public ProfileCatalog CoreCatalog {get;}
    public double MaximumRange {get;}
    public GameUnitProfileResolver(ProfileDocument document)
    {
        if(document.Database==null||!document.Database.ActiveVerified||document.Units==null||string.IsNullOrWhiteSpace(document.Version))throw new IntegrationValidationException(FailureCode.UnknownDatabase);
        ValidateHash(document.Database.Sha256);
        Document=document with{Units=document.Units.ToArray()};profiles=new();var core=new List<UnitTerritoryProfile>();
        foreach(var p in document.Units)
        {
            if(!profiles.TryAdd(p.UnitDefinitionId,p)||!double.IsFinite(p.ConnectivityRange)||p.ConnectivityRange<=0||
                !double.IsFinite(p.CaptureTypeMultiplier)||p.CaptureTypeMultiplier<0||p.CaptureTypeMultiplier>1000||
                !double.IsFinite(p.InfluenceRadius)||p.InfluenceRadius<=0||!Enum.IsDefined(typeof(StressPolicy),p.StressPolicy)||
                !Enum.IsDefined(typeof(NavigationChannel),p.ConnectivityChannel))throw new IntegrationValidationException(FailureCode.ProfileMismatch);
            // Role is corroboration, never a grant of capture or relay rights.
            if((p.CanCaptureTerritory||p.CanCreateLocalArea)&&p.GameRole is not (30 or 31))throw new IntegrationValidationException(FailureCode.ProfileMismatch);
            if(p.CanCreateLocalArea&&!p.CanCaptureTerritory)throw new IntegrationValidationException(FailureCode.ProfileMismatch);
            var role=p.GameRole switch{30 or 31=>UnitRole.Line,32=>UnitRole.Recon,33=>UnitRole.Sniper,36=>UnitRole.SpecialForces,_=>UnitRole.Other};
            core.Add(new(p.UnitDefinitionId,role,p.CanCaptureTerritory,p.CanRelayConnectivity,p.CaptureTypeMultiplier,p.InfluenceRadius,1,p.CanCreateLocalArea));
        }
        CoreCatalog=new(core);MaximumRange=document.Units.Length==0?1:document.Units.Max(p=>p.ConnectivityRange);
    }
    public GameProfile? Resolve(int id,int gameRole)
    {
        if(!profiles.TryGetValue(id,out var p)){if(Document.UnknownUnitPolicy==UnknownUnitPolicy.RejectSnapshot)throw new IntegrationValidationException(FailureCode.ProfileMismatch);return null;}
        if(p.GameRole!=gameRole)throw new IntegrationValidationException(FailureCode.ProfileMismatch);return p;
    }
    public void ValidateContext(DatabaseIdentity active)
    {if(!active.ActiveVerified)throw new IntegrationValidationException(FailureCode.UnknownDatabase);ValidateHash(active.Sha256);if(active!=Document.Database)throw new IntegrationValidationException(FailureCode.ProfileMismatch);}
    public static void ValidateHash(string? hash)
    {if(hash!=null&&(hash.Length!=64||hash.Any(c=>!Uri.IsHexDigit(c))))throw new IntegrationValidationException(FailureCode.ProfileMismatch);}
    public static GameUnitProfileResolver Load(string json,string expectedConfigSha256)
    {
        ValidateHash(expectedConfigSha256);string actual=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        if(!string.Equals(actual,expectedConfigSha256,StringComparison.OrdinalIgnoreCase))throw new IntegrationValidationException(FailureCode.ProfileMismatch);
        try{return new(JsonSerializer.Deserialize<ProfileDocument>(json,new JsonSerializerOptions{Converters={new JsonStringEnumConverter()}})??throw new JsonException());}
        catch(JsonException){throw new IntegrationValidationException(FailureCode.ProfileMismatch);}
    }
}
public sealed class GameUnitIdentityResolver
{
    private readonly record struct Entry(string Stamp,int Generation,bool Present,bool Alive);
    private Dictionary<long,Entry> entries=new();
    public IReadOnlyDictionary<long,UnitKey> AcceptComplete(Guid match,IReadOnlyList<RawGameUnit> units)
    {
        // Construct first, then swap. Rejected/incomplete frames never call this method.
        var next=entries.ToDictionary(p=>p.Key,p=>p.Value with{Present=false});var keys=new Dictionary<long,UnitKey>();
        foreach(var u in units)
        {
            int generation=1;
            if(entries.TryGetValue(u.Uid,out var old))generation=old.Present&&old.Stamp==u.LifecycleStamp&&(!u.Alive||old.Alive)?old.Generation:checked(old.Generation+1);
            if(!keys.TryAdd(u.Uid,new(match,u.Uid,generation)))throw new IntegrationValidationException(FailureCode.InvalidIdentity);
            next[u.Uid]=new(u.LifecycleStamp,generation,true,u.Alive);
        }
        entries=next;return keys;
    }
    public void Reset()=>entries.Clear();
}
