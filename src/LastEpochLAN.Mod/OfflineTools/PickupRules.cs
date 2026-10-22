using System.Numerics;
using LastEpochLAN.Core;
namespace LastEpochLAN.Mod.OfflineTools;
internal static class PickupRules
{
    internal const float Radius=5, SweepInterval=.2f;
    internal const int MaximumVisits=5,MaximumOperations=1;
    internal static bool Within(Vector3 player,Vector3 drop)
    {
        float dx=player.X-drop.X,dz=player.Z-drop.Z;
        return AutoPlayRules.WithinTargetRadiusSquared(dx*dx+dz*dz,Radius);
    }
}
