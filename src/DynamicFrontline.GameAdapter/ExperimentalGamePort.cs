using DynamicFrontline.Core;
namespace DynamicFrontline.GameAdapter;

public interface IUnitBindings
{
    // Must resolve exact match + generation on main thread, from own units only.
    object? ResolveOwnedLuaUnit(UnitKey key);
    PathStatus ReadPath(UnitKey key);
    Position? TeamSpawn { get; }
}

public sealed class ExperimentalGamePort : IGamePort
{
    private readonly IUnitBindings bindings;
    private readonly ReflectionUnitReader reader;
    private readonly ILog log;
    private readonly int thread=Environment.CurrentManagedThreadId;
    private bool faulted;
    // Remains false until actual wrappers, per-unit hook coverage and path binding are validated.
    public bool ReadyForControl => false;
    public int LocalOwnerId { get; }
    public Guid MatchId { get; }
    public Position? TeamSpawn => bindings.TeamSpawn;
    public ExperimentalGamePort(Guid matchId,int localOwner,IUnitBindings bindings,ReflectionUnitReader reader,ILog log)
    { MatchId=matchId; LocalOwnerId=localOwner; this.bindings=bindings; this.reader=reader; this.log=log; }
    public UnitRead ReadOwned(UnitKey key)
    {
        if (faulted || key.MatchId != MatchId || Environment.CurrentManagedThreadId != thread) return new(ReadStatus.Unavailable);
        try
        {
            object? unit=bindings.ResolveOwnedLuaUnit(key);
            return unit == null ? new(ReadStatus.Missing) : reader.Read(unit,key,LocalOwnerId);
        }
        catch(Exception e) { faulted=true; log.Error("Unit reader disabled: "+e.GetType().Name); return new(ReadStatus.Unavailable); }
    }
    public PathStatus ReadPath(UnitKey key) => ReadyForControl ? bindings.ReadPath(key) : PathStatus.Unknown;
    public bool CancelOrders(UnitKey key) => ReadyForControl && TryCommand(key,null);
    public bool MoveTo(UnitKey key,Position destination) => ReadyForControl && destination.IsFinite && TryCommand(key,destination);
    // Prepared actual method binding; cannot execute while ReadyForControl=false.
    private bool TryCommand(UnitKey key,Position? destination)
    {
        if (ReadOwned(key).Status != ReadStatus.Available) return false;
        object? unit=bindings.ResolveOwnedLuaUnit(key);
        if (unit == null) return false;
        try
        {
            if(destination is not { } p)
            {
                var stop=unit.GetType().GetMethod("StopAllCommands",Type.EmptyTypes) ?? throw new MissingMethodException("StopAllCommands");
                stop.Invoke(unit,null);
            }
            else
            {
                var move=unit.GetType().GetMethods().Single(m=>m.Name=="MoveTo" && m.GetParameters().Length==3);
                Type vectorType=move.GetParameters()[0].ParameterType;
                object vector=Activator.CreateInstance(vectorType,p.X,p.Y,p.Z) ?? throw new InvalidOperationException("Vector3");
                move.Invoke(unit,new object[]{vector,3,false});
            }
            return true;
        }
        catch(Exception e) { faulted=true; log.Error("Command binding disabled: "+e.GetType().Name); return false; }
    }
}
