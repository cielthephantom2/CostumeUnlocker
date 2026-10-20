using HarmonyLib;
using Il2Cpp;
using UnityEngine;

namespace LastEpochLAN.Mod.OfflineTools;

[HarmonyPatch(typeof(DisintegrateMutator),nameof(DisintegrateMutator.Mutate))]
internal static class DisintegrateRotationPenaltyPatch
{
    internal static long RemovedPenalties;
    internal static float LastOriginalPenalty;
    private static void Postfix(DisintegrateMutator __instance,GameObject __result)
    {
        if(__result==null || !OfflineDisintegrateMovement.Applies(__instance))return;
        // Mutate has finished applying the skill-tree penalty multiplier.
        // Change only the spawned local beam, never the shared prefab/asset.
        foreach(var rotation in __result.GetComponentsInChildren<ModifyDamageOnRotation>(true))
        {
            if(rotation.damageModifierPerDegree>=0 || !float.IsFinite(rotation.damageModifierPerDegree))continue;
            LastOriginalPenalty=rotation.damageModifierPerDegree;
            rotation.damageModifierPerDegree=0;
            RemovedPenalties++;
            if(NativeDisintegrateProbe.Running)NativeDisintegrateProbe.LastRotation=rotation;
        }
    }
}
