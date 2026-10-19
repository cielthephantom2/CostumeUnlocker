namespace LastEpochLAN.Core;

public static class OfflineBonusRules
{
    public const float MoveSpeed = 0.25f;
    public const float Area = 0.5f;
    public const float ManaRegen = 0.5f;
    public const float HealthRegen = 0.5f;

    public static int MinimumPotential(int rolled) => Math.Max(1, rolled);
    public static float GoldMultiplier(bool monolith, int corruption) => monolith ? Math.Max(1f, corruption / 40f) : 1f;
    public static int GoldAmount(int amount, float multiplier) => amount <= 0 ? amount :
        (int)Math.Min(int.MaxValue, Math.Floor(amount * (double)multiplier));
}
