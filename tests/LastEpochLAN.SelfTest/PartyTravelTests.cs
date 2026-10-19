using LastEpochLAN.Core;

internal static class PartyTravelTests
{
    internal static void Run(Action<bool,string> check)
    {
        check(PartyTravelRules.CanUseDefaultDestination(0,false) && PartyTravelRules.CanUseDefaultDestination(3,false),
            "party teleport accepts campaign and town destinations regardless of the origin map category");
        check(PartyTravelRules.CanUseDefaultDestination(1,true) && PartyTravelRules.CanUseDefaultDestination(2,true) && PartyTravelRules.CanUseDefaultDestination(5,true),
            "party teleport accepts non-combat hubs even when native scene category belongs to special content");
        check(!PartyTravelRules.CanUseDefaultDestination(1,false) && !PartyTravelRules.CanUseDefaultDestination(2,false) && !PartyTravelRules.CanUseDefaultDestination(5,false),
            "active special-run destinations require run context instead of silently creating a divergent instance");
        check(!PartyTravelRules.CanUseDefaultDestination(4,true),"party teleport cannot enter non-gameplay scenes");
        check(PartyTravelRules.SameScene("EoT","eot") && !PartyTravelRules.SameScene("","") && !PartyTravelRules.SameScene("A","B"),
            "party placement compares actual scene identity without case-only mismatches");
        check(!PartyTravelRules.ReadyToPlace(false,true,false,"A","A") && !PartyTravelRules.ReadyToPlace(true,false,false,"A","A") &&
            !PartyTravelRules.ReadyToPlace(true,true,true,"A","A") && !PartyTravelRules.ReadyToPlace(true,true,false,"A","B") &&
            PartyTravelRules.ReadyToPlace(true,true,false,"A","a"),"party placement waits for the destination spawn, visuals and loading screen before moving the actor");
    }
}
