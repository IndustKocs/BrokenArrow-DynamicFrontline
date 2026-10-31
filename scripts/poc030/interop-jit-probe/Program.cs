using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
// Compile only: never invoke a constructor, Unity API or native game binary.
var wrappers=Path.GetFullPath(args[0]); var loader=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(_,name)=> {
 foreach(var dir in new[]{wrappers,loader}) {var file=Path.Combine(dir,name.Name+".dll"); if(File.Exists(file)) return AssemblyLoadContext.Default.LoadFromAssemblyPath(file);}
 return null;
};
try {
 foreach(var entry in new[]{("UnityEngine.CoreModule.dll","UnityEngine.Debug"),("UnityEngine.CoreModule.dll","UnityEngine.Object"),("UnityEngine.IMGUIModule.dll","UnityEngine.GUI"),("UnityEngine.InputLegacyModule.dll","UnityEngine.Input")}) {
 var assembly=AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(wrappers,entry.Item1));
 var method=assembly.GetType(entry.Item2,true)!.TypeInitializer!;
 RuntimeHelpers.PrepareMethod(method.MethodHandle);
 Console.WriteLine($"PASS: {entry.Item2} .cctor JIT compilation; not invoked; no game launch.");
 }
} catch(Exception e) {Console.Error.WriteLine(e.ToString()); Environment.ExitCode=1;}

