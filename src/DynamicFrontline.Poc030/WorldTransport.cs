using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Threading.Channels;
namespace DynamicFrontline.Poc030;
public sealed record OwnedSourceFrame(Observation Observation,SourceHealth Health);
public sealed record OwnedDeltaFrame(OwnedDelta Delta,SourceHealth Health);
public sealed record WorldMessage(string Kind,JsonElement Payload);
public sealed record WorldEnvelope(Hello Hello,long Sequence,WorldMessage Message,string Digest,string Mac);
public sealed record WireChunk(string Id,int Index,int Count,int Total,string Hash,byte[] Bytes);
public static class WorldProtocol
{
    public const int ChunkSize=48*1024,MaxMessage=16*1024*1024,MaxChunk=100*1024;
    public static WorldEnvelope Sign(Hello hello,long sequence,WorldMessage message,byte[] key)
    {var hash=CanonicalCodec.Digest(message);return new(hello,sequence,message,hash,Convert.ToHexString(HMACSHA256.HashData(key,CanonicalCodec.Bytes(new{hello,sequence,hash}))));}
    public static void Verify(WorldEnvelope e,Hello local,long previous,byte[] key)
    {
        Protocol.Verify(Protocol.Sign(e.Hello,new(e.Sequence,0,0,"",0,""),key),local,key,previous);
        if(e.Digest!=CanonicalCodec.Digest(e.Message)||!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(e.Mac),HMACSHA256.HashData(key,CanonicalCodec.Bytes(new{hello=e.Hello,sequence=e.Sequence,hash=e.Digest}))))throw new InvalidDataException("WorldAuthentication");
    }
    public static async Task Write(Stream stream,WorldEnvelope e,CancellationToken ct)
    {
        var bytes=CanonicalCodec.Bytes(e);if(bytes.Length>MaxMessage)throw new InvalidDataException("WorldSizeLimit");var hash=Convert.ToHexString(SHA256.HashData(bytes));int count=(bytes.Length+ChunkSize-1)/ChunkSize;var id=Guid.NewGuid().ToString("N");
        for(int i=0;i<count;i++){var chunk=CanonicalCodec.Bytes(new WireChunk(id,i,count,bytes.Length,hash,bytes.Skip(i*ChunkSize).Take(ChunkSize).ToArray()));var header=new byte[4];BinaryPrimitives.WriteInt32BigEndian(header,chunk.Length);await stream.WriteAsync(header,ct);await stream.WriteAsync(chunk,ct);}
    }
    public static async Task<WorldEnvelope> Read(Stream stream,CancellationToken ct)
    {
        async Task Exact(byte[] b){int n=0;while(n<b.Length){int k=await stream.ReadAsync(b.AsMemory(n),ct);if(k==0)throw new EndOfStreamException();n+=k;}}
        byte[]? result=null;WireChunk? first=null;int offset=0;
        for(int index=0;index<(first?.Count??1);index++)
        {
            var header=new byte[4];await Exact(header);int size=BinaryPrimitives.ReadInt32BigEndian(header);if(size is <1 or >MaxChunk)throw new InvalidDataException("ChunkSizeLimit");var bytes=new byte[size];await Exact(bytes);var c=JsonSerializer.Deserialize<WireChunk>(bytes)??throw new InvalidDataException("NullChunk");
            if(c.Index!=index||c.Total is <1 or >MaxMessage||c.Count!=(c.Total+ChunkSize-1)/ChunkSize||c.Bytes.Length!=Math.Min(ChunkSize,c.Total-index*ChunkSize))throw new InvalidDataException("InvalidChunk");
            first??=c;if(first.Id!=c.Id||first.Hash!=c.Hash||first.Total!=c.Total||first.Count!=c.Count)throw new InvalidDataException("ChunkConflict");result??=new byte[c.Total];c.Bytes.CopyTo(result,offset);offset+=c.Bytes.Length;
        }
        if(result==null||offset!=result.Length||Convert.ToHexString(SHA256.HashData(result))!=first!.Hash)throw new InvalidDataException("IncompleteWorldFrame");return JsonSerializer.Deserialize<WorldEnvelope>(result)??throw new InvalidDataException("NullWorld");
    }
}
public sealed partial class DevChannel
{
    sealed record HealthStamp(SourceHealth Health,double At);
    HealthStamp? worldHealth;
    public void PublishHealth(SourceHealth h)=>Interlocked.Exchange(ref worldHealth,new(h,sourceClock.Elapsed.TotalSeconds));
    SourceHealth CurrentWorldHealth(){var h=Volatile.Read(ref worldHealth);return h==null?health.At(sourceClock.Elapsed.TotalSeconds):h.Health with{AgeSeconds=h.Health.AgeSeconds+Math.Max(0,sourceClock.Elapsed.TotalSeconds-h.At)};}
    readonly Channel<WorldMessage> outbound=Channel.CreateBounded<WorldMessage>(new BoundedChannelOptions(32){FullMode=BoundedChannelFullMode.Wait});
    public event Action<WorldMessage>? Received;
    public string ConnectionEpoch {get;private set;}="";
    public bool Send<T>(string kind,T payload)
    {
        if(kind=="CanonicalInput" && (config.Replication!="TrustedDiagnosticReplica"||!config.TrustedReplicaConsent))throw new InvalidDataException("FullWorldReplicaConsentRequired");
        if(!outbound.Writer.TryWrite(new(kind,JsonSerializer.SerializeToElement(payload)))){PublishFailure(SourceState.StoppedByBudget,"OutboundQueueFull_Resync");return false;}return true;
    }
    public string? LastTransportError {get;private set;}
    public int PendingMessages=>outbound.Reader.Count;
    public double? LastExchangeMilliseconds {get;private set;}
    TcpClient? liveClient;
    public void DisconnectForDiagnosticResync()=>Volatile.Read(ref liveClient)?.Close();
    public async Task RunWorld()
    {
        int attempts=0;while(!stop.IsCancellationRequested)
        {
            TcpListener? listener=null;
            try
            {
                var address=IPAddress.Parse(config.Host);var b=address.GetAddressBytes();if(!(IPAddress.IsLoopback(address)||b.Length==4&&(b[0]==10||b[0]==172&&b[1] is >=16 and <=31||b[0]==192&&b[1]==168))||config.Port is <1024 or >65535)throw new InvalidDataException("ExplicitPrivateEndpointRequired");
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));State="ConnectingTls";
                using var client=new TcpClient();TcpClient? accepted=null;
                if(config.Role=="A"){listener=new(address,config.Port);listener.Start(1);accepted=await listener.AcceptTcpClientAsync(timeout.Token);}else await client.ConnectAsync(address,config.Port,timeout.Token);
                using var active=accepted??client;Volatile.Write(ref liveClient,active);listener?.Stop();active.NoDelay=true;
                using var tls=new SslStream(active.GetStream(),false,(_,certificate,_,_)=>config.Role=="A"||certificate!=null&&Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()))==config.CertificatePin);
                if(config.Role=="A"){using var certificate=new X509Certificate2(config.CertificateFile,string.Empty,X509KeyStorageFlags.DefaultKeySet);await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,ClientCertificateRequired=false},timeout.Token);}
                else await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost="dynamic-frontline-private",EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13},timeout.Token);
                var nonce=RandomNumberGenerator.GetBytes(32);var remote=new byte[32];await tls.WriteAsync(nonce,timeout.Token);int offset=0;while(offset<32){int n=await tls.ReadAsync(remote.AsMemory(offset),timeout.Token);if(n==0)throw new EndOfStreamException();offset+=n;}
                ConnectionEpoch=Convert.ToHexString(SHA256.HashData(config.Role=="A"?nonce.Concat(remote).ToArray():remote.Concat(nonce).ToArray()));
                var h=hello with{Connection=ConnectionEpoch};long sent=0,received=0;attempts=0;
                // Drop queued data from a dead connection; full state is requested before new predictions.
                while(outbound.Reader.TryRead(out _)){}Send("Hello",new{config.Mode,config.Replication,config.TrustedReplicaConsent});
                bool handshaken=false;
                while(!stop.IsCancellationRequested)
                {
                    using var frame=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);frame.CancelAfter(TimeSpan.FromSeconds(10));
                    var message=outbound.Reader.TryRead(out var queued)?queued:new WorldMessage("SourceHealth",JsonSerializer.SerializeToElement(CurrentWorldHealth()));
                    var exchange=System.Diagnostics.Stopwatch.StartNew();var send=WorldProtocol.Write(tls,WorldProtocol.Sign(h,++sent,message,secret),frame.Token);var receive=WorldProtocol.Read(tls,frame.Token);await Task.WhenAll(send,receive);var peer=await receive;WorldProtocol.Verify(peer,h,received,secret);received=peer.Sequence;LastExchangeMilliseconds=exchange.Elapsed.TotalMilliseconds;
                    if(!handshaken){if(peer.Message.Kind!="Hello")throw new InvalidDataException("HelloRequired");var p=peer.Message.Payload;if(p.GetProperty("Mode").GetString()!=config.Mode||p.GetProperty("Replication").GetString()!=config.Replication||p.GetProperty("TrustedReplicaConsent").GetBoolean()!=config.TrustedReplicaConsent)throw new InvalidDataException("ReplicationConsentMismatch");handshaken=true;Send("Accepted",new{ConnectionEpoch});Send("ResyncRequest",new{Reason="FreshConnection"});}
                    State="AuthenticatedWorldTls";Received?.Invoke(peer.Message);await Task.Delay(10,stop.Token);
                }
            }
            catch(Exception ex)when(ex is IOException or SocketException or AuthenticationException or OperationCanceledException or InvalidDataException or JsonException or FormatException or ArgumentException or CryptographicException or ObjectDisposedException)
            {State="Disconnected:"+ex.GetType().Name;LastTransportError=ex.ToString();ConnectionEpoch="";Received?.Invoke(new("Error",JsonSerializer.SerializeToElement(new{Reason=State})));}
            finally{Volatile.Write(ref liveClient,null);listener?.Stop();}
            if(stop.IsCancellationRequested)break;if(++attempts>30){State="ReconnectBudgetExhausted";break;}try{await Task.Delay(Math.Min(5000,250*attempts),stop.Token);}catch(OperationCanceledException){break;}
        }
    }
}






