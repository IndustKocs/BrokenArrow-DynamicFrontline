using System.Reflection;
using DynamicFrontline.Poc030;
namespace DynamicFrontline.Steam.MelonLoader;
// Uses the existing Unity logger -> FileLog subscription. Never calls Init/WriteMessage or replaces handlers.
public sealed class UnityGamelog
{
 readonly int thread=Environment.CurrentManagedThreadId;readonly Action<string> log;readonly GamelogProbe probe=new();double next;string prior="";
 public string State=>probe.State;
 public UnityGamelog(){
  var debug=LiveReader.Resolve("UnityEngine.Debug");var method=debug.GetMethods().Single(m=>m.Name=="Log"&&m.GetParameters().Length==1);
  var argument=method.GetParameters()[0].ParameterType;
  var runtime=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Il2CppInterop.Runtime.IL2CPP")).First(t=>t!=null)!;
  var convert=runtime.GetMethod("ManagedStringToIl2Cpp",[typeof(string)])??throw new MissingMethodException("ManagedStringToIl2Cpp");
  log=line=>{if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("UnityLogMainThreadRequired");var pointer=convert.Invoke(null,[line]);method.Invoke(null,[Activator.CreateInstance(argument,[pointer!])]);};
 }
 public void Write(string line)=>log(line);
 static object? Field(Type t,string name){const BindingFlags f=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;return t.GetProperty(name,f)?.GetValue(null)??t.GetField(name,f)?.GetValue(null);}
 public void Tick(double now,Action<string> report){
  if(now<next)return;next=now+1;
  try{var type=LiveReader.Resolve("BrokenArrow.Shared.Ecs.FileLog");
   if(Field(type,"_isInitialized") is not true||Field(type,"_fileMaxSizeReached") is not false)return;
   var path=Field(type,"_logFilePath")?.ToString();if(string.IsNullOrWhiteSpace(path))return;
   if(probe.State=="GamelogMarkerNotSent")probe.Begin(path,now,log);else probe.Poll(path,now);
  }catch{report("GamelogReadUnavailable");return;}
  if(prior!=State){prior=State;report(State);}
 }
}
