namespace LastEpochLAN.Mod.OfflineTools;

internal static class ForgingPotentialRules
{
    // Reduce the range before native RNG/lucky rolls, not the capped FP loss.
    // Positive costs retain a minimum of one; free crafts remain free.
    internal static int Half(int cost)=>cost>0 ? Math.Max(1,cost/2) : cost;
}
