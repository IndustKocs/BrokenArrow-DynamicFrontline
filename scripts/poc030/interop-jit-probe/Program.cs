using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Runtime.InteropServices;
// Compile only: never invoke a constructor, Unity API or native game binary.
var wrappers=Path.GetFullPath(args[0]); var loader=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=> {
 foreach(var dir in new[]{wrappers,loader}) {var file=Path.Combine(dir,name.Name+".dll"); if(File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);}
 return null;
};
try {
 var core=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(wrappers,"UnityEngine.CoreModule.dll"));
 foreach(var entry in new[]{("UnityEngine.Rect",new[]{"m_XMin","m_YMin","m_Width","m_Height"}),("UnityEngine.Color",new[]{"r","g","b","a"}),("UnityEngine.Vector3",new[]{"x","y","z"})}) {
  var type=core.GetType(entry.Item1,true)!;
  if(Marshal.SizeOf(type)!=entry.Item2.Length*4)throw new InvalidDataException("Invalid native value size: "+entry.Item1);
  for(int i=0;i<entry.Item2.Length;i++)if(Marshal.OffsetOf(type,entry.Item2[i]).ToInt32()!=i*4)throw new InvalidDataException("Invalid native field offset: "+entry.Item1+"."+entry.Item2[i]);
  Console.WriteLine("PASS: native size and field offsets "+entry.Item1);
 }

 foreach(var entry in new[]{("UnityEngine.CoreModule.dll","UnityEngine.Debug"),("UnityEngine.CoreModule.dll","UnityEngine.Object"),("UnityEngine.IMGUIModule.dll","UnityEngine.GUI"),("UnityEngine.InputLegacyModule.dll","UnityEngine.Input")}) {
 var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(wrappers,entry.Item1));
 var method=assembly.GetType(entry.Item2,true)!.TypeInitializer!;
 RuntimeHelpers.PrepareMethod(method.MethodHandle);
 if(entry.Item2=="UnityEngine.GUI") {
  foreach(var name in new[]{"color","backgroundColor","contentColor","enabled","depth"}) {
   var property=assembly.GetType(entry.Item2,true)!.GetProperty(name)??throw new MissingMemberException("GUI."+name);
   if(property.GetMethod is null || property.SetMethod is null)throw new MissingMethodException("GUI."+name);
   RuntimeHelpers.PrepareMethod(property.GetMethod.MethodHandle);RuntimeHelpers.PrepareMethod(property.SetMethod.MethodHandle);
  }
 }
 Console.WriteLine($"PASS: {entry.Item2} .cctor JIT compilation; not invoked; no game launch.");
 }
} catch(Exception e) {Console.Error.WriteLine(e.ToString()); Environment.ExitCode=1;}

