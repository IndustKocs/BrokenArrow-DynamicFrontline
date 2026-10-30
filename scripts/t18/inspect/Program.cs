using AsmResolver.DotNet;
using System.Text.Json;
var found=new List<object>();
foreach(var file in Directory.GetFiles(args[0],"*.dll")) {
var m=ModuleDefinition.FromFile(file);
foreach(var t in m.GetAllTypes().Where(t=> System.Text.RegularExpressions.Regex.IsMatch(t.FullName,args[2],System.Text.RegularExpressions.RegexOptions.IgnoreCase)))
found.Add(new { Assembly=Path.GetFileName(file), Type=t.FullName, Methods=t.Methods.Select(x=>x.FullName).ToArray(),Fields=t.Fields.Select(x=>x.FullName).ToArray(),Properties=t.Properties.Select(x=>x.FullName).ToArray()});
}
File.WriteAllText(args[1],JsonSerializer.Serialize(found,new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(found.Count);
