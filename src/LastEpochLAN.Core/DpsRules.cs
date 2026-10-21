namespace LastEpochLAN.Core;

public static class DpsRules
{
    // Actual HP removed, excluding healing, overkill and invalid native values.
    public static float HealthLoss(float before, float after) =>
        !float.IsFinite(before) || !float.IsFinite(after) || before <= 0 ? 0 :
        Math.Clamp(before - Math.Max(0, after), 0, before);
    public static float Scale(float value) => float.IsFinite(value) ? Math.Clamp(value, 0.5f, 2f) : 1;
    public static bool HudActive(float now, float lastDamage, bool manipulating = false) =>
        manipulating || float.IsFinite(now) && float.IsFinite(lastDamage) && now >= lastDamage && now - lastDamage < 15;
}
