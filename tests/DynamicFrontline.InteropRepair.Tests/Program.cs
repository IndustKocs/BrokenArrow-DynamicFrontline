using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using System.Reflection;
using System.Runtime.Loader;
var plugin=ModuleDefinition.FromFile(args[1]);
var game=plugin.Assembly!.CustomAttributes.Single(a=>a.Constructor?.DeclaringType?.Name=="MelonGameAttribute").Signature!.FixedArguments;
if(game[0].Element?.ToString()!="SteelBalalaikaStudio" || game[1].Element?.ToString()!="BrokenArrow")throw new Exception("Wrong MelonGame identity");
Console.WriteLine("PASS: built plugin MelonGame matches observed runtime identity.");
var temp=Path.Combine(Path.GetFullPath(args[0]),Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
var source=ModuleDefinition.FromFile(Assembly.GetExecutingAssembly().Location);
foreach(var method in source.GetAllTypes().Single(t=>t.Name=="Fixture").Methods) {
 if(method.CilMethodBody is not {} body)continue;
 foreach(var instruction in body.Instructions) if(instruction.Operand is MethodSpecification spec && spec.Method?.Name=="RenderTypeName") {
 var t=spec.Signature!.TypeArguments[0];
 // Shared MethodSpec rows deliberately exercise multiple references to the same token.
 if(t is PointerTypeSignature) continue;
 spec.Signature.TypeArguments[0]=t.FullName switch {
 "System.Int16" => source.CorLibTypeFactory.Void.MakePointerType(),
 "System.Int64" => t.MakePointerType().MakePointerType(),
 _ => t.MakePointerType()};
 }
}
var file=Path.Combine(temp,"fixture.dll");source.Write(file);
bool rejected=false;try{PointerNameRepair.Run(temp,false);}catch(InvalidDataException){rejected=true;}
if(!rejected)throw new Exception("Invalid fixture accepted");
if(PointerNameRepair.Run(temp,true)!=5)throw new Exception("Not every shared-token call was repaired");
if(PointerNameRepair.Run(temp,true)!=0)throw new Exception("Repair not idempotent");
var context=new AssemblyLoadContext("fixture",true);
var asm=context.LoadFromAssemblyPath(file);
var result=(string)asm.GetType("Fixture")!.GetMethod("Names")!.Invoke(null,null)!;
const string expected="System.Byte*|System.Byte*&|System.Int64**|System.Void*|System.Int32*";
if(result!=expected)throw new Exception("Wrong rendered names: "+result);
Console.WriteLine("PASS: rejection, all shared-token sites, idempotence, byte*, byte*&, long**, void*, generic T*; executed only synthetic managed fixture.");
public static class Fixture {
 public static string Names()=>string.Join("|",Il2CppInterop.Runtime.IL2CPP.RenderTypeName<byte>(false),Il2CppInterop.Runtime.IL2CPP.RenderTypeName<byte>(true),Il2CppInterop.Runtime.IL2CPP.RenderTypeName<long>(false),Il2CppInterop.Runtime.IL2CPP.RenderTypeName<short>(false),Generic<int>());
 public static string Generic<T>()=>Il2CppInterop.Runtime.IL2CPP.RenderTypeName<T>(false);
}
namespace Il2CppInterop.Runtime {public static class IL2CPP {public static string RenderTypeName<T>(bool byRef)=>typeof(T).FullName+(byRef?"&":"");}}

