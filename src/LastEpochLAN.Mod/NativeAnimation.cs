using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

internal static class NativeAnimation
{
    private static readonly Dictionary<IntPtr, (float Until, Animator Animator)> found = new();
    private static readonly Dictionary<IntPtr, (IntPtr Controller, (int Hash, int Type)[] Values)> parameters = new();
    private static (int Hash, int Type)[] Parameters(Animator animator)
    {
        var controller = animator.runtimeAnimatorController?.Pointer ?? IntPtr.Zero;
        if (parameters.TryGetValue(animator.Pointer, out var cached) && cached.Controller == controller) return cached.Values;
        // Large controllers put locomotion/combat parameters beyond the first
        // serialized entries. Never spend the wire budget on cosmetic defaults
        // while omitting Move or the controller's state/speed inputs.
        var values = animator.parameters.Where(p => p.type != AnimatorControllerParameterType.Trigger)
            .OrderBy(p=>p.nameHash==Il2Cpp.AnimationManagerWithAnimator.animationHashMove ? 0 :
                new[]{"move","speed","attack","stun","freez","emerg","dead","die","cast","walk","run"}.Any(word=>p.name.IndexOf(word,StringComparison.OrdinalIgnoreCase)>=0) ? 1 : 2)
            .Select(p => (Hash: p.nameHash, Type: (int)p.type)).Take(64).ToArray();
        if (parameters.Count > 1024) parameters.Clear();
        parameters[animator.Pointer] = (controller, values);
        return values;
    }
    internal static Animator Find(Il2Cpp.Actor actor)
    {
        if (actor == null) return null;
        var offlineDriver = actor.animationManager?.TryCast<Il2Cpp.AnimationManagerWithAnimator>();
        if (offlineDriver?.animator != null)
        { found[actor.Pointer] = (Time.unscaledTime + 1, offlineDriver.animator); return offlineDriver.animator; }
        // Gameplay relay drivers can dereference an uninitialized visual owner.
        // Read the visual hierarchy directly, including during spawn/despawn.
        var visuals = actor.actorSync?.actorVisuals;
        var driver = visuals?.animationManager?.TryCast<Il2Cpp.AnimationManagerWithAnimator>();
        if (driver?.animator != null)
        { found[actor.Pointer] = (Time.unscaledTime + 1, driver.animator); return driver.animator; }
        if (found.TryGetValue(actor.Pointer, out var cached) && (cached.Animator != null || Time.unscaledTime < cached.Until)) return cached.Animator;
        var candidates = (visuals != null ? visuals.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>())
            .Concat(actor.GetComponentsInChildren<Animator>(true))
            .Concat(actor.actorSync != null ? actor.actorSync.GetComponentsInChildren<Animator>(true) : Array.Empty<Animator>());
        // Keep an Animator whose controller is still loading. Repeated hierarchy
        // searches do not make it load faster; Capture returns null until ready.
        var result = candidates.FirstOrDefault(animator => animator != null);
        if (found.Count > 1024) found.Clear();
        found[actor.Pointer] = (Time.unscaledTime + 5 + (uint)actor.Pointer.ToInt64() % 1993 / 1000f, result); return result;
    }

    internal static AnimationSnapshot Capture(Il2Cpp.Actor actor, bool includeParameters = true, int parameterLimit = 64) => Capture(Find(actor), includeParameters, parameterLimit);

    internal static AnimationSnapshot Capture(Animator animator, bool includeParameters = true, int parameterLimit = 64)
    {
        if (animator == null || animator.layerCount == 0) return null;
        var layers = new AnimationLayer[Math.Min(animator.layerCount, 8)];
        for (int layer = 0; layer < layers.Length; layer++)
        {
            var state = animator.GetCurrentAnimatorStateInfo(layer);
            float weight=animator.GetLayerWeight(layer);
            layers[layer] = new AnimationLayer { Hash = state.fullPathHash, Time = state.fullPathHash==0 && !float.IsFinite(state.normalizedTime) ? 0 : state.normalizedTime,
                Weight = float.IsFinite(weight) ? Mathf.Clamp01(weight) : 0 };
        }
        var parameters = !includeParameters ? Array.Empty<AnimationParameter>() : Parameters(animator)
            .Take(parameterLimit).Select(parameter => new AnimationParameter {
                Hash = parameter.Hash, Type = parameter.Type,
                Float = parameter.Type == 1 ? animator.GetFloat(parameter.Hash) : 0,
                Int = parameter.Type == 3 ? animator.GetInteger(parameter.Hash) : 0,
                Bool = parameter.Type == 4 && animator.GetBool(parameter.Hash)
            }).Where(parameter=>parameter.IsValid()).GroupBy(parameter=>parameter.Hash).Select(group=>group.First()).ToArray();
        var result = new AnimationSnapshot { Speed = animator.speed, Layers = layers, Parameters = parameters };
        return result.IsValid() ? result : null;
    }

    internal static void Apply(Animator animator, AnimationSnapshot snapshot, bool forcePhase = false,
        bool correctDrift = true, AnimationSnapshot previous = null)
    {
        if (animator == null || animator.runtimeAnimatorController == null || snapshot == null) return;
        animator.enabled = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = snapshot.Speed;
        var supported = snapshot.Parameters.Length == 0 ? null : Parameters(animator);
        foreach (var parameter in snapshot.Parameters)
        {
            if (!supported.Any(p => p.Hash == parameter.Hash && p.Type == parameter.Type)) continue;
            int type = parameter.Type;
            switch (type)
            {
                case 1: animator.SetFloat(parameter.Hash, parameter.Float); break;
                case 3: animator.SetInteger(parameter.Hash, parameter.Int); break;
                case 4: animator.SetBool(parameter.Hash, parameter.Bool); break;
            }
        }
        for (int layer = 0; layer < Math.Min(animator.layerCount, snapshot.Layers.Length); layer++)
        {
            var incoming = snapshot.Layers[layer];
            if (incoming.Hash == 0 || !animator.HasState(layer, incoming.Hash)) continue;
            animator.SetLayerWeight(layer, incoming.Weight);
            var current = animator.GetCurrentAnimatorStateInfo(layer);
            // Let animations advance between packets; repeated Play resets native animation events.
            // Packet latency/jitter must not seek a running clip backwards on
            // every 10 Hz world snapshot. Correct only a large phase mismatch.
            bool restarted = previous != null && layer < previous.Layers.Length &&
                previous.Layers[layer].Hash == incoming.Hash && incoming.Time + 0.05f < previous.Layers[layer].Time;
            if (forcePhase || restarted || current.fullPathHash != incoming.Hash || (correctDrift && Math.Abs(current.normalizedTime - incoming.Time) > 0.75f))
                animator.Play(incoming.Hash, layer, incoming.Time);
        }
    }
}
