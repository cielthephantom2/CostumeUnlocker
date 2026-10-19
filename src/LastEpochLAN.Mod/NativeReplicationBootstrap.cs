using Il2CppLE.Networking;
using Il2CppLE.Networking.Core.Networking;
using Il2CppLE.Networking.Replication;

namespace LastEpochLAN.Mod;

/// <summary>Fills the missing initial-world send for a private native session.</summary>
internal sealed class NativeReplicationBootstrap
{
    private readonly HashSet<long> seeded = new();
    internal void Tick(ServerNetworkService server)
    {
        var replication = server.ReplicationSystem;
        var connected = new HashSet<long>();
        foreach (var target in replication.targets)
        {
            long id = target.ConnectionID.Value;
            connected.Add(id);
            if (seeded.Contains(id)) continue;
            SendExistingObjects(replication, target);
            seeded.Add(id);
        }
        seeded.IntersectWith(connected);
    }

    private static void SendExistingObjects(ReplicationSystem replication, ReplicationTarget target)
    {
        // Called on the Unity thread after RegisterClient installs its target. A new
        // target does not receive pre-existing handles in the shipped client code.
        foreach (ReplicationHandle handle in replication.Handles.handles)
        {
            if (handle == null || handle.Destroyed || handle.Released ||
                replication.createdHandleSet.Contains(handle.Index)) continue;
            // Only seed the joining guest; reseeding all targets would reset their
            // baselines and interfere with acknowledgements already in flight.
            target.ReplicateBaseline(handle);
        }
    }
}
