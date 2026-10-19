namespace LastEpochLAN.Core;

// Visual state follows the same host-time cursor as the interpolated pose.
// HP, damage, rewards and native death never wait for this queue.
public sealed class BufferedEnemyPresentation
{
    private readonly List<(double Time, EnemyState State)> pending = new(8);
    private double latest = double.NegativeInfinity;
    private bool primed;
    public int Count => pending.Count;
    public bool Push(double time, EnemyState state)
    {
        if (state == null || !double.IsFinite(time) || time < 0 || time <= latest) return false;
        latest = time;
        if (pending.Count == 8) pending.RemoveAt(0);
        pending.Add((time, state));
        return true;
    }
    public bool TryTake(double renderTime, out EnemyState state)
    {
        state = null!;
        if (!double.IsFinite(renderTime)) return false;
        int due = 0;
        while (due < pending.Count && pending[due].Time <= renderTime) due++;
        // A newly admitted actor needs its first visual state immediately.
        // Subsequent states wait for the pose cursor. After a hitch, coalesce
        // obsolete visual states instead of replaying several attacks in a frame.
        if (!primed && pending.Count > 0 && due == 0) due = 1;
        if (due == 0) return false;
        state = pending[due - 1].State;
        pending.RemoveRange(0, due);
        primed = true;
        return true;
    }
}

public static class EnemyAdmission
{
    public static double DistanceSquared(EnemyState enemy, float x, float y, float z)
    {
        double dx = enemy.X - (double)x, dy = enemy.Y - (double)y, dz = enemy.Z - (double)z;
        return dx * dx + dy * dy + dz * dz;
    }
    public static int Compare(EnemyState a, EnemyState b, float x, float y, float z)
    {
        int distance = DistanceSquared(a,x,y,z).CompareTo(DistanceSquared(b,x,y,z));
        if (distance != 0) return distance;
        if (a.Boss != b.Boss) return a.Boss ? -1 : 1;
        return a.Id.CompareTo(b.Id);
    }
}
