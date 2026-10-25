using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DynamicFrontline.Core.Diagnostics;
public sealed record ResearchProfile(string Id,string Source,string AssetId,string Profile,string ResearchVersion,string SourceTableSha256,string? AssetSha256,bool RuntimeConfirmed,string[] FormulaIds,Dictionary<string,float> Values);
public sealed class ResearchProfileCatalog
{
    private readonly Dictionary<string,ResearchProfile> profiles=new(StringComparer.Ordinal);
    public ResearchProfileCatalog(string json,string? expectedSha256=null)
    {
        if(expectedSha256!=null&&!Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).Equals(expectedSha256,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("Profile document hash mismatch");
        var input=JsonSerializer.Deserialize<ResearchProfile[]>(json)??throw new ArgumentException("Missing profiles");
        foreach(var p in input)
        {
            if(string.IsNullOrWhiteSpace(p.Id)||string.IsNullOrWhiteSpace(p.Source)||string.IsNullOrWhiteSpace(p.AssetId)||string.IsNullOrWhiteSpace(p.Profile)||string.IsNullOrWhiteSpace(p.ResearchVersion)||p.RuntimeConfirmed||p.FormulaIds==null||p.FormulaIds.Length==0||p.Values==null)throw new ArgumentException("Incomplete or runtime-labelled research example");
            ValidateHash(p.SourceTableSha256);if(p.AssetSha256!=null)ValidateHash(p.AssetSha256);
            foreach(float v in p.Values.Values)VerifiedCombat.FormulaDomain.Finite(v,nameof(json));
            if(!profiles.TryAdd(p.Id,p with{FormulaIds=p.FormulaIds.ToArray(),Values=new(p.Values)}))throw new ArgumentException("Duplicate profile");
        }
    }
    private static void ValidateHash(string hash){if(hash==null||hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new ArgumentException("Invalid hash");}
    public ResearchProfile Get(string id)
    {if(!profiles.TryGetValue(id,out var p))throw new ArgumentException("Unknown research profile");return p with{FormulaIds=p.FormulaIds.ToArray(),Values=new(p.Values)};}
    public ReadOnlyCollection<string> Ids=>profiles.Keys.OrderBy(x=>x,StringComparer.Ordinal).ToList().AsReadOnly();
}
