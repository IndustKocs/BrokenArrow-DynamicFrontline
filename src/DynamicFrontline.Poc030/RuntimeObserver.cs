using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Poc030;
public sealed record RuntimeView(string Status,SourceHealth? Local,SourceHealth? Peer,Public023? Projection,string InputHash,string[] Blockers,string PredictionLabel);
public sealed record RuntimeState(ObservationCheckpoint Bridge,WorldState World);
// Single sequential worker. Only copied managed records cross this boundary, never ECS/Unity references.
public sealed class RuntimeObserver : IDisposable
{
    readonly ProbeConfig config;readonly DevChannel channel;readonly MatchBinding? binding;readonly MapDescriptor? map;readonly ProfileSet? profiles;readonly string fingerprint;
    readonly CancellationTokenSource stop=new();readonly Channel<WorldMessage> incoming=Channel.CreateBounded<WorldMessage>(32);
    readonly Stopwatch clock=Stopwatch.StartNew();readonly SourceHealthTracker source=new();Observation? latest;SourceHealth? peer;double peerReceived;CanonicalWorld? world;ObservationBridge? bridge;
    bool resyncRequired; string seenEpoch="";long published=-1;string? inputHash;readonly Dictionary<long,StageDigests> expected=new();
    public RuntimeView View {get;private set;}=new("WaitingForMatch",null,null,null,"",[],"Forecast only; native effects OFF");
    public RuntimeObserver(ProbeConfig config,DevChannel channel,string fingerprint,MatchBinding? binding=null,MapDescriptor? map=null,ProfileSet? profiles=null)
    {this.config=config;this.channel=channel;this.fingerprint=fingerprint;this.binding=binding;this.map=map;this.profiles=profiles;channel.Received+=OnMessage;}
    void OnMessage(WorldMessage m){if(!incoming.Writer.TryWrite(m)){source.Failure(SourceState.StoppedByBudget,"InboundQueueFull");View=View with{Status="OverBudget",Projection=null};}}
    public void Submit(Observation o){source.Success(o,clock.Elapsed.TotalSeconds);Interlocked.Exchange(ref latest,o);}
    public void Fail(SourceState state,string reason){source.Failure(state,reason);channel.PublishFailure(state,reason);Interlocked.Exchange(ref latest,null);View=View with{Status=state.ToString(),Projection=null};}
    public async Task Run()
    {
        while(!stop.IsCancellationRequested)
        {
            try
            {
                var own=Volatile.Read(ref latest);var health=source.At(clock.Elapsed.TotalSeconds);channel.PublishHealth(health);
                if(channel.ConnectionEpoch!=seenEpoch){seenEpoch=channel.ConnectionEpoch;published=-1;peer=null;expected.Clear();resyncRequired=true;View=View with{Projection=null,Status="ResyncPending"};world?.Invalidate(config.Role=="A"?"B":"A");}
                if(own!=null&&own.Revision!=published&&channel.State=="AuthenticatedWorldTls")
                {
                    if(config.Role=="B"||config.Replication=="TrustedDiagnosticReplica")channel.Send("OwnedSnapshotFull",new OwnedSourceFrame(own,health));published=own.Revision;
                    if(config.Mode=="ObserveTwoClient"){EnsureCore();world!.Full(config.Role,own);}
                }
                while(incoming.Reader.TryRead(out var message))Handle(message);
                if(config.Mode=="ObserveTwoClient"&&config.Role=="A"&&!resyncRequired&&world!=null&&peer!=null&&own!=null)
                {
                    var aged=peer with{AgeSeconds=peer.AgeSeconds+Math.Max(0,clock.Elapsed.TotalSeconds-peerReceived)};
                    var input=world.Build(health,aged);if(input!=null){var digest=bridge!.Step(input,0);inputHash=input.Hash;expected[input.InputId]=digest;if(expected.Count>64)throw new InvalidDataException("UnackedResultsBudget");
                        if(config.Replication=="TrustedDiagnosticReplica")channel.Send("CanonicalInput",input);else{channel.Send("TeamProjection",bridge.Project(TeamFor("B"),true));expected.Remove(input.InputId);}
                        View=new("CanonicalObserved",health,aged,bridge.Project(TeamFor("A"),true),input.Hash,["SupplyUnknown","VictoryInputsUnknown"],"Prediction of encirclement — game effects OFF");
                    }
                    else View=View with{Status="WaitingForFreshCompletePair",Local=health,Peer=aged,Projection=health.State==SourceState.LiveComplete&&aged.State==SourceState.LiveComplete&&aged.AgeSeconds<=1?View.Projection:null};
                }
                else View=View with{Local=health,Peer=peer,Status=config.Mode=="LoadReadProbe"?"LoadReadProbe / "+channel.State:View.Status};
                if(peer!=null&&peer.AgeSeconds+Math.Max(0,clock.Elapsed.TotalSeconds-peerReceived)>1.5){View=View with{Projection=null,Status="PeerStale"};world?.Invalidate(config.Role=="A"?"B":"A");}
                if(health.State!=SourceState.LiveComplete){View=View with{Projection=null,Status=health.State.ToString()};world?.Invalidate(config.Role);}
            }
            catch(Exception e)when(e is InvalidDataException or ArgumentException or InvalidOperationException or JsonException)
            {resyncRequired=true;View=View with{Projection=null,Status="PredictionUnavailable",Blockers=[e.Message]};channel.Send("ResyncRequest",new{Reason="LocalValidationFailure"});channel.Send("Error",new{Reason=e.GetType().Name});}
            try{await Task.Delay(50,stop.Token);}catch(OperationCanceledException){break;}
        }
    }
    int TeamFor(string role)=>binding!.Team(binding.Players.Single(p=>p.Owner==binding.Owner(role)).TeamUid);
    void EnsureCore()
    {
        if(bridge!=null)return;if(binding==null||map==null||profiles==null)throw new InvalidDataException("VerifiedMapProfilesBindingRequired");
        bridge=new(binding,map,profiles,fingerprint);world=new(binding,profiles,map,new());
    }
    void Handle(WorldMessage message)
    {
        string other=config.Role=="A"?"B":"A";
        switch(message.Kind)
        {
            case "Hello":case "Accepted":break;
            case "SourceHealth":peer=message.Payload.Deserialize<SourceHealth>()!;peerReceived=clock.Elapsed.TotalSeconds;if(peer.State!=SourceState.LiveComplete)View=View with{Projection=null,Status="Peer "+peer.State};break;
            case "OwnedSnapshotFull":
                var received=message.Payload.Deserialize<OwnedSourceFrame>()!;var o=received.Observation;peer=received.Health;if(peer.ObservationRevision!=o.Revision)throw new InvalidDataException("SourceHealthRevisionMismatch");peerReceived=clock.Elapsed.TotalSeconds;
                if(config.Mode=="ObserveTwoClient"){EnsureCore();world!.Full(other,o);}break;
            case "OwnedSnapshotDelta":EnsureCore();world!.Delta(other,message.Payload.Deserialize<OwnedDelta>()!);break;
            case "CanonicalInput":
                if(config.Role!="B"||config.Replication!="TrustedDiagnosticReplica"||!config.TrustedReplicaConsent)throw new InvalidDataException("UnapprovedFullReplica");EnsureCore();var input=message.Payload.Deserialize<CanonicalInput>()!;var result=bridge!.Step(input,0);inputHash=input.Hash;
                View=new("IndependentCanonicalResult",source.At(clock.Elapsed.TotalSeconds),peer,bridge.Project(TeamFor("B"),true),input.Hash,["SupplyUnknown","VictoryInputsUnknown"],"Prediction only — game effects OFF");channel.Send("InputAck",new{input.InputId,Hash=input.Hash});channel.Send("ResultDigest",new{input.InputId,Digests=result});break;
            case "ResultDigest":
                long id=message.Payload.GetProperty("InputId").GetInt64();var digests=message.Payload.GetProperty("Digests").Deserialize<StageDigests>()!;
                if(!expected.Remove(id,out var own)||own!=digests)throw new InvalidDataException("ResultMismatch_StopAndResync");break;
            case "InputAck":var ackId=message.Payload.GetProperty("InputId").GetInt64();if(!expected.TryGetValue(ackId,out var ack)||ack.Input!=message.Payload.GetProperty("Hash").GetString())throw new InvalidDataException("InputAckMismatch");break;
            case "TeamProjection":if(config.Role!="B"||config.Replication!="TeamSafe")throw new InvalidDataException("ProjectionMode");View=View with{Projection=message.Payload.Deserialize<Public023>(),Status="TeamSafeProjection_NoIndependentHiddenWorldRecompute"};break;
            case "ResyncRequest":
                published=-1;expected.Clear();resyncRequired=false;if(config.Role=="A"&&bridge?.Last!=null&&config.Replication=="TrustedDiagnosticReplica")channel.Send("Checkpoint",new RuntimeState(bridge.Export(),world!.Export()));break;
            case "Checkpoint":
                if(config.Role!="B"||config.Replication!="TrustedDiagnosticReplica"||!config.TrustedReplicaConsent)throw new InvalidDataException("CheckpointConsent");var state=message.Payload.Deserialize<RuntimeState>()!;bridge=null;world=null;EnsureCore();bridge!.Restore(state.Bridge);world!.Restore(state.World);resyncRequired=false;View=View with{Status="Resynced",Projection=null};break;
            case "SessionEnded":world?.Invalidate(other);View=View with{Projection=null,Status="PeerNoSession"};break;
            case "LifecycleEvent":case "ServiceEvent":throw new InvalidDataException("NativeEventFeedNotValidated_NoPrediction");
            case "Error":resyncRequired=true;world?.Invalidate(other);View=View with{Projection=null,Status="PeerUnavailable_Resync"};break;
            default:throw new InvalidDataException("UnknownMessageKind");
        }
    }
    public void Dispose(){stop.Cancel();channel.Received-=OnMessage;}
}
