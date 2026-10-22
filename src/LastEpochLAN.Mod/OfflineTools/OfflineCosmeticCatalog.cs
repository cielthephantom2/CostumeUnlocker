using Il2CppLE.AssetManagement;
using Il2CppLE.Services.Cosmetics;
using Il2CppCysharp.Threading.Tasks;
using Il2CppInterop.Runtime.InteropTypes;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class NativeNull
{
    internal static bool IsNullOrDestroyed(this Il2CppObjectBase value)=>value==null || value is UnityEngine.Object obj && obj==null;
}

internal static class OfflineCosmeticCatalog
{
    private static IntPtr registry;
    private static HashSet<string> ids=new(StringComparer.Ordinal);
    private static Il2CppSystem.Collections.Generic.List<string> owned;
    private static UniTask<Il2CppSystem.Collections.Generic.List<string>> task;
    private static bool Refresh()
    {
        if(!OfflineToolkit.Enabled || !OfflineToolkit.Settings.UnlockCosmetics || !GlobalAssets.CosmeticsAvailable) return false;
        MeteorDefaultCosmetic.Ensure();
        var source=GlobalAssets.Cosmetics;
        if(source==null) return false;
        if(source.Pointer==registry && owned!=null) return true;
        var next=new HashSet<string>(StringComparer.Ordinal);var visited=new HashSet<IntPtr>();
        var bundles=CosmeticStoreIcon.CurrentBundles();
        void Add(Cosmetic cosmetic)
        {
            if(cosmetic==null || !visited.Add(cosmetic.Pointer)) return;
            var bundle=cosmetic.TryCast<CosmeticBundle>();
            if(bundle?.bundledCosmetics!=null) foreach(var child in bundle.bundledCosmetics) Add(child);
            if(!CosmeticCatalogRules.IsJunk(cosmetic.BackendID,cosmetic.DisplayName,
                CosmeticStoreIcon.HasStoreIcon(cosmetic,bundles),CosmeticStoreIcon.IsTitle(cosmetic))) next.Add(cosmetic.BackendID);
        }
        void List<T>(Il2CppSystem.Collections.Generic.List<T> list) where T:Cosmetic
        {if(list!=null) foreach(var cosmetic in list) Add(cosmetic);}
        List(source.ItemCosmetics);List(source.AbilityCosmetics);List(source.BackSlotCosmetics);List(source.PortalCosmetics);
        List(source.PetCosmetics);List(source.PetEffectCosmetics);List(source.PortraitCosmetics);List(source.CharacterTitleCosmetics);
        List(source.FootprintCosmetics);List(source.CharacterFXCosmetics);List(source.StashCosmetics);List(source.CosmeticBundles);
        if(source.CosmeticLookupPairs!=null) foreach(var pair in source.CosmeticLookupPairs) Add(pair.Value);
        if(next.Count==0) return false; // Native ownership remains intact until assets are ready.
        ids=next;owned=new();foreach(var id in ids) owned.Add(id);
        task=new UniTask<Il2CppSystem.Collections.Generic.List<string>>(owned);registry=source.Pointer;return true;
    }
    internal static bool TryIsOwned(string id,out bool value)
    {value=false;if(!Refresh()) return false;value=ids.Contains(id??"");return true;}
    internal static Il2CppSystem.Collections.Generic.List<string> OwnedIds()=>Refresh()?owned:null;
    internal static UniTask<Il2CppSystem.Collections.Generic.List<string>> OwnedIdsTask()=>Refresh()?task:null;
    internal static bool IsJunk(Cosmetic cosmetic)=>Refresh() && !ids.Contains(cosmetic.BackendID??"");
}
