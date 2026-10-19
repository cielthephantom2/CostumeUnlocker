namespace LastEpochLAN.Core;

public enum DeathReceiptOutcome { Completed, Skipped, Failed }
public sealed record DeathReceipt(long Epoch, int EnemyId, DeathReceiptOutcome Outcome)
{
    public bool IsValid() => Epoch >= 0 && EnemyId > 0 && Enum.IsDefined(typeof(DeathReceiptOutcome), Outcome);
}

/// <summary>Session-scoped acknowledgements; this is not a durable saved reward transaction.</summary>
public sealed class EnemyDeathLedger
{
    private sealed class Delivery
    {
        internal readonly HashSet<string> Required;
        internal readonly Dictionary<string, DeathReceiptOutcome> Results = new(StringComparer.Ordinal);
        internal Delivery(IEnumerable<string> peers) => Required = peers.ToHashSet(StringComparer.Ordinal);
    }
    private readonly object gate = new();
    private readonly Dictionary<int, Delivery> deaths = new();
    private readonly HashSet<int> retired = new();
    private long epoch = -1;
    private string scene = "";

    public void Observe(WorldState world, IEnumerable<PlayerState> guests)
    {
        lock (gate)
        {
            if (epoch != world.Epoch || scene != world.Scene) { deaths.Clear(); retired.Clear(); epoch = world.Epoch; scene = world.Scene; }
            var eligible = guests.Where(p=>p.Available && p.Scene == scene).Select(p=>p.Id).ToArray();
            foreach (var enemy in world.Enemies.Where(e=>e.Dead))
                if (!retired.Contains(enemy.Id) && !deaths.ContainsKey(enemy.Id)) deaths.Add(enemy.Id, new Delivery(eligible));
        }
    }
    public int[] Eligible(string peerId, WorldState world)
    {
        lock (gate)
        {
            if (world.Epoch != epoch || world.Scene != scene) return Array.Empty<int>();
            return world.Enemies.Where(e=>e.Dead && deaths.TryGetValue(e.Id,out var d) && d.Required.Contains(peerId) && !d.Results.ContainsKey(peerId)).Select(e=>e.Id).ToArray();
        }
    }
    public bool Record(string authenticatedPeerId, DeathReceipt receipt)
    {
        lock (gate)
        {
            if (!receipt.IsValid() || receipt.Epoch != epoch || !deaths.TryGetValue(receipt.EnemyId, out var d) || !d.Required.Contains(authenticatedPeerId)) return false;
            // The first outcome wins: a partial native failure must never become an automatic retry/double grant.
            if (!d.Results.ContainsKey(authenticatedPeerId)) d.Results.Add(authenticatedPeerId, receipt.Outcome);
            return true;
        }
    }
    public bool CanRetire(long instanceEpoch, int enemyId, IEnumerable<string> connectedPeers)
    {
        lock (gate)
        {
            if (instanceEpoch != epoch) return false;
            if (retired.Contains(enemyId)) return true;
            if (!deaths.TryGetValue(enemyId, out var d)) return false;
            var connected = connectedPeers.ToHashSet(StringComparer.Ordinal);
            return d.Required.All(id=>!connected.Contains(id) || d.Results.TryGetValue(id,out var result) && result != DeathReceiptOutcome.Failed);
        }
    }
    public HashSet<int> RetireAcknowledged(long instanceEpoch, IEnumerable<string> connectedPeers)
    {
        lock (gate)
        {
            if (instanceEpoch != epoch) return new HashSet<int>();
            var connected = connectedPeers.ToHashSet(StringComparer.Ordinal);
            var ready = deaths.Where(pair=>pair.Value.Required.All(id=>!connected.Contains(id) ||
                pair.Value.Results.TryGetValue(id,out var result) && result != DeathReceiptOutcome.Failed)).Select(pair=>pair.Key).ToHashSet();
            foreach (int id in ready) { deaths.Remove(id); retired.Add(id); }
            return ready;
        }
    }
}
