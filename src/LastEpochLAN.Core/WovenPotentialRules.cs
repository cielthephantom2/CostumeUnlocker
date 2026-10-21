namespace LastEpochLAN.Core;

public static class WovenPotentialRules
{
    public static bool AppliesTo(int potential) => potential is >= 1 and <= 4;
    public static int UpgradePercent(int potential) => potential switch { 1 => 50, 2 or 3 or 4 => 25, _ => 0 };
    public static int Roll(int potential, int percentile)
    {
        if ((uint)percentile >= 100) throw new ArgumentOutOfRangeException(nameof(percentile));
        if (!AppliesTo(potential)) return potential;
        if (percentile < UpgradePercent(potential)) return Math.Min(4, potential + 1);
        return potential - 1;
    }
}
