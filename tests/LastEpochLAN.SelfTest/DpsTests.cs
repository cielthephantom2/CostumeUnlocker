using LastEpochLAN.Core;

internal static class DpsTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(!DpsRules.HudActive(0,float.NegativeInfinity),"DPS HUD is hidden before the first damage event");
        check(DpsRules.HudActive(10,10) && DpsRules.HudActive(14.999f,10) && !DpsRules.HudActive(15,10),
            "DPS HUD appears on damage and hides after five seconds without new damage");
        check(DpsRules.HudActive(20,19) && !DpsRules.HudActive(18,19) && !DpsRules.HudActive(float.NaN,19),
            "new damage extends HUD visibility while invalid clock values cannot show it");
        check(DpsRules.HudActive(20,10,true),"DPS HUD remains available until an active drag or resize finishes");
        check(DpsRules.HealthLoss(100,75)==25 && DpsRules.HealthLoss(10,-90)==10,
            "DPS records actual HP loss and excludes overkill");
        check(DpsRules.HealthLoss(100,100)==0 && DpsRules.HealthLoss(100,150)==0 && DpsRules.HealthLoss(0,-50)==0,
            "DPS ignores immunity, healing and hits on dead targets");
        check(DpsRules.HealthLoss(float.NaN,0)==0 && DpsRules.HealthLoss(10,float.PositiveInfinity)==0,
            "invalid native health cannot poison DPS totals");
        check(DpsRules.Scale(float.NaN)==1 && DpsRules.Scale(-10)==0.5f && DpsRules.Scale(20)==2,
            "DPS scale normalizes invalid settings and stays within 0.5 to 2");
    }
}
