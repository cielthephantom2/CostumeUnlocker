using LastEpochLAN.Core;

internal static class FortuneTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(FortuneRules.EnemyDoubleChance==1 && FortuneRules.EchoDoubleChance==1,"CoF Rank 1 and Rank 5 use guaranteed native item duplication");
        check(FortuneRules.IdolExtraChance==0.5f && FortuneRules.AdditionalIdols==2,"CoF Rank 3 rolls 50 percent for two extra Idols");
        check(FortuneRules.ExaltedMultiplier==2 && FortuneRules.LpMultiplier==5,"CoF Rank 6 uses plus 100 percent and Rank 8 uses total x5 native LP chance");
        check(FortuneRules.MageCount(2,true)==3 && FortuneRules.MageCount(1,true)==1.5f && FortuneRules.MageCount(4,true)==6,
            "CoF Rank 7 replaces existing native x2 with x3 before stochastic rounding, preserving other quantity modifiers");
        check(FortuneRules.MageCount(2,false)==2 && FortuneRules.MageCount(0,true)==0,"ineligible mage recipients and zero-count loot retain native quantity");
        check(FortuneRules.ProphecyCount(10)==20 && FortuneRules.ProphecyCount(2)==4 && FortuneRules.ProphecyCount(1)==2,
            "all Prophecy base counts double including Rare, Unique and single-item rewards");
        check(FortuneRules.ProphecyCount(0)==0 && FortuneRules.ProphecyCount(-1)==-1 && FortuneRules.ProphecyCount(int.MaxValue)==int.MaxValue,
            "Prophecy scaling preserves invalid/zero count and prevents integer overflow");
        check(FortuneRules.RankDescription(1).Contains("100%") && FortuneRules.RankDescription(3).Contains("50%") &&
            FortuneRules.RankDescription(7).Contains("3 times") && FortuneRules.RankDescription(8).Contains("5 times"),
            "changed CoF rank descriptions match configured gameplay amounts");
        check(FortuneRules.RankDescription(2)=="" && FortuneRules.RankDescription(4)=="" && FortuneRules.RankDescription(10)=="",
            "unrequested rank descriptions retain native text");
        check(OfflineBonusRules.MinimumPotential(0)==1 && OfflineBonusRules.MinimumPotential(3)==3,
            "requested CoF LP multiplier retains the confirmed minimum-one-LP policy");
    }
}
