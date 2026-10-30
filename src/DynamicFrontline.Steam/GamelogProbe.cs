using System.Text;
using DynamicFrontline.Poc030;
namespace DynamicFrontline.Steam;
// Read-only receipt, not a second writer. No raw native log contents leave this object.
public sealed class GamelogProbe
{
 readonly string marker="[DF] {\"Kind\":\"GamelogProbe\",\"Nonce\":\""+Guid.NewGuid().ToString("N")+"\"}";
 string? path;long offset;double deadline;string carry="";
 public string State {get;private set;}="GamelogMarkerNotSent";
 public void Begin(string file,double now,Action<string> unityLog){
  SafeDiagnostics.NoReparse(file);using var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  offset=f.Length;path=Path.GetFullPath(file);deadline=now+15;unityLog(marker);State="GamelogMarkerPending";
 }
 public void Poll(string file,double now){
  if(State!="GamelogMarkerPending")return;
  if(Path.GetFullPath(file)!=path){State="GamelogPathChanged_Unverified";return;}
  SafeDiagnostics.NoReparse(file);using var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
  if(f.Length<offset){State="GamelogTruncated_Unverified";return;}
  f.Position=offset;var b=new byte[65536];int n=f.Read(b,0,b.Length);offset+=n;
  var text=carry+Encoding.UTF8.GetString(b,0,n);
  if(text.Contains(marker,StringComparison.Ordinal)){State="GamelogMarkerConfirmed";return;}
  carry=text.Length>marker.Length?text[^marker.Length..]:text;
  if(now>=deadline)State="GamelogMarkerTimeout_Unverified";
 }
}
