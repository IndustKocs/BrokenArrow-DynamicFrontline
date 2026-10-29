using System.Text.Json;
using DynamicFrontline.Poc030;
var json=new JsonSerializerOptions{WriteIndented=true};
try {
 if(args.Length>=2&&args[0]=="metrics"){Console.WriteLine(JsonSerializer.Serialize(DiagnosticTools.Metrics(args[1]),json));return 0;}
 if(args.Length>=3&&args[0]=="network-probe"){var probe=await ConnectionProbe.Run(JsonSerializer.Deserialize<ProbeConfig>(File.ReadAllText(args[1]))!);string report=JsonSerializer.Serialize(probe,json);File.WriteAllText(args[2],report);Console.WriteLine(report);return JsonSerializer.SerializeToElement(probe).GetProperty("Passed").GetBoolean()?0:2;}
 if(args.Length>=2&&args[0] is "compare" or "replay" or "collect") {object diagnostic=args[0] switch{"compare"=>DiagnosticTools.Compare(args[1],args[2]),"replay"=>DiagnosticTools.Replay(args[1]),_=>DiagnosticTools.Collect(args[1],args[2],args.Contains("--include-sensitive-replay"))};var text=JsonSerializer.Serialize(diagnostic,json);Console.WriteLine(text);using var parsed=JsonDocument.Parse(text);return parsed.RootElement.TryGetProperty("Passed",out var passed)&&!passed.GetBoolean()?2:0;}
 if(args.Length<2)throw new ArgumentException("plan <target> <manifest> <plan-output> | [sandbox-]install <target> <plan> | [sandbox-]verify/recover/remove <target>. Real mutations require --explicit-install-consent or --explicit-removal-consent.");
 if(args[0]=="plan") {var plan=InstallPlanning.Plan(args[1],JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(args[2]))!);File.WriteAllText(args[3],JsonSerializer.Serialize(plan,json));Console.WriteLine(JsonSerializer.Serialize(new{Operation="PlanOnly",Target=Path.GetFullPath(args[1]),Files=plan.Length,WritesToTarget=0,Plan=Path.GetFullPath(args[3])},json));return 0;}
 bool sandbox=args[0].StartsWith("sandbox-",StringComparison.Ordinal);var command=sandbox?args[0][8..]:args[0];
 bool consent=sandbox||args.Contains(command=="remove"?"--explicit-removal-consent":"--explicit-install-consent");
 InstallFile[]? files=command=="install"?JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(args[2])):null;
 if(!sandbox&&files!=null){if(!consent)throw new InvalidDataException("Separate explicit installation consent required");InstallPlanning.VerifyGame(args[1],files);}
 using var installer=new DevInstall(args[1],sandbox,consent);
 var result=command switch {"install"=>installer.Apply(files!),"verify"=>installer.Verify(),"recover"=>installer.Recover(),"remove"=>installer.Remove(),_=>throw new ArgumentException("Unknown command")};
 Console.WriteLine(JsonSerializer.Serialize(result,json));return result.Conflicts.Length==0?0:2;
}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
