namespace LastEpochLAN.Core;

public static class WovenPotentialRules
{
    public static bool AppliesTo(int potential) => potential is >= 1 and <= 3;
    public static int UpgradePercent(int potential) => potential switch { 1 => 50, 2 => 50, 3 => 25, _ => 0 };
    public static int Roll(int potential, int percentile)
    {
        if ((uint)percentile >= 100) throw new ArgumentOutOfRangeException(nameof(percentile));
        if (!AppliesTo(potential)) return potential;
        if (percentile < UpgradePercent(potential)) return potential + 1;
        return potential == 1 ? 0 : 1;
    }
}
