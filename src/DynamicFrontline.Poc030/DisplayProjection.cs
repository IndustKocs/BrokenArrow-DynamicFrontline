using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Poc030;
public sealed record ForecastDelta(long Unit,double? Delta,string Basis,int Team=-1);
// Team-safe: absence means unknown, NEVER enemy/neutral. No foreign unit coordinates.
public sealed record DisplayGrid(Guid Session,double Time,int Width,int Height,double CellSize,Point2 Origin,int Team,int[] OwnCells,int[] PocketCells,ForecastDelta[] Forecast,bool FullDebug=false,int[]? OtherCells=null,LocalControlArea[]? LocalAreas=null)
{
 public void Validate(bool allowFullDebug=false){if(FullDebug&&!allowFullDebug||!FullDebug&&((OtherCells?.Length??0)>0||(LocalAreas?.Length??0)>0))throw new InvalidDataException("FullDebugConsentRequired");if(Width is <1 or >256||Height is <1 or >256||!double.IsFinite(CellSize)||CellSize<=0||!double.IsFinite(Time)||!double.IsFinite(Origin.X)||!double.IsFinite(Origin.Y)||Team is <0 or >1||OwnCells.Length>Width*Height||(!FullDebug&&PocketCells.Length!=0)||PocketCells.Length>Width*Height||(OtherCells?.Length??0)>Width*Height||(OtherCells??[]).Any(i=>i<0||i>=Width*Height)||(LocalAreas?.Length??0)>100||(LocalAreas??[]).Any(a=>!a.Center.IsFinite||!double.IsFinite(a.Radius)||a.Radius<0)||Forecast.Length>100||OwnCells.Concat(PocketCells).Any(i=>i<0||i>=Width*Height)||Forecast.Any(f=>f.Delta.HasValue&&!double.IsFinite(f.Delta.Value)))throw new InvalidDataException("DisplayGridBudgetOrDomain");}
}
public sealed record ResultWitness(long InputId,StageDigests Digests,string SessionHash,string ProjectionHash);
public sealed record TeamDisplay(Public023 Projection,DisplayGrid Display,ResultWitness? Witness=null);
public static class DisplayProjection
{
 public static DisplayGrid Build(ObservationCheckpoint checkpoint,Frame023 frame,int team,bool fullDebug=false)
 {
  var t=checkpoint.Core.Territory;
  // Hidden-ring geometry is not a team-safe disclosure. Keep own-unit warnings in Public023.
  int[] pockets=fullDebug?frame.Geometry.Where(g=>g.Enclosed==Evidence.True&&g.Certificate!=null).SelectMany(g=>g.Certificate!.Interior).Distinct().OrderBy(i=>i).ToArray():[];
  var predictions=frame.Encirclement.Where(u=>fullDebug||u.Team==team).Select(u=>{var p=(frame.Predictions??[]).Where(p=>p.Unit==u.Key).ToArray();return new ForecastDelta(u.Key.Uid,p.Length==0?null:p.Sum(p=>p.Delta),p.Length==0?"Unknown: no justified penalty interval":"Forecast interval only; never applied",u.Team);}).ToArray();
  var result=new DisplayGrid(frame.Session,frame.Time,t.Width,t.Height,t.CellSize,t.Origin,team,t.Cells.Where(c=>c.Available&&c.Owner==team).Select(c=>c.Index).ToArray(),pockets,predictions,fullDebug,fullDebug?t.Cells.Where(c=>c.Available&&c.Owner==1-team).Select(c=>c.Index).ToArray():null,fullDebug?frame.LocalAreas:null);result.Validate(fullDebug);return result;
 }
}
