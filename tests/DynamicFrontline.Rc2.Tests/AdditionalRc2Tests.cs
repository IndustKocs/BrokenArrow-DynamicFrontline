using System.Text.Json;
using DynamicFrontline.Poc030;
public static class AdditionalRc2Tests
{
    public static void Run(Action<string,Action> test,Action<bool> assert,Action<Action> reject,string output,InstallFile[] files,string sandbox)
    {
        test("Clock staggering waits without consuming a canonical revision",()=>{var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());w.Full("A",Rc2Fixtures.Observation("A",1,.5));w.Full("B",Rc2Fixtures.Observation("B",1,.25));var a=new SourceHealth(SourceState.LiveComplete,1,1,.5,false,0,"fixture");assert(w.Build(a,a with{GameTime=.25})==null&&w.Export().Next==0);w.Full("B",Rc2Fixtures.Observation("B",2,.5));assert(w.Build(a,a with{ObservationRevision=2})!.InputId==0);});
        test("Checkpoint with active tau preserves forecast and duplicate idempotence",()=>{
            var map=Rc2Fixtures.Map with{Width=64,Height=64,Scoring=Enumerable.Repeat(true,4096).ToArray(),Infantry=Enumerable.Repeat(true,4096).ToArray(),Ground=Enumerable.Repeat(true,4096).ToArray(),Roots=[new(0,new(25,1625)),new(1,new(3175,1625))]};var a=new ObservationBridge(Rc2Fixtures.Binding,map,Rc2Fixtures.Profiles,"fixture-build");var input=Rc2Fixtures.Input() with{MapHash=map.Hash,Units=Fixtures023.Ring(),SupplyComplete=true};StageDigests? digest=null;
            for(int tick=0;tick<64;tick++){input=input with{InputId=tick,Start=tick*.25,End=(tick+1)*.25,DataGap=false};digest=a.Step(input,0);}assert(a.Project(0).Own[0].Tau>0);
            var b=new ObservationBridge(Rc2Fixtures.Binding,map,Rc2Fixtures.Profiles,"fixture-build");b.Restore(JsonSerializer.Deserialize<ObservationCheckpoint>(JsonSerializer.Serialize(a.Export()))!);assert(b.Step(input,0)==digest);var next=input with{InputId=64,Start=16,End=16.25};assert(a.Step(next,0)==b.Step(next,0));assert(a.Project(0).Own[0].Tau==b.Project(0).Own[0].Tau);
        });
        test("R2 retire old unprefixed cache and restore on removal",()=>{
            var root=Path.Combine(sandbox,"retire-cache");var old=Path.Combine(root,"MelonLoader/Il2CppAssemblies/OldGame.dll");Directory.CreateDirectory(Path.GetDirectoryName(old)!);File.WriteAllText(old,"foreign old generated cache");
            var plan=InstallPlanning.Plan(root,[new("MelonLoader/Il2CppAssemblies/Il2CppNewGame.dll",files[0].Source,files[0].Sha256,FileCategory.SharedInteropCache)]);assert(plan.Any(p=>p.Category==FileCategory.RetiredInteropCache));
            using var installer=new DevInstall(root,true,true);installer.Apply(plan);assert(!File.Exists(old)&&installer.Verify().Conflicts.Length==0);installer.Remove();assert(File.ReadAllText(old)=="foreign old generated cache");
        });
        test("R2 retired cache interruption restores complete old cache",()=>{
            for(int point=1;point<=12;point++){var root=Path.Combine(sandbox,"retire-interrupt-"+point);var old=Path.Combine(root,"MelonLoader/Il2CppAssemblies/OldGame.dll");Directory.CreateDirectory(Path.GetDirectoryName(old)!);File.WriteAllText(old,"old bytes");var plan=InstallPlanning.Plan(root,[new("MelonLoader/Il2CppAssemblies/Il2CppNewGame.dll",files[0].Source,files[0].Sha256,FileCategory.SharedInteropCache)]);int n=0;try{using var installer=new DevInstall(root,true,true,_=>{if(++n==point)throw new IOException("Injected storage interruption");});installer.Apply(plan);}catch(IOException){}using var recovery=new DevInstall(root,true,true);assert(recovery.Recover().Conflicts.Length==0);if(!File.Exists(old))recovery.Remove();assert(File.ReadAllText(old)=="old bytes");}
        });
        test("Positive service evidence is deduplicated and never invents intervals",()=>{
            var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());var a=Rc2Fixtures.Observation("A",1,.25);var b=Rc2Fixtures.Observation("B",1,.25);var u=a.Owned[0];var proof=new ServiceProof(1,a.Owner,u.NetworkId!.Value,.25,5,"Repair","Synthetic exact native-operation fixture",u.Local);a=a with{ServiceProofs=[proof]};
            w.Full("A",a);w.Full("B",b);var h=new SourceHealth(SourceState.LiveComplete,1,1,.25,false,0,"fixture");var first=w.Build(h,h)!;assert(first.PositiveServices!.Length==1&&first.Services.Length==0&&!first.SupplyComplete);
            var restored=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());restored.Restore(JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(w.Export()))!);restored.Full("A",a with{Revision=2,GameTime=.5});restored.Full("B",b with{Revision=2,GameTime=.5});h=h with{ObservationRevision=2,GameTime=.5};assert(restored.Build(h,h)!.PositiveServices!.Length==0);
            restored.Full("A",a with{Revision=3,GameTime=.75,ServiceProofs=[proof with{Amount=99}]});restored.Full("B",b with{Revision=3,GameTime=.75});h=h with{ObservationRevision=3,GameTime=.75};reject(()=>restored.Build(h,h));assert(restored.Export().Next==2);
        });
        test("Old local entity service proof cannot attach to reused NetID",()=>{
            var w=new CanonicalWorld(Rc2Fixtures.Binding,Rc2Fixtures.Profiles,Rc2Fixtures.Map,new());var a=Rc2Fixtures.Observation("A",1,.25);var b=Rc2Fixtures.Observation("B",1,.25);var u=a.Owned[0];a=a with{ServiceProofs=[new(1,a.Owner,u.NetworkId!.Value,.25,5,"Ammo","Synthetic proof",u.Local with{Version=99})]};w.Full("A",a);w.Full("B",b);var h=new SourceHealth(SourceState.LiveComplete,1,1,.25,false,0,"fixture");assert(w.Build(h,h)!.PositiveServices!.Length==0);
        });
    }
}
