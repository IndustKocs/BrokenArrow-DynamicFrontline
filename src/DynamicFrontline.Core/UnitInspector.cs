using System.Globalization;
namespace DynamicFrontline.Core;

public sealed class UnitInspector(IGamePort game, ILog log)
{
    public bool Inspect(UnitKey key)
    {
        UnitRead read=game.ReadOwned(key);
        if (read.Status != ReadStatus.Available || read.Snapshot is not { } s || s.Key != key || s.OwnerId != game.LocalOwnerId) return false;
        log.Info(string.Create(CultureInfo.InvariantCulture,
            $"uid={s.Key.Uid} unitId={s.UnitId} owner={s.OwnerId} position=({s.Position.X},{s.Position.Y},{s.Position.Z}) aliveSoldiers={s.AliveSoldierCount} stress={s.CurrentStressValue}/{s.MaxStress} level={s.CurrentStressLevel} command={s.CurrentCommand}"));
        return true;
    }
}
