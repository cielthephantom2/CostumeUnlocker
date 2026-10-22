using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

// The shipped ArcaneAscendance ailment (ID57) has roots=true. Keep the
// original definition intact: each local application uses a private copy so
// native addition AND removal see roots=false and root counters stay balanced.
[HarmonyPatch(typeof(AilmentReceiver),nameof(AilmentReceiver.ApplyAilmentWithDamageStats))]
internal static class OfflineArcaneMovementPatch
{
    private static Ailment mobile;
    internal static long Applications;
    private static void Prefix(AilmentReceiver __instance,ref Ailment ailment)
    {
        if(!MeteorNodeRuntime.Local(__instance?.actor) || ailment?.id!=AilmentID.ArcaneAscendance || !ailment.roots)return;
        if(mobile==null)
        {
            mobile=UnityEngine.Object.Instantiate(ailment);
            mobile.name="TM Arcane Ascendance (mobile)";
            mobile.hideFlags=HideFlags.HideAndDontSave;
            mobile.roots=false;
        }
        ailment=mobile;Applications++;
    }
}

[HarmonyPatch(typeof(Il2CppLocalizationSystem.LocalizationAbilities),nameof(Il2CppLocalizationSystem.LocalizationAbilities.GetDescription))]
internal static class OfflineArcaneDescriptionPatch
{
    private static void Postfix(Ability ability,ref string __result)
    {
        if(OfflineToolkit.Enabled && ability?.playerAbilityID is "arcas" or "arcae" && __result!=null)
            __result=__result.Replace("prevent movement and ","",StringComparison.OrdinalIgnoreCase);
    }
}
