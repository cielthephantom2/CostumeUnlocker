using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

[HarmonyPatch(typeof(Il2Cpp.UsingAbility), nameof(Il2Cpp.UsingAbility.UseAbility))]
internal static class SkillCapture
{
    internal static LanSession Session;
    internal static long Sent;
    private static void Prefix(Il2Cpp.UsingAbility __instance)
    {
        if (Session == null) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local?.stateController?.usingAbility?.Pointer == __instance.Pointer)
            NativePlayerAnimationReplica.ClearLocalAttack();
    }
    private static void Postfix(Il2Cpp.UsingAbility __instance, Il2Cpp.Ability _ability, Vector3 target)
    {
        try
        {
            AutoPlayRuntime.CastObserved(__instance,_ability);
            if (Session == null || _ability == null || Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return;
            var local = Il2Cpp.PlayerFinder.getPlayerActor();
            if (local == null || local.stateController?.usingAbility == null || local.stateController.usingAbility.Pointer != __instance.Pointer) return;
            int slot = -1;
            bool zone = false;
            var playerUse = __instance.TryCast<Il2Cpp.UsingAbilityPlayer>();
            if (playerUse != null)
            {
                for (int i = 0; i < 5; i++)
                    if (playerUse.GetNonBufferableAbility(Il2Cpp.BufferableAbilityType.Bar, i)?.Pointer == _ability.Pointer)
                    { slot = i; break; }
                if (slot < 0 && playerUse.GetNonBufferableAbility(Il2Cpp.BufferableAbilityType.Zone, 0)?.Pointer == _ability.Pointer)
                { slot = 0; zone = true; }
            }
            var motion = NativePlayerAnimationReplica.Capture(local);
            // Zone abilities may be replaced by a class-specific evade asset,
            // so pointer equality with the bar's original entry is insufficient.
            if (_ability.name.IndexOf("Evade", StringComparison.OrdinalIgnoreCase) >= 0)
            { zone = true; slot = 0; }
            NativeSkillVisualCapture.TrackCasting(local, _ability, zone);
            int animation = (int)_ability.animation;
            // Mutated class evade abilities override the base asset's animation.
            if (__instance.abilityInfoSet && __instance.abilityInfo != null)
                animation = (int)__instance.abilityInfo.getAbilityAnimation();
            float speed = motion?.AnimationSpeed ?? 1;
            bool actualCommand = NativePlayerAnimationReplica.ReadLocalAttack(out var actualAnimation, out var actualSpeed);
            if (actualCommand) { animation = actualAnimation; speed = actualSpeed; }
            if (Session.PublishSkill(_ability.name, target.x, target.y, target.z, slot, zone,
                !actualCommand && !zone && motion != null && motion.State is 5 or 9 ? motion.AnimationNumber : animation,
                speed)) Sent++;
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Skill capture: " + ex.Message); }
    }
}
