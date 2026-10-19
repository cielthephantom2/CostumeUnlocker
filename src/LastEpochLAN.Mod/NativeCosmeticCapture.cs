using LastEpochLAN.Core;
using UnityEngine;
using Il2CppLE.AssetBundles;

namespace LastEpochLAN.Mod;

internal static class NativeCosmeticCapture
{
    internal static (CosmeticPrefab Foot,CosmeticPrefab Aura) Capture(Il2Cpp.Actor actor)
    {
        if(actor==null) return (null,null);
        var identity=actor.GetActorSync()?.actorVisuals?.equipmentVisualsManager?.userIdentity ?? Il2CppLE.Networking.PlayerStore.LocalUserIdentity;
        if(identity==null) return (null,null);
        CosmeticPrefab foot=null,fx=null;
        var footprints=Il2CppLE.Services.ServiceProvider.FootprintService;
        var equipped=footprints?.equippedFootprints;
        var pools=footprints?.visualPools;
        // IL2CPP's generated generic out-value invocation can corrupt native
        // memory here (reported fatal AccessViolation in TryGetValue). Avoid
        // the by-ref generic ABI; all reads run on the Unity thread.
        if(equipped!=null && pools!=null && equipped.ContainsKey(identity))
        {
            var key=equipped[identity];
            if(pools.ContainsKey(key)) foot=Prefab(pools[key]?.Visual?.PrefabSoftRef);
        }
        var effects=Il2CppLE.Services.ServiceProvider.CharacterFXService?._equippedCharacterFXVisuals;
        if(effects!=null && effects.ContainsKey(identity)) fx=Prefab(effects[identity]?.PrefabSoftRef);
        return (foot,fx);
    }
    private static CosmeticPrefab Prefab(SoftRef<GameObject> prefab)=>prefab==null || !prefab ? null :
        new CosmeticPrefab {P0=prefab.Guid.P0,P1=prefab.Guid.P1,SubPath=prefab.SubPath??""};
}
