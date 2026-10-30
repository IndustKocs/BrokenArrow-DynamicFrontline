using DynamicFrontline.Frontline;
using DynamicFrontline.Frontline.Rules023;
namespace DynamicFrontline.Poc030;
public sealed record ForecastDelta(long Unit,double? Delta,string Basis);
// Team-safe: absence means unknown, NEVER enemy/neutral. No foreign unit coordinates.
public sealed record DisplayGrid(Guid Session,double Time,int Width,int Height,double CellSize,Point2 Origin,int Team,int[] OwnCells,int[] PocketCells,ForecastDelta[] Forecast)
{
 public void Validate(){if(Width is <1 or >256||Height is <1 or >256||!double.IsFinite(CellSize)||CellSize<=0||!double.IsFinite(Time)||!double.IsFinite(Origin.X)||!double.IsFinite(Origin.Y)||Team is <0 or >1||OwnCells.Length>Width*Height||PocketCells.Length!=0||Forecast.Length>100||OwnCells.Concat(PocketCells).Any(i=>i<0||i>=Width*Height)||Forecast.Any(f=>f.Delta.HasValue&&!double.IsFinite(f.Delta.Value)))throw new InvalidDataException("DisplayGridBudgetOrDomain");}
}
public sealed record ResultWitness(long InputId,StageDigests Digests,string SessionHash,string ProjectionHash);
public sealed record TeamDisplay(Public023 Projection,DisplayGrid Display,ResultWitness? Witness=null);
public static class DisplayProjection
{
 public static DisplayGrid Build(ObservationCheckpoint checkpoint,Frame023 frame,int team)
 {
  var t=checkpoint.Core.Territory;
  // Hidden-ring geometry is not a team-safe disclosure. Keep own-unit warnings in Public023.
  int[] pockets=[];
  var predictions=frame.Encirclement.Where(u=>u.Team==team).Select(u=>{var p=(frame.Predictions??[]).Where(p=>p.Unit==u.Key).ToArray();return new ForecastDelta(u.Key.Uid,p.Length==0?null:p.Sum(p=>p.Delta),p.Length==0?"Unknown: no justified penalty interval":"Forecast interval only; never applied");}).ToArray();
  var result=new DisplayGrid(frame.Session,frame.Time,t.Width,t.Height,t.CellSize,t.Origin,team,t.Cells.Where(c=>c.Available&&c.Owner==team).Select(c=>c.Index).ToArray(),pockets,predictions);result.Validate();return result;
 }
}
