using LastEpochLAN.Core;

internal static class WovenPotentialTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach (var (lp, percent, failure) in new[] { (1,75,0),(2,50,1),(3,25,1) })
        {
            var outcomes=Enumerable.Range(0,100).Select(roll=>WovenPotentialRules.Roll(lp,roll)).ToArray();
            check(outcomes.Count(value=>value==lp+1)==percent && outcomes.Count(value=>value==failure)==100-percent,
                $"Woven {lp} LP: exact {percent}% to {lp+1} LP and {100-percent}% to {failure} LP over all percentile buckets");
            check(WovenPotentialRules.Roll(lp,0)==lp+1 && WovenPotentialRules.Roll(lp,percent-1)==lp+1 &&
                WovenPotentialRules.Roll(lp,percent)==failure && WovenPotentialRules.Roll(lp,99)==failure,
                $"Woven {lp} LP threshold boundaries have no overlap or missing outcomes");
        }
        check(!WovenPotentialRules.AppliesTo(0) && !WovenPotentialRules.AppliesTo(4) &&
            WovenPotentialRules.Roll(0,0)==0 && WovenPotentialRules.Roll(4,99)==4,
            "Woven override excludes 0/4 LP, leaving their native behavior untouched");
        foreach (int invalid in new[] { -1,100,int.MaxValue })
        {
            bool rejected=false;
            try {WovenPotentialRules.Roll(1,invalid);} catch (ArgumentOutOfRangeException) {rejected=true;}
            check(rejected,"Woven rejects invalid percentile "+invalid);
        }
    }
}
