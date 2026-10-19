namespace LastEpochLAN.Core;

public static class PartyTravelRules
{
    // SceneType values are pinned to the inspected game build. A hub can have
    // a special scene category without requiring an active combat run.
    public static bool CanUseDefaultDestination(int destinationType,bool nonCombatZone) =>
        destinationType!=4 && (destinationType is 0 or 3 || nonCombatZone);
    public static bool SameScene(string? a,string? b)=>!string.IsNullOrEmpty(a) &&
        !string.IsNullOrEmpty(b) && string.Equals(a,b,StringComparison.OrdinalIgnoreCase);
    public static bool ReadyToPlace(bool spawned,bool visualReady,bool loadingVisible,string? actual,string? destination)=>
        spawned && visualReady && !loadingVisible && SameScene(actual,destination);
}
