using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DynamicFrontline.Poc030;
namespace DynamicFrontline.Steam;
public interface ISteamWire { void Send(byte[] packet); bool TryReceive(out byte[] packet); }
public sealed record SteamOffer(Hello Hello,string PublicKey,string Nonce);
public sealed record SteamPacket(string Kind,JsonElement Body);
// Pumped exclusively by the game thread; managed observer communicates through bounded queues.
public sealed class SteamWorldChannel : IWorldChannel
{
 readonly object queueGate=new();readonly ISteamWire wire; readonly Hello hello; ECDiffieHellman ecdh=ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
 readonly Stopwatch clock=Stopwatch.StartNew(); readonly ConcurrentQueue<WorldMessage> queue=new(); readonly Queue<byte[]> fragments=new();
 SteamOffer offer; SteamOffer? peerOffer; byte[]? key; long sent,received; int queued; double nextHello,nextHealth,lastReceive,handshakeStart;
 byte[]? assembling; WireChunk? first; int index,offset; double assemblyStarted; bool disposed; SourceHealth? health;
 public event Action<WorldMessage>? Received;
 public string State {get;private set;}="SteamHandshake"; public string ConnectionEpoch {get;private set;}="";
 public bool Authenticated=>State=="AuthenticatedSteam";
 public int PendingMessages=>Volatile.Read(ref queued)+fragments.Count; public double? LastExchangeMilliseconds {get;private set;}
 public SteamWorldChannel(ISteamWire wire,Hello hello){this.wire=wire;this.hello=hello;offer=new(hello,Convert.ToBase64String(ecdh.ExportSubjectPublicKeyInfo()),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));}
 void Packet<T>(string kind,T value)=>wire.Send(CanonicalCodec.Bytes(new SteamPacket(kind,JsonSerializer.SerializeToElement(value))));
 public bool Send<T>(string kind,T payload){lock(queueGate){if(disposed||queued>=32)return false;queue.Enqueue(new(kind,JsonSerializer.SerializeToElement(payload)));queued++;return true;}}
 bool Dequeue(out WorldMessage m){lock(queueGate){if(!queue.TryDequeue(out m!))return false;queued--;return true;}}
 public void PublishHealth(SourceHealth h)=>Volatile.Write(ref health,h);
 public void PublishFailure(SourceState s,string reason){Send("SessionEnded",new{State=s.ToString(),Reason=reason});}
 public void Pump()
 {
  if(disposed)return;
  try{
   if(!Authenticated&&clock.Elapsed.TotalSeconds-handshakeStart>20)throw new InvalidDataException("HandshakeTimeout");
   if(!Authenticated&&clock.Elapsed.TotalSeconds>=nextHello){nextHello=clock.Elapsed.TotalSeconds+.5;Packet("Offer",offer);}
   for(int n=0;n<32&&wire.TryReceive(out var bytes);n++){
    if(bytes.Length>WorldProtocol.MaxChunk+4096)throw new InvalidDataException("SteamPacketBudget");
    var p=JsonSerializer.Deserialize<SteamPacket>(bytes)??throw new InvalidDataException("NullSteamPacket");
    switch(p.Kind){
     case "Offer":AcceptOffer(p.Body.Deserialize<SteamOffer>()!);break;
     case "Ready":
      if(key==null||p.Body.GetString()!=ReadyMac())throw new InvalidDataException("SteamKeyConfirmation");
      if(!Authenticated){State="AuthenticatedSteam";lastReceive=clock.Elapsed.TotalSeconds;Send("ResyncRequest",new{Reason="SteamFreshConnection"});}break;
     case "Chunk":if(!Authenticated)throw new InvalidDataException("WorldBeforeAuthentication");AcceptChunk(p.Body.Deserialize<WireChunk>()!);lastReceive=clock.Elapsed.TotalSeconds;break;
     default:throw new InvalidDataException("UnknownSteamPacket");
    }
   }
   if(assembling!=null&&clock.Elapsed.TotalSeconds-assemblyStarted>10)throw new InvalidDataException("SteamChunkTimeout");
   if(!Authenticated)return;
   if(clock.Elapsed.TotalSeconds-lastReceive>10)throw new InvalidDataException("SteamPeerTimeout");
   if(clock.Elapsed.TotalSeconds>=nextHealth){nextHealth=clock.Elapsed.TotalSeconds+.25;var h=Volatile.Read(ref health);if(h!=null)Send("SourceHealth",h);else Send("Accepted",new{ConnectionEpoch});}
   if(fragments.Count==0&&Dequeue(out var m)){var data=CanonicalCodec.Bytes(WorldProtocol.Sign(hello with{Connection=ConnectionEpoch},++sent,m,key!));
    if(data.Length>WorldProtocol.MaxMessage)throw new InvalidDataException("SteamWorldBudget");int count=(data.Length+WorldProtocol.ChunkSize-1)/WorldProtocol.ChunkSize;string hash=Convert.ToHexString(SHA256.HashData(data)),id=Guid.NewGuid().ToString("N");
    for(int i=0;i<count;i++)fragments.Enqueue(CanonicalCodec.Bytes(new SteamPacket("Chunk",JsonSerializer.SerializeToElement(new WireChunk(id,i,count,data.Length,hash,data.Skip(i*WorldProtocol.ChunkSize).Take(WorldProtocol.ChunkSize).ToArray())))));
   }
   for(int i=0;i<8&&fragments.TryDequeue(out var f);i++)wire.Send(f);
  }catch(Exception e) when(e is InvalidDataException or JsonException or CryptographicException or FormatException or ArgumentException or IOException or InvalidOperationException){State="SteamDisconnected:"+e.Message;ConnectionEpoch="";disposed=true;Received?.Invoke(new("Error",JsonSerializer.SerializeToElement(new{Reason=State})));ClearSecret();}
 }
 void AcceptOffer(SteamOffer peer)
 {
  if(peer==null||peer.Hello==null||peer.PublicKey==null||peer.Nonce==null||peer.PublicKey.Length>512||peer.Nonce.Length!=64)throw new InvalidDataException("SteamOfferBudget");
  var ph=peer.Hello;Protocol.Verify(Protocol.Sign(ph,new(1,0,0,"",0,""),new byte[32]),hello,new byte[32],0);
  if(peerOffer!=null){if(peer==peerOffer){Packet("Ready",ReadyMac());return;}Restart();Packet("Offer",offer);}
  using var other=ECDiffieHellman.Create();other.ImportSubjectPublicKeyInfo(Convert.FromBase64String(peer.PublicKey),out int read);if(read!=Convert.FromBase64String(peer.PublicKey).Length)throw new InvalidDataException("ECDHKeyTrailingData");
  var transcript=CanonicalCodec.Bytes(hello.Role=="A"?new[]{offer,peer}:new[]{peer,offer});var shared=ecdh.DeriveKeyMaterial(other.PublicKey);key=HMACSHA256.HashData(shared,transcript);CryptographicOperations.ZeroMemory(shared);ConnectionEpoch=Convert.ToHexString(SHA256.HashData(transcript));peerOffer=peer;Packet("Ready",ReadyMac());
 }
 string ReadyMac()=>Convert.ToHexString(HMACSHA256.HashData(key!,System.Text.Encoding.UTF8.GetBytes("DF-T18-ready/"+ConnectionEpoch)));
 void AcceptChunk(WireChunk c)
 {
  if(c==null||c.Total is <1 or >WorldProtocol.MaxMessage||c.Count!=(c.Total+WorldProtocol.ChunkSize-1)/WorldProtocol.ChunkSize||c.Index!=index||c.Bytes.Length!=Math.Min(WorldProtocol.ChunkSize,c.Total-index*WorldProtocol.ChunkSize))throw new InvalidDataException("SteamChunkOrder");
  if(first==null){first=c;assembling=new byte[c.Total];assemblyStarted=clock.Elapsed.TotalSeconds;}
  if(c.Id!=first.Id||c.Hash!=first.Hash||c.Total!=first.Total||c.Count!=first.Count)throw new InvalidDataException("SteamChunkConflict");
  c.Bytes.CopyTo(assembling!,offset);offset+=c.Bytes.Length;index++;
  if(index!=c.Count)return;
  if(offset!=c.Total||Convert.ToHexString(SHA256.HashData(assembling!))!=c.Hash)throw new InvalidDataException("SteamChunkHash");
  var e=JsonSerializer.Deserialize<WorldEnvelope>(assembling!)??throw new InvalidDataException("NullWorld");WorldProtocol.Verify(e,hello with{Connection=ConnectionEpoch},received,key!);received=e.Sequence;
  LastExchangeMilliseconds=(clock.Elapsed.TotalSeconds-lastReceive)*1000;first=null;assembling=null;index=offset=0;Received?.Invoke(e.Message);
 }
 public void Restart(){ClearSecret();ecdh.Dispose();ecdh=ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);offer=new(hello,Convert.ToBase64String(ecdh.ExportSubjectPublicKeyInfo()),Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));peerOffer=null;disposed=false;State="SteamHandshake";ConnectionEpoch="";sent=received=0;index=offset=0;handshakeStart=clock.Elapsed.TotalSeconds;nextHello=nextHealth=0;}
 void ClearSecret(){if(key!=null)CryptographicOperations.ZeroMemory(key);key=null;assembling=null;first=null;fragments.Clear();lock(queueGate){while(queue.TryDequeue(out _)){}queued=0;}}
 public void Dispose(){disposed=true;State="Stopped";ConnectionEpoch="";ClearSecret();ecdh.Dispose();}
}
