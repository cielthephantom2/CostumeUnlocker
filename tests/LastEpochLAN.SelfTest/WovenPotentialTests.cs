using LastEpochLAN.Core;

internal static class WovenPotentialTests
{
    internal static void Run(Action<bool,string> check)
    {
        foreach (var (lp, percent, failure) in new[] { (1,50,0),(2,25,1),(3,25,2),(4,25,3) })
        {
            int success=Math.Min(4,lp+1);
            var outcomes=Enumerable.Range(0,100).Select(roll=>WovenPotentialRules.Roll(lp,roll)).ToArray();
            check(outcomes.Count(value=>value==success)==percent && outcomes.Count(value=>value==failure)==100-percent,
                $"Woven {lp} LP: exact {percent}% to {success} LP and {100-percent}% to {failure} LP over all percentile buckets");
            check(WovenPotentialRules.Roll(lp,0)==success && WovenPotentialRules.Roll(lp,percent-1)==success &&
                WovenPotentialRules.Roll(lp,percent)==failure && WovenPotentialRules.Roll(lp,99)==failure,
                $"Woven {lp} LP threshold boundaries have no overlap or missing outcomes");
        }
        check(!WovenPotentialRules.AppliesTo(0) && !WovenPotentialRules.AppliesTo(5) &&
            WovenPotentialRules.Roll(0,0)==0 && WovenPotentialRules.Roll(5,99)==5,
            "Woven override excludes 0 LP and invalid LP above 4; native eligibility continues rejecting 0 LP");
        foreach (int invalid in new[] { -1,100,int.MaxValue })
        {
            bool rejected=false;
            try {WovenPotentialRules.Roll(1,invalid);} catch (ArgumentOutOfRangeException) {rejected=true;}
            check(rejected,"Woven rejects invalid percentile "+invalid);
        }
    }
}
