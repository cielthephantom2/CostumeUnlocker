using LastEpochLAN.Core;
using UnityEngine;
using Actor = Il2Cpp.Actor;

namespace LastEpochLAN.Mod;

/// <summary>Native entities created for host IDs; never adopts a guest's locally spawned monster.</summary>
internal sealed class HostEnemyReplicas
{
    private readonly Dictionary<int, Actor> created = new();
    private readonly HashSet<IntPtr> ownedPointers = new();
    private readonly HashSet<IntPtr> hiddenPointers = new();
    private readonly HashSet<IntPtr> frozenSimulation = new();
    private int spawnBudget;
    internal void BeginUpdate() => spawnBudget = 2;
    internal bool HasSpawnBudget => spawnBudget > 0;
    private sealed record HiddenActor(Actor Actor, bool GameplayActive, bool SyncActive);
    private readonly Dictionary<int, HiddenActor> hiddenLocal = new();
    private readonly Dictionary<string, Il2Cpp.ActorData> templates = new();
    internal void ObserveTemplates(IEnumerable<Actor> candidates)
    {
        foreach (var actor in candidates)
            if (actor?.data is { } data && IsMonsterData(data)) templates[data.name] = data;
    }
    private readonly Dictionary<int, float> retryAt = new();
    private readonly Dictionary<int, string> failures = new();
    private readonly Dictionary<int, float> pendingUntil = new();
    internal int CreatedCount => created.Values.Count(actor => actor != null);
    internal int PendingCount => pendingUntil.Count;
    internal int HiddenCount => hiddenLocal.Values.Count(entry => entry.Actor != null);
    internal IReadOnlyDictionary<int, string> Failures => failures;

    internal Actor GetOrCreate(EnemyState state, bool pendingDeath)
    {
        if (created.TryGetValue(state.Id, out var existing) && existing != null)
        {
            if (state.Dead && !pendingDeath && pendingUntil.ContainsKey(state.Id))
            {
                ownedPointers.Remove(existing.Pointer); DestroyActor(existing); created.Remove(state.Id); pendingUntil.Remove(state.Id);
                retryAt.Remove(state.Id); failures.Remove(state.Id);
                return null;
            }
            if (existing.health != null && existing.health.maxHealth > 0)
            {
                pendingUntil.Remove(state.Id); failures.Remove(state.Id);
                if (existing.navMeshAgent != null) existing.navMeshAgent.enabled = false;
                return existing;
            }
            if (pendingUntil.TryGetValue(state.Id, out float deadline) && Time.unscaledTime < deadline) return null;
            ownedPointers.Remove(existing.Pointer); DestroyActor(existing); created.Remove(state.Id); pendingUntil.Remove(state.Id);
            failures[state.Id] = "Native monster health initialization timed out.";
            retryAt[state.Id] = Time.unscaledTime + 2;
            return null;
        }
        if (existing is not null) ownedPointers.Remove(existing.Pointer);
        created.Remove(state.Id);
        pendingUntil.Remove(state.Id);
        // No retrospective death callbacks or corpse recreation on a late join.
        if (state.Dead && !pendingDeath) return null;
        if (retryAt.TryGetValue(state.Id, out float next) && Time.unscaledTime < next) return null;
        if (spawnBudget <= 0) return null;
        spawnBudget--;
        retryAt[state.Id] = Time.unscaledTime + 2f;
        Actor actor = null;
        try
        {
            if (!templates.TryGetValue(state.Template, out var data) || data == null)
            {
                data = Resources.FindObjectsOfTypeAll<Il2Cpp.ActorData>().FirstOrDefault(candidate =>
                    candidate != null && candidate.name == state.Template && IsMonsterData(candidate));
                if (data == null) throw new InvalidOperationException("Native actor asset is not loaded: " + state.Template);
                templates[state.Template] = data;
            }
            var local = Il2Cpp.PlayerFinder.getPlayerActor();
            if (local == null || !local.gameObject.scene.IsValid())
                throw new InvalidOperationException("A live local campaign scene is required for host monsters.");
            actor = data.spawn(new Vector3(state.X, state.Y, state.Z), state.Yaw, null,
                Il2Cpp.AbilitySync.PositionSyncType.Absolute, null, "", true, 0,
                new Il2CppSystem.Nullable<UnityEngine.SceneManagement.Scene>(local.gameObject.scene), true);
            if (actor == null || actor.isPlayerActor || actor.data == null || !IsMonsterData(actor.data))
                throw new InvalidOperationException("Native spawn did not yield a monster actor.");
            if (Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer != local?.Pointer)
                throw new InvalidOperationException("Native monster creation changed the local player.");
            actor.gameObject.name = "LAN host enemy " + state.Id + " - " + state.Template;
            if (actor.navMeshAgent != null) actor.navMeshAgent.enabled = false;
            created[state.Id] = actor;
            ownedPointers.Add(actor.Pointer);
            retryAt.Remove(state.Id); failures.Remove(state.Id);
            // Native Awake/Start and asset initialization can finish on later
            // frames. Keep the same host ID instead of destroying/recreating it.
            if (actor.health == null || actor.health.maxHealth <= 0)
            { pendingUntil[state.Id] = Time.unscaledTime + 15; return null; }
            return actor;
        }
        catch (Exception ex)
        {
            if (actor != null) { ownedPointers.Remove(actor.Pointer); DestroyActor(actor); }
            if (!failures.TryGetValue(state.Id, out string previous) || previous != ex.Message)
                MelonLoader.MelonLogger.Warning("Host enemy " + state.Id + " unavailable: " + ex.Message);
            failures[state.Id] = ex.Message;
            return null;
        }
    }

    internal bool Owns(Actor actor) => actor is not null && ownedPointers.Contains(actor.Pointer);
    internal bool IsHidden(Actor actor) => actor is not null && hiddenPointers.Contains(actor.Pointer);
    internal void FreezeSimulation(Actor actor)
    {
        if (actor == null || frozenSimulation.Contains(actor.Pointer) || actor.stateController == null || !actor.stateController.initialised) return;
        // Initialization must finish first. Stop Unity/native update scheduling,
        // rather than entering a managed prefix for every NPC every frame.
        // Explicit hit/death methods and visual animation remain callable.
        actor.stateController.enabled = false;
        if (actor.health != null) actor.health.enabled = false;
        frozenSimulation.Add(actor.Pointer);
    }

    internal void HideLocal(IEnumerable<Actor> candidates)
    {
        var owned = created.Values.Where(actor => actor != null).Select(actor => actor.GetInstanceID()).ToHashSet();
        foreach (var actor in candidates)
        {
            if (actor == null || owned.Contains(actor.GetInstanceID())) continue;
            if (!IsHidden(actor))
            {
                hiddenLocal[actor.GetInstanceID()] = new HiddenActor(actor, actor.gameObject.activeSelf, Root(actor).activeSelf);
                hiddenPointers.Add(actor.Pointer); SetActive(actor, false);
            }
        }
    }

    internal void RemoveAbsent(ISet<int> ids)
    {
        foreach (int id in created.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            if (created[id] != null) { ownedPointers.Remove(created[id].Pointer); DestroyActor(created[id]); }
            created.Remove(id); retryAt.Remove(id); failures.Remove(id); pendingUntil.Remove(id);
        }
    }

    internal void Clear()
    {
        foreach (var actor in created.Values) if (actor != null) DestroyActor(actor);
        created.Clear(); retryAt.Clear(); failures.Clear(); templates.Clear(); pendingUntil.Clear();
        foreach (var entry in hiddenLocal.Values)
            if (entry.Actor != null)
            { entry.Actor.gameObject.SetActive(entry.GameplayActive); Root(entry.Actor).SetActive(entry.SyncActive); }
        hiddenLocal.Clear();
        ownedPointers.Clear(); hiddenPointers.Clear();
        frozenSimulation.Clear();
    }

    private static bool IsMonsterData(Il2Cpp.ActorData data) => data.actorType == Il2Cpp.ActorData.Type.Normal ||
        data.actorType == Il2Cpp.ActorData.Type.MiniBoss || data.actorType == Il2Cpp.ActorData.Type.Boss;
    internal static GameObject Root(Actor actor) => actor.GetActorSync()?.gameObject ?? actor.gameObject;
    internal static void SetActive(Actor actor, bool active)
    {
        if (actor == null) return;
        actor.gameObject.SetActive(active);
        var syncRoot = Root(actor);
        if (syncRoot.Pointer != actor.gameObject.Pointer) syncRoot.SetActive(active);
    }
    internal static void DestroyActor(Actor actor)
    {
        if (actor == null) return;
        var gameplay = actor.gameObject;
        var sync = Root(actor);
        UnityEngine.Object.Destroy(gameplay);
        if (sync.Pointer != gameplay.Pointer) UnityEngine.Object.Destroy(sync);
    }
}
