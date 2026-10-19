using Il2CppLE.Networking.Core;
using Il2CppLE.Networking;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Networking.Generated;
using UnityEngine;

namespace LastEpochLAN.Mod;

/// <summary>Projects the offline host's real actors into a private native registry.</summary>
internal sealed class NativeOfflineHostWorld : IDisposable
{
    private sealed record Projection(Il2Cpp.Actor Actor, NetObjectKey Key, ActorSyncNetObject Object);
    private readonly NetObjectRegistry registry;
    private readonly Dictionary<IntPtr, Projection> actors = new();
    internal NativeOfflineHostWorld(NetObjectRegistry registry)
    {
        this.registry = registry;
        if (!NetObjectTypeRegistry.TryGetTypeConstructor(ActorSyncNetObject.TypeInfo.TypeID, out _))
            ActorSyncNetObject.RegisterNetObjectType();
    }

    internal ActorSyncNetObject Track(Il2Cpp.Actor actor)
    {
        if (actor == null || actor.health == null) throw new ArgumentException("Host actor requires native health.");
        if (actors.TryGetValue(actor.Pointer, out var found)) return found.Object;
        var obj = new ActorSyncNetObject { CreatePrefabIndex = -1, MasterAccountId = "", ScaleX1000 = 1000 };
        Capture(actor, obj);
        var key = registry.TakeNextDynamicNetObjectKey();
        // Never register the offline actor's original NetObject in this registry:
        // changing its key/handle would corrupt the host's own native world.
        registry.Register(key, obj.Cast<INetObject>());
        actors.Add(actor.Pointer, new Projection(actor, key, obj));
        return obj;
    }

    internal void Tick()
    {
        foreach (var pair in actors.ToArray())
        {
            var projection = pair.Value;
            if (projection.Actor == null || projection.Actor.health == null)
            { registry.Destroy(projection.Key); actors.Remove(pair.Key); continue; }
            Capture(projection.Actor, projection.Object);
        }
    }

    private static void Capture(Il2Cpp.Actor actor, ActorSyncNetObject obj)
    {
        var transform = actor.GetActorSync()?.transform ?? actor.transform;
        obj.position = transform.position; obj.rotation = transform.rotation;
        float ratio = actor.health.maxHealth > 0 ? actor.health.currentHealth / actor.health.maxHealth : 0;
        obj.healthRatio = (byte)Math.Clamp((int)MathF.Round(ratio * 255), 0, 255);
        obj.Enabled = HostEnemyReplicas.Root(actor).activeInHierarchy;
    }

    public void Dispose()
    {
        foreach (var projection in actors.Values) registry.Destroy(projection.Key);
        actors.Clear();
    }
}
