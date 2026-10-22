using System.Numerics;
using LastEpochLAN.Core;
namespace LastEpochLAN.Mod.OfflineTools;
internal static class PickupRules
{
    internal const float Radius=3;
    internal static bool Within(Vector3 player,Vector3 drop,bool moving)
    {
        float dx=player.X-drop.X,dz=player.Z-drop.Z;
        return moving && AutoPlayRules.WithinTargetRadiusSquared(dx*dx+dz*dz,Radius);
    }
    internal static bool HasMoved(Vector3 previous,Vector3 current,double elapsed)
    {
        float dx=current.X-previous.X,dz=current.Z-previous.Z;
        float d=dx*dx+dz*dz;
        return elapsed>0 && elapsed<=.25 && float.IsFinite(d) && d>0.000001f && d<9;
    }
}
