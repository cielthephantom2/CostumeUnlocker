namespace LastEpochLAN.Core;

/// <summary>Retains terminal records for the current instance after Unity destroys a corpse.</summary>
public sealed class EnemyHistory
{
    private readonly Dictionary<int, EnemyState> states = new();
    private readonly HashSet<int> terminal = new();

    public void Observe(EnemyState state)
    {
        if (!state.IsValid()) throw new ArgumentException("Invalid enemy state.");
        if (states.TryGetValue(state.Id, out var previous))
        {
            if (previous.Template != state.Template) throw new InvalidOperationException("Enemy identity was reused in the same instance.");
            if (previous.Dead) return;
        }
        if (terminal.Contains(state.Id)) return;
        if (state.Dead) terminal.Add(state.Id);
        states[state.Id] = state;
    }

    public EnemyState[] Snapshot(IEnumerable<int> presentIds)
    {
        var present = presentIds.ToHashSet();
        // Absence is not a death: despawning alive must never award loot or experience.
        var live = states.Values.Where(state => !state.Dead && present.Contains(state.Id)).ToArray();
        var dead = states.Values.Where(state => state.Dead).ToArray();
        var retained = live.Concat(dead).Select(state => state.Id).ToHashSet();
        foreach (var id in states.Keys.Where(id => !retained.Contains(id)).ToArray()) states.Remove(id);
        return live.Concat(dead).OrderBy(state => state.Id).ToArray();
    }

    public void RetireDeaths(Func<int, bool> canRetire)
    {
        foreach (var id in states.Values.Where(state=>state.Dead && canRetire(state.Id)).Select(state=>state.Id).ToArray()) states.Remove(id);
        // Keep only the small identity tombstone until instance end, preventing stale resurrection.
    }

    public void Clear() { states.Clear(); terminal.Clear(); }
}
