using Il2CppLE.Networking;
using Il2CppLE.Networking.Core;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Networking.Generated;
using Il2CppLE.Networking.Messaging;

namespace LastEpochLAN.Mod;

internal static class NativeServerProbe
{
    // A private transport smoke only. Never change GameplayEnvironment or register gameplay Actors.
    internal static object Smoke()
    {
        var router = new GeneratedMessageRouter();
        var registry = new NetObjectRegistry();
        var auth = new Il2CppLE.Networking.UserAuthenticationProvider();
        var service = new ServerNetworkService(router.Cast<IMessageRouter>(), registry.Cast<INetObjectRegistry>(),
            auth.Cast<IAuthenticationProvider>(), new NetworkOptions());
        try
        {
            service.Start("127.0.0.1", 0, "LAN-Transport-Probe");
            if (!service.Running) throw new InvalidOperationException("Native server transport did not start.");
            string endpoint = service.EndPoint?.ToString() ?? "";
            service.Stop();
            if (service.Running) throw new InvalidOperationException("Native server transport did not stop.");
            return new { passed = true, endpoint,
                scope = "Native ServerNetworkService construction/start/stop only; no authenticated client, gameplay bootstrap or replication verified" };
        }
        finally { if (service.Running) service.Stop(); service.Dispose(); }
    }
}
