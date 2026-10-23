using LastEpochLAN.Core;

internal static class OfflineBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(OfflineBonusRules.ShadeCorruption(100,110,0,0,int.MaxValue,100)==new OfflineBonusRules.CorruptionReward(150,0,0),
            "Shade corruption scales only the positive reward delta x5, not existing corruption");
        check(OfflineBonusRules.ShadeCorruption(100,130,10,12,int.MaxValue,100)==new OfflineBonusRules.CorruptionReward(200,0,60),
            "Shade scales base/Gaze only; catchup stops once boosted earned rewards already pass its target");
        check(OfflineBonusRules.ShadeCorruption(100,90,0,0,int.MaxValue,100).Total==90 &&
            OfflineBonusRules.ShadeCorruption(100,100,0,0,int.MaxValue,100).Total==100,
            "Shade corruption preserves native losses and zero gain");
        var capped=OfflineBonusRules.ShadeCorruption(40,49,3,0,50,40);
        check(capped.Total==50 && capped.Catchup==0 && capped.Gaze==0 &&
            OfflineBonusRules.ShadeCorruption(50,50,0,0,50,50).Total==50,
            "Shade corruption respects native timeline cap and limits the displayed reward breakdown");
        check(OfflineBonusRules.ShadeCorruption(int.MaxValue-2,int.MaxValue,0,0,int.MaxValue,int.MaxValue-2).Total==int.MaxValue,
            "Shade corruption uses wide arithmetic and saturates instead of overflowing saved corruption");
        var runaway=OfflineBonusRules.ShadeCorruption(100,14255,14149,0,int.MaxValue,15000);
        check(100+(14255-100)*5==70875 && runaway.Total<=15000 && runaway.Total<15000 && runaway.Gaze==0,
            "regression: 100->~71k from x5 catchup is reproduced; fix converges below the 15000 target");
        check(runaway.Total==130+runaway.Catchup && runaway.Catchup<=14149,
            "runaway regression retains base +30 and recomputes smaller catchup after that gain");
        bool stable=true;
        int highest=15000;
        foreach(int current in new[]{100,300,500,1000,5000,10000})
        {
            int nativeCatchup=(int)MathF.Round((highest-current-6)*.95f);
            var reward=OfflineBonusRules.ShadeCorruption(current,current+6+nativeCatchup,nativeCatchup,0,int.MaxValue,highest);
            stable &= reward.Total<=highest && reward.Total>=current && reward.Total-current==30+reward.Catchup;
        }
        check(stable,"catchup across multiple low timelines never raises the highest target through x5 feedback");
        var overflow=OfflineBonusRules.ShadeCorruption(100,2000000000,1999999894,0,int.MaxValue,int.MaxValue);
        check(overflow.Total>=130 && overflow.Total<=int.MaxValue && overflow.Catchup>=0,
            "large native catchup stays bounded without integer overflow or negative breakdown");
        check(OfflineBonusRules.MinimumPotential(0)==1 && OfflineBonusRules.MinimumPotential(1)==1 &&
            OfflineBonusRules.MinimumPotential(2)==2 && OfflineBonusRules.MinimumPotential(4)==4,
            "minimum LP upgrades only zero; keeps native higher LP rolls");
        check(OfflineBonusRules.GoldMultiplier(false,1000)==1 && OfflineBonusRules.GoldMultiplier(true,0)==1 &&
            OfflineBonusRules.GoldMultiplier(true,-100)==1,
            "gold scaling leaves outside maps and low corruption at native amount");
        check(OfflineBonusRules.GoldMultiplier(true,500)==12.5f && OfflineBonusRules.GoldMultiplier(true,1000)==25 &&
            OfflineBonusRules.GoldMultiplier(true,2000)==50,
            "Monolith gold follows corruption/40 independently of density");
        check(OfflineBonusRules.GoldAmount(8,25)==200 && OfflineBonusRules.GoldAmount(3,2.5f)==7 &&
            OfflineBonusRules.GoldAmount(0,25)==0 && OfflineBonusRules.GoldAmount(int.MaxValue,25)==int.MaxValue,
            "scaled gold floors fractional currency, preserves zero and prevents integer overflow");
        check(OfflineBonusRules.BaseRegen(8)==16 && OfflineBonusRules.BaseRegen(3)==6,
            "base regeneration doubles before item/passive modifiers");
        check(OfflineBonusRules.MoveSpeed==0.25f && OfflineBonusRules.Area==0.5f,
            "fixed speed and area bonuses use the HUD percentage units");
    }
}
