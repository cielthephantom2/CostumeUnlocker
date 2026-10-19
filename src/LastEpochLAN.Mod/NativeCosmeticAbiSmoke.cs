using Il2CppLE.Services.Models;
using Il2CppLE.Services.Visuals.Footprints;
using Il2CppLE.Services.Visuals.CharacterFX;

namespace LastEpochLAN.Mod;

// Isolated native ABI test; touches no real services, equipped cosmetics or saves.
internal static class NativeCosmeticAbiSmoke
{
    internal static void Run()
    {
        var identity=new UserIdentity("LAN cosmetic ABI QA","lan-cosmetic-qa","lan-cosmetic-qa","LAN","LAN",false);
        var key=new FootprintVisualKey("lan-qa-footprint");
        var equipped=new Il2CppSystem.Collections.Generic.Dictionary<UserIdentity,FootprintVisualKey>();
        if(equipped.ContainsKey(identity))throw new InvalidOperationException("Empty native cosmetic dictionary has an entry.");
        equipped[identity]=key;
        if(!equipped.ContainsKey(identity) || equipped[identity].ItemId!="lan-qa-footprint")
            throw new InvalidOperationException("Native footprint value-type indexer ABI failed.");
        var pools=new Il2CppSystem.Collections.Generic.Dictionary<FootprintVisualKey,FootprintService.EquippedFootprint>();
        pools[key]=new FootprintService.EquippedFootprint();
        if(!pools.ContainsKey(equipped[identity]) || pools[key]==null)
            throw new InvalidOperationException("Native footprint pool key ABI failed.");
        var effects=new Il2CppSystem.Collections.Generic.Dictionary<UserIdentity,CharacterFXVisual>();
        effects[identity]=null;
        if(!effects.ContainsKey(identity) || effects[identity]!=null)
            throw new InvalidOperationException("Native aura optional-value indexer ABI failed.");
    }
}
