using System.Reflection;
using DynamicFrontline.Core;
namespace DynamicFrontline.GameAdapter;

// Experimental runtime binding, not validated against generated IL2CPP wrappers.
// Reads snapshots only: never Set(), unsafe pointers or writes to boxed struct copies.
public sealed class ReflectionUnitReader(Func<string,Type?> resolve)
{
    private const string Shared="Il2CppBrokenArrow.Shared.Ecs.Components.";
    private const string Client="Il2CppBrokenArrow.Client.Ecs.";
    public UnitRead Read(object luaUnit, UnitKey key, int localOwner)
    {
        // Do not access position/components before ownership is established.
        if (Convert.ToInt32(Invoke(luaUnit,"GetOwnerPlayerUID")) != localOwner) return new(ReadStatus.NotOwned);
        if (Convert.ToInt64(Member(luaUnit,"UID")) != key.Uid) return new(ReadStatus.Unavailable);
        object entity=Member(luaUnit,"Entity");
        if (!Convert.ToBoolean(Member(entity,"IsAliveVersion")) || !Convert.ToBoolean(Invoke(luaUnit,"IsAlive")))
            return new(ReadStatus.Destroyed);
        object? infantry=Component(entity,Client+"Infantry.Components.InfantryUnitComponent");
        if (infantry == null) return new(ReadStatus.Unavailable);
        object unit=Component(entity,Shared+"UnitComponent") ?? throw new MissingMemberException("UnitComponent");
        if(Convert.ToInt32(Member(Member(unit,"Owner"),"UID")) != localOwner) return new(ReadStatus.NotOwned);
        object stress=Component(entity,Client+"BattleSystem.Components.StressComponent") ?? throw new MissingMemberException("StressComponent");
        object commands=Component(entity,Shared+"CommandsComponent") ?? throw new MissingMemberException("CommandsComponent");
        object pos=Invoke(luaUnit,"GetPosition");
        Position p=new(Convert.ToSingle(Member(pos,"x")),Convert.ToSingle(Member(pos,"y")),Convert.ToSingle(Member(pos,"z")));
        float current=Convert.ToSingle(Member(stress,"CurrentStressValue")), max=Convert.ToSingle(Member(stress,"MaxStress"));
        if (!p.IsFinite || !float.IsFinite(current) || !float.IsFinite(max) || max < 0) return new(ReadStatus.Unavailable);
        object? command=OptionalMember(commands,"CurrentCommand");
        // Never call command.ToString(): it may include a hidden target or coordinates.
        return new(ReadStatus.Available,new(key,Convert.ToInt32(Member(Member(unit,"UnitData"),"Id")),localOwner,p,
            true,true,Convert.ToUInt32(Member(infantry,"AliveSoldierCount")),current,max,
            Member(stress,"CurrentStressLevel").ToString() ?? "unknown",command?.GetType().Name ?? "none"));
    }
    private object? Component(object entity,string name)
    {
        Type type=resolve(name) ?? throw new TypeLoadException(name);
        MethodInfo has=Generic(entity,"Has",type), get=Generic(entity,"Get",type);
        if (!Convert.ToBoolean(has.Invoke(entity,null))) return null;
        // Invoke boxes a read value on supported wrappers. Unsupported ref returns throw and disable adapter.
        return get.Invoke(entity,null) ?? throw new InvalidOperationException("Null component: "+name);
    }
    private static MethodInfo Generic(object target,string name,Type type) => target.GetType().GetMethods()
        .Single(m=>m.Name==name && m.IsGenericMethodDefinition && m.GetGenericArguments().Length==1 && m.GetParameters().Length==0)
        .MakeGenericMethod(type);
    internal static object? OptionalMember(object target,string name)
    {
        Type t=target.GetType();
        PropertyInfo? p=t.GetProperty(name); if(p != null) return p.GetValue(target);
        FieldInfo? f=t.GetField(name); if(f != null) return f.GetValue(target);
        throw new MissingMemberException(t.FullName,name);
    }
    internal static object Member(object target,string name) => OptionalMember(target,name) ?? throw new InvalidOperationException("Null: "+name);
    internal static object Invoke(object target,string name) => target.GetType().GetMethod(name,Type.EmptyTypes)?.Invoke(target,null)
        ?? throw new MissingMethodException(target.GetType().FullName,name);
}
