using System.Text.Json;
using DynamicFrontline.Core.Diagnostics;
try
{
    if(args.Length==0){Console.WriteLine("Usage: dotnet DynamicFrontline.Combat.Explorer.dll requests.json [research-profiles.json]\nInput: JSON array of {Formula, Profile:null|string, Parameters:{...}}. Results remain separate for comparison. See config/core021/explorer-examples.json.");return;}
    string profilePath=args.Length>1?args[1]:"config/core021/research-profiles.json";
    var profiles=new ResearchProfileCatalog(File.ReadAllText(profilePath));
    var requests=JsonSerializer.Deserialize<ExplorerRequest[]>(File.ReadAllText(args[0]))??throw new ArgumentException("Missing requests");
    var results=requests.Select(r=>FormulaExplorer.Evaluate(r,profiles)).ToArray();
    Console.WriteLine(JsonSerializer.Serialize(new{Mode="Offline research examples; not active match configuration",Results=results},FormulaExplorer.JsonOptions));
}
catch(Exception e)when(e is ArgumentException or ArithmeticException or JsonException or IOException or KeyNotFoundException or InvalidOperationException)
{Console.Error.WriteLine(JsonSerializer.Serialize(new{Error=e.Message}));Environment.ExitCode=2;}
