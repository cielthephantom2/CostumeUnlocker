namespace LastEpochLAN.Core;

public static class FortuneRules
{
    public const float EnemyDoubleChance=1, IdolExtraChance=0.5f, EchoDoubleChance=1, ExaltedMultiplier=2, LpMultiplier=20;
    public const int AdditionalIdols=2;
    public const float BossSpecificLootIncrease=3, RareToExaltedChance=.5f;
    // Native Rank 7 already doubles the expected mage count. Replace x2 with x15
    // before native stochastic rounding; retain other native quantity modifiers.
    public static float MageCount(float nativeCount,bool eligible)=>eligible && float.IsFinite(nativeCount) && nativeCount>=0 ? nativeCount*7.5f : nativeCount;
    public static int ProphecyCount(int count)=>count>0 && count<=int.MaxValue/2 ? count*2 : count;
    public static string RankDescription(int rank)=>rank switch
    {
        1=>"Enemies have a 100% chance to drop twice as many items.",
        3=>"When an Idol drops, there is a 50% chance for 2 additional Idols to drop.",
        5=>"You have a 100% chance to receive double item rewards from Monolith Echoes.",
        6=>"100% increased chance for items to become Exalted.",
        7=>"Exiled Mages drop 15 times as many Experimental items.",
        8=>"Unique items have 20 times the native chance to roll Legendary Potential.",
        10=>"Bosses have 300% increased drop rate for any specific rewards they have.",
        11=>"Items that would drop as Rare have a 50% chance of becoming Exalted.",
        _=>""
    };
}
