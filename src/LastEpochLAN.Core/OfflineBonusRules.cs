namespace LastEpochLAN.Core;

public static class OfflineBonusRules
{
    public const float MoveSpeed = 0.25f;
    public const float Area = 0.5f;
    public const float ManaRegen = 0.5f;
    public const float HealthRegen = 0.5f;
    public const int ShadeCorruptionMultiplier = 5;

    public readonly record struct CorruptionReward(int Total, int Catchup, int Gaze);
    public static CorruptionReward ShadeCorruption(int current, int nativeTotal, int catchup, int gaze, int maximum, int highest)
    {
        // Scale the reward delta once, never the accumulated corruption. Native
        // losses and no-gain echoes retain their original outcome and breakdown.
        if (nativeTotal <= current) return new(nativeTotal, catchup, gaze);
        long delta = (long)nativeTotal - current;
        long room = Math.Max(0L, (long)maximum - current);
        // Catch-up is convergence to another timeline, not an earned x5 reward.
        // Multiplying it made a low timeline overshoot the high one, increasing
        // the next catch-up target and feeding runaway growth across timelines.
        long nativeGaze = Math.Min(Math.Max(0L, gaze), delta);
        long nativeCatchup = Math.Min(Math.Max(0L, catchup), delta - nativeGaze);
        long baseGain = delta - nativeGaze - nativeCatchup;
        long scaledBase = Math.Min(baseGain * ShadeCorruptionMultiplier, room);
        long scaledGaze = Math.Min(nativeGaze * ShadeCorruptionMultiplier, room - scaledBase);
        long subtotal = current + scaledBase + scaledGaze;
        long gap = Math.Max(0L, (long)highest - subtotal);
        // Match native float/rounding; the integer gap clamp also prevents float
        // rounding near int.MaxValue from crossing the catch-up target.
        long scaledCatchup = nativeCatchup > 0 ? Math.Min(gap, (long)MathF.Round((float)gap * .95f)) : 0;
        scaledCatchup = Math.Min(scaledCatchup, room - scaledBase - scaledGaze);
        return new((int)(subtotal + scaledCatchup), (int)scaledCatchup, (int)scaledGaze);
    }

    public static int MinimumPotential(int rolled) => Math.Max(1, rolled);
    public static float GoldMultiplier(bool monolith, int corruption) => monolith ? Math.Max(1f, corruption / 40f) : 1f;
    public static int GoldAmount(int amount, float multiplier) => amount <= 0 ? amount :
        (int)Math.Min(int.MaxValue, Math.Floor(amount * (double)multiplier));
}
