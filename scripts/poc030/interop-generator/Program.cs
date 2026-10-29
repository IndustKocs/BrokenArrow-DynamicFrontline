using AsmResolver.DotNet;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using System.Runtime.Loader;
if(args[0]=="verify"){BindingInspector.Run(args[1],args[2]);return;}
AssemblyLoadContext.Default.Resolving+=(_,name)=>{var path=Path.Combine(args[0],name.Name+".dll");return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;};
Run(args);
static void Run(string[] args)
{
    if(Directory.Exists(args[3])&&Directory.EnumerateFileSystemEntries(args[3]).Any())throw new InvalidOperationException("Fresh output directory required");
    var source=Directory.GetFiles(args[1],"*.dll").OrderBy(x=>x,StringComparer.Ordinal).Select(x=>AssemblyDefinition.FromFile(x)).ToList();
    var options=new GeneratorOptions{Source=source,GameAssemblyPath=args[2],OutputDir=args[3],NoXrefCache=true,Parallel=false};
    using var generator=Il2CppInteropGenerator.Create(options).AddInteropAssemblyGenerator();generator.Run();
    Console.WriteLine("Generated wrapper files: "+Directory.GetFiles(args[3],"*.dll").Length);
}
