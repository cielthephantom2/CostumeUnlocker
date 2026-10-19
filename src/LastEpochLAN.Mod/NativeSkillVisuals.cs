using HarmonyLib;
using LastEpochLAN.Core;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace LastEpochLAN.Mod;

internal static class NativeSkillVisualCapture
{
    private sealed record Pending(Il2Cpp.AbilitySync Source, Il2Cpp.ActorSync Owner, Il2Cpp.Ability Ability, byte Index);
    private static readonly System.Collections.Concurrent.ConcurrentQueue<Pending> pending = new();
    private sealed class Tracked
    {
        internal Il2Cpp.AbilitySync Source;
        internal GameObject AbilityObject;
        internal IntPtr Creator;
        internal long Id, Key;
        internal byte Index;
        internal float Started, Ended = -1;
        internal int CreatedFrame;
        internal SkillVisualState Last;
        internal Il2CppLE.AssetBundles.SoftRef<GameObject> Prefab;
        internal IntPtr LayoutRoot;
        internal Renderer[] Renderers;
        internal LineRenderer[] Lines;
        internal (string Path, Transform Node)[] PoseNodes;
        internal float NextLayout;
        internal ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
        internal float NextParts;
        internal bool ContainerOnly;
        internal IntPtr ProfiledRoot;
    }
    private static readonly Dictionary<IntPtr, Tracked> objects = new();
    private static readonly Dictionary<IntPtr,Dictionary<IntPtr,Tracked>> creatorObjects=new();
    private static void Store(IntPtr pointer,Tracked entry)
    {
        Remove(pointer);objects[pointer]=entry;
        if(!creatorObjects.TryGetValue(entry.Creator,out var owned)) creatorObjects[entry.Creator]=owned=new();
        owned[pointer]=entry;
    }
    private static void Remove(IntPtr pointer)
    {
        if(!objects.Remove(pointer,out var entry)) return;
        if(creatorObjects.TryGetValue(entry.Creator,out var owned))
        {owned.Remove(pointer);if(owned.Count==0)creatorObjects.Remove(entry.Creator);}
    }
    internal static void NativeIndexSmoke()
    {
        var key=new IntPtr(-101);var child=new IntPtr(-102);var first=new IntPtr(-201);var second=new IntPtr(-202);
        try
        {
            Store(key,new Tracked {Creator=first});Store(child,new Tracked {Creator=first});
            if(creatorObjects[first].Count!=2) throw new InvalidOperationException("VFX owner index lost a child visual.");
            Store(key,new Tracked {Creator=second});
            if(creatorObjects[first].ContainsKey(key) || creatorObjects[second].Count!=1) throw new InvalidOperationException("Pooled VFX retained its previous creator index.");
            Remove(child);
            if(creatorObjects.ContainsKey(first)) throw new InvalidOperationException("Empty VFX owner index was retained.");
        }
        finally {Remove(key);Remove(child);}
    }
    private static readonly Dictionary<IntPtr, Il2CppLE.AssetBundles.SoftRef<GameObject>> pooledPrefabs = new();
    private static readonly Dictionary<IntPtr, VisualPoint> origins = new();
    internal static void RecordOrigin(GameObject root, Vector3 origin)
    {
        if (SkillCapture.Session == null || root == null) return;
        if (origins.Count >= 512 && !origins.ContainsKey(root.Pointer)) origins.Clear();
        origins[root.Pointer] = Point(origin);
    }
    private static Il2Cpp.Actor VisualOwner(Il2Cpp.Actor creator, Il2Cpp.Actor local)
    {
        if (creator != null && local != null && creator.Pointer != local.Pointer && creator.IsMinion() && creator.IsMinionOf(local))
            return local;
        return creator;
    }
    internal static long PooledObjectsObserved { get; private set; }
    internal static void ObservePooled(GameObject root, Il2CppLE.AssetBundles.SoftRef<GameObject> prefab)
    {
        if (SkillCapture.Session == null || root == null || prefab == null) return;
        if (NativeVisualEvents.SuppressSnapshots) return;
        origins.Remove(root.Pointer);
        if (pooledPrefabs.Count >= 512 && !pooledPrefabs.ContainsKey(root.Pointer)) pooledPrefabs.Clear();
        pooledPrefabs[root.Pointer] = prefab;
        PooledObjectsObserved++;
        // Global VFX without CreationReferences still inherit ownership when
        // created inside the native cast/ability construction scope.
        if (ConstructionOwner != null && ConstructionAbility != null)
            TrackObject(ConstructionOwner, root, ConstructionAbility, prefab);
    }
    private static readonly Dictionary<IntPtr, Il2Cpp.AbilityVisuals> discovered = new();
    internal static void ObserveVisual(Il2Cpp.AbilityVisuals visual)
    {
        if (SkillCapture.Session != null && visual != null && discovered.Count < 256) discovered[visual.Pointer] = visual;
    }
    private sealed record Casting(Il2Cpp.Actor Actor, long Id, long AbilityKey, long VfxKey, float Started, float Until, float Height);
    private static readonly Dictionary<long, Casting> casting = new();
    private static long nextId;
    [ThreadStatic] internal static Il2Cpp.Actor ConstructionOwner;
    [ThreadStatic] internal static Il2Cpp.Ability ConstructionAbility;
    internal static Il2Cpp.Actor ResolveOwner(Il2Cpp.AbilityObjectConstructor constructor) =>
        constructor.actor ?? constructor.myCreationReferences?.creator ?? ConstructionOwner;
    internal static long Captured { get; private set; }
    internal static string Error { get; private set; } = "";
    internal static long ObjectsObserved { get; private set; }
    internal static long GlobalObjectsObserved { get; private set; }
    internal static long SnapshotsPublished { get; private set; }
    internal static long InvalidSnapshots { get; private set; }
    internal static long LocalSnapshotsPublished { get; private set; }
    internal static string LastLocalSnapshot { get; private set; } = "";
    internal static long RenderedPartsCaptured { get; private set; }
    internal static long ParticleValuesCaptured { get; private set; }
    internal static long MinionVisualsCaptured { get; private set; }
    internal sealed record Profile(long Ability, string Root, string[] Scripts);
    private static readonly Dictionary<string, Profile> profiles = new();
    internal static Profile[] Profiles => profiles.Values.ToArray();
    internal static void TrackGlobal(Il2Cpp.ActorSync owner, GameObject root, Il2Cpp.Ability ability,
        Il2CppLE.AssetBundles.SoftRef<GameObject> prefab = null)
    {
        if (NativeVisualEvents.SuppressSnapshots) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (owner == null || local == null) return;
        var creator = owner.Pointer == local.actorSync?.Pointer ? local : VisualOwner(owner.GameplayActor, local);
        if (creator == null || creator.Pointer != local.Pointer) return;
        if (root != null && prefab == null) pooledPrefabs.TryGetValue(root.Pointer, out prefab);
        TrackObject(creator, root, ability, prefab);
        if (root != null && objects.TryGetValue(root.Pointer, out var entry))
        {
            if (prefab != null) entry.Prefab = prefab;
            entry.Last = Read(entry); GlobalObjectsObserved++;
        }
    }
    internal static void FinishGlobal(GameObject root)
    {
        if (root == null || !objects.TryGetValue(root.Pointer, out var entry)) return;
        // Read before the native pool clears line points and detaches transforms.
        // Some callers deactivate/reset a pooled object before releasing it.
        // Preserve the last visible pose instead of publishing the reset state.
        if (root.activeInHierarchy || entry.Last == null) entry.Last = Read(entry);
        entry.Ended = Time.unscaledTime;
    }
    internal static void TrackCasting(Il2Cpp.Actor actor, Il2Cpp.Ability ability, bool evade)
    {
        if (SkillCapture.Session == null || actor == null || ability == null) return;
        var vfx = ability.castingVFXRef;
        try { vfx = ability.GetCastingVFX(0, actor.actorSync); }
        catch (Exception ex) { Error = "Casting VFX resolution: " + ex.Message; }
        if (!vfx.keyNotDefault() || vfx.keyIsNull() || casting.Count >= 64) return;
        long id = ++nextId;
        casting[id] = new Casting(actor, id, new Il2Cpp.AbilityRef(ability).key, vfx.key,
            Time.unscaledTime, Time.unscaledTime + (evade ? 0.75f : 0.5f), evade ? 0.05f : 0.8f);
        Captured++;
    }

    internal static void TrackObject(Il2Cpp.Actor creator, GameObject root, Il2Cpp.Ability ability,
        Il2CppLE.AssetBundles.SoftRef<GameObject> prefab = null)
    {
        if (SkillCapture.Session == null || root == null || ability == null || creator == null ||
            Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        var attributed = VisualOwner(creator, local);
        if (attributed != null && attributed.Pointer != creator.Pointer) { MinionVisualsCaptured++; creator = attributed; }
        if (local == null || (local.Pointer != creator.Pointer && WorldSynchronizer.Active?.CanCaptureEnemyVisuals(creator) != true)) return;
        ObjectsObserved++;
        var aoi = root.GetComponent<Il2Cpp.AbilityObjectIndicator>();
        var sync = aoi?.abilitySync ?? root.GetComponent<Il2Cpp.AbilitySync>();
        if (objects.Count >= 128 && !objects.ContainsKey(root.Pointer)) return;
        long key = new Il2Cpp.AbilityRef(ability).key;
        if (objects.TryGetValue(root.Pointer, out var existing) && existing.CreatedFrame == Time.frameCount && existing.Key == key && existing.Ended < 0)
        {
            existing.Source = sync; existing.Index = aoi?.visualsToSpawnID ?? existing.Index;
            if (prefab != null) existing.Prefab = prefab;
            existing.Last = Read(existing); return;
        }
        var entry = new Tracked { Source = sync, AbilityObject = root, Creator = creator.Pointer, Id = ++nextId,
            Key = key, Index = aoi?.visualsToSpawnID ?? 0, Started = Time.unscaledTime, CreatedFrame = Time.frameCount, Prefab = prefab };
        if (entry.Key == 0 || entry.Index > 63) return;
        entry.Last = Read(entry);
        Store(root.Pointer,entry); Captured++;
    }
    internal static void Enqueue(Il2Cpp.AbilitySync source, Il2Cpp.ActorSync owner, Il2Cpp.Ability ability, byte index)
    {
        if (SkillCapture.Session != null && pending.Count < 128) pending.Enqueue(new Pending(source, owner, ability, index));
    }

    internal static void Track(Il2Cpp.AbilitySync source, Il2Cpp.ActorSync owner, Il2Cpp.Ability ability, byte index)
    {
        if (SkillCapture.Session == null || source == null || ability == null || owner == null ||
            Il2CppEHG.Multiplayer.GameplayEnvironment.IsOnlinePlay) return;
        var local = Il2Cpp.PlayerFinder.getPlayerActor();
        if (local == null) return;
        var attributed = owner.Pointer == local.actorSync?.Pointer ? local : VisualOwner(owner.GameplayActor, local);
        if (attributed == null || attributed.Pointer != local.Pointer) return;
        if (objects.Count >= 128 && !objects.ContainsKey(source.Pointer)) return;
        var entry = new Tracked { Source = source, Creator = local.Pointer, Id = ++nextId, Key = new Il2Cpp.AbilityRef(ability).key,
            Index = index, Started = Time.unscaledTime };
        if (entry.Key == 0 || index > 63) return;
        Store(source.Pointer,entry); Captured++;
        entry.Last = Read(entry);
    }

    private static int sampledFrame = -1;
    private static int visualSceneCount = -1;
    private static void DiscoverVisualRoots()
    {
        if (SkillCapture.Session == null) return;
        if (visualSceneCount != UnityEngine.SceneManagement.SceneManager.sceneCount)
        {
            visualSceneCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            foreach (var visual in UObject.FindObjectsOfType<Il2Cpp.AbilityVisuals>()) ObserveVisual(visual);
        }
        foreach (var pair in discovered.ToArray())
        {
            var visual = pair.Value;
            if (visual == null || !visual.gameObject.activeInHierarchy) { discovered.Remove(pair.Key); continue; }
            if (visual == null || !visual.gameObject.activeInHierarchy || objects.ContainsKey(visual.gameObject.Pointer)) continue;
            var refs = visual.creationReferences ?? visual.abilitySync?.GetCreationReferences();
            if (refs == null) continue;
            var creator = refs.creator;
            var local = Il2Cpp.PlayerFinder.getPlayerActor();
            if (creator == null && refs.syncCreator != null && local?.actorSync?.Pointer == refs.syncCreator.Pointer) creator = local;
            var ability = refs.thisAbility ?? refs.originalAbility;
            if (creator != null && ability != null) TrackObject(creator, visual.gameObject, ability);
        }
    }
    internal static SkillVisualState[] Capture() => CaptureFor(Il2Cpp.PlayerFinder.getPlayerActor());
    internal static SkillVisualState[] CaptureFor(Il2Cpp.Actor creator,int limit=32)
    {
        if (creator == null || limit<=0) return Array.Empty<SkillVisualState>();
        limit=Math.Min(32,limit);
        if (sampledFrame != Time.frameCount)
        {
        sampledFrame = Time.frameCount;
        try { DiscoverVisualRoots(); } catch (Exception ex) { Error = "Visual root discovery: " + ex; }
        foreach (var id in casting.Keys.Where(id => casting[id].Actor == null || Time.unscaledTime >= casting[id].Until).ToArray()) casting.Remove(id);
        var creations = new HashSet<(IntPtr Source, IntPtr Owner, IntPtr Ability, byte Index)>();
        while (pending.TryDequeue(out var next))
        {
            try
            {
                if (next.Source == null || next.Owner == null || next.Ability == null) continue;
                // Send and receive can describe the same creation. A later creation
                // must get a new ID even when the native pool reuses its object.
                if (creations.Add((next.Source.Pointer, next.Owner.Pointer, next.Ability.Pointer, next.Index)))
                    Track(next.Source, next.Owner, next.Ability, next.Index);
            }
            catch (Exception ex) { Error = ex.ToString(); }
        }
        foreach (var pair in objects.ToArray())
        {
            var entry = pair.Value;
            try
            {
                var active = entry.AbilityObject != null ? entry.AbilityObject.activeInHierarchy :
                    entry.Source != null && entry.Source.gameObject.activeInHierarchy;
                if (active && entry.Ended >= 0)
                {
                    // The pool can reuse a visual root for a subsequent cast
                    // inside the brief delivery retention window.
                    entry.Id = ++nextId; entry.Started = Time.unscaledTime; entry.Ended = -1; Captured++;
                }
                if (active && entry.Ended < 0)
                {
                    entry.Last = Read(entry);
                    DiscoverRenderedParts(entry);
                }
                else if (entry.Ended < 0) entry.Ended = Time.unscaledTime;
                if (entry.Ended >= 0 && Time.unscaledTime - entry.Ended > 0.75f) Remove(pair.Key);
            }
            catch (Exception ex) { Error = ex.ToString(); Remove(pair.Key); }
        }
        }
        var castingStates = casting.Values.Where(value => value.Actor != null && value.Actor.Pointer == creator.Pointer)
            .Select(value => new SkillVisualState { Id = value.Id, AbilityKey = value.AbilityKey, CastingVfxKey = value.VfxKey,
                Position = Point(value.Actor.position() + Vector3.up * value.Height), Rotation = Point(value.Actor.transform.eulerAngles),
                Age = Time.unscaledTime - value.Started });
        IEnumerable<Tracked> entries=creatorObjects.TryGetValue(creator.Pointer,out var ownedObjects)?ownedObjects.Values:Enumerable.Empty<Tracked>();
        InvalidSnapshots += entries.Count(entry => entry.Last != null && !entry.Last.IsValid());
        var result = entries.Where(entry => !entry.ContainerOnly && entry.Last != null && entry.Last.IsValid() &&
                (entry.Renderers?.Length > 0 || entry.Last.Lines.Length > 0))
            .Select(entry => entry.Last with { Ended = entry.Ended >= 0 }).Concat(castingStates).Where(state => state.IsValid())
            .OrderByDescending(state => state.Id).Take(limit).Select(state => state.Copy()).ToArray();
        int particleBudget = 128;
        for (int i = 0; i < result.Length; i++)
        {
            var frames = new List<VisualParticleFrame>();
            foreach (var frame in result[i].Particles)
            {
                int count = Math.Min(frame.Count, particleBudget); particleBudget -= count;
                frames.Add(frame with { Count = count });
            }
            result[i] = result[i] with { Particles = frames.ToArray() };
        }
        SnapshotsPublished += result.Length;
        if (result.Length > 0 && creator.Pointer == Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer)
        {
            LocalSnapshotsPublished += result.Length;
            var newest = result.FirstOrDefault();
            if (newest != null)
                LastLocalSnapshot = $"id={newest.Id}; ability={newest.AbilityKey}; prefab={newest.PrefabGuid0:X}:{newest.PrefabGuid1:X}; casting={newest.CastingVfxKey}; poses={newest.Poses.Length}; lines={newest.Lines.Length}";
        }
        return result;
    }

    private static SkillVisualState Read(Tracked entry)
    {
        var source = entry.Source;
        if (source == null && entry.AbilityObject != null)
            source = entry.AbilityObject.GetComponent<Il2Cpp.AbilityObjectIndicator>()?.abilitySync;
        var visual = entry.AbilityObject?.GetComponent<Il2Cpp.AbilityVisuals>() ?? source?.abilityVisuals;
        var root = visual != null ? visual.gameObject : entry.AbilityObject ?? source.gameObject;
        var transform = root.transform;
        if (entry.LayoutRoot != root.Pointer || (entry.Renderers.Length == 0 && Time.unscaledTime >= entry.NextLayout))
        {
            entry.NextLayout = Time.unscaledTime + 0.15f;
            entry.LayoutRoot = root.Pointer;
            entry.Renderers = root.GetComponentsInChildren<Renderer>(true).ToArray();
            entry.Lines = root.GetComponentsInChildren<LineRenderer>(true).Take(4).ToArray();
            entry.Particles = root.GetComponentsInChildren<ParticleSystem>(true).Take(32).ToArray();
            var nodes = new Dictionary<string, Transform>();
            foreach (var renderer in entry.Renderers)
                for (var child = renderer.transform; child != null && child.Pointer != transform.Pointer && nodes.Count < 24; child = child.parent)
                {
                    var path = new List<int>(); var node = child;
                    while (node != null && node.Pointer != transform.Pointer && path.Count < 16)
                    { path.Add(node.GetSiblingIndex()); node = node.parent; }
                    if (node == null || node.Pointer != transform.Pointer) continue;
                    path.Reverse(); nodes[string.Join("/", path)] = child;
                }
            entry.PoseNodes = nodes.Select(p => (p.Key, p.Value)).ToArray();
        }
        var poses = new Dictionary<string, VisualPose>();
        foreach (var poseNode in entry.PoseNodes)
        {
                var child = poseNode.Node; if (child == null) continue;
                poses[poseNode.Path] = new VisualPose { Path = poseNode.Path, Position = Point(child.localPosition), Rotation = Point(child.localEulerAngles),
                    Scale = Point(child.localScale), Active = child.gameObject.activeSelf, Layer = child.gameObject.layer };
        }
        var lines = entry.Lines.Where(line => line != null).Select((line, index) =>
        {
            int count = Math.Min(line.positionCount, 16);
            var points = new VisualPoint[count];
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = count < line.positionCount ? i * (line.positionCount - 1) / Math.Max(1, count - 1) : i;
                var point = line.GetPosition(sourceIndex);
                points[i] = Point(line.useWorldSpace ? point : line.transform.TransformPoint(point));
            }
            var tint = line.startColor;
            var material = line.sharedMaterial != null ? line.sharedMaterial.name.Replace(" (Instance)", "") : "";
            return new VisualLine { Index = index, Points = points,
                Width = Mathf.Clamp(Math.Max(line.startWidth, line.endWidth), 0.001f, 10),
                Color = new VisualPoint { X = Mathf.Clamp(tint.r, 0, 16), Y = Mathf.Clamp(tint.g, 0, 16), Z = Mathf.Clamp(tint.b, 0, 16) },
                Material = material.Length <= 128 ? material : material.Substring(0, 128) };
        }).Where(line => line.Points.Length >= 2).ToArray();
        // Gameplay prefabs often contain no renderer. Their GUID must not
        // override the ability's separate native visual prefab on receipt.
        var load = entry.Renderers.Length > 0
            ? root.GetComponent<Il2CppLE.AssetBundles.LoadRefComponent>()?.LoadRef : null;
        // The constructor can report a gameplay-only prefab while AbilitySync
        // points at a separate visual root. Only transmit a prefab identity
        // belonging to a root that actually contains rendering components.
        var exactPrefab = entry.Renderers.Length > 0 && entry.AbilityObject != null &&
            entry.AbilityObject.Pointer == root.Pointer ? entry.Prefab : null;
        var guid = exactPrefab != null ? exactPrefab.Guid : load != null ? load.Guid : default;
        if (entry.ProfiledRoot != root.Pointer)
        {
            entry.ProfiledRoot = root.Pointer;
            string profileKey = $"{entry.Key}:{guid.P0:X}:{guid.P1:X}";
            if (profiles.Count < 32 || profiles.ContainsKey(profileKey))
                profiles[profileKey] = new Profile(entry.Key, root.name, root.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(script => script != null).Select(script => script.GetIl2CppType().FullName).Distinct().OrderBy(name => name).Take(32).ToArray());
        }
        var stretch = root.GetComponentInChildren<Il2Cpp.SubObjectsStretchToLocationCreatedFrom>(true);
        VisualPoint origin = null;
        if (!origins.TryGetValue(root.Pointer, out origin) && entry.AbilityObject != null)
            origins.TryGetValue(entry.AbilityObject.Pointer, out origin);
        if (stretch != null && stretch.locationFound && (origin == null || entry.CreatedFrame != Time.frameCount))
            origin = Point(stretch.useTransformInsteadOfLocationCreatedFrom != null
                ? stretch.useTransformInsteadOfLocationCreatedFrom.position + stretch.offset : stretch.locationCreatedFrom);
        return new SkillVisualState { Id = entry.Id, AbilityKey = entry.Key, VisualIndex = entry.Index, Layer = root.layer,
            PrefabGuid0 = guid.P0, PrefabGuid1 = guid.P1, PrefabSubPath = exactPrefab?.SubPath ?? load?.SubPath ?? "",
            StretchOrigin = origin,
            Position = Point(transform.position), Rotation = Point(transform.eulerAngles), Scale = Point(transform.lossyScale),
            Age = Mathf.Clamp(Time.unscaledTime - entry.Started, 0, 30), Lines = lines, Poses = poses.Values.Where(p => p.IsValid()).ToArray(),
            Particles = CaptureParticles(entry) };
    }

    private static VisualParticleFrame[] CaptureParticles(Tracked entry)
    {
        // Player effects only, bounded independently of monster/world snapshots.
        if (entry.Creator != Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer) return Array.Empty<VisualParticleFrame>();
        if (entry.Particles.Length == 0) return Array.Empty<VisualParticleFrame>();
        var frames = new List<VisualParticleFrame>(); int budget = 64;
        var candidates = entry.Particles.Select((system, index) => (System: system, Index: index))
            .Where(p => p.System != null && p.System.gameObject.activeInHierarchy)
            .Select(p => (p.System, p.Index, Count: Math.Clamp(p.System.particleCount, 0, 32)))
            .OrderByDescending(p => p.Count > 0).ThenByDescending(p => p.Count).ThenBy(p => p.Index).Take(4);
        foreach (var candidate in candidates)
        {
            var system = candidate.System;
            var shape = system.shape; var main = system.main;
            int count = Math.Min(candidate.Count, budget);
            var frame = new VisualParticleFrame { Index = candidate.Index, Count = count, ShapeScale = Point(shape.scale),
                ShapePosition = Point(shape.position), ShapeRotation = Point(shape.rotation),
                Size = main.startSizeMultiplier, Speed = main.startSpeedMultiplier, Lifetime = main.startLifetimeMultiplier };
            if (!frame.IsValid()) continue;
            budget -= count; ParticleValuesCaptured += count; frames.Add(frame);
        }
        return frames.ToArray();
    }

    private static void DiscoverRenderedParts(Tracked entry)
    {
        if (entry.Creator != Il2Cpp.PlayerFinder.getPlayerActor()?.Pointer || entry.Renderers == null ||
            entry.Renderers.Length == 0 || Time.unscaledTime < entry.NextParts) return;
        entry.NextParts = Time.unscaledTime + 0.2f;
        var parts = new Dictionary<IntPtr, (GameObject Root, Il2CppLE.AssetBundles.LoadRef Load)>();
        bool allCovered = true;
        foreach (var renderer in entry.Renderers)
        {
            if (renderer == null) continue;
            bool covered = false;
            for (var node = renderer.transform; node != null && node.gameObject.Pointer != entry.LayoutRoot; node = node.parent)
            {
                var load = node.GetComponent<Il2CppLE.AssetBundles.LoadRefComponent>()?.LoadRef;
                if (load == null) continue;
                parts[node.gameObject.Pointer] = (node.gameObject, load); covered = true; break;
            }
            if (!covered) allCovered = false;
        }
        bool trackedAll = true;
        foreach (var part in parts.Values.Take(8))
        {
            if (objects.ContainsKey(part.Root.Pointer)) continue;
            if (objects.Count >= 128) { trackedAll = false; break; }
            var child = new Tracked { AbilityObject = part.Root, Creator = entry.Creator, Id = ++nextId, Key = entry.Key,
                Started = entry.Started, CreatedFrame = Time.frameCount,
                Prefab = new Il2CppLE.AssetBundles.SoftRef<GameObject>(part.Load.Guid, part.Load.SubPath) };
            child.Last = Read(child); Store(part.Root.Pointer,child); Captured++; RenderedPartsCaptured++;
        }
        // Do not clone a rendererless carrier whose actual visuals were added
        // as independent loaded prefabs by a mutator at runtime.
        entry.ContainerOnly = parts.Count > 0 && parts.Count <= 8 && allCovered && trackedAll;
    }
    internal static VisualPoint Point(Vector3 point) => new() { X = point.x, Y = point.y, Z = point.z };
    internal static Vector3 Vector(VisualPoint point) => new(point.X, point.Y, point.Z);
    internal static void Reset() { objects.Clear();creatorObjects.Clear(); origins.Clear(); profiles.Clear(); pooledPrefabs.Clear(); casting.Clear(); discovered.Clear(); while (pending.TryDequeue(out _)) { } Error = ""; sampledFrame = -1; visualSceneCount = -1; }
}

[HarmonyPatch(typeof(Il2Cpp.AbilityVisuals), nameof(Il2Cpp.AbilityVisuals.OnEnable))]
internal static class NativeEnabledVisualDiscoveryPatch
{
    private static void Postfix(Il2Cpp.AbilityVisuals __instance) => NativeSkillVisualCapture.ObserveVisual(__instance);
}

[HarmonyPatch(typeof(Il2Cpp.GlobalVFXManager), nameof(Il2Cpp.GlobalVFXManager.ReceiveSpawnConnectedVfx))]
internal static class NativeConnectedVisualCapturePatch
{
    private static void Postfix(Il2CppLE.AssetBundles.SoftRef<GameObject> prefabSoftRef,
        Il2Cpp.ActorSync actorCasterSync, Il2Cpp.Ability ability, uint connectedVfxID)
    {
        try
        {
            var pool = Il2Cpp.GlobalVFXManager.ConnectedVFXs;
            if (pool != null && pool.TryGetValue(connectedVfxID, out var root))
                NativeSkillVisualCapture.TrackGlobal(actorCasterSync, root, ability, prefabSoftRef);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Connected VFX capture: " + ex); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.GlobalVFXManager), nameof(Il2Cpp.GlobalVFXManager.HandleSettingCreationReferences))]
internal static class NativeGlobalVisualCapturePatch
{
    private static void Postfix(GameObject go, Il2Cpp.ActorSync actorSync, Il2Cpp.Ability ability, Vector3 locationCreatedFrom)
    {
        try { NativeSkillVisualCapture.RecordOrigin(go, locationCreatedFrom); NativeSkillVisualCapture.TrackGlobal(actorSync, go, ability); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Global VFX capture: " + ex); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.GlobalVFXManager), nameof(Il2Cpp.GlobalVFXManager.ReceiveDestroyConnectedVfx))]
internal static class NativeConnectedVisualFinishPatch
{
    private static void Prefix(uint connectedVfxID)
    {
        try
        {
            var pool = Il2Cpp.GlobalVFXManager.ConnectedVFXs;
            if (pool != null && pool.TryGetValue(connectedVfxID, out var root)) NativeSkillVisualCapture.FinishGlobal(root);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Connected VFX completion: " + ex); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.ActorVisualsVFXHandler), nameof(Il2Cpp.ActorVisualsVFXHandler.CreateCastingVFX))]
internal static class NativeActualCastingVisualCapturePatch
{
    private static void Postfix(Il2Cpp.ActorVisuals actorVisuals, Il2Cpp.Ability ability, Il2Cpp.SelfDestroyer __result)
    {
        try
        {
            if (__result != null) NativeSkillVisualCapture.TrackGlobal(actorVisuals?.actorSync, __result.gameObject, ability);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Actual casting VFX capture: " + ex); }
    }
}

[HarmonyPatch(typeof(Il2Cpp.AbilityObjectConstructor), nameof(Il2Cpp.AbilityObjectConstructor.constructAbilityObject))]
internal static class NativeConstructedSkillVisualCapturePatch
{
    private static void Prefix(Il2Cpp.AbilityObjectConstructor __instance, Il2Cpp.Ability ability,
        out (Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        __state = (NativeSkillVisualCapture.ConstructionOwner, NativeSkillVisualCapture.ConstructionAbility);
        if (SkillCapture.Session == null) return;
        NativeSkillVisualCapture.ConstructionOwner = NativeSkillVisualCapture.ResolveOwner(__instance);
        NativeSkillVisualCapture.ConstructionAbility = ability;
    }
    private static void Postfix(Il2Cpp.AbilityObjectConstructor __instance, Il2Cpp.Ability ability, GameObject __result)
    {
        try { NativeSkillVisualCapture.TrackObject(NativeSkillVisualCapture.ResolveOwner(__instance), __result, ability); }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Constructed skill visual capture: " + ex); }
    }
    private static void Finalizer((Il2Cpp.Actor Owner, Il2Cpp.Ability Ability) __state)
    {
        NativeSkillVisualCapture.ConstructionOwner = __state.Owner;
        NativeSkillVisualCapture.ConstructionAbility = __state.Ability;
    }
}

[HarmonyPatch(typeof(Il2Cpp.AbilityObjectConstructor), nameof(Il2Cpp.AbilityObjectConstructor.InstantiateAbility))]
internal static class NativeActualAbilityPrefabCapturePatch
{
    private static void Postfix(Il2Cpp.AbilityObjectConstructor __instance,
        Il2CppLE.AssetBundles.SoftRef<GameObject> prefabSoftRef, GameObject __result)
    {
        try
        {
            if (SkillCapture.Session != null && NativeSkillVisualCapture.ConstructionAbility != null)
                NativeSkillVisualCapture.TrackObject(NativeSkillVisualCapture.ResolveOwner(__instance), __result,
                    NativeSkillVisualCapture.ConstructionAbility, prefabSoftRef);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Actual ability prefab capture: " + ex); }
    }
}

// Capture the native ability object after its visuals are created. This method
// has no Nullable arguments and avoids IL2CPP's nullable trampoline failure.
[HarmonyPatch(typeof(Il2Cpp.AbilityObjectIndicator), nameof(Il2Cpp.AbilityObjectIndicator.createVisuals),
    new[] { typeof(Il2Cpp.Ability), typeof(byte) })]
internal static class NativeSkillVisualCapturePatch
{
    private static void Postfix(Il2Cpp.AbilityObjectIndicator __instance, Il2Cpp.Ability ability, byte visualsID)
    {
        try
        {
            var source = __instance.abilitySync;
            var references = source != null ? source.GetCreationReferences() : __instance.GetComponent<Il2Cpp.CreationReferences>();
            var owner = references?.syncCreator ?? references?.creator?.actorSync;
            NativeSkillVisualCapture.Enqueue(source, owner, ability, visualsID);
        }
        catch (Exception ex) { MelonLoader.MelonLogger.Warning("Native skill visual capture: " + ex); }
    }
}
