using AsmResolver.DotNet;
using Il2CppInterop.Generator;
using Il2CppInterop.Generator.Runners;
using System.Runtime.Loader;
if(args[0]=="verify-lobby-hooks"){try{LobbyHookContract.Verify(args[1],args.Length>2);}catch(InvalidDataException e){Console.Error.WriteLine(e.Message);Environment.ExitCode=1;}return;}
if(args[0]=="verify-layout"){LayoutContract.Source(args[1]);LayoutContract.Output(args[1],args[2]);return;}
if(args[0]=="repair-pointer-names"){PointerNameRepair.Run(args[1],true);return;}
if(args[0]=="verify-pointer-names"){PointerNameRepair.Run(args[1],false);return;}
string loader=args[0]=="verify"?args[3]:args[0];
AssemblyLoadContext.Default.Resolving+=(_,name)=>{var path=Path.Combine(loader,name.Name+".dll");return File.Exists(path)?AssemblyLoadContext.Default.LoadFromAssemblyPath(path):null;};
if(args[0]=="verify"){BindingInspector.Run(args[1],args[2]);return;}
Run(args);
static void Run(string[] args)
{
    if(Directory.Exists(args[3])&&Directory.EnumerateFileSystemEntries(args[3]).Any())throw new InvalidOperationException("Fresh output directory required");
    LayoutContract.Source(args[1]);
    LobbyHookContract.Verify(args[1]);
    var source=Directory.GetFiles(args[1],"*.dll").OrderBy(x=>x,StringComparer.Ordinal).Select(x=>AssemblyDefinition.FromFile(x)).ToList();
    if(args.Length!=5||!Directory.Exists(args[4]))throw new InvalidDataException("Exact Unity base libraries directory required");
    var options=new GeneratorOptions{Source=source,GameAssemblyPath=args[2],OutputDir=args[3],UnityBaseLibsDir=args[4],NoXrefCache=false,Parallel=false,Il2CppPrefixMode=GeneratorOptions.PrefixMode.OptOut};
    using var generator=Il2CppInteropGenerator.Create(options).AddInteropAssemblyGenerator();generator.Run();
    PointerNameRepair.Run(args[3],true);
    LayoutContract.Output(args[1],args[3]);
    Console.WriteLine("Generated wrapper files: "+Directory.GetFiles(args[3],"*.dll").Length);
}
