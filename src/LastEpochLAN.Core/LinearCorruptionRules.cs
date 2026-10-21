namespace LastEpochLAN.Core;

// Remove diminishing without reducing existing practical rewards. LP/WW use
// the tangent at zero; Echo rarity uses the first integer Corruption step.
// Gold's native contribution is linear; the existing /40 mod bonus stays separate.
public static class LinearCorruptionRules
{
    private static double Positive(double c) => double.IsFinite(c) ? Math.Max(0, c) : 0;
    public static float Experience(double c) => (float)(.01 * Positive(c));
    public static float PotentialMultiplier(double c) => (float)(1 + .166 / Math.Log(10) * Positive(c) / 100);
    public static float WillMultiplier(double c) => (float)(1 + .2 / Math.Log(10) * Positive(c) / 100);
    public static float GoldMultiplier(double c) => (float)(1 + .0012 * Positive(c));
    public static float EchoRarity(double c) => (float)(EchoRarityAtOne * Positive(c));
    private static readonly double EchoRarityAtOne = .0165 * Math.Pow(.005 * .088055804 * Math.Pow(Math.Log(101), 4.6), .88);

    // Retain the native float evaluation and banker's rounding when removing
    // the inlined old contribution from MonolithRun.increasedExperience.
    public static float NativeExperience(int c)
    {
        float effective = Math.Min(Math.Max(c, 0), 300) + .25f * Math.Max(Math.Min(c, 1000) - 300, 0)
            + .05f * (float)Math.Max((double)c - 1000, 0);
        float power = effective <= 100 ? effective * .6f : MathF.Pow(effective, 1.52f) * .002f + effective * 1.055f - 47.692955f;
        return MathF.Round(power) * .01f;
    }
    public static float NativeGoldMultiplier(int c)
    {
        double effective = c <= 200 ? Math.Max(c, 0) : c <= 500 ? 200 + (c - 200) / 2 :
            c <= 1000 ? 350 + (c - 500) / 10 : 400 + (c - 1000) / 100;
        return (float)(1 + .0012 * effective);
    }
    public static float AmberCorrection(int c, float baseline, float earlySlope, float lateSlope)
    {
        double corruption = Positive(c);
        double oldMaximum = baseline + Math.Min(corruption, 500) * earlySlope + Math.Max(corruption - 500, 0) * lateSlope;
        // Some asset variant may already have a larger late slope. Never lower it.
        return oldMaximum > 0 ? (float)((baseline + corruption * Math.Max(earlySlope, lateSlope)) / oldMaximum) : 1;
    }

    // Native WW decision tree from the pinned game: same RNG calls, level
    // gates, minimum and maximum; only the Corruption promotion multiplier differs.
    public static int RollWill(int ilvl, int effectiveLevel, int minimum, float corruption,
        Func<float, bool> roll, Func<int, int, int> range)
    {
        float level = ((float)ilvl - effectiveLevel + 20) / 120;
        int result = 5;
        if (roll(MathF.Pow(level, 1.304f) * 1.8f))
        {
            if (roll(MathF.Pow(level, 1.976f) * .96f * WillMultiplier(corruption)))
            {
                result = 8;
                if (roll(MathF.Pow(level, 2) * .3f))
                {
                    result = 11;
                    if (roll(MathF.Pow(level, 2) * .08f)) result = 17;
                    else result = 14;
                }
            }
        }
        int floor = ilvl < 55 ? 5 : ilvl < 82 ? 6 : corruption >= 40 ? 8 : 7;
        result = Math.Max(result, floor) + range(0, 8);
        if (roll(.1f))
        {
            if (!roll(.1f)) result += 1;
            else if (!roll(.1f)) result += 2;
            else if (!roll(.1f)) result += 3;
            else result += 4;
        }
        return result < minimum ? minimum : Math.Min(result, 28);
    }
}
