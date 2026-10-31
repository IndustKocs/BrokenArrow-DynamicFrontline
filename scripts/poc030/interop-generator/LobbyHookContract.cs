using AsmResolver.DotNet;
static class LobbyHookContract
{
 public static void Verify(string directory,bool rejectOldGetter=false)
 {
  const string type="BrokenArrow.Client.Ecs.GNetwork.Services.Lobby.NetworkLobbyService";
  var all=new List<(string Type,string Name,string? Rva)>();
  foreach(var file in Directory.GetFiles(directory,"*.dll"))foreach(var t in ModuleDefinition.FromFile(file).GetAllTypes())foreach(var m in t.Methods){
   var a=m.CustomAttributes.FirstOrDefault(a=>a.Constructor?.DeclaringType?.Name=="AddressAttribute");
   var rva=a?.Signature?.NamedArguments.FirstOrDefault(n=>n.MemberName=="RVA")?.Argument.Element?.ToString();
   all.Add((t.FullName,m.Name?.ToString()??"",rva));
  }
  var counts=all.Where(x=>x.Rva!=null).GroupBy(x=>x.Rva!).ToDictionary(g=>g.Key,g=>g.Count());
  foreach(var name in rejectOldGetter?new[]{"get_State"}:new[]{"Connect","Dispose"}){
   var targets=all.Where(x=>x.Type==type&&x.Name==name).ToArray();
   if(targets.Length!=1||targets[0].Rva is not string rva||rva=="0x0"||!counts.TryGetValue(rva,out int n)||n!=1)
    throw new InvalidDataException("UnsafeSharedOrMissingLobbyHook:"+name);
   Console.WriteLine("PASS: unique native lobby hook "+name+" "+rva);
  }
 }
}
