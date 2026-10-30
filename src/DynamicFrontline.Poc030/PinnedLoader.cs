using System.Text.Json;
namespace DynamicFrontline.Poc030;
public static class PinnedLoader
{
 public static IReadOnlyDictionary<string,string> Files {get;}=Load();
 static Dictionary<string,string> Load(){using var stream=typeof(PinnedLoader).Assembly.GetManifestResourceStream("DynamicFrontline.Poc030.PinnedLoaderInventory.json")!;return JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;}
}
