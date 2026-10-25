namespace DynamicFrontline.Core;

public enum DesertionState { Controlled, Deserting, Retreating, Recovering, Destroyed, Failed }
public sealed record StateEvent(Guid MatchId, UnitKey Unit, long Revision, long Sequence,
    DesertionState State, int OwnerId, Position? Destination, double At, string Reason);
public sealed record DesertionOptions(bool DebugEnabled = false, double RecoverySeconds = 10,
    double RetreatTimeoutSeconds = 120, double ArrivalRadius = 3)
{
    public void Validate()
    {
        if (!double.IsFinite(RecoverySeconds) || RecoverySeconds < 0 ||
            !double.IsFinite(RetreatTimeoutSeconds) || RetreatTimeoutSeconds <= 0 ||
            !double.IsFinite(ArrivalRadius) || ArrivalRadius <= 0)
            throw new ArgumentOutOfRangeException(nameof(DesertionOptions));
    }
}

// Main-thread only. One manually selected squad per controller in PoC 0.1.
public sealed class DesertionController
{
    private readonly IGamePort game;
    private readonly IRetreatPlanner planner;
    private readonly DesertionOptions options;
    private readonly ILog log;
    private readonly CalmStressRecoveryCondition? recoveryCondition;
    private readonly Queue<StateEvent> events = new();
    private readonly HashSet<UnitKey> activated = new();
    private UnitKey? unit;
    private int owner;
    private Position? target;
    private double started, recoveryStarted, lastTime = -1;
    private long revision, sequence;
    public DesertionState State { get; private set; } = DesertionState.Controlled;
    public string LastReason { get; private set; } = "initial";
    public bool Disabled { get; private set; }
    public bool Locked => !Disabled && State is DesertionState.Deserting or DesertionState.Retreating or DesertionState.Recovering;
    public UnitKey? ActiveUnit => unit;

    public DesertionController(IGamePort game, IRetreatPlanner planner, DesertionOptions options, ILog log,
        CalmStressRecoveryCondition? recoveryCondition = null)
    { options.Validate(); this.game=game; this.planner=planner; this.options=options; this.log=log; this.recoveryCondition=recoveryCondition; }

    private void Clock(double now)
    {
        if (!double.IsFinite(now) || now < 0 || now < lastTime) throw new ArgumentOutOfRangeException(nameof(now));
        lastTime = now;
    }
    public bool Activate(UnitKey key, double now)
    {
        Clock(now);
        if (Disabled || !options.DebugEnabled || !game.ReadyForControl || key.MatchId != game.MatchId || Locked || activated.Contains(key)) return false;
        try
        {
            UnitRead read = game.ReadOwned(key);
            if (read.Status != ReadStatus.Available || read.Snapshot is not { IsAlive:true, IsInfantry:true } s ||
                s.Key != key || s.OwnerId != game.LocalOwnerId || s.AliveSoldierCount == 0 || !s.Position.IsFinite) return false;
            Position? destination = planner.Plan(s, game.TeamSpawn);
            if (destination is not { IsFinite:true }) { LastReason="no-safe-position"; return false; }
            unit=key; owner=s.OwnerId; target=destination; started=now; revision=0;
            activated.Add(key);
            Transition(DesertionState.Deserting, now, "manual");
            // This dedicated port is not the user command gateway. No public 'internal' flag to spoof.
            if (!game.CancelOrders(key)) { Fail(now,"cancel-rejected"); return false; }
            if (!game.MoveTo(key, destination.Value)) { Fail(now,"move-rejected"); return false; }
            Transition(DesertionState.Retreating,now,"move-issued");
            return true;
        }
        catch (Exception e) { Disable(now, "adapter-error:"+e.GetType().Name); return false; }
    }
    public void Tick(double now)
    {
        Clock(now);
        if (!Locked || unit is not { } key) return;
        try
        {
            if (!game.ReadyForControl) { Disable(now,"adapter-not-ready"); return; }
            UnitRead read=game.ReadOwned(key);
            if (read.Status == ReadStatus.Destroyed || read.Snapshot is { IsAlive:false })
            { Transition(DesertionState.Destroyed,now,"destroyed"); return; }
            if (read.Status != ReadStatus.Available || read.Snapshot is not { } s || s.Key != key)
            { Fail(now,"unit-unavailable"); return; }
            if (s.OwnerId != owner || s.OwnerId != game.LocalOwnerId) { Fail(now,"ownership-changed"); return; }
            if (s.AliveSoldierCount == 0) { Transition(DesertionState.Destroyed,now,"no-soldiers"); return; }
            if (!s.Position.IsFinite) { Fail(now,"invalid-position"); return; }
            if (State == DesertionState.Retreating)
            {
                PathStatus path=game.ReadPath(key);
                if (path == PathStatus.Unreachable) { Fail(now,"unreachable"); return; }
                if (path == PathStatus.Arrived && s.Position.DistanceTo(target!.Value) <= options.ArrivalRadius)
                { recoveryStarted=now; Transition(DesertionState.Recovering,now,"arrived"); }
                else if (now-started >= options.RetreatTimeoutSeconds) Fail(now,"retreat-timeout");
            }
            else if (State == DesertionState.Recovering)
            {
                if (s.Position.DistanceTo(target!.Value) > options.ArrivalRadius) { Fail(now,"left-safe-position"); return; }
                if (now-recoveryStarted >= options.RecoverySeconds && (recoveryCondition?.IsSatisfied(s) ?? true))
                    Transition(DesertionState.Controlled,now,"recovered");
                else if (recoveryCondition != null && now-recoveryStarted >= options.RetreatTimeoutSeconds)
                    Fail(now,"stress-recovery-timeout");
            }
        }
        catch (Exception e) { Disable(now,"adapter-error:"+e.GetType().Name); }
    }
    public bool BlocksUser(UnitKey key) => Locked && unit == key;
    public StateEvent[] DrainEvents() { StateEvent[] copy=events.ToArray(); events.Clear(); return copy; }
    public void Disable(double now, string reason)
    {
        Clock(now);
        if (Disabled) return;
        Disabled=true;
        if (unit != null && State != DesertionState.Destroyed) Transition(DesertionState.Failed,now,reason);
        // Do not send another command on failure: game ownership remains intact, user lock is released.
        try { log.Error(reason); } catch { /* Logging cannot retain a control lock. */ }
    }
    private void Fail(double now,string reason) => Transition(DesertionState.Failed,now,reason);
    private void Transition(DesertionState next,double now,string reason)
    {
        State=next; LastReason=reason;
        if (unit is { } key) events.Enqueue(new(key.MatchId,key,++revision,++sequence,next,owner,target,now,reason));
        try { log.Info($"desertion uid={unit?.Uid} state={next} reason={reason}"); } catch { }
    }
}
