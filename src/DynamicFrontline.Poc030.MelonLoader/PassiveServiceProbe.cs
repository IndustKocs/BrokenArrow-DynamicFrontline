using System.Collections.Concurrent;
using System.Reflection;
using HarmonyLib;
using DynamicFrontline.Poc030;
namespace DynamicFrontline.Poc030.MelonLoader;
// Observer-only patches. No argument/result substitution and no skipped original method.
// Positive operations are diagnostic evidence; incomplete lifetime coverage never enables penalties.
public sealed class PassiveServiceProbe : IDisposable
{
    sealed record Supplier(int Owner,int NetId,LocalIdentity Local);
    sealed record RepairCapture(object Entity,int Owner,int NetId,double Time,float Health,LocalIdentity Local,Supplier? Source);
    sealed record SupplyScope(int Depth,Supplier? Source);
    static PassiveServiceProbe? active;readonly HarmonyLib.Harmony harmony=new("DynamicFrontline.RC2.PassiveServiceProbe");
    readonly ConcurrentQueue<ServiceProof> receipts=new();long sequence;int queued;
    readonly int readerThread=Environment.CurrentManagedThreadId;
    [ThreadStatic] static int supplyDepth;
    [ThreadStatic] static Supplier? supplySource;
    [ThreadStatic] static (int Ammo,double Amount)? pendingAmmo;
    public string State {get;private set;}="NotInstalled";
    public void Install()
    {
        if(active!=null)throw new InvalidOperationException("One service observer only");active=this;
        try{
            Patch("BrokenArrow.Client.Ecs.Transports.Systems.RepairSystem","ApplyRepairAndHealthNewValues",7,nameof(RepairBefore),nameof(RepairAfter));
            Patch("BrokenArrow.Client.Ecs.Transports.Systems.SupplySystem","InternalUpdate",2,nameof(SupplyBefore),nameof(SupplyAfter));
            Patch("BrokenArrow.Client.Ecs.BattleSystem.AmmunitionContainer","AddAmmo",1,nameof(AmmoBefore),nameof(AmmoAfter));
            Patch("BrokenArrow.Client.Ecs.GNetwork.Services.NetworkWeaponsService","SetAmmoCount",3,null,nameof(AmmoRecipient));
            State="PassiveBindingsInstalled_LifetimeCoverageUnvalidated";
        }catch{Dispose();State="BindingUnavailable_SupplyUnknown";throw;}
    }
    void Patch(string type,string name,int count,string? before,string? after)
    {
        var method=LiveReader.Resolve(type).GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Single(m=>m.Name==name&&m.GetParameters().Length==count);
        HarmonyMethod? Wrap(string? n)=>n==null?null:new(typeof(PassiveServiceProbe).GetMethod(n,BindingFlags.Static|BindingFlags.NonPublic)!);
        harmony.Patch(method,Wrap(before),Wrap(after),finalizer:Wrap(nameof(Finalizer)));
    }
    static Supplier? ReadSupplier(object entity){var s=LiveReader.ServiceRecipient(entity);return s==null?null:new(s.Value.Owner,s.Value.NetId,s.Value.Local);}
    static bool ReaderThread(){if(active==null)return false;if(Environment.CurrentManagedThreadId==active.readerThread)return true;active.State="NativeCallbackThreadUnvalidated_SupplyUnknown";return false;}
    static void Finalizer(Exception? __exception){if(__exception!=null){supplyDepth=0;supplySource=null;pendingAmmo=null;if(active!=null)active.State="NativeOperationException_SupplyUnknown";}}
    static void RepairBefore(object[] __args,out RepairCapture? __state)
    {__state=null;if(!ReaderThread())return;try{var r=LiveReader.ServiceRecipient(__args[0]);if(r!=null)__state=new(__args[0],r.Value.Owner,r.Value.NetId,r.Value.Time,r.Value.Health,r.Value.Local,ReadSupplier(__args[1]));}catch{if(active!=null)active.State="RecipientReadUnavailable_SupplyUnknown";}}
    static void RepairAfter(RepairCapture? __state)
    {if(__state==null)return;try{var r=LiveReader.ServiceRecipient(__state.Entity);if(r!=null&&r.Value.Owner==__state.Owner&&r.Value.NetId==__state.NetId&&r.Value.Local==__state.Local&&r.Value.Health>__state.Health)active?.Add(r.Value.Owner,r.Value.NetId,r.Value.Time,r.Value.Health-__state.Health,"Repair","Positive HP change strictly inside native RepairSystem.ApplyRepairAndHealthNewValues",r.Value.Local,__state.Source);}catch{if(active!=null)active.State="RepairPostReadUnavailable_SupplyUnknown";}}
    static void SupplyBefore(object[] __args,out SupplyScope __state){__state=new(supplyDepth,supplySource);pendingAmmo=null;supplyDepth=0;supplySource=null;if(!ReaderThread())return;try{supplySource=ReadSupplier(__args[0]);if(supplySource!=null)supplyDepth=__state.Depth+1;}catch{if(active!=null)active.State="SupplierReadUnavailable_SupplyUnknown";}}
    static void SupplyAfter(SupplyScope __state){supplyDepth=__state.Depth;supplySource=__state.Source;pendingAmmo=null;}
    static void AmmoBefore(object __instance,out int? __state){__state=null;if(supplyDepth<=0)return;try{__state=Convert.ToInt32(LiveReader.Get(__instance,"AmmoAvailable"));}catch{if(active!=null)active.State="AmmoReadUnavailable_SupplyUnknown";}}
    static void AmmoAfter(object __instance,int? __state){if(__state==null)return;try{int after=Convert.ToInt32(LiveReader.Get(__instance,"AmmoAvailable"));int id=Convert.ToInt32(LiveReader.Get(LiveReader.Get(__instance,"AmmunitionInfo")!,"Id"));pendingAmmo=after>__state.Value?(id,after-__state.Value):null;}catch{pendingAmmo=null;if(active!=null)active.State="AmmoPostReadUnavailable_SupplyUnknown";}}
    static void AmmoRecipient(object[] __args)
    {try{var proof=pendingAmmo;pendingAmmo=null;if(supplyDepth<=0||proof==null||proof.Value.Ammo!=Convert.ToInt32(__args[1]))return;var r=LiveReader.ServiceRecipient(__args[0]);if(r!=null)active?.Add(r.Value.Owner,r.Value.NetId,r.Value.Time,proof.Value.Amount,"Ammo","Positive native AddAmmo within owned SupplySystem context, correlated recipient/ammunition in SetAmmoCount",r.Value.Local,supplySource);}catch{if(active!=null)active.State="AmmoRecipientUnavailable_SupplyUnknown";}}
    void Add(int owner,int netId,double time,double amount,string kind,string evidence,LocalIdentity local,Supplier? supplier){if(Interlocked.Increment(ref queued)>256){Interlocked.Decrement(ref queued);State="ProofQueueExceeded_SupplyUnknown";return;}receipts.Enqueue(new(Interlocked.Increment(ref sequence),owner,netId,time,amount,kind,evidence,local,supplier?.NetId,supplier?.Local,supplier?.Owner));}
    public ServiceProof[] Drain(){var result=new List<ServiceProof>();while(receipts.TryDequeue(out var p)){Interlocked.Decrement(ref queued);result.Add(p);}return result.ToArray();}
    public void Dispose(){harmony.UnpatchSelf();if(active==this)active=null;supplyDepth=0;supplySource=null;pendingAmmo=null;}
}
