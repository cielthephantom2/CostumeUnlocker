using LastEpochLAN.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

// Base-class appearance only. This is deliberately not a second local character:
// no tracker, inventory, input, health, AI, or persistent character initialization.
internal static class ClassVisualReplica
{
    internal static string CaptureError { get; private set; } = "";
    private static float nextCaptureWarning;
    private static IntPtr appearanceActor;
    private static EquipmentAppearance[] lastEquipment = Array.Empty<EquipmentAppearance>();
    private static string lastBack = "";
    private static CosmeticPrefab lastFootprint, lastAura;
    private static float nextEquipmentCapture;

#if LELAN_QA
    internal static object Smoke()
    {
        var reports = new List<object>();
        for (int classId = 0; classId <= 4; classId++)
        {
            var root = Create(classId);
            try
            {
                int scripts = root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Count(script => script.TryCast<Il2CppLE.AssetBundles.LoadRefComponent>() == null &&
                        script.TryCast<Il2Cpp.AnimationManagerWithAnimator>() == null &&
                        script.TryCast<Il2Cpp.IMSlotManager>() == null && script.TryCast<Il2Cpp.IMSlot>() == null &&
                        script.TryCast<Il2Cpp.BodyHiding>() == null && script.TryCast<Il2CppAnimationLoader.AnimationLoader>() == null &&
                        script.TryCast<Il2Cpp.WeaponJoint>() == null && script.TryCast<Il2Cpp.EquipTypeAngles>() == null &&
                        script.TryCast<Il2Cpp.WeaponJointDirectionPointer>() == null);
                int colliders = root.GetComponentsInChildren<Collider>(true).Count(collider => collider.enabled);
                int activeDrivers = root.GetComponentsInChildren<MonoBehaviour>(true).Count(script =>
                    script.TryCast<Il2Cpp.AnimationManagerWithAnimator>() != null && script.enabled);
                if (scripts != 0 || colliders != 0 || activeDrivers != 0)
                    throw new InvalidOperationException("Visual-only replica retained active drivers, gameplay scripts or colliders.");
                reports.Add(new { classId, renderers = root.GetComponentsInChildren<Renderer>(true).Length,
                    animators = root.GetComponentsInChildren<Animator>(true).Length, gameplayScripts = scripts, enabledColliders = colliders });
            }
            finally { UObject.Destroy(root); }
        }
        return reports;
    }
#endif

    internal static GameObject Create(int classId)
    {
        if (classId is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(classId));
        var original = Il2Cpp.PlayerFinder.getPlayerActor();
        GameObject root = null;
        try
        {
            // The native driver owns serialized controller/animation references.
            // Keep it disabled and drive its Animator from snapshots; removing
            // the driver before Awake can leave the replica without a controller.
            var visual = new NativeClassAnimator(classId, true);
            root = visual.Root;
            root.name = "LAN class model " + classId;
            if (root.GetComponentsInChildren<Renderer>(true).Length == 0) throw new InvalidOperationException("Class model contains no renderers.");
            if (Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer != original?.Pointer)
                throw new InvalidOperationException("Local player changed during visual creation.");
            return root;
        }
        catch { UObject.Destroy(root); throw; }
    }

    internal static AvatarState Capture(Il2Cpp.Actor actor)
    {
        if (actor == null || actor.characterDataTracker?.charData == null) return null;
        int classId = actor.characterDataTracker.charData.CharacterClass;
        if (classId is < 0 or > 4) return null;
        // GameplayRelayAnimationManager can legitimately have no Animator. The live
        // skeleton is owned by ActorVisuals, not by the gameplay Actor's relay.
        CaptureError = "";
        if (appearanceActor != actor.Pointer)
        { appearanceActor = actor.Pointer; lastEquipment = Array.Empty<EquipmentAppearance>(); lastBack = ""; lastFootprint=null;lastAura=null; }
        AnimationSnapshot animation = null;
        NativeMotionState motion = null;
        SkillVisualState[] visuals = Array.Empty<SkillVisualState>();
        try { motion = NativePlayerAnimationReplica.Capture(actor); }
        catch (Exception ex) { RecordCaptureError("native motion", ex); }
        if (motion == null || motion.State is 5 or 9)
            try { animation = NativeAnimation.Capture(actor, motion == null); }
            catch (Exception ex) { RecordCaptureError("animation", ex); }
        try { visuals = NativeSkillVisualCapture.Capture(); }
        catch (Exception ex) { RecordCaptureError("skill visuals", ex); }
        try
        {
            if (Time.unscaledTime >= nextEquipmentCapture || lastEquipment.Length == 0)
            {
                nextEquipmentCapture = Time.unscaledTime + 0.5f;
                var appearance = NativeEquipmentReplica.Capture(actor);
                lastEquipment = appearance.Equipment; lastBack = appearance.Back;
                var cosmetics=NativeCosmeticReplica.Capture(actor); lastFootprint=cosmetics.Foot; lastAura=cosmetics.Aura;
            }
        }
        catch (Exception ex) { RecordCaptureError("equipment", ex); }
        return new AvatarState { ClassId = classId, Animation = animation, Motion = motion, Visuals = visuals,
            Equipment = lastEquipment.ToArray(), BackCosmetic = lastBack, Footprint=lastFootprint,Aura=lastAura,
            AnimationHash = animation?.Layers[0].Hash ?? 0, AnimationTime = animation?.Layers[0].Time ?? 0 };
    }

    private static void RecordCaptureError(string stage, Exception exception)
    {
        CaptureError += stage + ": " + exception.Message + "; ";
        if (Time.unscaledTime < nextCaptureWarning) return;
        nextCaptureWarning = Time.unscaledTime + 5;
        MelonLoader.MelonLogger.Warning("Optional avatar " + stage + " capture failed; LAN remains connected. " + exception);
    }

    internal static void ApplyAnimation(GameObject root, AvatarState state)
    {
        if (state == null) return;
        foreach (var animator in root.GetComponentsInChildren<Animator>(true))
            if (state.Animation != null) NativeAnimation.Apply(animator, state.Animation);
            else if (state.AnimationHash != 0 && animator.runtimeAnimatorController != null && animator.layerCount > 0 && animator.HasState(0, state.AnimationHash))
                animator.Play(state.AnimationHash, 0, state.AnimationTime);
    }
}
