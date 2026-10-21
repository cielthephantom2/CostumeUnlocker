namespace LastEpochLAN.Core;

public static class WeaverImprintRules
{
    public const float Multiplier = 2f;
    // Weighting/Corruption multipliers are not probabilities: do not clamp them
    // to one before the native consumer has multiplied its other factors.
    public static float DoubleWeight(float value) => float.IsNaN(value) || value <= 0 ? 0 :
        (float)Math.Min(float.MaxValue, (double)value * Multiplier);
    public static float DoubleChance(float value) => Math.Min(1f, DoubleWeight(value));
}
