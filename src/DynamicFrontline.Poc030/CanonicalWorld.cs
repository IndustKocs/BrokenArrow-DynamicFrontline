using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Poc030;
public sealed record MatchBinding(Guid ModSession,long FightId,int Seed,string Map,Participant[] Players,int OwnerA,int OwnerB,string Evidence)
{
    public void Validate(){if(ModSession==Guid.Empty||FightId<=0||Players.Length!=2||Players.Any(p=>p.Bot)||Players.Select(p=>p.Owner).Distinct().Count()!=2||OwnerA==OwnerB||!Players.Any(p=>p.Owner==OwnerA)||!Players.Any(p=>p.Owner==OwnerB)||string.IsNullOrWhiteSpace(Evidence))throw new InvalidDataException("UnprovenTwoPlayerBinding");}
    public int Owner(string role)=>role=="A"?OwnerA:role=="B"?OwnerB:throw new InvalidDataException("Role");
    public void Check(Observation o,string role){Validate();if(o.FightId!=FightId||o.Seed!=Seed||o.Map!=Map||o.Owner!=Owner(role)||o.Players!=2||o.Participants==null||!o.Participants.OrderBy(p=>p.Owner).SequenceEqual(Players.OrderBy(p=>p.Owner)))throw new InvalidDataException("NativeMatchOwnerBindingMismatch");}
    public int Team(int nativeTeam){var teams=Players.Select(p=>p.TeamUid).Distinct().OrderBy(x=>x).ToArray();int n=Array.IndexOf(teams,nativeTeam);if(n<0)throw new InvalidDataException("UnknownTeam");return n;}
}
public static class CanonicalCodec
{
    public static byte[] Bytes<T>(T value)=>JsonSerializer.SerializeToUtf8Bytes(value);
    public static string Digest<T>(T value)=>Convert.ToHexString(SHA256.HashData(Bytes(value)));
    public static CanonicalInput Normalize(CanonicalInput i)=>i with{Units=i.Units.OrderBy(u=>u.Key.Uid).ThenBy(u=>u.Key.Generation).ToArray(),Services=i.Services.OrderBy(s=>s.Start).ThenBy(s=>s.EventId,StringComparer.Ordinal).ToArray(),SourceRevisions=i.SourceRevisions.OrderBy(s=>s.Owner).ToArray()};
}
public sealed record SourceRevision(int Owner,long Revision,double Time);
public sealed record CanonicalInput(int Schema,Guid Session,string BindingHash,string MapHash,string ProfilesHash,string RulesHash,long InputId,double Start,double End,SourceRevision[] SourceRevisions,Unit023[] Units,ServiceInterval[] Services,bool SupplyComplete,bool VictoryComplete,bool DataGap=false)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Hash=>CanonicalCodec.Digest(new{Schema,Session,BindingHash,MapHash,ProfilesHash,RulesHash,InputId,Start,End,SourceRevisions,Units,Services,SupplyComplete,VictoryComplete,DataGap});
}
public sealed record ProfileEntry(int Definition,int NativeRole,float NativeS0,bool Capture,bool Infantry,bool ApcIfv,double Strength,MoraleClass Morale,string Evidence);
public sealed record ProfileSet(string BuildFingerprint,string SourceDigest,string Policy,ProfileEntry[] Entries)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Hash=>CanonicalCodec.Digest(this);
    public Profile023 Resolve(OwnedSample sample)
    {
        var p=Entries.SingleOrDefault(p=>p.Definition==sample.Definition)??throw new InvalidDataException("UnsupportedDefinition:"+sample.Definition);
        if(!sample.FieldsComplete||sample.Role!=p.NativeRole||sample.LoadedDatabaseMaxStress!=p.NativeS0||p.NativeS0<=0||sample.MaxStress<=0||sample.ShockThreshold==null||sample.PanicThreshold==null||p.Definition is 710 or 711 or 713||string.IsNullOrWhiteSpace(p.Evidence))throw new InvalidDataException("UnprovenActiveProfile");
        if(p.Capture&&!Gates.CaptureEligible(sample.Role,sample.Definition,Entries.Where(p=>p.Capture).Select(p=>p.Definition).ToHashSet()))throw new InvalidDataException("CaptureAllowlist");
        return new(p.Definition.ToString(),p.Capture,p.Capture,true,p.Infantry,p.ApcIfv,p.Infantry?Mobility.Infantry:Mobility.GroundVehicle,p.Strength,p.Morale,true,p.NativeS0,sample.ShockThreshold.Value/sample.MaxStress,sample.PanicThreshold.Value/sample.MaxStress,p.Evidence+"; active UnitData/threshold comparison");
    }
}
public sealed record MapDescriptor(string Map,string GameFingerprint,string Evidence,string SourceSha256,long Revision,int Width,int Height,double CellSize,Point2 Origin,bool[] Scoring,bool[] Infantry,bool[] Ground,TeamRoot[] Roots,NavPortal[] Portals,bool GeometryValidated)
{
    [System.Text.Json.Serialization.JsonIgnore] public string Hash=>CanonicalCodec.Digest(this);
    public PreparedTerrain Create(string actualMap,string fingerprint)
    {
        if(Map!=actualMap||GameFingerprint!=fingerprint||string.IsNullOrWhiteSpace(Evidence)||SourceSha256.Length!=64||!GeometryValidated||Revision<1||Width<2||Height<2||Width>512||Height>512||Scoring.Length!=Width*Height||Infantry.Length!=Scoring.Length||Ground.Length!=Scoring.Length||Roots.Length<2)throw new InvalidDataException("MapUnsupportedOrUnproven");
        var grid=new TerritoryGrid(Width,Height,CellSize,Scoring.Select((s,i)=>s||Infantry[i]||Ground[i]).ToArray(),Origin);
        var t=new PreparedTerrain(grid,Roots){Revision=Revision,Portals=Portals};Array.Copy(Infantry,t.Passable[0],Infantry.Length);Array.Copy(Ground,t.Passable[1],Ground.Length);return t;
    }
}
public sealed record SpawnEntry(int NetId,int Generation,int Owner,int Definition,bool Dead,LocalIdentity Local);
public sealed class SpawnRegistry
{
    readonly Dictionary<int,SpawnEntry> entries=new();
    public int Resolve(OwnedSample u)
    {
        if(u.NetworkId==null)throw new InvalidDataException("NoCanonicalNetId");int id=u.NetworkId.Value;
        if(!entries.TryGetValue(id,out var old)){if(entries.Count>=10000)throw new InvalidDataException("SpawnBudget");entries[id]=new(id,0,u.Owner,u.Definition,!u.Alive,u.Local);return 0;}
        if(old.Owner!=u.Owner)throw new InvalidDataException("OwnershipTransferRequiresLifecycleEvidence");
        int generation=old.Generation;
        if(old.Dead&&u.Alive)generation++;
        else if(old.Definition!=u.Definition||old.Local!=u.Local&&old.Owner==u.Owner)throw new InvalidDataException("UnprovenSpawnRebind_ResyncRequired");
        entries[id]=new(id,generation,u.Owner,u.Definition,!u.Alive,u.Local);return generation;
    }
    public SpawnEntry[] Export()=>entries.Values.OrderBy(e=>e.NetId).ToArray();
    public void Restore(SpawnEntry[] records){if(records.Length>10000)throw new InvalidDataException("SpawnBudget");entries.Clear();foreach(var e in records)entries.Add(e.NetId,e);}
}
public sealed record OwnedDelta(long BaseRevision,Observation Header,OwnedSample[] Upserts,int[] RemovedNetIds);
public sealed class CanonicalWorld
{
    readonly MatchBinding binding;readonly ProfileSet profiles;readonly MapDescriptor map;readonly Rules023Config rules;readonly SpawnRegistry registry=new();
    readonly Dictionary<string,string> deltas=new();readonly Dictionary<string,Observation> sources=new();readonly Dictionary<string,string> hashes=new();long next;double? lastTime;
    public CanonicalWorld(MatchBinding binding,ProfileSet profiles,MapDescriptor map,Rules023Config rules){binding.Validate();this.binding=binding;this.profiles=profiles;this.map=map;this.rules=rules;}
    public void Full(string role,Observation observation)
    {
        binding.Check(observation,role);if(observation.Owned.Length>1000||observation.Owned.Any(u=>u.Owner!=observation.Owner||u.TeamUid!=binding.Players.Single(p=>p.Owner==observation.Owner).TeamUid)||observation.Owned.Where(u=>u.NetworkId!=null).Select(u=>u.NetworkId).Distinct().Count()!=observation.Owned.Length||!double.IsFinite(observation.GameTime))throw new InvalidDataException("InvalidOwnedSnapshot");
        string hash=CanonicalCodec.Digest(observation);
        if(sources.TryGetValue(role,out var old)){if(observation.Revision<old.Revision)throw new InvalidDataException("LateSnapshotResync");if(observation.Revision==old.Revision){if(hashes[role]!=hash)throw new InvalidDataException("ConflictingObservation");return;}}
        sources[role]=observation;hashes[role]=hash;
    }
    public void Delta(string role,OwnedDelta delta)
    {
        string key=role+":"+delta.Header.Revision;string digest=CanonicalCodec.Digest(delta);if(deltas.TryGetValue(key,out var previous)){if(previous!=digest)throw new InvalidDataException("ConflictingDelta");return;}
        if(!sources.TryGetValue(role,out var old)||old.Revision!=delta.BaseRevision||!old.Complete||delta.Header.Revision!=delta.BaseRevision+1)throw new InvalidDataException("DeltaBaseGap_Resync");
        var records=old.Owned.ToDictionary(u=>u.NetworkId!.Value);foreach(int id in delta.RemovedNetIds)records.Remove(id);foreach(var u in delta.Upserts){if(u.NetworkId==null)throw new InvalidDataException("DeltaIdentity");records[u.NetworkId.Value]=u;}
        Full(role,delta.Header with{Owned=records.Values.OrderBy(u=>u.NetworkId).ToArray()});deltas[key]=digest;if(deltas.Count>256)deltas.Remove(deltas.Keys.First());
    }
    public CanonicalInput? Build(SourceHealth a,SourceHealth b)
    {
        if(!sources.TryGetValue("A",out var x)||!sources.TryGetValue("B",out var y)||!x.Complete||!y.Complete||a.State!=SourceState.LiveComplete||b.State!=SourceState.LiveComplete||a.AgeSeconds>1||b.AgeSeconds>1||x.Revision!=a.ObservationRevision||y.Revision!=b.ObservationRevision||x.Paused||y.Paused)return null;
        if(Math.Abs(x.GameTime-y.GameTime)>.125)throw new InvalidDataException("ClockSkew");
        double end=Math.Floor(Math.Min(x.GameTime,y.GameTime)*4)/4;if(lastTime.HasValue&&end<=lastTime.Value)return null;
        bool gap=lastTime==null||end-lastTime.Value>.25+1e-8;double start=end-.25;if(start<0)return null;
        var units=x.Owned.Concat(y.Owned).OrderBy(u=>u.NetworkId).Select(u=>{
            var profile=profiles.Resolve(u);if(u.ObservedLevel is null or <0 or >2||u.MaxHp<=0||!float.IsFinite(u.X)||!float.IsFinite(u.Z)||profile.IsInfantry&&u.Soldiers==null)throw new InvalidDataException("UnknownObservedInput");
            return new Unit023(new(binding.ModSession,u.NetworkId!.Value,registry.Resolve(u)),binding.Team(u.TeamUid),u.Owner,u.Definition,profile,new(u.X,u.Z),u.Soldiers??0,u.Hp,u.MaxHp,u.Stress,(StressLevel)u.ObservedLevel.Value,u.Alive,u.Loaded||u.Loading||u.Unloading,false,false,true,false,0,"UA01 guarded gameplay read",Math.Max(x.GameTime,y.GameTime)-end,u.MaxStress);
        }).ToArray();
        foreach(var u in units)u.Validate(binding.ModSession);
        var input=new CanonicalInput(2,binding.ModSession,CanonicalCodec.Digest(binding with{Players=binding.Players.OrderBy(p=>p.Owner).ToArray()}),map.Hash,profiles.Hash,rules.Hash,next++,start,end,[new(x.Owner,x.Revision,x.GameTime),new(y.Owner,y.Revision,y.GameTime)],units,[],false,false,gap);lastTime=end;return CanonicalCodec.Normalize(input);
    }
    public void Invalidate(string role){sources.Remove(role);hashes.Remove(role);}
    public WorldState Export()=>new(next,lastTime,registry.Export(),sources.Select(p=>new OwnerSource(p.Key,p.Value)).ToArray(),new Dictionary<string,string>(deltas));
    public void Restore(WorldState s){next=s.Next;lastTime=s.Time;registry.Restore(s.Spawns);sources.Clear();hashes.Clear();deltas.Clear();if(s.DeltaHashes?.Count>256)throw new InvalidDataException("DeltaHistoryBudget");foreach(var item in s.DeltaHashes??new Dictionary<string,string>())deltas.Add(item.Key,item.Value);foreach(var source in s.Sources)Full(source.Role,source.Observation);}
}
public sealed record OwnerSource(string Role,Observation Observation);
public sealed record WorldState(long Next,double? Time,SpawnEntry[] Spawns,OwnerSource[] Sources,Dictionary<string,string>? DeltaHashes=null);
public sealed record StageDigests(string Input,string Territory,string Zoc,string Encirclement,string Service,string Projection);
public sealed record ObservationCheckpoint(string MapHash,string ProfilesHash,long NextInput,string? LastHash,Checkpoint023 Core);
public sealed class ObservationBridge
{
    readonly Simulation023 core;readonly string bindingHash;readonly MapDescriptor map;readonly ProfileSet profiles;long next;string? lastHash;long coreRevision;double time;
    public Frame023? Last=>core.Last;
    public ObservationBridge(MatchBinding binding,MapDescriptor map,ProfileSet profiles,string fingerprint)
    {
        bindingHash=CanonicalCodec.Digest(binding with{Players=binding.Players.OrderBy(p=>p.Owner).ToArray()});this.map=map;this.profiles=profiles;var terrain=map.Create(binding.Map,fingerprint);
        var catalog=new ProfileCatalog(profiles.Entries.Select(p=>new UnitTerritoryProfile(p.Definition,p.Capture?UnitRole.Line:UnitRole.Other,p.Capture,false,1,50,CanCreateLocalArea:p.Capture)));
        core=new(binding.ModSession,new(),new(){TerritoryScale=null},terrain,catalog,new Rules(),map.Scoring,SimulationLane.Observe);
    }
    public StageDigests Step(CanonicalInput input,int team)
    {
        var normalized=CanonicalCodec.Normalize(input);string hash=normalized.Hash;
        if(input.InputId==next-1){if(hash!=lastHash)throw new InvalidDataException("ConflictingCanonicalInput");return Digests(hash,team);}
        if(input.BindingHash!=bindingHash||input.Schema!=2||input.InputId!=next||input.MapHash!=map.Hash||input.ProfilesHash!=profiles.Hash)throw new InvalidDataException("CanonicalGapOrContext_ResyncRequired");
        if(input.Start!=time){core.ObservationGap(input.Start);coreRevision++;}
        core.Step(new(input.Session,"Zoc_023/1.2",input.RulesHash,coreRevision++,input.Start,input.End,normalized.Units,true,input.Services,[],[],input.SupplyComplete,input.VictoryComplete));
        time=input.End;next++;lastHash=hash;return Digests(hash,team);
    }
    StageDigests Digests(string input,int team){var f=core.Last!;return new(input,CanonicalCodec.Digest(core.Checkpoint().Territory.Cells),CanonicalCodec.Digest(f.Zoc.Select(z=>new{z.Unit.Key,z.Radius,z.Power})),CanonicalCodec.Digest(f.Encirclement),CanonicalCodec.Digest(f.Encirclement.Select(e=>new{e.Key,e.Relief,e.Tau})),CanonicalCodec.Digest(Project(team)));}
    public Public023 Project(int team,bool circles=false)=>core.Project(team,circles,false);
    public ObservationCheckpoint Export()=>new(map.Hash,profiles.Hash,next,lastHash,core.Checkpoint());
    public void Restore(ObservationCheckpoint s){if(s.MapHash!=map.Hash||s.ProfilesHash!=profiles.Hash)throw new InvalidDataException("CheckpointContext");core.Restore(s.Core);next=s.NextInput;lastHash=s.LastHash;time=s.Core.Time;coreRevision=s.Core.Revision+1;}
}
