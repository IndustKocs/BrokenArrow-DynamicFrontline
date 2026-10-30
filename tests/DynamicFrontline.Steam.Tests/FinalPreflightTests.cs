using DynamicFrontline.Poc030;
using DynamicFrontline.Steam;
using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
public static class FinalPreflightTests
{
 public static void Run(Action<bool,string> check){
  check(!RelayOnlyPolicy.PayloadReady(1,0,0)&&!RelayOnlyPolicy.PayloadReady(0,0,0),"no application payload before connected relay proof");
  check(RelayOnlyPolicy.PayloadReady(3,16,123),"verified relay permits payload");
  var good=new ScenarioEvidence(true,false,true,true,true,true,false,false,false,"Scenario","5454584",20,123);
  check(ScenarioGate.Evaluate(good)=="AllowedScenario","server allows Scenario, native room and inactive EOS session permit observation");
  var blocked=new[]{good with{PolicyKnown=false},good with{ScenarioProtected=true},good with{EosSessionPresent=true},good with{IntegrityViolated=true},good with{LoggedIn=false},good with{ScenarioEnabled=false},good with{PrivateLobbyBound=false},good with{Connected=false},good with{Replay=true},good with{LobbyType="Rating"},good with{Scenario="other"},good with{FightId=0},good with{RoomStatus=0},good with{RoomStatus=30},good with{RoomStatus=100}};
  foreach(var e in blocked)check(ScenarioGate.Evaluate(e)!="AllowedScenario","session gate denies "+Array.IndexOf(blocked,e));
  check(ScenarioGate.Evaluate(good with{RoomStatus=10})=="AllowedScenario","deployment phase supported with proven session");
  var input=Rc2Fixtures.Input();var bridge=Rc2Fixtures.Bridge();bridge.Step(input,0);var frame=bridge.Last!;var cp=bridge.Export();
  var hidden=frame.Zoc.Where(z=>z.Unit.Team!=0).Select(z=>z with{Unit=z.Unit with{Visible=false,Position=new Point2(123,456)}}).ToArray();
  var secretFrame=frame with{Zoc=frame.Zoc.Where(z=>z.Unit.Team==0).Concat(hidden).ToArray(),Geometry=[new(CorridorState.Blocked,Evidence.True,new(0,Mobility.Infantry,1,[101,102,103],[99,100],"hidden enemy ring","fixture",true,false))]};
  var safe=DisplayProjection.Build(cp,frame,0);var changed=DisplayProjection.Build(cp,secretFrame,0);
  check(CanonicalCodec.Digest(safe)==CanonicalCodec.Digest(changed)&&changed.PocketCells.Length==0,"same visible/own state with hidden enemy ring gives identical display geometry");
  var altered=Rc2Fixtures.Bridge();altered.Step(input with{Units=input.Units.Select(u=>u.Team==0?u:u with{Visible=false,Position=new Point2(1400,1400)}).ToArray()},0);
  check(bridge.Display(0).PocketCells.Length==0&&altered.Display(0).PocketCells.Length==0,"actual core runs with different hidden units transmit no pocket cells");
  check(changed.Forecast.Length==safe.Forecast.Length&&bridge.Project(0).Own.Length>0,"own encirclement state and forecasts remain available");
  bool denied=false;try{(safe with{PocketCells=[1]}).Validate();}catch(InvalidDataException){denied=true;}check(denied,"incoming nonempty pocket geometry rejected");
  string root=Path.Combine("artifacts","t18b-probe-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"native.log");File.WriteAllText(path,"old native content\n");
  var p=new GamelogProbe();string marker="";p.Begin(path,0,s=>marker=s);p.Poll(path,1);check(p.State=="GamelogMarkerPending","successful Unity call alone never confirms Gamelog");
  File.AppendAllText(path,"[timestamp] Log: "+marker+"\n");p.Poll(path,2);check(p.State=="GamelogMarkerConfirmed","read-only receipt recognizes exact fresh marker in native-format fixture");
  var timeout=new GamelogProbe();timeout.Begin(path,0,_=>{});timeout.Poll(path,16);check(timeout.State=="GamelogMarkerTimeout_Unverified","missing marker times out unverified");
  var rotated=new GamelogProbe();rotated.Begin(path,0,_=>{});rotated.Poll(Path.Combine(root,"different.log"),1);check(rotated.State=="GamelogPathChanged_Unverified","native path change cannot falsely confirm receipt");
  var trunc=new GamelogProbe();trunc.Begin(path,0,_=>{});File.WriteAllText(path,"");trunc.Poll(path,1);check(trunc.State=="GamelogTruncated_Unverified","truncated native log cannot falsely confirm receipt");
 }
}
