namespace DynamicFrontline.Core;

// The generation prevents a recycled UID from receiving an old command.
public readonly record struct UnitKey(Guid MatchId, long Uid, int Generation);
public readonly record struct Position(float X, float Y, float Z)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
    public double DistanceTo(Position p) => Math.Sqrt(Math.Pow((double)X-p.X, 2) + Math.Pow((double)Y-p.Y, 2) + Math.Pow((double)Z-p.Z, 2));
}
public sealed record UnitSnapshot(UnitKey Key, int UnitId, int OwnerId, Position Position,
    bool IsInfantry, bool IsAlive, uint AliveSoldierCount, float CurrentStressValue,
    float MaxStress, string CurrentStressLevel, string CurrentCommand);
public enum ReadStatus { Available, Missing, Destroyed, NotOwned, Unavailable }
public readonly record struct UnitRead(ReadStatus Status, UnitSnapshot? Snapshot = null);
public enum PathStatus { Moving, Arrived, Unreachable, Unknown }
public interface IGamePort
{
    Guid MatchId { get; }
    // Production implementation must reject remote units BEFORE reading their coordinates.
    int LocalOwnerId { get; }
    bool ReadyForControl { get; }
    UnitRead ReadOwned(UnitKey key);
    Position? TeamSpawn { get; }
    bool CancelOrders(UnitKey key);
    bool MoveTo(UnitKey key, Position destination);
    PathStatus ReadPath(UnitKey key);
}
public interface ILog { void Info(string message); void Error(string message); }
public interface IRetreatPlanner { Position? Plan(UnitSnapshot unit, Position? teamSpawn); }
public sealed class FixedPointPlanner(Position destination) : IRetreatPlanner
{
    public Position? Plan(UnitSnapshot unit, Position? teamSpawn) => destination.IsFinite ? destination : null;
}
public sealed class TeamSpawnPlanner : IRetreatPlanner
{
    public Position? Plan(UnitSnapshot unit, Position? teamSpawn) => teamSpawn is { IsFinite: true } ? teamSpawn : null;
}
