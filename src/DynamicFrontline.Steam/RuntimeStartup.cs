using System.Diagnostics;
using System.Reflection;
namespace DynamicFrontline.Steam;

public static class RuntimeStartup
{
 // A controller can exist in menus or during teardown without a network room.
 // Missing state never grants permission to start observations or transport.
 public static object? RoomState(object? game, Func<object,string,object?> read)
 {
  if(game==null)return null;
  var room=read(game,"_networkRoomService");
  return room==null?null:read(room,"State");
 }
 public static string Fault(string stage,Exception exception)
 {
  while(exception is TargetInvocationException {InnerException:not null})exception=exception.InnerException;
  var frames=new StackTrace(exception,false).GetFrames()??[];
  var names=frames.Select(f=>f.GetMethod()).Where(m=>m?.DeclaringType?.Namespace?.StartsWith("DynamicFrontline.",StringComparison.Ordinal)==true)
   .Take(5).Select(m=>m!.DeclaringType!.Name+"."+m.Name);
  // No exception message, source filename, locals, IDs or argument values.
  return stage+":"+exception.GetType().Name+":"+string.Join(" > ",names);
 }
}
