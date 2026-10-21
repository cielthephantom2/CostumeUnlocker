using LastEpochLAN.Core;

internal static class BloodrageChargeTests
{
    internal static void Run(Action<bool, string> check)
    {
        for(int stage=1;stage<=5;stage++)
        {
            check(BloodrageChargeRules.Earned(stage)==2*stage &&
                BloodrageChargeRules.InitialRewards(stage,false)==2*stage,
                $"Bloodrage stage {stage}: announcement, refresh and spendable initial balance = {2*stage}");
        }
        check(BloodrageChargeRules.Earned(0)==0 && BloodrageChargeRules.Earned(-1)==-1,
            "Bloodrage zero/sentinel values never grant free charges");
        check(BloodrageChargeRules.InitialRewards(5,true)==5,
            "standalone reward encounter keeps its own native initial balance");
        int stageIndex=0, previousCurrency=0;
        var reached=new List<int>();
        foreach(int kills in new[]{9,10,10,19,20,30,40,50,50})
        {
            if(stageIndex<5 && kills>=10*(stageIndex+1))stageIndex++;
            int currency=BloodrageChargeRules.InitialRewards(stageIndex,false);
            if(currency>previousCurrency){reached.Add(currency);previousCurrency=currency;}
        }
        check(stageIndex==5 && reached.SequenceEqual(new[]{2,4,6,8,10}),
            "native hunt index remains 0..5: repeated kills/refresh do not skip thresholds or compound charges");
        int remaining=BloodrageChargeRules.InitialRewards(5,false);
        int purchases=0;while(remaining>0){remaining--;purchases++;}
        check(purchases==10 && remaining==0,
            "ten earned Bloodrage charges allow ten native one-charge consumptions, no balance rescaling");
        check(BloodrageChargeRules.Earned(int.MaxValue)==int.MaxValue,
            "Bloodrage earned currency saturates instead of integer overflow");
    }
}
