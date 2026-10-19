using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;

namespace LastEpochLAN.Mod;

// An independently owned target/collision Actor. No player input, inventory,
// CharacterDataTracker, PlayerFinder registration, quest or save components.
internal sealed class NativeGuestCombat
{
    private sealed record PeerActor(GameObject Root, Il2Cpp.Actor Actor, string Id)
    {
        internal Il2Cpp.BaseHealth Health;
        internal readonly List<Il2Cpp.DList<Il2Cpp.Actor>> Registries = new();
    }
    private readonly Dictionary<string, PeerActor> peers = new();
    private static readonly Dictionary<IntPtr, PeerActor> owned = new();
    private static readonly List<PeerActor> retired = new();
    internal static int RetiredCount => retired.Count;
    internal static NativeGuestCombat Active;
    private LanSession session;
    private string scene = "";
    private HarmonyLib.Harmony combatHooks;
    internal bool HooksInstalled => combatHooks != null;
    internal string Error { get; private set; } = "";
    internal int Count => peers.Count;
    internal long Sent { get; private set; }
    internal long Applied { get; private set; }
    internal string TargetScene { get; private set; } = "";
    internal bool RegistrationVerified { get; private set; }
    internal static bool Owns(Il2Cpp.Actor actor) => actor is not null && owned.ContainsKey(actor.Pointer);
    internal static bool Unavailable(Il2Cpp.Actor actor) => owned.TryGetValue(actor.Pointer, out var peer) &&
        (peer.Root == null || !peer.Root.activeInHierarchy || actor.neverValidTarget);

    internal void Tick(LanSession room, string zone)
    {
        PruneRetired();
        if (session != room) { Reset(); session = room; scene = zone; Active = this; }
        else if (scene != zone)
        {
            // Keep the detours across travel. Reinstalling them on the Unity
            // thread causes a large hitch, and stale scene targets may be gone.
            foreach (var id in peers.Keys.ToArray()) Remove(id);
            scene = zone;
        }
        try
        {
            if (!room.IsHost)
            {
                var local = Il2Cpp.PlayerFinder.getPlayerActor();
                while (room.TryTakeDamage(out var hit))
                {
                    if (local?.protection == null || local.isDying(false) || hit.Scene != zone || hit.Epoch != room.WorldInfo.Epoch) continue;
                    ApplyDamage(local, hit); Applied++;
                }
                return;
            }
            var states = room.PlayerPositions.Where(p => p.Id != room.LocalId && p.Available && !p.Dead && p.Scene == zone && p.MaxHealth > 0).ToArray();
            var ids = states.Select(p => p.Id).ToHashSet();
            foreach (var id in peers.Keys.Where(id => !ids.Contains(id)).ToArray()) Remove(id);
            var player = Il2Cpp.PlayerFinder.getPlayerActor();
            if (player == null || zone == "") return;
            if (states.Length > 0) InstallHooks();
            foreach (var state in states)
            {
                if (!peers.TryGetValue(state.Id, out var peer))
                {
                    var root = new GameObject("LAN combat target " + state.Id); root.SetActive(false);
                    // New objects otherwise inherit the active UI/bootstrap
                    // scene. Native TargetFinder excludes non-combat scenes.
                    var combatScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(zone);
                    if (!combatScene.IsValid() || !combatScene.isLoaded) combatScene = player.gameObject.scene;
                    if (!combatScene.IsValid() || Il2Cpp.SceneList.IsNonCombatZone(combatScene.name))
                    { UnityEngine.Object.Destroy(root); throw new InvalidOperationException("Guest target requires a loaded combat scene: " + combatScene.name); }
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, combatScene);
                    TargetScene = combatScene.name;
                    MelonLoader.MelonLogger.Msg("Guest combat: constructing private target.");
                    var actor = root.AddComponent<Il2Cpp.Actor>();
                    peer = new PeerActor(root, actor, state.Id); owned[actor.Pointer] = peer; peers[state.Id] = peer;
                    actor.enabled = false; actor.tForm = root.transform; actor.data = player.data;
                    actor.isPlayerActor = true; actor.initialised = true; actor.neverValidTarget = false;
                    actor.isPendingSpawn = false; actor.unscaledRadius = actor.initialRadius = 0.5f;
                    actor.alignmentManager = root.AddComponent<Il2Cpp.AlignmentManager>();
                    actor.alignmentManager.alignment = player.GetAlignment();
                    actor.health = root.AddComponent<Il2Cpp.BaseHealth>(); actor.health.actor = actor;
                    peer.Health = actor.health;
                    actor.health.tForm = root.transform; actor.health.enabled = false;
                    actor.health.canDie = false; actor.health.damageable = true;
                    actor.protection = root.AddComponent<Il2Cpp.ProtectionClass>();
                    actor.protection.actor = actor; actor.protection.enabled = false;
                    var localCollider = player.GetComponentInChildren<Collider>();
                    root.layer = localCollider != null ? localCollider.gameObject.layer : player.gameObject.layer;
                    var collider = root.AddComponent<CapsuleCollider>();
                    collider.radius = 0.5f; collider.height = 1.8f; collider.center = Vector3.up * 0.9f;
                    actor.colliders = new Il2CppSystem.Collections.Generic.List<Collider>(); actor.colliders.Add(collider);
                    // Set world pose and health before Awake adds BaseHealth to
                    // the native target registry. Every component is privately owned.
                    Update(peer, state); root.SetActive(true);
                    if (!Il2Cpp.BaseHealth.all.Contains(actor.health)) Il2Cpp.BaseHealth.all.Add(actor.health);
                    Il2Cpp.ActorManager.Add(actor);
                    CacheRegistries(peer);
                    var registered = Il2Cpp.ActorManager.instance.GetActorsOfAlignment(actor.GetAlignment());
                    RegistrationVerified = registered.Cast<Il2CppSystem.Collections.Generic.ICollection<Il2Cpp.Actor>>().Contains(actor);
                    if (!RegistrationVerified) throw new InvalidOperationException("Guest ActorManager registration was not retained.");
                    MelonLoader.MelonLogger.Msg("Guest combat: private target activated and health registered.");
                }
                else Update(peer, state);
            }
        }
        catch (Exception ex) { Error = ex.ToString(); }
    }
    private void InstallHooks()
    {
        if (combatHooks != null) return;
        var harmony = new HarmonyLib.Harmony("LastEpochLAN.GuestCombat.Runtime");
        try
        {
            void Patch(Type original, string method, Type prefix, Type[] args = null)
            {
                var target = args == null ? AccessTools.Method(original, method) : AccessTools.Method(original, method, args);
                if (target == null) throw new MissingMethodException(original.FullName, method);
                MelonLoader.MelonLogger.Msg("Guest combat: installing " + original.Name + "." + method);
                harmony.Patch(target, prefix: new HarmonyMethod(AccessTools.Method(prefix, "Prefix")));
            }
            foreach (var name in new[] { "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy" })
                Patch(typeof(Il2Cpp.Actor), name, typeof(NativeGuestActorLifecyclePatch));
            // Actor.isDying is a 22-byte conditional tail-call thunk into
            // Dying.isDying. Leave it untouched. Our target has no Dying
            // component, so the original native method returns false safely;
            // dead peers are removed rather than running a dummy death state.
            Patch(typeof(Il2Cpp.Actor), nameof(Il2Cpp.Actor.isInvalidTarget), typeof(NativeGuestTargetValidityPatch));
            Patch(typeof(Il2Cpp.Actor), nameof(Il2Cpp.Actor.ITargetableActor_isInvalidTarget), typeof(NativeGuestTargetInterfacePatch));
            Patch(typeof(Il2Cpp.Actor), nameof(Il2Cpp.Actor.GetUnimportance), typeof(NativeGuestTargetPriorityPatch));
            Patch(typeof(Il2Cpp.ProtectionClass), nameof(Il2Cpp.ProtectionClass.Awake), typeof(NativeGuestProtectionAwakePatch));
            Patch(typeof(Il2Cpp.DamageStatsHolder), nameof(Il2Cpp.DamageStatsHolder.applyDamage), typeof(NativeGuestHolderDamagePatch), new[] { typeof(Il2Cpp.Actor) });
            Patch(typeof(Il2Cpp.DamageStatsHolder), nameof(Il2Cpp.DamageStatsHolder.applyDamage), typeof(NativeGuestHolderProtectionDamagePatch), new[] { typeof(Il2Cpp.ProtectionClass), typeof(float) });
            // Do not detour ProtectionClass.ApplyDamage: its native ValueTuple
            // return needs a struct ABI trampoline; bool/void holder hooks suffice.
            combatHooks = harmony;
            MelonLoader.MelonLogger.Msg("Guest combat: runtime hooks installed; Actor.isDying remains native.");
        }
        catch { harmony.UnpatchSelf(); throw; }
    }
    internal void RefreshActivation(Il2Cpp.Actor enemy)
    {
        if (session?.IsHost != true || peers.Count == 0) return;
        var ability = enemy.stateController?.usingAbility;
        if (ability == null || ability.activated || !ability.initialised || enemy.stateController.currentState?.cannotActivateCombatInState() == true) return;
        var position = enemy.position();
        foreach (var peer in peers.Values)
        {
            if (!(bool)peer.Root || !peer.Root.activeInHierarchy || peer.Actor.neverValidTarget) continue;
            if ((peer.Root.transform.position - position).sqrMagnitude > 35 * 35) continue;
            // Call the game's exact alignment/range/height activation check,
            // once per existing host world sample. Never taunt or force a target.
            ability.activateIfEnemyInRange();
            break;
        }
    }
    private static void Update(PeerActor peer, PlayerState state)
    {
        peer.Root.transform.SetPositionAndRotation(new Vector3(state.X, state.Y, state.Z), Quaternion.Euler(0, state.Yaw, 0));
        peer.Actor.health.maxHealth = state.MaxHealth; peer.Actor.health.currentHealth = state.Health;
    }
    internal static bool Forward(Il2Cpp.Actor target, Il2Cpp.DamageStats stats, float modifier)
    {
        if (target == null || !owned.TryGetValue(target.Pointer, out var peer)) return false;
        try
        {
            if (peer.Root == null || !peer.Root.activeInHierarchy) return true;
            if (stats == null || Active?.session?.IsHost != true) return true;
            var amounts = Enumerable.Range(0, 7).Select(i => stats.damage[i]).ToArray();
            var penetration = Enumerable.Range(0, 7).Select(i => stats.penetration != null ? stats.getPenetration(i) : 0).ToArray();
            var hit = new IncomingDamage { Epoch = Active.session.WorldInfo.Epoch, Scene = Active.scene, Damage = amounts,
                Penetration = penetration, Modifier = modifier, IsHit = stats.isHit, CritChance = stats.critChance,
                CritMultiplier = stats.critMultiplier, Tags = (int)stats.getTags(), StunChance = stats.increasedStunChance,
                StunDuration = stats.increasedStunDuration, FreezeRate = stats.freezeRate };
            if (Active.session.SendDamage(peer.Id, hit)) Active.Sent++;
        }
        catch (Exception ex) { if (Active != null) Active.Error = ex.ToString(); }
        return true;
    }
    private static void ApplyDamage(Il2Cpp.Actor local, IncomingDamage hit)
    {
        var root = new GameObject("LAN received native attack"); root.SetActive(false);
        try
        {
            var source = root.AddComponent<Il2Cpp.DamageEnemiesWithBeam>();
            source.refs = root.AddComponent<Il2Cpp.CreationReferences>();
            source.refs.locationCreatedFrom = local.position();
            var stats = new Il2Cpp.DamageStats { damage = hit.Damage, penetration = new Il2Cpp.DamageStats.Penetration(hit.Penetration),
                isHit = hit.IsHit, critChance = hit.CritChance, critMultiplier = hit.CritMultiplier,
                otherTags = (Il2Cpp.AT)hit.Tags, increasedStunChance = hit.StunChance,
                increasedStunDuration = hit.StunDuration, freezeRate = hit.FreezeRate };
            source.damageStats = stats;
            local.protection.ApplyDamage(stats, source.Cast<Il2Cpp.DamageSource>(), hit.Modifier, null, false);
        }
        finally { UnityEngine.Object.Destroy(root); }
    }
    private void Remove(string id)
    {
        // Consume first: a destroyed native target must never be retried every
        // frame, nor prevent the rest of the room from being cleaned up.
        if (!peers.Remove(id, out var peer)) return;
        // NPC abilities may retain an Actor target across peer travel/removal.
        // Make it invalid before unregistering, but keep its Transform alive
        // until Unity unloads the owning scene. Immediate destruction leaves
        // native AI with a dangling position/health reference.
        try
        {
            // Native ActorManager.Remove checks Unity object validity first and
            // cannot remove a destroyed Actor. Cached DLists remove the managed
            // identity even after scene unload has invalidated its Transform.
            Unregister(peer);
            if (!((bool)peer.Root)) { owned.Remove(peer.Actor.Pointer); return; }
            peer.Actor.neverValidTarget = true;
            if (peer.Actor.health != null) peer.Actor.health.damageable = false;
            peer.Root.SetActive(false);
            retired.Add(peer);
        }
        catch (Exception ex)
        {
            // Scene unload can invalidate the native object during cleanup.
            // Drop bookkeeping even when a native callback rejects access.
            owned.Remove(peer.Actor.Pointer);
            Error = "Target cleanup during travel: " + ex.Message;
        }
    }
    private static void PruneRetired()
    {
        for (int i = retired.Count - 1; i >= 0; i--)
            if (!((bool)retired[i].Root)) { Unregister(retired[i]); owned.Remove(retired[i].Actor.Pointer); retired.RemoveAt(i); }
    }
    private static void CacheRegistries(PeerActor peer)
    {
        var manager = Il2Cpp.ActorManager.instance;
        foreach (var groups in new[] { manager.actors, manager.disabledActors, manager.pendingSpawnActors })
        {
            if (groups == null) continue;
            foreach (var group in groups)
                if (group?.actors != null) peer.Registries.Add(group.actors);
        }
    }
    private static void Unregister(PeerActor peer)
    {
        foreach (var registry in peer.Registries) registry.Remove(peer.Actor);
        if (peer.Health is not null) Il2Cpp.BaseHealth.all.Remove(peer.Health);
    }
    internal static void OnLifecycle(Il2Cpp.Actor actor, string method)
    {
        if (!owned.TryGetValue(actor.Pointer, out var peer) || method is not ("OnDisable" or "OnDestroy")) return;
        Unregister(peer);
        if (Active != null && Active.peers.TryGetValue(peer.Id, out var current) && current.Actor.Pointer == actor.Pointer)
            Active.Remove(peer.Id);
        if (method == "OnDestroy") owned.Remove(actor.Pointer);
    }
    internal void Reset()
    {
        foreach (var id in peers.Keys.ToArray()) Remove(id);
        PruneRetired();
        combatHooks?.UnpatchSelf(); combatHooks = null;
        session = null; scene = ""; if (Active == this) Active = null;
    }
    internal Il2Cpp.Actor FindTarget(string id) => peers.TryGetValue(id, out var peer) ? peer.Actor : null;
    internal static void NativeSmoke()
    {
        var root = new GameObject("LAN destroyed travel target QA"); root.SetActive(false);
        var actor = root.AddComponent<Il2Cpp.Actor>();
        var peer = new PeerActor(root, actor, "travel-qa");
        var combat = new NativeGuestCombat();
        combat.peers.Add(peer.Id, peer); owned.Add(actor.Pointer, peer);
        var registry = new Il2Cpp.DList<Il2Cpp.Actor>("LAN travel target QA", 4);
        registry.Add(actor); peer.Registries.Add(registry);
        peer.Health = root.AddComponent<Il2Cpp.BaseHealth>();
        Il2Cpp.BaseHealth.all.Add(peer.Health);
        UnityEngine.Object.DestroyImmediate(root);
        combat.Remove(peer.Id); combat.Reset(); combat.Reset();
        if (combat.Count != 0 || owned.ContainsKey(actor.Pointer) || registry.Contains(actor) || Il2Cpp.BaseHealth.all.Contains(peer.Health))
            throw new InvalidOperationException($"Destroyed travel target retained: peers={combat.Count}; owned={owned.ContainsKey(actor.Pointer)}; actorList={registry.Contains(actor)}; healthList={Il2Cpp.BaseHealth.all.Contains(peer.Health)}.");
    }
}

internal static class NativeGuestActorLifecyclePatch
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
        new[] { "Awake", "Start", "OnEnable", "OnDisable", "OnDestroy" }.Select(name => AccessTools.Method(typeof(Il2Cpp.Actor), name));
    private static bool Prefix(Il2Cpp.Actor __instance, System.Reflection.MethodBase __originalMethod)
    {
        if (!NativeGuestCombat.Owns(__instance)) return true;
        NativeGuestCombat.OnLifecycle(__instance, __originalMethod.Name);
        return false;
    }
}
internal static class NativeGuestTargetValidityPatch
{
    private static bool Prefix(Il2Cpp.Actor __instance, Il2Cpp.LifeState requiredLifeState, ref bool __result)
    {
        if (!NativeGuestCombat.Owns(__instance)) return true;
        if (NativeGuestCombat.Unavailable(__instance)) { __result = true; return false; }
        bool dead = __instance.health.currentHealth <= 0;
        __result = !__instance.gameObject.activeInHierarchy || requiredLifeState == Il2Cpp.LifeState.PropTarget ||
            (requiredLifeState == Il2Cpp.LifeState.Alive && dead) || (requiredLifeState == Il2Cpp.LifeState.Dead && !dead) ||
            (requiredLifeState == Il2Cpp.LifeState.Injured && (__instance.health.currentHealth >= __instance.health.maxHealth || dead));
        return false;
    }
}
internal static class NativeGuestTargetInterfacePatch
{
    private static bool Prefix(Il2Cpp.Actor __instance, ref bool __result)
    {
        if (!NativeGuestCombat.Owns(__instance)) return true;
        if (NativeGuestCombat.Unavailable(__instance)) { __result = true; return false; }
        __result = !__instance.gameObject.activeInHierarchy || __instance.health.currentHealth <= 0; return false;
    }
}
internal static class NativeGuestProtectionAwakePatch
{
    private static bool Prefix(Il2Cpp.ProtectionClass __instance) => !NativeGuestCombat.Owns(__instance.actor);
}
internal static class NativeGuestTargetPriorityPatch
{
    private static bool Prefix(Il2Cpp.Actor __instance, float distance, ref float __result)
    {
        if (!NativeGuestCombat.Owns(__instance)) return true;
        if (NativeGuestCombat.Unavailable(__instance)) { __result = float.MaxValue; return false; }
        // The private target has no CharacterStats/player save lifecycle.
        // Native GetUnimportance always dereferences BaseStats. Use the native
        // base score (distance and taunt included), with neutral stat multiplier.
        __result = __instance.GetUnimportanceBeforeStats(distance); return false;
    }
}
internal static class NativeGuestHolderDamagePatch
{
    private static bool Prefix(Il2Cpp.DamageStatsHolder __instance, Il2Cpp.Actor actor) =>
        !NativeGuestCombat.Owns(actor) || !NativeGuestCombat.Forward(actor, __instance.damageStats,
            __instance.damageModifier * __instance.DamageModifierFromDistanceScaling(actor));
}
internal static class NativeGuestHolderProtectionDamagePatch
{
    private static bool Prefix(Il2Cpp.DamageStatsHolder __instance, Il2Cpp.ProtectionClass enemyProtection, float distanceDamageModifier) =>
        !NativeGuestCombat.Owns(enemyProtection?.actor) || !NativeGuestCombat.Forward(enemyProtection.actor, __instance.damageStats,
            __instance.damageModifier * distanceDamageModifier);
}
