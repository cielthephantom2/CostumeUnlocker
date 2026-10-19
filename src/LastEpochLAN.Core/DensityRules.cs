namespace LastEpochLAN.Core;

// Multipliers of native placement density, not raw spawners/1000 area units.
public static class DensityRules
{
    public static float Multiplier(bool monolith, int corruption) =>
        monolith ? Math.Max(1f, corruption / 100f) : 5f;

    public static bool CanGenerate(bool offline, bool connected, bool host) =>
        offline && (!connected || host);

    public static float PlacementModifier(float nativeModifier, float multiplier) =>
        (1f + nativeModifier) * multiplier - 1f;

    // The native roll stores its multiplied target on the room. Remove only our
    // contribution so another roll cannot compound the multiplier repeatedly.
    public static float RestoreTarget(float rolledTarget, float multiplier) => rolledTarget / multiplier;
}
