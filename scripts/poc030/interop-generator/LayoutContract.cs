using AsmResolver.DotNet;
using System.Globalization;

static class LayoutContract
{
 static int? Offset(FieldDefinition field){var a=field.CustomAttributes.FirstOrDefault(a=>a.Constructor?.DeclaringType?.Name=="FieldOffsetAttribute");var text=a?.Signature?.NamedArguments.FirstOrDefault(n=>n.MemberName=="Offset")?.Argument.Element?.ToString();return text!=null&&text.StartsWith("0x",StringComparison.Ordinal)&&int.TryParse(text[2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture,out var n)?n:null;}
 public static void Source(string directory)
 {
  var module=ModuleDefinition.FromFile(Path.Combine(directory,"UnityEngine.CoreModule.dll"));
  foreach(var name in new[]{"UnityEngine.Rect","UnityEngine.Color","UnityEngine.Vector3"}) {
   var t=module.GetAllTypes().Single(t=>t.FullName==name);var fields=t.Fields.Where(f=>!f.IsStatic).ToArray();
   for(int i=0;i<fields.Length;i++)if(Offset(fields[i])!=i*4)throw new InvalidDataException("Missing/invalid injected layout: "+fields[i].FullName+"; attributeinjector required");
   if(!t.Methods.Any(m=>m.CustomAttributes.Any(a=>a.Constructor?.DeclaringType?.Name=="TokenAttribute")))throw new InvalidDataException("Missing injected method tokens: "+name);
  }
 }
 public static void Output(string source,string output)
 {
  int checkedFields=0;
  foreach(var file in Directory.GetFiles(source,"*.dll")) {
   string dest=Path.Combine(output,Path.GetFileName(file));if(!File.Exists(dest))dest=Path.Combine(output,"Il2Cpp"+Path.GetFileName(file));if(!File.Exists(dest))continue;
   var original=ModuleDefinition.FromFile(file);var generated=ModuleDefinition.FromFile(dest);var types=generated.GetAllTypes().Where(t=>t.BaseType?.FullName=="System.ValueType").ToDictionary(t=>t.FullName);
   foreach(var t in original.GetAllTypes().Where(t=>t.BaseType?.FullName=="System.ValueType")) {
    if(!types.TryGetValue(t.FullName,out var g)&&!types.TryGetValue("Il2Cpp"+t.FullName,out g))continue;
    if(g.BaseType?.FullName!="System.ValueType")continue;
    foreach(var f in t.Fields.Where(f=>!f.IsStatic)) {
     var field=g.Fields.SingleOrDefault(x=>!x.IsStatic&&x.Name==f.Name);if(field==null)continue;
     var expected=Offset(f);if(expected==null||field.FieldOffset!=expected)throw new InvalidDataException("Generated layout mismatch: "+f.FullName);
     checkedFields++;
    }
   }
  }
  if(checkedFields<11)throw new InvalidDataException("Insufficient layout coverage");
  Console.WriteLine($"PASS: {checkedFields} generated value fields match injected native offsets.");
 }
}

