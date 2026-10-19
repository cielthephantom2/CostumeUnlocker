namespace LastEpochLAN.Core;

public sealed class RecentEnemyDamage
{
    private string scene = "";
    private long epoch = -1, revision = -1;
    private readonly Dictionary<int, float> health = new();
    private readonly Dictionary<int, double> visibleUntil = new();
    public void Observe(WorldState world, double now)
    {
        if (scene != world.Scene || epoch != world.Epoch)
        { Reset(); scene = world.Scene; epoch = world.Epoch; }
        if (world.Revision <= revision) return;
        revision = world.Revision;
        var ids = world.Enemies.Select(enemy => enemy.Id).ToHashSet();
        foreach (var enemy in world.Enemies)
        {
            if (!enemy.Dead && health.TryGetValue(enemy.Id, out float previous) && enemy.Health < previous)
                visibleUntil[enemy.Id] = now + 3;
            if (enemy.Dead) visibleUntil.Remove(enemy.Id);
            health[enemy.Id] = enemy.Health;
        }
        foreach (var id in health.Keys.Where(id => !ids.Contains(id)).ToArray())
        { health.Remove(id); visibleUntil.Remove(id); }
    }
    public bool IsVisible(int enemyId, double now) => visibleUntil.GetValueOrDefault(enemyId) > now;
    public void Reset() { health.Clear(); visibleUntil.Clear(); scene = ""; epoch = -1; revision = -1; }
}
