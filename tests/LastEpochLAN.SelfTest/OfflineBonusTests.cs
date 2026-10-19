using LastEpochLAN.Core;

internal static class OfflineBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(OfflineBonusRules.ShadeCorruption(100,110,0,0,int.MaxValue)==new OfflineBonusRules.CorruptionReward(150,0,0),
            "Shade corruption scales only the positive reward delta x5, not existing corruption");
        check(OfflineBonusRules.ShadeCorruption(100,130,10,12,int.MaxValue)==new OfflineBonusRules.CorruptionReward(250,50,60),
            "Shade corruption includes native catchup and Gaze rewards exactly once with matching preview components");
        check(OfflineBonusRules.ShadeCorruption(100,90,0,0,int.MaxValue).Total==90 &&
            OfflineBonusRules.ShadeCorruption(100,100,0,0,int.MaxValue).Total==100,
            "Shade corruption preserves native losses and zero gain");
        var capped=OfflineBonusRules.ShadeCorruption(40,49,3,0,50);
        check(capped.Total==50 && capped.Catchup==0 && capped.Gaze==0 &&
            OfflineBonusRules.ShadeCorruption(50,50,0,0,50).Total==50,
            "Shade corruption respects native timeline cap and limits the displayed reward breakdown");
        check(OfflineBonusRules.ShadeCorruption(int.MaxValue-2,int.MaxValue,0,0,int.MaxValue).Total==int.MaxValue,
            "Shade corruption uses wide arithmetic and saturates instead of overflowing saved corruption");
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
        check(8*(1+OfflineBonusRules.ManaRegen)==12 && 8*(1+OfflineBonusRules.HealthRegen)==12,
            "regen increases use 0.5 for 50 percent, not flat 50 or 50x");
        check(OfflineBonusRules.MoveSpeed==0.25f && OfflineBonusRules.Area==0.5f,
            "fixed speed and area bonuses use the HUD percentage units");
    }
}
