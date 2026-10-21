using LastEpochLAN.Core;

internal static class WeaverImprintTests
{
    internal static void Run(Action<bool,string> check)
    {
        bool Near(float a,float b) => Math.Abs(a-b)<0.000001f;
        for(int points=0;points<=10;points++)
        {
            check(Near(WeaverImprintRules.SealedChance(points),Math.Min(1f,.12f*points)),
                $"Sealed Riches {points} points uses12%/point from verified unmodded6% tooltip, capped at100%");
            check(Near(WeaverImprintRules.DoubleChance(.03f*points),.06f*points),
                $"Warlord Riches {points} points doubles native 3%/point without unlocking zero points");
        }
        foreach(float corruptionMultiplier in new[]{.6f,.9f,1.125f,1.2f})
        {
            float native=.00125f*120*corruptionMultiplier;
            check(Near(.00125f*WeaverImprintRules.DoubleWeight(120)*corruptionMultiplier,2*native),
                "Commoner inlined numerator doubles probability independently of corruption curve");
            check(Near(.00125f*120*WeaverImprintRules.DoubleWeight(corruptionMultiplier),2*native),
                "Echo completion multiplier doubles full probability without clamping multiplier to one");
        }
        foreach(float weight in new[]{0f,.001f,.05f,.3f})
            check(Near(WeaverImprintRules.DoubleWeight(weight),2*weight),
                "Woven/Loomed weighting doubled including zero remains zero: "+weight);
        check(WeaverImprintRules.DoubleChance(.75f)==1 && WeaverImprintRules.DoubleWeight(.75f)==1.5f,
            "Actual probability caps at100%; intermediate Woven/Corruption weighting is not prematurely capped");
        check(WeaverImprintRules.DoubleChance(float.NaN)==0 && WeaverImprintRules.DoubleChance(-1)==0 &&
            float.IsFinite(WeaverImprintRules.DoubleWeight(float.MaxValue)),
            "Imprint scaling does not produce NaN, negative chances or overflowing infinite weights");
        check(WeaverImprintRules.DoubleTooltipPercent("3%") == "6%" &&
            WeaverImprintRules.DoubleTooltipPercent("6%") == "12%" &&
            WeaverImprintRules.DoubleTooltipPercent("50%") == "100%", "verified one-point Sealed/Warlord/Echoing tooltip targets are12%/6%/100%");
        check(WeaverImprintRules.SealedChance(-1)==0 && WeaverImprintRules.SealedChance(int.MaxValue)==1,
            "Sealed explicit rate never unlocks negative/zero points or overflows at large point counts");
        check(WeaverImprintRules.DoubleTooltipPercent("+8,7 %") == "+17,4 %", "multi-point tooltip preserves comma decimal and percent formatting");
        check(WeaverImprintRules.DoubleTooltipPercent("75%") == "100%" &&
            WeaverImprintRules.DoubleTooltipPercent("79228162514264337593543950335%") == "100%", "tooltip probability caps safely without decimal overflow");
        check(WeaverImprintRules.DoubleTooltipPercent("3 items, 2 affixes") == "3 items, 2 affixes" &&
            WeaverImprintRules.DoubleTooltipPercent("<color=white>3%</color>") == "<color=white>3%</color>", "tooltip scaler never modifies prose, quantities or markup");
    }
}
