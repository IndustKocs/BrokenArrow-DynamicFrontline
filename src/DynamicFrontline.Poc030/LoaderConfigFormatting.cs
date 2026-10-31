using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace DynamicFrontline.Poc030;
public static class LoaderConfigFormatting
{
 public const string Relative="MelonLoader/Dependencies/Il2CppAssemblyGenerator/Config.cfg";
 // Only recognize the exact generated schema. Match its ORIGINAL receipt hash,
 // never the incoming package or a guessed configuration.
 public static bool Matches(string text,string receiptHash)
 {
  if(text.Length>128*1024)return false;
  var lines=text.Replace("\r\n","\n").Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).ToArray();
  var keys=new[]{"GameAssemblyHash","DeobfuscationRegex","UnityVersion","DumperVersion","DumperSCRSVersion"};
  if(lines.Length!=7||lines[0]!="[Il2CppAssemblyGenerator]")return false;
  var canonical=new List<string>{lines[0]};
  for(int i=0;i<keys.Length;i++) {
   var match=Regex.Match(lines[i+1],"\\A"+keys[i]+"\\s*=\\s*\"([A-Za-z0-9_.-]*)\"\\z",RegexOptions.CultureInvariant);
   if(!match.Success)return false;
   canonical.Add(keys[i]+" = \""+match.Groups[1].Value+"\"");
  }
  var array=Regex.Match(lines[6],"\\AOldFiles\\s*=\\s*(\\[.*\\])\\z",RegexOptions.CultureInvariant);if(!array.Success)return false;
  try {
   using var doc=JsonDocument.Parse(array.Groups[1].Value,new JsonDocumentOptions{AllowTrailingCommas=true});
   var names=doc.RootElement.EnumerateArray().Select(x=>x.GetString()!).ToArray();
   if(names.Length>1000||names.Any(x=>x==null||!Regex.IsMatch(x,"\\A[A-Za-z0-9_.-]+\\z"))||names.Distinct(StringComparer.Ordinal).Count()!=names.Length)return false;
   canonical.Add("OldFiles = "+JsonSerializer.Serialize(names));
   return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",canonical))))==receiptHash;
  }catch(JsonException){return false;}catch(InvalidOperationException){return false;}
 }
}
