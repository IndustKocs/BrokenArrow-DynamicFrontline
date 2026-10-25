using System.Text.Json;
using System.Text.Json.Serialization;
namespace DynamicFrontline.Frontline;

public sealed record FrontlineConfiguration(Rules Rules,UnitTerritoryProfile[] Profiles)
{
    public static readonly JsonSerializerOptions JsonOptions=new(){WriteIndented=true,PropertyNameCaseInsensitive=false,
        Converters={new JsonStringEnumConverter()}};
    public static FrontlineConfiguration Load(string json)
    {
        var result=JsonSerializer.Deserialize<FrontlineConfiguration>(json,JsonOptions)??throw new ArgumentException("Missing configuration");
        if(result.Rules==null||result.Profiles==null)throw new ArgumentException("Missing rules or profiles");
        result.Rules.Validate();_ = new ProfileCatalog(result.Profiles);return result;
    }
}
