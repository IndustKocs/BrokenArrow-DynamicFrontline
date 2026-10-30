using System.Collections.Concurrent;
using System.Text.Json;
namespace DynamicFrontline.Steam;
public sealed class DfEvents
{
 readonly ConcurrentQueue<(string Kind,string Detail)> pending=new();readonly Queue<string> buffer=new();readonly Dictionary<string,double> last=new();int count;double window;int emitted;
 public long Dropped {get;private set;} public string SinkState {get;private set;}="FallbackOnly";
 public void Add(string kind,string detail){if(Interlocked.Increment(ref count)>128){Interlocked.Decrement(ref count);Interlocked.Increment(ref dropped);return;}pending.Enqueue((kind.Length>48?kind[..48]:kind,detail.Length>320?detail[..320]:detail));}
 long dropped;
 public void Flush(double now,Action<string>? gameLog,Action<string> backup)
 {
  if(now-window>=1){window=now;emitted=0;}
  for(int n=0;n<32&&pending.TryDequeue(out var e);n++){
   Interlocked.Decrement(ref count);if(emitted>=4||(last.TryGetValue(e.Kind,out var t)&&now-t<2)){dropped++;continue;}
   if(last.Count>=128)last.Remove(last.Keys.First());last[e.Kind]=now;emitted++;
   string line="[DF] "+JsonSerializer.Serialize(new{v=1,utc=DateTime.UtcNow,kind=e.Kind,detail=e.Detail});buffer.Enqueue(line);while(buffer.Count>256)buffer.Dequeue();backup(line);
   try{if(gameLog!=null){gameLog(line);SinkState="GameLoggerAccepted_UnverifiedFile";}}catch{SinkState="GameLoggerUnavailable_Fallback";}
  }Dropped=Interlocked.Read(ref dropped);
 }
 public string[] Snapshot()=>buffer.ToArray();
}
