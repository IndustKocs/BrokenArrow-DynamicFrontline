using System.Diagnostics;
using System.Reflection;
namespace DynamicFrontline.Poc030;
// Read-only name bindings from UA01. No pointers, Set, native command queues, or synthetic fallback.
public sealed class LiveReader
{
    const string S="BrokenArrow.Shared.Ecs.Components.";
    const string C="BrokenArrow.Client.Ecs.";
    long revision,epoch; object? previousWorld; double previousTime=-1;
    public static Type Resolve(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Il2Cpp"+name,false) ?? a.GetType(name,false)).FirstOrDefault(t=>t!=null) ?? throw new TypeLoadException(name);
    public static object? Get(object target,string name) => Get(target.GetType(),target,name);
    static object? Get(Type type,object? target,string name)
    {
        const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        var property=type.GetProperty(name,flags);
        if(property!=null) return property.GetValue(target);
        var field=type.GetField(name,flags) ?? throw new MissingMemberException(type.FullName,name);
        return field.GetValue(target);
    }
    static object Required(object target,string name)=>Get(target,name) ?? throw new InvalidDataException("Null:"+name);
    static object Call(object target,string name)=>target.GetType().GetMethod(name,Type.EmptyTypes)?.Invoke(target,null) ?? throw new MissingMethodException(target.GetType().FullName,name);
    static int Int(object target,string name)=>Convert.ToInt32(Required(target,name));
    static float Float(object target,string name) { var v=Convert.ToSingle(Required(target,name)); if(!float.IsFinite(v))throw new InvalidDataException("NonFinite:"+name);return v; }
    static object? Component(object entity,string name)
    {
        var type=Resolve(name);
        MethodInfo Generic(string method)=>entity.GetType().GetMethods().Single(m=>m.Name==method&&m.IsGenericMethodDefinition&&m.GetGenericArguments().Length==1&&m.GetParameters().Length==0).MakeGenericMethod(type);
        if(!Convert.ToBoolean(Generic("Has").Invoke(entity,null)))return null;
        // Unsupported IL2CPP ref-return/generic wrappers are caught at the plugin boundary, never emulated.
        return Generic("Get").Invoke(entity,null) ?? throw new InvalidDataException("NullComponent:"+name);
    }
    public Observation Read()
    {
        var gc=Resolve(C+"Controllers.GameController");
        if(!Convert.ToBoolean(Get(gc,null,"IsInstanceAlive")))throw new InvalidOperationException("NoSession");
        var game=Get(gc,null,"Instance") ?? throw new InvalidOperationException("NoController");
        var session=Required(game,"GameSession"); var player=Required(session,"CurrentPlayer");
        int owner=Int(player,"UID"); var world=Required(game,"GameContext"); double time=Float(game,"GameTime");
        // Epoch is only a local binding lifetime, NEVER a canonical unit generation.
        if(previousWorld==null || !world.Equals(previousWorld) || time<previousTime)epoch++;
        previousWorld=world; previousTime=time;
        var enumerator=Call(world,"GetEnumerator"); var units=new List<OwnedSample>(); var watch=Stopwatch.StartNew();
        string quality="OwnedEnumerationOnly_CanonicalGenerationUnknown"; int visited=0;
        try
        {
            while(Convert.ToBoolean(Call(enumerator,"MoveNext")))
            {
                if(++visited>10000 || watch.Elapsed.TotalMilliseconds>8){quality="BudgetExceeded_Incomplete";break;}
                var entity=Required(enumerator,"Current");
                if(!Convert.ToBoolean(Required(entity,"IsAliveVersion")))continue;
                var unit=Component(entity,S+"UnitComponent"); if(unit==null)continue;
                // Ownership gate precedes all position, health, stress and network reads.
                if(Int(Required(unit,"Owner"),"UID")!=owner)continue;
                var data=Required(unit,"UnitData"); var transform=Component(entity,S+"TransformComponent");
                var health=Component(entity,C+"BattleSystem.Components.HealthComponent");
                var stress=Component(entity,C+"BattleSystem.Components.StressComponent");
                if(transform==null || health==null || stress==null){quality="MissingOwnedComponent_Incomplete";continue;}
                var position=Required(transform,"Position"); var infantry=Component(entity,C+"Infantry.Components.InfantryUnitComponent");
                var network=Component(entity,C+"GNetwork.Components.NetworkUnitComponent");
                int? netId=network==null ? null : Int(Required(network,"NetEntity"),"NetID");
                units.Add(new(new(Int(entity,"WorldId"),Int(entity,"EntityId"),Int(entity,"Version")),netId,null,owner,Int(data,"Id"),Float(health,"CurrentHealth"),Float(health,"MaxHealth"),Float(stress,"CurrentStressValue"),Float(stress,"MaxStress"),Int(data,"Role"),Float(data,"MaxStress"),Float(position,"x"),Float(position,"y"),Float(position,"z"),infantry==null?null:Convert.ToUInt32(Required(infantry,"AliveSoldierCount"))));
            }
        }
        finally { var dispose=enumerator.GetType().GetMethod("Dispose",Type.EmptyTypes);dispose?.Invoke(enumerator,null); }
        return new(++revision,epoch,time,Convert.ToBoolean(Required(session,"IsPaused")),owner,Int(session,"PlayersCount"),Convert.ToString(Required(game,"CurrentMapName"))??"",Convert.ToString(Required(game,"LoadStatus"))??"",false,quality,units.ToArray());
    }
}
