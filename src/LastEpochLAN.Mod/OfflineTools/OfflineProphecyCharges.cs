using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class OfflineProphecyCharges
{
    private sealed class Notice
    {
        internal int Charges;
        internal ProphecyTargetType Target;
    }
    [ThreadStatic] private static Dictionary<IntPtr,Notice> nativeCalls;
    internal static bool Local(Actor actor)=>OfflineToolkit.Enabled && actor!=null &&
        actor.Pointer==PlayerFinder.getPlayerActor()?.Pointer;
    internal static bool Enter(ProphecySlot slot)=>(nativeCalls??=new()).TryAdd(slot.Pointer,new());
    internal static bool NativeCall(ProphecySlot slot)=>slot!=null && nativeCalls?.ContainsKey(slot.Pointer)==true;
    internal static bool BufferNotice(ProphecySlot slot,int charges,ProphecyTargetType target)
    {
        if(slot==null || nativeCalls==null || !nativeCalls.TryGetValue(slot.Pointer,out var notice))return false;
        if(charges>0){notice.Charges+=charges;notice.Target=target;}
        return true;
    }
    internal static void Exit(ProphecySlot slot)
    {
        if(nativeCalls==null || !nativeCalls.Remove(slot.Pointer,out var notice))return;
        // Coalesce presentation only. All native gameplay/faction events already
        // ran normally; the original notification formats this reward's own
        // quantity and localized text using the total successfully spent charges.
        if(notice.Charges<=0)return;
        try{Notifications.ProphecyFulfilled(slot,notice.Charges,notice.Target);}
        catch(Exception ex){MelonLoader.MelonLogger.Warning("Prophecy notification: "+ex.GetBaseException().Message);}
    }
    // Preserve the native localized sentence and rich-text tags. Only its visible
    // numeric charge count changes; reward quantities remain owned by each type.
    private static readonly Regex visibleNumbers=new(@"(<[^>]*>)|\d+",RegexOptions.CultureInvariant);
    internal static string ChargeText(string native,int charges)=>charges<2?native:
        visibleNumbers.Replace(native??"",m=>m.Groups[1].Success?m.Value:charges.ToString(CultureInfo.InvariantCulture));
}

[HarmonyPatch(typeof(ProphecySlot),nameof(ProphecySlot.TryTriggerReward))]
internal static class OfflineProphecyConsumeAllPatch
{
    private static bool Prefix(ProphecySlot __instance,ProphecyTargetType __0,bool __1,Actor __2,ref int __3,ref bool __result)
    {
        if(__instance==null || !OfflineProphecyCharges.Local(__2) || __instance.CompletedCharges==0 ||
            OfflineProphecyCharges.NativeCall(__instance))return true;
        if(!OfflineProphecyCharges.Enter(__instance))return true;
        int initial=0,total=0;
        bool succeeded=false;
        try
        {
            initial=__instance.CompletedCharges;
            // Native TryTriggerReward inlines its old tier limits, so changing
            // its separate UI helper alone cannot change actual loot. Drain via
            // the original validated transaction, preserving each reward's item
            // count, target/lens rules, double-reward bonus, and native events.
            for(int attempt=0;attempt<initial && total<initial;attempt++)
            {
                int before=__instance.CompletedCharges;
                if(before==0 || before>initial-total)break; // external charge gain
                bool accepted=__instance.TryTriggerReward(__0,__1,__2,out int used);
                if(!accepted)break;
                succeeded=true;
                if(used>0)total+=used;
                // A rejected/no-progress or externally changed transaction must
                // never become a retry loop. Exceptions also leave charges as the
                // native transaction left them; never restore spent charges.
                if(used<=0 || used>before || __instance.CompletedCharges>=before)break;
            }
            __3=total;__result=succeeded;return false;
        }
        finally{OfflineProphecyCharges.Exit(__instance);}
    }
}

[HarmonyPatch(typeof(ProphecySlot),nameof(ProphecySlot.GetChargesToUseWhenTriggered))]
internal static class OfflineProphecyChargeCountPatch
{
    private static void Postfix(ProphecySlot __instance,ref int __result)
    {if(OfflineToolkit.Enabled && __instance!=null)__result=__instance.CompletedCharges;}
}

[HarmonyPatch(typeof(ProphecySlot),nameof(ProphecySlot.GetChargesToUseWhenTriggeredUIString))]
internal static class OfflineProphecyChargeTextPatch
{
    private static void Postfix(ProphecySlot __instance,ref string __result)
    {if(OfflineToolkit.Enabled && __instance!=null)__result=OfflineProphecyCharges.ChargeText(__result,__instance.CompletedCharges);}
}

[HarmonyPatch(typeof(Notifications),nameof(Notifications.ProphecyFulfilled))]
internal static class OfflineProphecyNotificationPatch
{
    private static bool Prefix(ProphecySlot __0,int __1,ProphecyTargetType __2)=>
        !OfflineProphecyCharges.BufferNotice(__0,__1,__2);
}
