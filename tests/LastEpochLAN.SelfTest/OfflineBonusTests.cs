using LastEpochLAN.Core;

internal static class OfflineBonusTests
{
    internal static void Run(Action<bool,string> check)
    {
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
