namespace DynamicFrontline.Poc030;
public enum SourceState { WaitingForMatch, LiveComplete, LivePartial, ReadUnavailable, Stale, NoSession, SessionChanged, StoppedByBudget }
public sealed record SourceHealth(SourceState State,long ObservationRevision,long BindingEpoch,double GameTime,bool Paused,double AgeSeconds,string Reason);
public sealed class SourceHealthTracker
{
    readonly object gate=new(); SourceHealth value=new(SourceState.WaitingForMatch,0,0,0,false,0,"Waiting");double? success;
    public void Success(Observation o,double monotonic){lock(gate){if(!double.IsFinite(monotonic))throw new InvalidDataException("Clock");success=monotonic;value=new(o.Complete?SourceState.LiveComplete:SourceState.LivePartial,o.Revision,o.BindingEpoch,o.GameTime,o.Paused,0,o.Quality);}}
    public void Failure(SourceState state,string reason){lock(gate){value=value with{State=state,Reason=reason};}}
    public SourceHealth At(double monotonic)
    {lock(gate){var age=success.HasValue?Math.Max(0,monotonic-success.Value):0;var state=value.State;if(age>1.5&&state is SourceState.LiveComplete or SourceState.LivePartial)state=SourceState.Stale;return value with{State=state,AgeSeconds=age};}}
}

