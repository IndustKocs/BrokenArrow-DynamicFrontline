using DynamicFrontline.Core;
using DynamicFrontline.Frontline;
using DynamicFrontline.Integration;

sealed class MutableContext : IGameMatchContextProvider {public GameMatchContext? Value;public GameMatchContext? ReadContext()=>Value;}
sealed class MutableAccess : IReadOnlyGameDataAccess
{
    public RawGameUnit[] Units=Array.Empty<RawGameUnit>();public bool Complete=true,Throw;public long Revision;public RawGameFrame? Override;
    public RawGameFrame Capture(GameMatchContext c){if(Throw)throw new Exception("SECRET enemy coordinates 12,34");return Override??new(++Revision,Revision*250,Complete,Units);}
}
sealed class TestRoutes:IGameRouteBackend
{
    public List<RouteRequest> Requests=new();public Queue<RouteAnswer> Answers=new();public bool AutoAnswer=true,Throw;
    public RouteState State=RouteState.Reachable;public double Cost=10;public bool Complete=true;
    public void Submit(RouteRequest r){if(Throw)throw new Exception();Requests.Add(r);if(AutoAnswer)Answers.Enqueue(new(r.RequestId,State,Complete,Cost));}
    public IReadOnlyList<RouteAnswer> DrainAnswers(){var a=Answers.ToArray();Answers.Clear();return a;}
}
sealed class Fixture:IDisposable
{
    public readonly MutableContext Context=new();public readonly MutableAccess Access=new();public readonly TestRoutes Routes=new();
    public GameUnitProfileResolver Profiles;public readonly GameMapDefinition Map;public readonly GameNavigationAdapter Navigation;
    public readonly FrontlineSimulationBridge Bridge;public readonly CoordinateTransform Coordinates=new(new(0,0,0),1);
    public Guid Match=>Context.Value!.MatchId;public long Time;
    public Fixture(int size=32,int period=250,IntegrationSettings? settings=null,Guid? match=null)
    {
        var db=new DatabaseIdentity("fixture-scenario","fixture-profile","v1","game-fixture","Synthetic",new string('a',64),true);
        Context.Value=new(match??Guid.NewGuid(),db,1,1,new[]{new TeamBinding(42,0,new[]{10,11,12,13,14}),new TeamBinding(87,1,new[]{20,21,22,23,24})},DataOrigin.Synthetic,true,Environment.CurrentManagedThreadId);
        GameProfile P(int id,int role,bool cap,bool relay,bool vehicle=false)=>new(id,role,cap,cap,relay,1,10,40,NavigationChannel.OperationalRelay,0,1,StressPolicy.Standard,false,vehicle);
        Profiles=new(new(db,"poc02-fixture-v1",UnknownUnitPolicy.Exclude,new[]{P(1,30,true,true),P(2,36,false,true),P(3,32,false,true),P(4,10,false,true,true),P(5,11,false,true,true),P(6,31,false,false),P(7,31,true,true),P(8,33,false,false),P(9,34,false,false)}));
        int count=size*size;var owners=Enumerable.Repeat(-1,count).ToArray();var protectedOwners=Enumerable.Repeat(-1,count).ToArray();
        for(int y=0;y<size;y++){owners[y*size]=0;owners[y*size+1]=0;owners[y*size+size-1]=1;owners[y*size+size-2]=1;}
        protectedOwners[0]=0;protectedOwners[size-1]=1;
        Map=new(size,size,10,default,Enumerable.Repeat(true,count).ToArray(),owners,protectedOwners,new[]{new TeamRoot(0,new(5,5)),new TeamRoot(1,new(size*10-5,5))},1,DataOrigin.Synthetic);
        var ns=new NavigationSettings(64,512,20,3,1000);Navigation=new(Routes,ns);
        Bridge=new(settings??new(true,true,true,false),new Rules{TickMilliseconds=period,CaptureThreshold=8000,IsolationDelayTicks=2,RecoveryDelayTicks=2},Context,Access,
            new GameTerritoryMapProvider(Map),Coordinates,_=>Profiles,Navigation,ns);
    }
    public RawGameUnit Unit(long uid=1,int id=1,int team=0,double x=15,double z=5)
    {
        var p=Profiles.Document.Units.Single(p=>p.UnitDefinitionId==id);bool infantry=p.GameRole is >=30 and <=36;
        return new(uid,"spawn-"+uid,id,p.GameRole,team==0?10:20,team==0?42:87,team,new(x,3,z),true,infantry?8u:null,100,100,
            infantry?0f:null,infantry?100f:null,infantry?StressLevel.Calm:null,false,false,false,1,new(false,false,false,false,Array.Empty<int>()));
    }
    public BrokenArrowUnitSnapshotProvider Provider()=>new(Access,Profiles,Coordinates,GameTerritoryMapProvider.CreateGrid(Map),Context.Value!);
    public bool Step(params RawGameUnit[] units){Access.Units=units;bool ok=Bridge.Update(Time);Time+=250;return ok;}
    public void Dispose()=>Bridge.Dispose();
}
sealed class NavRig
{
    public Guid Match=Guid.NewGuid();public TestRoutes Backend=new(){AutoAnswer=false};public GameNavigationAdapter Adapter;
    public FrontlineUnit[] Units;public TeamRoot Root=new(0,new(5,5));public long Revision=1;
    public NavRig(NavigationSettings? settings=null){Adapter=new(Backend,settings??new(2,8,2,2,100));Units=new[]{new FrontlineUnit(new(Match,1,1),1,0,new(15,5),8),new FrontlineUnit(new(Match,2,1),1,0,new(25,5),8)};}
    public RouteQuery Query=>new(Match,new(Units[0].Key,null,Units[0].Position,0),new(Units[1].Key,null,Units[1].Position,0),NavigationChannel.OperationalRelay,0,1,Revision,100);
    public void Begin(long tick)=>Adapter.BeginFrame(Match,Revision,tick,Units,new[]{Root});
}
