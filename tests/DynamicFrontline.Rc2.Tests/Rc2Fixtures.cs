using DynamicFrontline.Poc030;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
public static class Rc2Fixtures
{
 public static MatchBinding Binding=>new(Fixtures023.Session,12345,99,"offline-fixture",[new(1,11,101,false),new(2,22,102,false)],1,2,"Synthetic two process fixture, NOT runtime");
 public static ProfileSet Profiles=>new("fixture-build","fixture-database","Synthetic",[new(1,30,1000,true,true,false,1,MoraleClass.Regular,"Synthetic explicit fixture")]);
 public static MapDescriptor Map=>new("offline-fixture","fixture-build","Synthetic test only",new string('0',64),1,32,32,50,new(0,0),Enumerable.Repeat(true,1024).ToArray(),Enumerable.Repeat(true,1024).ToArray(),Enumerable.Repeat(true,1024).ToArray(),[new(0,new(25,825)),new(1,new(1575,825))],[],true);
 public static Observation Observation(string role,long revision,double time){int owner=role=="A"?1:2;var u=new OwnedSample(new(99,owner,1),owner,0,owner,1,100,100,80,1000,30,1000,owner==1?525:1025,0,825,8,owner==1?11:22,0,true,false,false,false,true,500,800);return new(revision,1,time,false,owner,2,"offline-fixture","Synthetic",true,"FixtureComplete",[u],12345,99,Binding.Players);}
 public static CanonicalInput Input(){var w=new CanonicalWorld(Binding,Profiles,Map,new());var a=Observation("A",1,.25);var b=Observation("B",1,.25);w.Full("A",a);w.Full("B",b);return w.Build(new(SourceState.LiveComplete,1,1,.25,false,0,"fixture"),new(SourceState.LiveComplete,1,1,.25,false,0,"fixture"))!;}
 public static ObservationBridge Bridge()=>new(Binding,Map,Profiles,"fixture-build");
}
