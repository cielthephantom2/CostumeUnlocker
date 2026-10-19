using HarmonyLib;
using Il2CppLE.Networking;
using Il2CppLE.Networking.Core;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Services;

namespace LastEpochLAN.Mod;

// Native behaviours normally resolve their dependencies through ServiceProvider.
// A short thread-local scope lets private receivers resolve the right session
// without replacing the offline player's global service group.
internal sealed class NativeServiceScope : IDisposable
{
    [ThreadStatic] internal static NativeServiceScope Current;
    internal readonly BaseNetworkService Network;
    internal readonly NetObjectRegistry Registry;
    private readonly NativeServiceScope previous;
    internal NativeServiceScope(BaseNetworkService network, NetObjectRegistry registry)
    { Network = network; Registry = registry; previous = Current; Current = this; }
    public void Dispose() { Current = previous; }
}

[HarmonyPatch(typeof(ServiceProvider), nameof(ServiceProvider.NetworkService), MethodType.Getter)]
internal static class NativeScopedNetworkServicePatch
{
    private static bool Prefix(ref INetworkService __result)
    {
        if (NativeServiceScope.Current == null) return true;
        __result = NativeServiceScope.Current.Network.Cast<INetworkService>(); return false;
    }
}

[HarmonyPatch(typeof(ServiceProvider), nameof(ServiceProvider.NetObjectRegistry), MethodType.Getter)]
internal static class NativeScopedRegistryPatch
{
    private static bool Prefix(ref NetObjectRegistry __result)
    {
        if (NativeServiceScope.Current == null) return true;
        __result = NativeServiceScope.Current.Registry; return false;
    }
}
