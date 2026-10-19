namespace LastEpochLAN.Core;

public sealed record ConfirmedDeath(string HostId, long Epoch, string Scene, EnemyState Enemy);

/// <summary>
/// In-memory transition filter for local reward dispatch. This is not a persistent
/// reward transaction ledger: callers must persist grants before claiming exactly-once rewards.
/// </summary>
public sealed class ConfirmedDeaths
{
    private string host = "";
    private string scene = "";
    private long epoch = -1;
    private long revision = -1;
    private readonly HashSet<int> alive = new();
    private readonly HashSet<int> consumed = new();

    public IReadOnlyList<ConfirmedDeath> Observe(string hostId, WorldState world)
    {
        if (!Guid.TryParse(hostId, out _) || !world.IsValid()) throw new ArgumentException("Invalid authoritative snapshot.");
        bool baseline = host != hostId || epoch != world.Epoch || scene != world.Scene;
        if (!baseline && world.Revision <= revision) return Array.Empty<ConfirmedDeath>();
        if (!baseline && world.Epoch < epoch) return Array.Empty<ConfirmedDeath>();
        if (host == hostId && world.Epoch < epoch) return Array.Empty<ConfirmedDeath>();
        if (baseline)
        {
            host = hostId; epoch = world.Epoch; scene = world.Scene;
            alive.Clear(); consumed.Clear();
        }
        revision = world.Revision;
        var deaths = new List<ConfirmedDeath>();
        foreach (var enemy in world.Enemies)
        {
            if (!enemy.Dead) { if (!consumed.Contains(enemy.Id)) alive.Add(enemy.Id); continue; }
            // Existing corpses on join/zone load must never grant retrospective loot or XP.
            if (!baseline && alive.Contains(enemy.Id) && consumed.Add(enemy.Id))
                deaths.Add(new ConfirmedDeath(host, epoch, scene, enemy));
            else consumed.Add(enemy.Id);
        }
        return deaths;
    }
}
