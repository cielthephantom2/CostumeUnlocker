using System.Reflection;
using HarmonyLib;
using Il2Cpp;

namespace LastEpochLAN.Mod.OfflineTools;

// These native methods only enqueue the "sent to the Forge/Storage" banners.
// Leave the pickup, inventory grant and buffered-item bookkeeping native.
[HarmonyPatch]
internal static class OfflinePickupNotificationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach(string name in new[]{
            nameof(Notifications.ShardAdded),
            nameof(Notifications.MultiShardsAdded),
            nameof(Notifications.RuneOrGlyphAddedNotification),
            nameof(Notifications.KeyOrWovenEchoAddedNotification),
            nameof(Notifications.MaterialAdded)})
            yield return AccessTools.Method(typeof(Notifications),name);
    }
    private static bool Prefix()=>!OfflineToolkit.Enabled;
}
