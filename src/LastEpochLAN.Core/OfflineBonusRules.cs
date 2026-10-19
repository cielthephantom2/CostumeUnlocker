namespace LastEpochLAN.Core;

public static class OfflineBonusRules
{
    public const float MoveSpeed = 0.25f;
    public const float Area = 0.5f;
    public const float ManaRegen = 0.5f;
    public const float HealthRegen = 0.5f;
    public const int ShadeCorruptionMultiplier = 5;

    public readonly record struct CorruptionReward(int Total, int Catchup, int Gaze);
    public static CorruptionReward ShadeCorruption(int current, int nativeTotal, int catchup, int gaze, int maximum)
    {
        // Scale the reward delta once, never the accumulated corruption. Native
        // losses and no-gain echoes retain their original outcome and breakdown.
        if (nativeTotal <= current) return new(nativeTotal, catchup, gaze);
        long delta = (long)nativeTotal - current;
        long gain = Math.Min(delta * ShadeCorruptionMultiplier, Math.Max(0L, (long)maximum - current));
        long baseGain = Math.Max(0L, delta - Math.Max(0, catchup) - Math.Max(0, gaze));
        long scaledGaze = Math.Min(Math.Max(0L, (long)gaze * ShadeCorruptionMultiplier),
            Math.Max(0L, gain - baseGain * ShadeCorruptionMultiplier));
        long scaledCatchup = Math.Min(Math.Max(0L, (long)catchup * ShadeCorruptionMultiplier),
            Math.Max(0L, gain - baseGain * ShadeCorruptionMultiplier - scaledGaze));
        return new((int)(current + gain), (int)scaledCatchup, (int)scaledGaze);
    }

    public static int MinimumPotential(int rolled) => Math.Max(1, rolled);
    public static float GoldMultiplier(bool monolith, int corruption) => monolith ? Math.Max(1f, corruption / 40f) : 1f;
    public static int GoldAmount(int amount, float multiplier) => amount <= 0 ? amount :
        (int)Math.Min(int.MaxValue, Math.Floor(amount * (double)multiplier));
}
