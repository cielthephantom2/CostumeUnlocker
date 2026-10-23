using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;
using SoftPrefab = Il2CppLE.AssetBundles.SoftRef<UnityEngine.GameObject>;

namespace LastEpochLAN.Mod;

// Spawn commands are reliable events; the receiving game owns visual lifecycle,
// material animation, pooling and native mutation. No gameplay ability is cast.
internal static class NativeVisualEvents
{
    [ThreadStatic] internal static bool SuppressSnapshots;
    [ThreadStatic] private static bool replaying;
    internal static bool Replaying => replaying;
    private sealed record Pending(string Scene, NativeVisualCommand Command, float Until);
    private static readonly Queue<Pending> pending = new();
    private static readonly Dictionary<(ulong, ulong, string), Il2CppLE.AssetBundles.LoadRef<GameObject>> assets = new();
    internal static long Sent, Received, Played, Dropped;
    internal static long HookCalls, UnownedCalls, NoPrefabCalls, DeferredCalls, DeferredOwned;
    internal static long ReplayRenderers;
    internal static long ReplayParticles;
    private static readonly bool renderProbe = Environment.GetCommandLineArgs().Contains("--lelan-vfx-smoke");
    private static readonly List<(GameObject Root, float Until)> probeRoots = new();
    internal static void ObserveReplayRoot(GameObject root)
    {
        if (renderProbe && replaying && root != null && probeRoots.Count < 32)
            probeRoots.Add((root, Time.unscaledTime + 3));
    }
    internal static string LastUnowned = "";
    internal static Il2Cpp.Actor Resolve(Il2Cpp.CreationReferences refs)
    {
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (refs?.creator != null) return refs.creator;
        if (refs?.syncCreator != null && local?.actorSync?.Pointer == refs.syncCreator.Pointer) return local;
        return refs?.syncCreator?.GameplayActor;
    }
    internal static string Error = "";
    private static string activeScene = "";
    internal static bool IsOwned(Il2Cpp.Actor actor)
    {
        if (actor == null) return false;
        if (SkillCapture.Session?.IsHost == true && WorldSynchronizer.Active?.CanCaptureEnemyVisuals(actor) == true) return true;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        return local != null && (actor.Pointer == local.Pointer || actor.IsMinion() && actor.IsMinionOf(local));
    }

    internal static void Capture(System.Reflection.MethodBase method, object[] args)
    {
        HookCalls++;
        var session = SkillCapture.Session;
        if (session == null || replaying || SuppressSnapshots) return;
        var parameters = method.GetParameters();
        object Find(string name) { for (int i = 0; i < parameters.Length; i++) if (parameters[i].Name == name) return args[i]; return null; }
        T Value<T>(string name, T fallback = default) => Find(name) is T value ? value : fallback;
        VisualPoint Point(string name) => Find(name) is Vector3 v ? NativeSkillVisualCapture.Point(v) : null;
        var sync = Value<Il2Cpp.ActorSync>("actorSync");
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        var owner = sync != null && local?.actorSync?.Pointer == sync.Pointer ? local : sync?.GameplayActor ?? NativeSkillVisualCapture.ConstructionOwner;
        if (!IsOwned(owner)) { UnownedCalls++; LastUnowned = method.Name; return; }
        var prefab = Value<SoftPrefab>("prefabSoftRef");
        if (prefab == null && Find("prefab") is Il2Cpp.VFXRef vfx) prefab = vfx.GetSoftRef();
        if (prefab == null) { NoPrefabCalls++; return; }
        var ability = Value<Il2Cpp.Ability>("ability") ?? NativeSkillVisualCapture.ConstructionAbility;
        var command = new NativeVisualCommand { Guid0 = prefab.Guid.P0, Guid1 = prefab.Guid.P1, SubPath = prefab.SubPath ?? "",
            AbilityKey = ability == null ? 0 : new Il2Cpp.AbilityRef(ability).key,
            Position = Point("position") ?? new(), Rotation = NativeSkillVisualCapture.Point(Value<Quaternion>("rotation", Quaternion.identity).eulerAngles),
            Scale = Point("scaleMultiplier") ?? new VisualPoint { X = 1, Y = 1, Z = 1 },
            Origin = Point("locationCreatedFrom"), Start = Point("startLocation"), Target = Point("targetLocation"), TargetOffset = Point("targetLocationOffset") ?? new(),
            StretchFromOrigin = method.Name.EndsWith("SpawnSOSTLCF", StringComparison.Ordinal), UseAutoPool = Value<bool>("useAutoPool", true),
            Mutable = method.Name.Contains("Mutable", StringComparison.Ordinal), ApplyMutation = Value<bool>("isMutable"), Modifiers = new[] { Value<float>("increasedRadius"), Value<float>("increasedHeight"),
                Value<float>("increasedArcSize"), Value<float>("increasedDuration"), Value<float>("increasedLength"),
                Value<float>("increasedParticleLifetime"), Value<float>("increasedWidth") },
            ToggleEnum = Value<int>("toggleEnumEnumId"), ToggleMask = Value<ulong>("toggleEnumBitMask"),
            TimingMode = Value<ulong>("particleSystemsTimingsMode"), TimingLo = Value<ulong>("particleSystemsTimingsVLo"), TimingHi = Value<ulong>("particleSystemsTimingsVHi") };
        if (command.IsValid() && session.PublishSkill("native-vfx", command.Position.X, command.Position.Y, command.Position.Z, nativeVisual: command))
        { Sent++; SuppressSnapshots = true; }
        else Dropped++;
    }

    internal static void Receive(SkillEvent skill)
    {
        if (skill.NativeVisual == null) return;
        Received++;
        if (pending.Count >= 128) { Dropped++; return; }
        pending.Enqueue(new Pending(skill.Scene, skill.NativeVisual, Time.unscaledTime + 5));
    }

    internal static void Tick(string scene)
    {
        if (renderProbe)
        {
            for (int i = probeRoots.Count - 1; i >= 0; i--)
            {
                var item = probeRoots[i];
                if (item.Root == null || Time.unscaledTime > item.Until) { probeRoots.RemoveAt(i); continue; }
                ReplayRenderers = Math.Max(ReplayRenderers, item.Root.GetComponentsInChildren<Renderer>(true).Count(r => r != null && r.enabled && r.gameObject.activeInHierarchy));
                ReplayParticles = Math.Max(ReplayParticles, item.Root.GetComponentsInChildren<ParticleSystem>(true).Sum(p => p == null ? 0 : p.particleCount));
            }
        }
        if (activeScene != scene)
        { foreach (var asset in assets.Values) asset.Dispose(); assets.Clear(); activeScene = scene; }
        int count = Math.Min(pending.Count, 8);
        for (int i = 0; i < count; i++)
        {
            var next = pending.Dequeue();
            if (next.Scene != scene || next.Until < Time.unscaledTime) { Dropped++; continue; }
            try
            {
                var c = next.Command; var key = (c.Guid0, c.Guid1, c.SubPath);
                var prefab = new SoftPrefab(new Il2CppLE.AssetBundles.Guid(c.Guid0, c.Guid1), c.SubPath);
                if (!assets.TryGetValue(key, out var asset))
                {
                    if (assets.Count >= 64) { Dropped++; Error = "Native VFX asset cache limit reached."; continue; }
                    assets[key] = asset = Il2CppLE.AssetBundles.SoftRefExtensions.CreateLoadRef<GameObject>(prefab, "LastEpochLAN.NativeVFX", 0);
                }
                if (asset.Status == Il2CppLE.AssetBundles.AssetStatus.Loading) { pending.Enqueue(next); continue; }
                var template = asset.AssetOrNull;
                if (template == null) throw new InvalidOperationException("Native VFX asset failed to load.");
                if (template.GetComponentsInChildren<Il2Cpp.Actor>(true).Length > 0 ||
                    template.GetComponentsInChildren<Il2Cpp.DamageStatsHolder>(true).Length > 0 ||
                    template.GetComponentsInChildren<Il2Cpp.AbilityObjectConstructor>(true).Length > 0)
                    throw new InvalidOperationException("VFX command rejected a gameplay prefab.");
                replaying = true; SuppressSnapshots = true;
                Replay(prefab, c); Played++;
            }
            catch (Exception ex) { Dropped++; Error = ex.ToString(); }
            finally { replaying = false; SuppressSnapshots = false; }
        }
    }

    private static void Replay(SoftPrefab prefab, NativeVisualCommand c)
    {
        var p = NativeSkillVisualCapture.Vector(c.Position); var r = Quaternion.Euler(NativeSkillVisualCapture.Vector(c.Rotation));
        var origin = c.Origin == null ? p : NativeSkillVisualCapture.Vector(c.Origin);
        var start = c.Start == null ? p : NativeSkillVisualCapture.Vector(c.Start);
        var target = c.Target == null ? p : NativeSkillVisualCapture.Vector(c.Target);
        var offset = NativeSkillVisualCapture.Vector(c.TargetOffset); var m = c.Modifiers;
        var ability = c.AbilityKey == 0 ? null : new Il2Cpp.AbilityRef(c.AbilityKey).GetAbility();
        if (c.Mutable && c.Target != null && c.Origin != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnMutableVfxWithCreationReferencesAndLocationDetector(prefab, p, r, c.UseAutoPool, c.ApplyMutation,
                m[0], m[1], m[2], m[3], m[4], null, origin, ability, false, start, offset, target, m[5], m[6],
                c.ToggleEnum, c.ToggleMask, c.TimingMode, c.TimingLo, c.TimingHi);
        else if (c.Mutable && c.Target != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnMutableVfxWithLocationDetector(prefab, p, r, c.UseAutoPool, c.ApplyMutation,
                m[0], m[1], m[2], m[3], m[4], start, offset, target, m[5], m[6], c.ToggleEnum, c.ToggleMask, c.TimingMode, c.TimingLo, c.TimingHi);
        else if (c.Mutable && c.Origin != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnMutableVfxWithCreationReferences(prefab, p, r, c.UseAutoPool, c.ApplyMutation,
                m[0], m[1], m[2], m[3], m[4], null, origin, ability, false, m[5], m[6], c.ToggleEnum, c.ToggleMask, c.TimingMode, c.TimingLo, c.TimingHi);
        else if (c.Mutable)
            Il2Cpp.GlobalVFXManager.RecieveSpawnMutableVfx(prefab, p, r, c.UseAutoPool, c.ApplyMutation,
                m[0], m[1], m[2], m[3], m[4], m[5], m[6], c.ToggleEnum, c.ToggleMask, c.TimingMode, c.TimingLo, c.TimingHi);
        else if (c.Target != null && c.Origin != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnVfxWithCreationReferencesAndLocationDetector(prefab, p, r, c.UseAutoPool, null, origin, ability, false, start, offset, target);
        else if (c.Target != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnVfxWithLocationDetector(prefab, p, r, c.UseAutoPool, start, offset, target);
        else if (c.StretchFromOrigin)
            Il2Cpp.GlobalVFXManager.RecieveSpawnSOSTLCF(prefab, p, origin);
        else if (c.Origin != null)
            Il2Cpp.GlobalVFXManager.RecieveSpawnVfxWithCreationReferences(prefab, p, r, c.UseAutoPool, null, origin, ability, false);
        else Il2Cpp.GlobalVFXManager.RecieveSpawnVfxScale(prefab, p, r, c.UseAutoPool, NativeSkillVisualCapture.Vector(c.Scale));
    }

    internal static void Reset()
    {
        pending.Clear(); foreach (var asset in assets.Values) asset.Dispose(); assets.Clear();
        probeRoots.Clear();
        SuppressSnapshots = false; replaying = false; activeScene = "";
    }
}

[HarmonyPatch(typeof(Il2Cpp.HitsTargets), nameof(Il2Cpp.HitsTargets.TryHitTargets))]
internal static class NativeHitVfxOwnerScopePatch
{
    private static void Prefix(Il2Cpp.HitsTargets __instance, out (Il2Cpp.Actor, Il2Cpp.Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        if (SkillCapture.Session == null || NativeVisualEvents.Replaying) return;
        var refs = __instance.creationReferences ?? __instance.GetComponent<Il2Cpp.CreationReferences>();
        var owner = NativeVisualEvents.Resolve(refs);
        if (!NativeVisualEvents.IsOwned(owner)) return;
        NativeSkillVisualCapture.ConstructionOwner = owner;
        NativeSkillVisualCapture.ConstructionAbility = refs.thisAbility ?? refs.originalAbility;
    }
    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    { NativeSkillVisualCapture.ConstructionOwner = __state.Owner; NativeSkillVisualCapture.ConstructionAbility = __state.Ability; }
}

#if LELAN_QA
[HarmonyPatch(typeof(Il2Cpp.GlobalVFXManager), nameof(Il2Cpp.GlobalVFXManager.HandleAutoPool))]
internal static class NativeReplayRendererProbePatch
{
    private static void Postfix(GameObject go)
    {
        if (NativeVisualEvents.Replaying && go != null)
            NativeVisualEvents.ReplayRenderers += go.GetComponentsInChildren<Renderer>(true).Count(r => r != null && r.enabled && r.gameObject.activeInHierarchy);
    }
}

#endif

// The native reference helper dereferences actorSync even when attachToCaster is
// false. Remote presentation deliberately has no gameplay actor or ActorSync.
// Supply only the visual creation data for this narrowly scoped replay call.
[HarmonyPatch(typeof(Il2Cpp.GlobalVFXManager), nameof(Il2Cpp.GlobalVFXManager.HandleSettingCreationReferences))]
internal static class NativeVisualReplayReferencesPatch
{
    private static bool Prefix(GameObject go, Il2Cpp.ActorSync actorSync, Vector3 locationCreatedFrom,
        Il2Cpp.Ability ability, ref Il2Cpp.CreationReferences __result)
    {
        if (!NativeVisualEvents.Replaying || actorSync != null) return true;
        var refs = go.GetComponent<Il2Cpp.CreationReferences>() ?? go.AddComponent<Il2Cpp.CreationReferences>();
        refs.creator = null; refs.syncCreator = null;
        refs.locationCreatedFrom = locationCreatedFrom; refs.thisAbility = ability;
        refs.originalAbility = null;
        refs.oType = (Il2Cpp.CreationReferences.OType)2; refs.useType = default;
        __result = refs;
        return false;
    }
}

[HarmonyPatch]
internal static class NativeVisualSpawnCommandPatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() => typeof(Il2Cpp.GlobalVFXManager).GetMethods()
        .Where(m => m.Name.StartsWith("RecieveSpawnVfx", StringComparison.Ordinal) ||
            m.Name.StartsWith("RecieveSpawnMutableVfx", StringComparison.Ordinal) || m.Name == "RecieveSpawnSOSTLCF" ||
            m.Name.StartsWith("SendSpawnVfx", StringComparison.Ordinal) || m.Name.StartsWith("SendSpawnMutableVfx", StringComparison.Ordinal) || m.Name == "SendSpawnSOSTLCF");
    private static void Prefix(System.Reflection.MethodBase __originalMethod, object[] __args, out bool __state)
    {
        __state = NativeVisualEvents.SuppressSnapshots;
        try { NativeVisualEvents.Capture(__originalMethod, __args); }
        catch (Exception ex) { NativeVisualEvents.Error = ex.ToString(); }
    }
    private static void Finalizer(bool __state) => NativeVisualEvents.SuppressSnapshots = __state;
}

[HarmonyPatch(typeof(Il2Cpp.CreateVfxOnDeath), nameof(Il2Cpp.CreateVfxOnDeath.createObjects))]
internal static class NativeDeferredVfxOwnerScopePatch
{
    private static void Prefix(Il2Cpp.CreateVfxOnDeath __instance, out (Il2Cpp.Actor, Il2Cpp.Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        NativeVisualEvents.DeferredCalls++;
        if (SkillCapture.Session == null) return;
        var refs = __instance.references ?? __instance.GetComponent<Il2Cpp.CreationReferences>();
        var owner = NativeVisualEvents.Resolve(refs);
        if (!NativeVisualEvents.IsOwned(owner)) return;
        NativeVisualEvents.DeferredOwned++;
        NativeSkillVisualCapture.ConstructionOwner = owner;
        NativeSkillVisualCapture.ConstructionAbility = refs.thisAbility ?? refs.originalAbility;
    }
    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    { NativeSkillVisualCapture.ConstructionOwner = __state.Owner; NativeSkillVisualCapture.ConstructionAbility = __state.Ability; }
}
