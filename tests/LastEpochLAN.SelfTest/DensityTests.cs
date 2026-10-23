using LastEpochLAN.Core;

internal static class DensityTests
{
    internal static void Run(Action<bool, string> check)
    {
        check(DensityRules.Multiplier(false, 0) == 1 && DensityRules.Multiplier(false, 1000) == 1,
            "outside density is native x1 and does not inherit a previous Monolith corruption");
        check(DensityRules.Multiplier(true, -1) == 1 && DensityRules.Multiplier(true, 0) == 1 &&
            DensityRules.Multiplier(true, 50) == 1 && DensityRules.Multiplier(true, 100) == 1,
            "Monolith never loses native density at low or absent corruption");
        check(DensityRules.Multiplier(true, 250) == 2.5f && DensityRules.Multiplier(true, 500) == 5 &&
            DensityRules.Multiplier(true, 1000) == 10 && DensityRules.Multiplier(true, 2000) == 20,
            "Monolith scales linearly with corruption, without applying an outside multiplier");
        check(DensityRules.CanGenerate(true, false, false) && DensityRules.CanGenerate(true, true, true) &&
            !DensityRules.CanGenerate(true, true, false) && !DensityRules.CanGenerate(false, false, false),
            "density supports solo and Host; Guest and online do not generate extra populations");
        foreach (float nativeModifier in new[] { -1f, -0.5f, 0f, 0.5f, 2f })
        {
            float modifier = DensityRules.PlacementModifier(nativeModifier, 5);
            check(Math.Abs((1 + modifier) - (1 + nativeModifier) * 5) < 0.0001f,
                $"density preserves native room modifier {nativeModifier} including disabled spawns");
        }
        float baseline = 2.5f;
        for (int roll = 0; roll < 100; roll++)
            baseline = DensityRules.RestoreTarget(baseline * (1 + DensityRules.PlacementModifier(0, 5)), 5);
        check(baseline == 2.5f, "repeated native room rolls cannot compound density x5 to x25");
    }
}
