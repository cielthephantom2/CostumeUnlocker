using Il2CppLE.Networking.Core;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Networking.Generated;
using HarmonyLib;

namespace LastEpochLAN.Mod;

internal static class NativeActorBinding
{
    private sealed record Binding(BaseNetworkService Network, NetObjectRegistry Registry, ActorSyncNetObject Object);
    private static readonly Dictionary<IntPtr, Binding> bindings = new();
    // Bind a fresh native receiver to this private registry. Existing offline actors
    // have their own registry/key and must never be reinitialized into another world.
    internal static void Attach(BaseNetworkService network, NetObjectRegistry registry, ActorSyncNetObject netObject, Il2Cpp.ActorSync receiver)
    {
        if (receiver == null || receiver.Initialized) throw new InvalidOperationException("Native Actor receiver must be fresh.");
        bindings.Add(receiver.Pointer, new Binding(network, registry, netObject));
        using var scope = new NativeServiceScope(network, registry);
        registry.AttachBehaviour(netObject.Cast<INetObject>(), receiver.Cast<INetBehaviour>());
        if (!receiver.Initialized || receiver.NetObject?.Pointer != netObject.Pointer)
            throw new InvalidOperationException("Native Actor receiver failed to bind to the private object.");
    }

    internal static bool DestroyPrivate(Il2CppSystem.Object instance)
    {
        if (!bindings.Remove(instance.Pointer, out var binding)) return false;
        using var scope = new NativeServiceScope(binding.Network, binding.Registry);
        var receiver = instance.Cast<Il2Cpp.ActorSync>();
        receiver.IsDestroyed = true;
        receiver.OnDestroyInternal();
        // Mirrors the generic native OnDestroy cleanup, with the owning registry.
        // A global SafeDelete can otherwise delete an unrelated offline object with
        // the same numeric key when Unity destroys this component asynchronously.
        if (binding.Registry.TryGetObject(receiver.Key, out var current) && current.Pointer == binding.Object.Pointer)
            binding.Registry.Destroy(receiver.Key);
        return true;
    }
}

[HarmonyPatch(typeof(NetBehaviour<ActorSyncNetObject>), nameof(NetBehaviour<ActorSyncNetObject>.OnDestroy))]
internal static class NativePrivateActorDestroyPatch
{
    private static bool Prefix(Il2CppSystem.Object __instance) => !NativeActorBinding.DestroyPrivate(__instance);
}
