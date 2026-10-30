using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DynamicFrontline.Poc030;
// Status-only dev channel. It has no player/world coordinates, identity claims, or game RPCs.
public sealed record Hello(string Protocol,string Session,string Role,string Compatibility,string Payload,string Rules,string Profile,string Map,string Abi,string Capabilities,string Connection="");
public sealed record StatusFrame(long Sequence,long Revision,long BindingEpoch,string Quality,int OwnedCount,string ObservationHash, SourceHealth? Health = null);
public sealed record WireMessage(Hello Hello,StatusFrame Status,string Mac);
public static class Protocol
{
    public const int MaxFrame=16384;
    static byte[] Body(Hello h,StatusFrame s)=>JsonSerializer.SerializeToUtf8Bytes(new {Hello=h,Status=s});
    public static WireMessage Sign(Hello h,StatusFrame s,byte[] secret)=>new(h,s,Convert.ToHexString(HMACSHA256.HashData(secret,Body(h,s))));
    public static void Verify(WireMessage m,Hello local,byte[] secret,long previous)
    {
        if(m.Hello==null||m.Status==null||m.Mac==null)throw new InvalidDataException("MalformedEnvelope"); var h=m.Hello;
        if(h.Connection!=local.Connection||h.Protocol!=local.Protocol||h.Session!=local.Session||h.Role==local.Role||h.Role is not ("A" or "B")||h.Compatibility!=local.Compatibility||h.Payload!=local.Payload||h.Rules!=local.Rules||h.Profile!=local.Profile||h.Map!=local.Map||h.Abi!=local.Abi||h.Capabilities!=local.Capabilities)throw new InvalidDataException("HandshakeMismatch");
        if(m.Status.Sequence!=previous+1 || m.Status.Revision<0 || m.Status.OwnedCount is <0 or >10000)throw new InvalidDataException("SequenceGapOrInvalidStatus_ResyncRequired");
        byte[] mac; try {mac=Convert.FromHexString(m.Mac);}catch(FormatException){throw new InvalidDataException("AuthenticationFailed");}
        if(!CryptographicOperations.FixedTimeEquals(mac,HMACSHA256.HashData(secret,Body(h,m.Status))))throw new InvalidDataException("AuthenticationFailed");
    }
    public static async Task Write(Stream stream,WireMessage message,CancellationToken ct)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(message); if(bytes.Length>MaxFrame)throw new InvalidDataException("Oversize");
        var header=new byte[4];BinaryPrimitives.WriteInt32BigEndian(header,bytes.Length);
        await stream.WriteAsync(header,ct);await stream.WriteAsync(bytes,ct);
    }
    public static async Task<WireMessage> Read(Stream stream,CancellationToken ct)
    {
        async Task Exact(byte[] bytes) {int n=0;while(n<bytes.Length){int read=await stream.ReadAsync(bytes.AsMemory(n),ct);if(read==0)throw new EndOfStreamException();n+=read;}}
        var header=new byte[4]; await Exact(header); int length=BinaryPrimitives.ReadInt32BigEndian(header);
        if(length is <1 or >MaxFrame)throw new InvalidDataException("Oversize");
        var body=new byte[length];await Exact(body);return JsonSerializer.Deserialize<WireMessage>(body)??throw new InvalidDataException("EmptyMessage");
    }
}
public sealed partial class DevChannel : IWorldChannel
{
    readonly CancellationTokenSource stop=new(); readonly Hello hello; readonly byte[] secret; readonly ProbeConfig config;
    readonly SourceHealthTracker health=new(); readonly System.Diagnostics.Stopwatch sourceClock=System.Diagnostics.Stopwatch.StartNew();
    StatusFrame latest=new(0,0,0,"WaitingForRealSession",0,"");
    public string State {get;private set;}="Disabled";
    public bool Authenticated => State=="AuthenticatedWorldTls";
    public StatusFrame? Peer {get;private set;}
    public DevChannel(ProbeConfig config,Hello hello,byte[] secret){if(secret.Length<32)throw new InvalidDataException("SecretTooShort");this.config=config;this.hello=hello;this.secret=secret;}
    public void Publish(Observation observation)
    {
        health.Success(observation,sourceClock.Elapsed.TotalSeconds);
        // Hash is opaque evidence of local sampling, NEVER asserted equal across independent frames.
        var digest=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(observation)));
        Interlocked.Exchange(ref latest,new(0,observation.Revision,observation.BindingEpoch,observation.Quality,observation.Owned.Length,digest,health.At(sourceClock.Elapsed.TotalSeconds)));
    }
    public void PublishFailure(SourceState state,string reason){health.Failure(state,reason);var old=Volatile.Read(ref latest);Interlocked.Exchange(ref latest,old with{Quality=state+":"+reason,Health=health.At(sourceClock.Elapsed.TotalSeconds)});}
    public async Task Run()
    {
        TcpListener? listener=null;
        try
        {
            var address=IPAddress.Parse(config.Host);
            var octets=address.GetAddressBytes(); bool privateAddress=IPAddress.IsLoopback(address)||(octets.Length==4&&(octets[0]==10||(octets[0]==172&&octets[1] is >=16 and <=31)||(octets[0]==192&&octets[1]==168)));
            if(!privateAddress||config.Port is <1024 or >65535)throw new InvalidDataException("ExplicitPrivateEndpointRequired");
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(60));
            State="Connecting";
            using TcpClient client=await Connect();
            async Task<TcpClient> Connect()
            {
                if(config.Role=="A"){listener=new TcpListener(address,config.Port);listener.Start(1);return await listener.AcceptTcpClientAsync(timeout.Token);}
                var c=new TcpClient();try{await c.ConnectAsync(address,config.Port,timeout.Token);return c;}catch{c.Dispose();throw;}
            }
            listener?.Stop();client.NoDelay=true;using var stream=client.GetStream();long seq=0,received=0;
            var ownNonce=RandomNumberGenerator.GetBytes(32);var peerNonce=new byte[32];
            using(var nonceTimeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token))
            {
                nonceTimeout.CancelAfter(TimeSpan.FromSeconds(5));await stream.WriteAsync(ownNonce,nonceTimeout.Token);
                int read=0;while(read<32){int n=await stream.ReadAsync(peerNonce.AsMemory(read),nonceTimeout.Token);if(n==0)throw new EndOfStreamException();read+=n;}
            }
            var nonceBytes=config.Role=="A"?ownNonce.Concat(peerNonce).ToArray():peerNonce.Concat(ownNonce).ToArray();
            var connectedHello=hello with {Connection=Convert.ToHexString(SHA256.HashData(nonceBytes))};
            while(!stop.IsCancellationRequested)
            {
                using var frameTimeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);frameTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                var frame=Volatile.Read(ref latest) with {Sequence=++seq,Health=health.At(sourceClock.Elapsed.TotalSeconds)};
                await Protocol.Write(stream,Protocol.Sign(connectedHello,frame,secret),frameTimeout.Token);
                var peer=await Protocol.Read(stream,frameTimeout.Token);Protocol.Verify(peer,connectedHello,secret,received);received=peer.Status.Sequence;Peer=peer.Status;State="AuthenticatedStatusOnly";
                await Task.Delay(250,stop.Token);
            }
        }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or JsonException or FormatException or ArgumentException)
        { State="Disconnected:"+ex.GetType().Name;Peer=null; }
        finally {listener?.Stop();}
    }
    public void Dispose(){stop.Cancel();} // no migration, no replay of effects, no unbounded outbound queue
}
