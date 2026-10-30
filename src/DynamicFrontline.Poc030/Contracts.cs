using System.Security.Cryptography;
using System.Text.Json;
namespace DynamicFrontline.Poc030;
public sealed record GameCompatibilityDescriptor(string BuildId,string UnityVersion,int MetadataVersion,string BindingSchema,Dictionary<string,string> Files)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Digest => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(this with { Files = Files.OrderBy(x=>x.Key,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Value) })));
    public void Verify(string root)
    {
        foreach(var (relative,expected) in Files)
        {
            var path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(Path.GetFullPath(root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid descriptor path");
            var before=new FileInfo(path); long size=before.Length; var stamp=before.LastWriteTimeUtc;
            using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            using var sha=SHA256.Create(); var actual=Convert.ToHexString(sha.ComputeHash(stream));
            var after=new FileInfo(path);
            if(size!=after.Length || stamp!=after.LastWriteTimeUtc || !actual.Equals(expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("BuildMismatch: "+relative);
        }
    }
}
public enum RuntimeMode { OfflineReplay, ObserveTwoClient, EffectsTest, FullModeTest }
public sealed record Capability(string Name,bool Implemented,bool OfflineVerified,bool PreparedForProbe,bool RuntimeValidated,bool WriteAuthorized,string Reason);
public static class Gates
{
    // This candidate deliberately contains no native writer. Config cannot grant one.
    public static bool CanWrite(RuntimeMode mode,Capability capability) => false;
    public static bool CaptureEligible(int role,int id,IReadOnlySet<int> reviewed) => (role==30 || role==31) && reviewed.Contains(id) && id is not (710 or 711 or 713);
}
public sealed record LocalIdentity(int WorldId,int EntityId,int Version);
public sealed record OwnedSample(LocalIdentity Local,int? NetworkId,int? SpawnGeneration,int Owner,int Definition,float Hp,float MaxHp,float Stress,float MaxStress,int Role,float LoadedDatabaseMaxStress,float X,float Y,float Z,uint? Soldiers,int TeamUid = -1,int? ObservedLevel = null,bool Alive = true,bool Loaded = false,bool Loading = false,bool Unloading = false,bool FieldsComplete = true,float? ShockThreshold = null,float? PanicThreshold = null);
public sealed record NativeMapSample(string Scenario,string NavigationName,int NavigationCellSize,double MinX,double MinZ,double SizeX,double SizeZ,string Evidence);
public sealed record ServiceProof(long Sequence,int Owner,int NetId,double Time,double Amount,string Kind,string Evidence,LocalIdentity RecipientLocal,int? SourceNetId=null,LocalIdentity? SourceLocal=null,int? SourceOwner=null);
public sealed record Observation(long Revision,long BindingEpoch,double GameTime,bool Paused,int Owner,int Players,string Map,string Phase,bool Complete,string Quality,OwnedSample[] Owned,long FightId = 0,int Seed = 0,Participant[]? Participants = null,NativeMapSample? MapState=null,ServiceProof[]? ServiceProofs=null);
public sealed record Participant(int Owner,int TeamUid,int NetworkId,bool Bot);
public sealed record ProbeConfig(bool RuntimeReadAuthorized,bool PrivateSessionConfirmed,string Mode,string SessionId,string Role,bool NetworkEnabled,string Host,int Port,string SecretFile,string CertificateFile = "",string CertificatePin = "",string Replication = "TeamSafe",bool TrustedReplicaConsent = false,string BindingFile = "",string MapFile = "",string ProfilesFile = "",bool RosterContractConfirmed = false,string CircleKey = "F8",string SelectOwnKey = "F9",bool PassiveServiceProbeEnabled=false,bool DetailedReplayConsent=false,int MaximumCanonicalUnits=100,bool RadminPair=false);
