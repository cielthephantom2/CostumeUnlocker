namespace LastEpochLAN.Core;

public static class BloodrageChargeRules
{
    // Input is the native earned tier count, never the remaining spendable
    // balance. Preserve zero/sentinels and protect the native integer currency.
    public static int Earned(int nativeCount) => nativeCount <= 0 ? nativeCount :
        (int)Math.Min(int.MaxValue, (long)nativeCount * 2);
    public static int InitialRewards(int nativeCount, bool standalone) => standalone ? nativeCount : Earned(nativeCount);
}
