using HarmonyLib;
using Il2Cpp;
using Il2CppLE.AssetManagement;
using Il2CppLE.Networking;
using Il2CppLE.Networking.Cosmetics;
using Il2CppLE.Services;
using Il2CppLE.Services.Cosmetics;
using Il2CppLE.Services.Models.Abilities;
using Il2CppLE.Services.Visuals;
using Il2CppLE.Services.Visuals.Abilities;
using UnityEngine;
using VisualEntry=Il2CppLE.Services.Visuals.VisualDatabase<Il2CppLE.Services.Models.Abilities.AbilityVisualKey,Il2CppLE.Services.Visuals.Abilities.AbilityVisual>.DatabaseEntry;
using LightweightMessage=Il2CppLE.Networking.Generated.Static.AbilitySyncService.MessageCreateVisuals;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class MeteorDefaultCosmetic
{
    internal const string Id="MTX_Skills_TM_Default_Meteor";
    internal const long MeteorKey=2015287513;
    private static CosmeticAbility cosmetic;
    private static readonly HashSet<long> family=new(){MeteorKey};
    internal static long VisualOverrides;
    internal static bool Enabled=>OfflineToolkit.Enabled && OfflineToolkit.Settings.UnlockCosmetics;
    internal static bool IsDefault(Cosmetic c)=>c?.BackendID==Id;
    internal static void Register(CosmeticResources source)
    {
        if(!Enabled || source?.AbilityCosmetics==null || source.AbilityLookupPairs==null || source.CosmeticLookupPairs==null)return;
        if(source.AbilityLookupPairs.ContainsKey(Id))return;
        CosmeticAbility template=null;
        foreach(var item in source.AbilityCosmetics)
            if(item!=null && item.BackendID!=Id && item.AbilityRef.key==MeteorKey){template=item;break;}
        if(template==null)return;
        var added=UnityEngine.Object.Instantiate(template);
        added.name="TM Default Meteor";added.hideFlags=HideFlags.HideAndDontSave;
        added.BackendID=Id;added.DisplayName="Default Meteor";added.StoreLocalizedString="";added.SortOrder=-1;
        added.SupporterPackItem=false;added.SupporterPackBackendId="";added.twitchDrop=false;added.unixTimePromoEnds=0;
        added.VisualKey=new AbilityVisualKey{Ability=template.AbilityRef,BackendId=Id};
        added.AdditionalAbilityKeys=new();
        if(template.AdditionalAbilityKeys!=null)
            foreach(var key in template.AdditionalAbilityKeys)
            {family.Add(key.Ability.key);added.AdditionalAbilityKeys.Add(new AbilityVisualKey{Ability=key.Ability,BackendId=Id});}
        // Keep the template's class/equipment eligibility so the new option
        // appears beside existing Meteor costumes, including Vilatria's skill.
        cosmetic=added;
        source.AbilityCosmetics.Add(added);source.AbilityLookupPairs[Id]=added;source.CosmeticLookupPairs[Id]=added;
    }
    internal static void Ensure()
    {
        if(!Enabled || !GlobalAssets.CosmeticsAvailable)return;
        Register(GlobalAssets.Cosmetics);
        if(cosmetic==null || !GlobalAssets.AbilityVisualsAvailable)return;
        var meteor=Ability.getAbility(AbilityID.meteor);
        if(meteor!=null)cosmetic.StoreSpriteSoftRef=meteor.abilitySpriteSoftRef;
        var shower=Ability.getAbility(AbilityID.meteorShower);
        if(shower!=null)
        {
            var reference=new AbilityRef(shower);
            if(family.Add(reference.key))cosmetic.AdditionalAbilityKeys.Add(new AbilityVisualKey{Ability=reference,BackendId=Id});
        }
        var database=GlobalAssets.AbilityVisuals;
        if(database==null)return;
        var lookup=database.cachedLookup??database.BuildLookup();
        foreach(long key in family)
        {
            var visualKey=new AbilityVisualKey{Ability=new AbilityRef(key),BackendId=Id};
            if(lookup.ContainsKey(visualKey))continue;
            // A successful identity visual uses the shipped ability visuals.
            // The presentation hooks below choose variant0, bypassing item or
            // tree conversion skins without touching damage conversion.
            var entry=new VisualEntry{Key=visualKey,Visual=new AbilityVisual()};
            database.Entries.Add(entry);lookup[visualKey]=entry;
        }
    }
    internal static bool Selected(ActorSync actorSync)
    {
        if(!Enabled || actorSync==null || actorSync.Pointer!=PlayerFinder.getLocalActorSync()?.Pointer)return false;
        var account=PlayerStore.LocalUserIdentity?.MasterAccountID;
        if(string.IsNullOrEmpty(account))return false;
        var manager=ServiceProvider.CosmeticsNetworkManager?.TryCast<ClientCosmeticsNetworkManager>();
        var cache=manager?.CachedCharacterEquippedCosmeticSkills;
        if(cache==null || !cache.ContainsKey(account))return false;
        var skills=cache[account];
        return skills!=null && skills.ContainsKey(MeteorKey) && skills[MeteorKey].BackendId==Id;
    }
    internal static bool Applies(ActorSync actorSync,Ability ability)=>Enabled && ability!=null && family.Contains(new AbilityRef(ability).key) && Selected(actorSync);
    internal static void Variant(ActorSync actorSync,Ability ability,ref byte variant)
    {if(Applies(actorSync,ability)){variant=0;VisualOverrides++;}}
}

[HarmonyPatch(typeof(CosmeticResources),nameof(CosmeticResources.OnEnable))]
internal static class OfflineMeteorDefaultRegistryPatch
{private static void Postfix(CosmeticResources __instance)=>MeteorDefaultCosmetic.Register(__instance);}
[HarmonyPatch(typeof(CosmeticSelectionFlyoutPanelUI),nameof(CosmeticSelectionFlyoutPanelUI.PopulateFlyoutWindowAsync))]
internal static class OfflineMeteorDefaultFlyoutPatch
{private static void Prefix()=>MeteorDefaultCosmetic.Ensure();}
[HarmonyPatch(typeof(CosmeticPanelUI),nameof(CosmeticPanelUI.SetupCosmeticAbilitySlots))]
internal static class OfflineMeteorDefaultSlotsPatch
{private static void Prefix()=>MeteorDefaultCosmetic.Ensure();}
[HarmonyPatch(typeof(AbilitySync),nameof(AbilitySync.ReceiveCreateVisualsInternal))]
internal static class OfflineMeteorDefaultVisualPatch
{
    private static void Prefix(ActorSync actorSync,Ability ability,ref byte visualsID)=>MeteorDefaultCosmetic.Variant(actorSync,ability,ref visualsID);
}
[HarmonyPatch(typeof(Il2CppLE.AbilitySyncService),nameof(Il2CppLE.AbilitySyncService.ReceiveCreateVisuals))]
internal static class OfflineMeteorDefaultLightweightPatch
{
    private static void Prefix(ref LightweightMessage msg)
    {
        if(!MeteorDefaultCosmetic.Enabled || !msg.ActorSyncKey.HasValue)return;
        var variant=msg.VisualsId;
        MeteorDefaultCosmetic.Variant(msg.ActorSyncKey.Value.GetActorSync(),msg.AbilityIndex.GetAbility(),ref variant);
        msg.VisualsId=variant;
    }
}
[HarmonyPatch(typeof(AbilityVisuals),nameof(AbilityVisuals.HandleMeteorIndicator),new[]{typeof(float),typeof(bool),typeof(bool),typeof(ActorSync)})]
internal static class OfflineMeteorDefaultIndicatorPatch
{
    private static void Prefix(ActorSync actorSync,ref bool convertedToLightning,ref bool convertedToCold)
    {if(MeteorDefaultCosmetic.Selected(actorSync)){convertedToLightning=false;convertedToCold=false;}}
}
