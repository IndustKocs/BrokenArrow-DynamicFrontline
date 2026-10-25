namespace DynamicFrontline.Core;

// Call ONLY from a verified user-input boundary. Network/system commands do not enter this gateway.
public sealed class UserCommandGate(IGamePort game, DesertionController controller)
{
    public bool Allows(UnitKey key, int requester)
    {
        if (requester != game.LocalOwnerId || key.MatchId != game.MatchId) return false;
        UnitRead read=game.ReadOwned(key);
        return read.Status == ReadStatus.Available && read.Snapshot is { IsAlive:true } s &&
            s.Key == key && s.OwnerId == requester && !controller.BlocksUser(key);
    }
    public UnitKey[] FilterGroup(IEnumerable<UnitKey> units,int requester) => units.Distinct().Where(x=>Allows(x,requester)).ToArray();
}
