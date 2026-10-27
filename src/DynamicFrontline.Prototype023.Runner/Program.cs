using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicFrontline.Frontline.Rules023;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length < 2) { Console.WriteLine("replay <scenario.json> <output-directory> | benchmark <output-directory> [updates=10000] [repeats=3]"); return 2; }
if(args[0]=="benchmark") { Benchmarks.Run(args[1],args.Length>2?int.Parse(args[2]):10000,args.Length>3?int.Parse(args[3]):3); return 0; }
if(args[0]!="replay"||args.Length<3) return 2;
var scenario = JsonSerializer.Deserialize<Scenario>(File.ReadAllText(args[1]),Formats.Json)!;
Replay.Run(scenario,args[2]); return 0;
public static class Formats { public static readonly JsonSerializerOptions Json = new(){WriteIndented=true,Converters={new JsonStringEnumConverter()}}; }
