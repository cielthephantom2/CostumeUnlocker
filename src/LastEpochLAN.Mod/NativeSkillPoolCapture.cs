using HarmonyLib;
using UnityEngine;

namespace LastEpochLAN.Mod;

// GlobalVFXManager's receive methods inline pooling setup and instantiate via
// AutoPool, bypassing AbilityObjectConstructor.InstantiateAbility entirely.
[HarmonyPatch]
internal static class NativePooledSkillVisualCapturePatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(Il2Cpp.AutoPool), "instantiate", new[] {
            typeof(Il2CppLE.AssetBundles.SoftRef<GameObject>), typeof(Transform), typeof(bool),
            typeof(bool), typeof(bool), typeof(string), typeof(int) });
        yield return AccessTools.Method(typeof(Il2Cpp.AutoPool), "instantiate", new[] {
            typeof(Il2CppLE.AssetBundles.SoftRef<GameObject>), typeof(Vector3), typeof(Quaternion),
            typeof(Transform), typeof(bool), typeof(bool), typeof(string), typeof(int) });
    }

    private static void Postfix(Il2CppLE.AssetBundles.SoftRef<GameObject> prefabSoftRef, GameObject __result)
    {
        try { NativeVisualEvents.ObserveReplayRoot(__result); NativeSkillVisualCapture.ObservePooled(__result, prefabSoftRef); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Pooled skill visual capture: " + ex.Message); }
    }
}

// Casting effects can be created before, or outside, ability-object construction.
// Maintain a nested scope and restore it even when the original native call fails.
[HarmonyPatch(typeof(Il2Cpp.UsingAbility), nameof(Il2Cpp.UsingAbility.PlayCastingVFX))]
internal static class NativeCastingVisualScopePatch
{
    private static void Prefix(Il2Cpp.UsingAbility __instance, Il2Cpp.Ability ability,
        out (Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        if (SkillCapture.Session == null) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local?.stateController?.usingAbility?.Pointer != __instance.Pointer) return;
        NativeSkillVisualCapture.ConstructionOwner = local;
        NativeSkillVisualCapture.ConstructionAbility = ability;
    }

    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        NativeSkillVisualCapture.ConstructionOwner = __state.Owner;
        NativeSkillVisualCapture.ConstructionAbility = __state.Ability;
    }
}

[HarmonyPatch(typeof(Il2Cpp.AutoPool), nameof(Il2Cpp.AutoPool.Release),
    new[] { typeof(GameObject), typeof(Il2CppLE.AssetBundles.SoftRef<GameObject>), typeof(bool) })]
internal static class NativePooledSkillVisualFinishPatch
{
    private static void Prefix(GameObject go)
    {
        if (SkillCapture.Session == null) return;
        try { NativeSkillVisualCapture.FinishGlobal(go); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Pooled skill visual completion: " + ex.Message); }
    }
}

// Hand/weapon cast effects use a separate player pool. They can be spawned
// from visual animation callbacks after the ability constructor scope ended.
[HarmonyPatch(typeof(Il2Cpp.PlayerCastVfxManager), nameof(Il2Cpp.PlayerCastVfxManager.attachVFX))]
internal static class NativePlayerCastPoolScopePatch
{
    private static void Prefix(Transform tForm, out (Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        if (SkillCapture.Session == null || tForm == null) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        var visuals = local?.actorSync?.actorVisuals;
        if (visuals == null || !tForm.IsChildOf(visuals.transform)) return;
        var ability = local.stateController?.usingAbility?.ability;
        if (ability == null) return;
        NativeSkillVisualCapture.ConstructionOwner = local;
        NativeSkillVisualCapture.ConstructionAbility = ability;
    }

    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        NativeSkillVisualCapture.ConstructionOwner = __state.Owner;
        NativeSkillVisualCapture.ConstructionAbility = __state.Ability;
    }
}

[HarmonyPatch(typeof(Il2Cpp.ActorVisualsVFXHandler), nameof(Il2Cpp.ActorVisualsVFXHandler.AddRefreshableVFX))]
internal static class NativeRefreshableSkillVisualScopePatch
{
    private static void Prefix(Il2Cpp.ActorVisualsVFXHandler __instance, Il2Cpp.Ability ability,
        out (Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        if (SkillCapture.Session == null) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local?.actorSync?.actorVisuals?.Pointer != __instance.actorVisuals?.Pointer) return;
        NativeSkillVisualCapture.ConstructionOwner = local;
        NativeSkillVisualCapture.ConstructionAbility = ability;
    }

    private static void Postfix(Il2Cpp.ActorVisualsVFXHandler __instance, Il2Cpp.Ability ability)
    {
        if (SkillCapture.Session == null || ability == null || __instance.refreshableVFX == null) return;
        try
        {
            foreach (var visual in __instance.refreshableVFX)
                if (visual != null && visual.Ability?.Pointer == ability.Pointer)
                    NativeSkillVisualCapture.TrackGlobal(__instance.actorVisuals?.actorSync, visual.gameObject, ability);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Refreshable skill visual capture: " + ex.Message); }
    }

    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        NativeSkillVisualCapture.ConstructionOwner = __state.Owner;
        NativeSkillVisualCapture.ConstructionAbility = __state.Ability;
    }
}
