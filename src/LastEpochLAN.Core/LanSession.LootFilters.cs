namespace LastEpochLAN.Core;

public sealed partial class LanSession
{
    private RoomLootFilter? localLootFilter;
    private RoomLootFilter? sentLootFilter;
    private RoomLootFilter[] receivedLootFilters = Array.Empty<RoomLootFilter>();

    public IReadOnlyList<RoomLootFilter> LootFilters => !Connected ? Array.Empty<RoomLootFilter>() :
        IsHost ? CreateLootFilterRoster() : Volatile.Read(ref receivedLootFilters).ToArray();

    public bool PublishLootFilter(string xml)
    {
        if (!Connected) return false;
        var current = Volatile.Read(ref localLootFilter);
        if (current?.Xml == xml) return true;
        var next = new RoomLootFilter(LocalId, xml);
        if (!next.IsValid()) return false;
        Volatile.Write(ref localLootFilter, next);
        return true;
    }

    private RoomLootFilter[] CreateLootFilterRoster() =>
        new[] { Volatile.Read(ref localLootFilter) }
            .Concat(peers.Values.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => Volatile.Read(ref p.LootFilter)))
            .Where(f => f != null).Cast<RoomLootFilter>().ToArray();

    private static bool LootFilterOnly(Packet p) => p.State == null && p.Players == null &&
        p.World == null && p.WorldChunk == null && p.Echo == null && p.Hud == null &&
        p.Hits == null && p.DeathReceipts == null && p.StatusVisuals == null && p.StatusCallbacks == null &&
        p.Skills == null && p.IncomingDamage == null && p.Gifts == null && p.ChestCommands == null &&
        p.ChestReplies == null && p.EncounterCommands == null && p.EncounterReplies == null && p.Encounters == null;
}
