using LastEpochLAN.Core;

internal static class WeaverImprintTests
{
    internal static void Run(Action<bool,string> check)
    {
        bool Near(float a,float b) => Math.Abs(a-b)<0.000001f;
        for(int points=0;points<=10;points++)
        {
            check(Near(WeaverImprintRules.DoubleChance(.029f*points),.058f*points),
                $"Sealed Riches {points} points doubles the native 2.9%/point probability");
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
    }
}
