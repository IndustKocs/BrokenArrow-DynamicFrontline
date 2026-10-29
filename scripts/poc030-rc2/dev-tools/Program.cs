using System.Text.Json;
using DynamicFrontline.Poc030;
var json=new JsonSerializerOptions{WriteIndented=true};
try {
 if(args.Length<2)throw new ArgumentException("plan <manifest> | sandbox-install <target> <manifest> | sandbox-verify/recover/remove <target>");
 if(args[0]=="plan") {
  var package=JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(args[1]))!;
  if(package.Any(f=>DevInstall.Hash(f.Source)!=f.Sha256))throw new InvalidDataException("PackageHashMismatch");
  Console.WriteLine(JsonSerializer.Serialize(new{Operation="PlanOnly",Files=package,GameInstallEnabled=false,Blocker="Verified loader cache transaction is required before game installation"},json));return 0;
 }
 if(!args[0].StartsWith("sandbox-",StringComparison.Ordinal))throw new InvalidDataException("Game installation unavailable in this development checkpoint");
 using var installer=new DevInstall(args[1],true,true);
 var result=args[0] switch {
  "sandbox-install"=>installer.Apply(JsonSerializer.Deserialize<InstallFile[]>(File.ReadAllText(args[2]))!),
  "sandbox-verify"=>installer.Verify(),"sandbox-recover"=>installer.Recover(),"sandbox-remove"=>installer.Remove(),
  _=>throw new ArgumentException("Unknown command")
 };
 Console.WriteLine(JsonSerializer.Serialize(result,json));return result.Conflicts.Length==0?0:2;
}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message);return 1;}
