using LastEpochLAN.Core;
namespace LastEpochLAN.Mod;

internal static class NativePartyScaling
{
    private static Il2Cpp.GroupScalingManager owner;
    private static bool originalOverride;
    private static int originalCount, applied = -1;
    private static float retryAt;
    internal static string Error { get; private set; } = "";
    internal static void Tick(LanSession session, string scene)
    {
        if (session?.IsHost != true || string.IsNullOrEmpty(scene) || UnityEngine.Time.unscaledTime < retryAt) return;
        try {
        var manager = Il2Cpp.GroupScalingManager.instance;
        if (!(bool)manager) return;
        if (owner?.Pointer != manager.Pointer)
        {
            owner=manager; originalOverride=manager.overrideNumberOfPlayers;
            originalCount=manager.overriddenNumberOfPlayers; applied=-1;
        }
        int count=Math.Clamp(session.PlayerPositions.Count(p=>p.Available && p.Cooperative && p.Scene==scene),1,4);
        if (applied==count && manager.overrideNumberOfPlayers && manager.overriddenNumberOfPlayers==count) return;
        manager.OverrideGroupScale(count);
        // Native curve includes scene level/type and preserves native health modifiers.
        manager.CalculateAndApplyNewHealthModifier(count);
        applied=count;
        Error="";
        } catch(Exception ex) {Error=ex.Message; retryAt=UnityEngine.Time.unscaledTime+2;}
    }
    internal static void Reset()
    {
        try { if ((bool)owner)
        {
            if (originalOverride) { owner.OverrideGroupScale(originalCount); owner.CalculateAndApplyNewHealthModifier(originalCount); }
            else { owner.ResetGroupScale(); owner.CalculateAndApplyNewHealthModifier(1); }
        }} catch(Exception ex) {MelonLoader.MelonLogger.Warning("Party scaling restore: "+ex.Message);}
        owner=null; applied=-1;
    }
}
