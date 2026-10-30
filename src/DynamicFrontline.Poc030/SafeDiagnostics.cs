using System.Text.Json;
using System.Text.RegularExpressions;
namespace DynamicFrontline.Poc030;
// Export schema is constructive: no raw objects, unknown properties or free-form strings survive.
public static class SafeDiagnostics
{
 public static readonly string[] DigestFields=["Input","Territory","Zoc","Encirclement","Service","Projection"];
 static readonly string[] Metrics=["ReadMilliseconds","ReadBytes","HudLastMilliseconds","HudLastBytes","WorkerBeforeDiagnosticMilliseconds","WorkerBeforeDiagnosticBytes","SourceAgeMilliseconds","IncomingQueue","OutgoingQueue","LastWireExchangeMilliseconds","UnknownGeometry"];
 static readonly string[] Codes=["RelayPolicyWriteFailed","RelayPolicyReadFailed","RelayPolicyChanged_DirectForbidden","RelayRouteNotVerified_DirectForbidden","AuthenticatedSteam","SteamHandshake","LoadReadProbe","ObserveTwoClient","PrivateLobbyNotWitnessed","OnlyScenario5454584","ProtectedSession_ModDisabled","InteropNotLoaded","UnsupportedGameBuild","SteamFriendRequired","HandshakeMismatch","ResultLogOverflow_Stop","SteamPeerTimeout","ReadUnavailable","LivePartial","PeerStale","SupplyUnknown"];
 static readonly HashSet<string> Kinds=new(["Build","Consent","Lobby","Connect","Status","Roster","RosterAttestation","Match","ReadUnavailable","Resync","NativeBinding","ServiceBindingUnavailable","HudUnavailable","Reconnect","Stopped","Shutdown","SourceMetric","Start","RelayPolicy","DiagnosticLoss"]);
 public static bool Hash(string? value)=>value!=null&&Regex.IsMatch(value,"\\A[0-9A-Fa-f]{64}\\z",RegexOptions.CultureInvariant);
 public static string Kind(JsonElement r)=>r.TryGetProperty("Kind",out var k)||r.TryGetProperty("kind",out k)?k.GetString()??"":"";
 public static JsonElement? Sanitize(JsonElement row)
 {
  if(row.ValueKind!=JsonValueKind.Object)return null;
  var kind=Kind(row);var result=new Dictionary<string,object?>{{"Kind",kind}};
  if(kind=="Result"){
   if(!row.TryGetProperty("InputId",out var id)||!id.TryGetInt64(out var n)||n is <0 or >1000000000||!row.TryGetProperty("Digests",out var d)||d.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("IncompleteResult");
   var hashes=new Dictionary<string,string>();foreach(var f in DigestFields){if(!d.TryGetProperty(f,out var h)||h.ValueKind!=JsonValueKind.String||!Hash(h.GetString()))throw new InvalidDataException("InvalidResultDigest");hashes[f]=h.GetString()!.ToUpperInvariant();}
   result["InputId"]=n;result["Digests"]=hashes;
   if(row.TryGetProperty("SessionHash",out var context)){if(context.ValueKind!=JsonValueKind.String||!Hash(context.GetString()))throw new InvalidDataException("InvalidResultContext");result["SessionHash"]=context.GetString()!.ToUpperInvariant();}
   string source=row.TryGetProperty("ResultSource",out var s)?s.GetString()??"":"LegacyUnscoped";
   if(source is not("Coordinator" or "Independent" or "CoordinatorReceipt" or "LegacyUnscoped"))throw new InvalidDataException("InvalidResultSource");result["ResultSource"]=source;
  }else if(!Kinds.Contains(kind))return null;
  if(kind!="Result"&&row.TryGetProperty("detail",out var detail)&&detail.ValueKind==JsonValueKind.String)result["Codes"]=Codes.Where(c=>(detail.GetString()??"").Contains(c,StringComparison.Ordinal)).ToArray();
  foreach(var f in Metrics)if(row.TryGetProperty(f,out var v)&&v.ValueKind==JsonValueKind.Number&&v.TryGetDouble(out var x)&&double.IsFinite(x)&&x>=0&&x<1e12)result[f]=x;
  // A dropped/corrupt stream must never compare as a complete successful run.
  if(kind=="DiagnosticLoss")result["Count"]=1;
  return JsonSerializer.SerializeToElement(result);
 }
 public static void NoReparse(string path){for(string? p=Path.GetFullPath(path);!string.IsNullOrEmpty(p);p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("DiagnosticReparsePoint");}
}
