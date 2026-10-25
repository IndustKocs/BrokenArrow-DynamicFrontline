using System.Reflection;
using DynamicFrontline.Core;
namespace DynamicFrontline.Integration;

public sealed record GameObjectHandle(object LuaUnit,long CanonicalUid,string LifecycleStamp);
public sealed record GameObjectBatch(long Revision,long CapturedMilliseconds,bool Complete,GameObjectHandle[] Units);
public interface IGameObjectFeed { GameObjectBatch ReadObjects(); }
// Generic Has/Get reflection is an experimental binding; all names below are audited signatures.
// The source owns enumeration/lifecycle. No pointer arithmetic, member writes or gameplay commands.
public sealed class ReadOnlyComponentInspector(Func<string,Type?> typeResolver,Func<object,RawVisibility> visibility,
    Func<long,bool>? isDeserting=null,Func<object,GameDeathState>? deathState=null)
{
    public const string UnitType="Il2CppBrokenArrow.Shared.Ecs.Components.UnitComponent";
    public const string InfantryType="Il2CppBrokenArrow.Client.Ecs.Infantry.Components.InfantryUnitComponent";
    public const string HealthType="Il2CppBrokenArrow.Client.Ecs.BattleSystem.Components.HealthComponent";
    public const string StressType="Il2CppBrokenArrow.Client.Ecs.BattleSystem.Components.StressComponent";
    public const string LoadedType="Il2CppBrokenArrow.Client.Ecs.Transports.Components.LoadedComponent";
    public const string LoadingType="Il2CppBrokenArrow.Client.Ecs.Infantry.Components.InfantryLoadingComponent";
    public static readonly string[] RequiredTypes={UnitType,InfantryType,HealthType,StressType,LoadedType,LoadingType};
    private readonly Dictionary<(Type,string),MemberInfo> members=new();
    private readonly Dictionary<(Type,Type,string),MethodInfo> generic=new();
    private object? Member(object value,string name)
    {
        var key=(value.GetType(),name);if(!members.TryGetValue(key,out var member))
        {member=(MemberInfo?)key.Item1.GetProperty(name)??key.Item1.GetField(name)??throw new MissingMemberException();members[key]=member;}
        return member is PropertyInfo p?p.GetValue(value):((FieldInfo)member).GetValue(value);
    }
    private object Required(object value,string name)=>Member(value,name)??throw new InvalidOperationException();
    private static object Invoke(object value,string name)=>value.GetType().GetMethod(name,Type.EmptyTypes)?.Invoke(value,null)??throw new MissingMethodException();
    private object? Component(object entity,string name)
    {
        var t=typeResolver(name)??throw new TypeLoadException(name);
        MethodInfo Method(string method)
        {
            var key=(entity.GetType(),t,method);if(!generic.TryGetValue(key,out var info))
            {info=key.Item1.GetMethods().Single(m=>m.Name==method&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1&&m.GetParameters().Length==0).MakeGenericMethod(t);generic[key]=info;}return info;
        }
        if(!Convert.ToBoolean(Method("Has").Invoke(entity,null)))return null;
        return Method("Get").Invoke(entity,null)??throw new InvalidOperationException();
    }
    public RawGameUnit Inspect(GameObjectHandle handle)
    {
        object entity=Required(handle.LuaUnit,"Entity");
        if(!Convert.ToBoolean(Required(entity,"IsAliveVersion")))throw new IntegrationValidationException(FailureCode.Incomplete);
        object unit=Component(entity,UnitType)??throw new IntegrationValidationException(FailureCode.Incomplete);
        object owner=Required(unit,"Owner"),data=Required(unit,"UnitData");
        object? infantry=Component(entity,InfantryType),health=Component(entity,HealthType),stress=Component(entity,StressType);
        object p=Invoke(handle.LuaUnit,"GetPosition");
        int player=Convert.ToInt32(Required(owner,"UID"));
        if(Convert.ToInt32(Invoke(handle.LuaUnit,"GetOwnerPlayerUID"))!=player)throw new IntegrationValidationException(FailureCode.InconsistentTeam);
        // TeamUid is copied explicitly, not derived from player UID or NetEntity ownership.
        return new(handle.CanonicalUid,handle.LifecycleStamp,Convert.ToInt32(Required(data,"Id")),Convert.ToInt32(Required(data,"Role")),player,
            Convert.ToInt32(Required(owner,"TeamUid")),Convert.ToInt32(Required(owner,"TeamSide")),
            new(Convert.ToDouble(Required(p,"x")),Convert.ToDouble(Required(p,"y")),Convert.ToDouble(Required(p,"z"))),
            Convert.ToBoolean(Invoke(handle.LuaUnit,"IsAlive")),infantry==null?null:Convert.ToUInt32(Required(infantry,"AliveSoldierCount")),
            health==null?null:Convert.ToSingle(Required(health,"CurrentHealth")),health==null?null:Convert.ToSingle(Required(health,"MaxHealth")),
            stress==null?null:Convert.ToSingle(Required(stress,"CurrentStressValue")),stress==null?null:Convert.ToSingle(Required(stress,"MaxStress")),
            stress==null?null:Enum.Parse<StressLevel>(Required(stress,"CurrentStressLevel").ToString()!,false),
            Component(entity,LoadedType)!=null,Component(entity,LoadingType)!=null,isDeserting?.Invoke(handle.CanonicalUid)??false,1,
            visibility(entity),deathState?.Invoke(entity)??GameDeathState.Unknown,"LuaUnit.GetPosition (runtime unverified)",0);
    }
}
public sealed class ReflectionGameDataAccess(IGameObjectFeed source,ReadOnlyComponentInspector inspector):IReadOnlyGameDataAccess
{
    public RawGameFrame Capture(GameMatchContext context)
    {
        if(Environment.CurrentManagedThreadId!=context.GameThreadId)throw new IntegrationValidationException(FailureCode.WrongThread);
        var batch=source.ReadObjects();if(!batch.Complete)return new(batch.Revision,batch.CapturedMilliseconds,false,Array.Empty<RawGameUnit>());
        var values=new RawGameUnit[batch.Units.Length];for(int i=0;i<values.Length;i++)values[i]=inspector.Inspect(batch.Units[i]);
        return new(batch.Revision,batch.CapturedMilliseconds,true,values);
    }
}
public sealed class RecordedFixtureAccess : IReadOnlyGameDataAccess
{
    private readonly Queue<RawGameFrame> frames;
    public RecordedFixtureAccess(IEnumerable<RawGameFrame> values)=>frames=new(values);
    public RawGameFrame Capture(GameMatchContext context)
    {
        if(context.Origin!=DataOrigin.RecordedFixture)throw new IntegrationValidationException(FailureCode.NoContext);
        if(!frames.TryDequeue(out var value))throw new IntegrationValidationException(FailureCode.ProviderUnavailable);
        return value with{Units=value.Units.Select(u=>u with{Visibility=u.Visibility with{ConfirmedVisibleTeams=u.Visibility.ConfirmedVisibleTeams.ToArray()}}).ToArray()};
    }
}
