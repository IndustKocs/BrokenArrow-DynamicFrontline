using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Poc030;
public sealed record RuntimeView(string Status,SourceHealth? Local,SourceHealth? Peer,Public023? Projection,string InputHash,string[] Blockers,string PredictionLabel,DisplayGrid? Display=null);
public sealed record RuntimeState(ObservationCheckpoint Bridge,WorldState World);
// Single sequential worker. Only copied managed records cross this boundary, never ECS/Unity references.
public sealed class RuntimeObserver : IDisposable
{
    readonly bool visual;readonly ProbeConfig config;readonly IWorldChannel channel;MatchBinding? binding;readonly MapDescriptor? map;readonly ProfileSet? profiles;readonly string fingerprint;
    Observation? remoteObservation,lastSent;string bindingSentEpoch="";
    public event Action<object>? Diagnostic;
    readonly CancellationTokenSource stop=new();readonly Channel<(string Epoch,WorldMessage Message)> incoming=Channel.CreateBounded<(string,WorldMessage)>(32);
    readonly Stopwatch clock=Stopwatch.StartNew();readonly SourceHealthTracker source=new();Observation? latest;SourceHealth? peer;double peerReceived;CanonicalWorld? world;ObservationBridge? bridge;
    readonly object sourceGate=new();
    bool resyncRequired; string seenEpoch="";long published=-1;string? inputHash;readonly Dictionary<long,StageDigests> expected=new();
    readonly Dictionary<long,StageDigests> recentResults=new();
    readonly Stopwatch workerStep=new();long workerAllocated;
    public RuntimeView View {get;private set;}=new("WaitingForMatch",null,null,null,"",[],"Forecast only; native effects OFF");
    public RuntimeObserver(ProbeConfig config,IWorldChannel channel,string fingerprint,MatchBinding? binding=null,MapDescriptor? map=null,ProfileSet? profiles=null,bool visual=false)
    {this.visual=visual;this.config=config;this.channel=channel;this.fingerprint=fingerprint;this.binding=binding;this.map=map;this.profiles=profiles;channel.Received+=OnMessage;}
    void OnMessage(WorldMessage m){if(!incoming.Writer.TryWrite((channel.ConnectionEpoch,m))){source.Failure(SourceState.StoppedByBudget,"InboundQueueFull");View=View with{Status="OverBudget",Projection=null,Display=null};}}
    void Send<T>(string kind,T value){if(!channel.Send(kind,value))throw new InvalidDataException("OutboundQueueFull_Resync");}
    public void Submit(Observation o){lock(sourceGate){source.Success(o,clock.Elapsed.TotalSeconds);Interlocked.Exchange(ref latest,o);}}
    public void Fail(SourceState state,string reason){lock(sourceGate){source.Failure(state,reason);Interlocked.Exchange(ref latest,null);}channel.PublishFailure(state,reason);View=View with{Status=state.ToString(),Projection=null,Display=null};}
    public async Task Run()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                workerStep.Restart();workerAllocated=GC.GetAllocatedBytesForCurrentThread();
                Observation? own;SourceHealth health;lock(sourceGate){own=latest;health=source.At(clock.Elapsed.TotalSeconds);}channel.PublishHealth(health);
                if(channel.ConnectionEpoch!=seenEpoch){seenEpoch=channel.ConnectionEpoch;published=-1;lastSent=null;remoteObservation=null;peer=null;expected.Clear();resyncRequired=true;View=View with{Projection=null,Display=null,Status="ResyncPending"};world?.Invalidate(config.Role=="A"?"B":"A");}
                if(own!=null&&own.Revision!=published&&channel.Authenticated)
                {
                    if(config.Role=="B"||config.Replication=="TrustedDiagnosticReplica"){
                        if(lastSent!=null&&lastSent.Complete&&own.Complete&&own.Revision==lastSent.Revision+1&&own.Revision%20!=0){var previous=lastSent.Owned.ToDictionary(u=>u.NetworkId!.Value);var upserts=own.Owned.Where(u=>!previous.TryGetValue(u.NetworkId!.Value,out var p)||p!=u).ToArray();var current=own.Owned.Select(u=>u.NetworkId!.Value).ToHashSet();Send("OwnedSnapshotDelta",new OwnedDeltaFrame(new(lastSent.Revision,own with{Owned=[]},upserts,previous.Keys.Where(id=>!current.Contains(id)).ToArray()),health));}
                        else Send("OwnedSnapshotFull",new OwnedSourceFrame(own,health));lastSent=own;
                    }published=own.Revision;
                    TryBind();if(config.Mode=="ObserveTwoClient"&&binding!=null){EnsureCore();world!.Full(config.Role,own);}
                }
                while(incoming.Reader.TryRead(out var message))if(message.Epoch==seenEpoch)Handle(message.Message);
                if(config.Mode=="ObserveTwoClient"&&config.Role=="A"&&!resyncRequired&&world!=null&&peer!=null&&own!=null)
                {
                    var aged=peer with{AgeSeconds=peer.AgeSeconds+Math.Max(0,clock.Elapsed.TotalSeconds-peerReceived)};
                    if(own.Owned.Length+(remoteObservation?.Owned.Length??0)>config.MaximumCanonicalUnits)throw new InvalidDataException("UnitBudgetExceeded_CompleteWorldNotTruncated");var input=world.Build(health,aged);if(input!=null){var digest=bridge!.Step(input,0);inputHash=input.Hash;expected[input.InputId]=digest;recentResults[input.InputId]=digest;while(recentResults.Count>64)recentResults.Remove(recentResults.Keys.First());if(expected.Count>64)throw new InvalidDataException("UnackedResultsBudget");
                        if(config.Replication=="TrustedDiagnosticReplica")Send("CanonicalInput",input);else{if(visual)Send("TeamDisplayT18",new TeamDisplay(bridge.Project(TeamFor("B"),true),bridge.Display(TeamFor("B"))));else Send("TeamProjection",bridge.Project(TeamFor("B"),true));expected.Remove(input.InputId);}
                        LogResult(input,digest);
                        if(config.DetailedReplayConsent)Diagnostic?.Invoke(new{Kind="ReplayInput",Input=input});
                        View=new("CanonicalObserved",health,aged,bridge.Project(TeamFor("A"),true),input.Hash,["SupplyUnknown","VictoryInputsUnknown","StaticMapGeometry_DynamicChangesUnvalidated"],"Prediction of encirclement — game effects OFF",visual?bridge.Display(TeamFor("A")):null);
                    }
                    else View=View with{Status="WaitingForFreshCompletePair",Local=health,Peer=aged,Projection=health.State==SourceState.LiveComplete&&aged.State==SourceState.LiveComplete&&aged.AgeSeconds<=1?View.Projection:null,Display=health.State==SourceState.LiveComplete&&aged.State==SourceState.LiveComplete&&aged.AgeSeconds<=1?View.Display:null};
                }
                else View=View with{Local=health,Peer=peer,Status=config.Mode=="LoadReadProbe"?"LoadReadProbe / "+channel.State:View.Status};
                if(peer!=null&&peer.AgeSeconds+Math.Max(0,clock.Elapsed.TotalSeconds-peerReceived)>1.5){View=View with{Projection=null,Display=null,Status="PeerStale"};world?.Invalidate(config.Role=="A"?"B":"A");}
                if(health.State!=SourceState.LiveComplete){View=View with{Projection=null,Display=null,Status=health.State.ToString()};world?.Invalidate(config.Role);}
            }
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or JsonException)
            {resyncRequired=true;View=View with{Projection=null,Display=null,Status="PredictionUnavailable",Blockers=[e.Message]};channel.Send("ResyncRequest",new{Reason="LocalValidationFailure"});channel.Send("Error",new{Reason=e.GetType().Name});}
            try{await Task.Delay(50,stop.Token);}catch(OperationCanceledException){break;}
        }
    }
    int TeamFor(string role)=>binding!.Team(binding.Players.Single(p=>p.Owner==binding.Owner(role)).TeamUid);
    void LogResult(CanonicalInput input,StageDigests digest)=>Diagnostic?.Invoke(new{Kind="Result",input.InputId,input.Start,input.End,Digests=digest,WorkerBeforeDiagnosticMilliseconds=workerStep.Elapsed.TotalMilliseconds,WorkerBeforeDiagnosticBytes=GC.GetAllocatedBytesForCurrentThread()-workerAllocated,CoreStages=bridge?.Timings,SourceAgeMilliseconds=source.At(clock.Elapsed.TotalSeconds).AgeSeconds*1000,IncomingQueue=incoming.Reader.Count,OutgoingQueue=channel.PendingMessages,LastWireExchangeMilliseconds=channel.LastExchangeMilliseconds,UnknownGeometry=bridge?.Last?.Geometry.Count(g=>g.Enclosed==Evidence.Unknown)});
    void TryBind()
    {
        var own=Volatile.Read(ref latest);if(own==null)return;
        if(binding==null&&config.Role=="A"&&remoteObservation!=null){var other=remoteObservation;
            if(own.FightId<=0||own.Participants==null||own.Participants.Length!=2||own.Owner==other.Owner)return;
            var candidate=new MatchBinding(Guid.Parse(config.SessionId),own.FightId,own.Seed,own.Map,own.Participants,own.Owner,other.Owner,"Native FightId/Seed/Map/participant/owner equality from authenticated A+B observations");candidate.Check(own,"A");candidate.Check(other,"B");binding=candidate;
            Diagnostic?.Invoke(new{Kind="NativeBinding",BindingHash=CanonicalCodec.Digest(binding),binding.Map,Players=binding.Players.Length,RuntimeValidated=false});
        }
        if(binding!=null){binding.Check(own,config.Role);if(config.Role=="A"&&bindingSentEpoch!=seenEpoch){if(!channel.Send("SessionBinding",binding))throw new InvalidDataException("BindingQueue");bindingSentEpoch=seenEpoch;}}
    }
    void EnsureCore()
    {
        if(bridge!=null)return;if(binding==null||map==null||profiles==null)throw new InvalidDataException("VerifiedMapProfilesBindingRequired");
        bridge=new(binding,map,profiles,fingerprint);world=new(binding,profiles,map,new());
        if(config.DetailedReplayConsent)Diagnostic?.Invoke(new{Kind="ReplayContext",Binding=binding,Map=map,Profiles=profiles,Fingerprint=fingerprint});
    }
    void Handle(WorldMessage message)
    {
        string other=config.Role=="A"?"B":"A";
        switch(message.Kind)
        {
            case "Hello":case "Accepted":break;
            case "SessionBinding":
                if(config.Role!="B")throw new InvalidDataException("BindingAuthority");var proposed=message.Payload.Deserialize<MatchBinding>()!;
                if(proposed.ModSession!=Guid.Parse(config.SessionId))throw new InvalidDataException("BindingSession");proposed.Check(Volatile.Read(ref latest)??throw new InvalidDataException("NoLocalSourceForBinding"),"B");
                if(binding!=null&&CanonicalCodec.Digest(binding)!=CanonicalCodec.Digest(proposed))throw new InvalidDataException("BindingChanged_NewSessionRequired");binding=proposed;break;
            case "SourceHealth":peer=message.Payload.Deserialize<SourceHealth>()!;peerReceived=clock.Elapsed.TotalSeconds;if(peer.State!=SourceState.LiveComplete)View=View with{Projection=null,Display=null,Status="Peer "+peer.State};break;
            case "OwnedSnapshotFull":
                var received=message.Payload.Deserialize<OwnedSourceFrame>()!;var o=received.Observation;peer=received.Health;if(peer.ObservationRevision!=o.Revision)throw new InvalidDataException("SourceHealthRevisionMismatch");peerReceived=clock.Elapsed.TotalSeconds;
                remoteObservation=o;TryBind();if(config.Mode=="ObserveTwoClient"&&binding!=null){EnsureCore();world!.Full(other,o);var local=Volatile.Read(ref latest);if(local!=null)world.Full(config.Role,local);}break;
            case "OwnedSnapshotDelta":
                var delta=message.Payload.Deserialize<OwnedDeltaFrame>()!;if(remoteObservation==null||remoteObservation.Revision!=delta.Delta.BaseRevision)throw new InvalidDataException("DeltaSourceGap");
                var roster=remoteObservation.Owned.ToDictionary(u=>u.NetworkId!.Value);foreach(var idToRemove in delta.Delta.RemovedNetIds)roster.Remove(idToRemove);foreach(var upsert in delta.Delta.Upserts)roster[upsert.NetworkId??throw new InvalidDataException("DeltaIdentity")]=upsert;
                var full=delta.Delta.Header with{Owned=roster.Values.OrderBy(u=>u.NetworkId).ToArray()};
                Handle(new("OwnedSnapshotFull",JsonSerializer.SerializeToElement(new OwnedSourceFrame(full,delta.Health))));break;
            case "CanonicalInput":
                if(config.Role!="B"||config.Replication!="TrustedDiagnosticReplica"||!config.TrustedReplicaConsent)throw new InvalidDataException("UnapprovedFullReplica");EnsureCore();var input=message.Payload.Deserialize<CanonicalInput>()!;if(input.Units.Length>config.MaximumCanonicalUnits)throw new InvalidDataException("UnitBudgetExceeded");var result=bridge!.Step(input,0);inputHash=input.Hash;
                View=new("IndependentCanonicalResult",source.At(clock.Elapsed.TotalSeconds),peer,bridge.Last==null?null:bridge.Project(TeamFor("B"),true),input.Hash,["SupplyUnknown","VictoryInputsUnknown","StaticMapGeometry_DynamicChangesUnvalidated"],"Prediction only — game effects OFF");Send("InputAck",new{input.InputId,Hash=input.Hash});Send("ResultDigest",new{input.InputId,Digests=result});LogResult(input,result);if(config.DetailedReplayConsent)Diagnostic?.Invoke(new{Kind="ReplayInput",Input=input});break;
            case "ResultDigest":
                long id=message.Payload.GetProperty("InputId").GetInt64();var digests=message.Payload.GetProperty("Digests").Deserialize<StageDigests>()!;
                if(!recentResults.TryGetValue(id,out var own)||own!=digests)throw new InvalidDataException("ResultMismatch_StopAndResync");expected.Remove(id);break;
            case "InputAck":var ackId=message.Payload.GetProperty("InputId").GetInt64();if(!recentResults.TryGetValue(ackId,out var ack)||ack.Input!=message.Payload.GetProperty("Hash").GetString())throw new InvalidDataException("InputAckMismatch");break;
            case "TeamDisplayT18":
                if(!visual||config.Role!="B"||config.Replication!="TeamSafe")throw new InvalidDataException("DisplayMode");var display=message.Payload.Deserialize<TeamDisplay>()!;display.Display.Validate();if(binding==null||display.Display.Team!=TeamFor("B")||display.Projection.Own.Length>100||display.Projection.Circles.Length>100||display.Projection.Notices.Length>256||display.Projection.Circles.Any(c=>!double.IsFinite(c.Radius)||c.Radius<0||!double.IsFinite(c.Position.X)||!double.IsFinite(c.Position.Y)))throw new InvalidDataException("DisplayProjectionDomain");if(display.Display.Session!=Guid.Parse(config.SessionId)||display.Projection.Session!=display.Display.Session||display.Projection.Time!=display.Display.Time)throw new InvalidDataException("DisplayContext");View=View with{Projection=display.Projection,Display=display.Display,Status="TeamSafeProjection"};break;
            case "TeamProjection":if(config.Role!="B"||config.Replication!="TeamSafe")throw new InvalidDataException("ProjectionMode");View=View with{Projection=message.Payload.Deserialize<Public023>(),Status="TeamSafeProjection_NoIndependentHiddenWorldRecompute"};break;
            case "ResyncRequest":
                published=-1;lastSent=null;expected.Clear();resyncRequired=false;View=View with{Projection=null,Display=null,Status="ResyncPending"};bindingSentEpoch="";TryBind();if(config.Role=="A"&&bridge!=null&&bridge.NextInput>0&&config.Replication=="TrustedDiagnosticReplica"){
                    var checkpoint=bridge.Export();Send("Checkpoint",new RuntimeState(checkpoint,world!.Export()));bridge=new(binding!,map!,profiles!,fingerprint);bridge.Restore(checkpoint);if(config.DetailedReplayConsent){Diagnostic?.Invoke(new{Kind="ReplayContext",Binding=binding,Map=map,Profiles=profiles,Fingerprint=fingerprint});Diagnostic?.Invoke(new{Kind="ReplayCheckpoint",State=checkpoint});}
                }if(config.Role=="B")Send("ResyncRequest",new{Reason="CoordinatorCheckpointRequired"});Diagnostic?.Invoke(new{Kind="Resync",Epoch=seenEpoch});break;
            case "Checkpoint":
                if(config.Role!="B"||config.Replication!="TrustedDiagnosticReplica"||!config.TrustedReplicaConsent)throw new InvalidDataException("CheckpointConsent");var state=message.Payload.Deserialize<RuntimeState>()!;if(bridge!=null&&state.Bridge.NextInput<bridge.NextInput)throw new InvalidDataException("CoordinatorHistoryLost_NewSessionRequired");bridge=null;world=null;EnsureCore();bridge!.Restore(state.Bridge);world!.Restore(state.World);if(config.DetailedReplayConsent)Diagnostic?.Invoke(new{Kind="ReplayCheckpoint",State=state.Bridge});resyncRequired=false;View=View with{Status="Resynced",Projection=null,Display=null};break;
            case "SessionEnded":world?.Invalidate(other);View=View with{Projection=null,Display=null,Status="PeerNoSession"};break;
            case "LifecycleEvent":case "ServiceEvent":throw new InvalidDataException("NativeEventFeedNotValidated_NoPrediction");
            case "Error":resyncRequired=true;world?.Invalidate(other);View=View with{Projection=null,Display=null,Status="PeerUnavailable_Resync"};break;
            default:throw new InvalidDataException("UnknownMessageKind");
        }
    }
    public void Dispose(){stop.Cancel();channel.Received-=OnMessage;}
}
