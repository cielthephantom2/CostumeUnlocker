namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute
 {public HarmonyPatch(Type t,string m){}public HarmonyPatch(Type t,string m,Type[] args){}}
}
namespace UnityEngine
{
 public enum HideFlags{HideAndDontSave}
 public class Object
 {
  static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);public string name;public HideFlags hideFlags;
  public T TryCast<T>()where T:class=>this as T;
  public static T Instantiate<T>(T original)where T:Object{var copy=(T)original.MemberwiseClone();copy.Pointer=(IntPtr)Interlocked.Increment(ref next);return copy;}
 }
}
namespace Il2Cpp
{
 public enum AbilityID{meteor=7,meteorShower=38}
 public class Ability:UnityEngine.Object
 {public long Key;public object abilitySpriteSoftRef;public static Dictionary<AbilityID,Ability> All=new();public static Ability getAbility(AbilityID id)=>All.GetValueOrDefault(id);}
 public struct AbilityRef
 {public long key;public AbilityRef(long k){key=k;}public AbilityRef(Ability a){key=a.Key;}}
 public class ActorSync:UnityEngine.Object{}
 public static class PlayerFinder{public static ActorSync Local;public static ActorSync getLocalActorSync()=>Local;}
 public class CosmeticSelectionFlyoutPanelUI{public void PopulateFlyoutWindowAsync(){}}
 public class CosmeticPanelUI{public void SetupCosmeticAbilitySlots(){}}
 public class AbilitySync{public void ReceiveCreateVisualsInternal(){}}
 public class AbilityVisuals{public void HandleMeteorIndicator(){}}
}
namespace Il2CppLE.Services.Models.Abilities
{
 public struct AbilityVisualKey{public Il2Cpp.AbilityRef Ability;public string BackendId;}
}
namespace Il2CppLE.Services.Cosmetics
{
 public class Cosmetic:UnityEngine.Object{public string BackendID,DisplayName,StoreLocalizedString;public object StoreSpriteSoftRef;public int SortOrder;}
 public class CosmeticAbility:Cosmetic
 {
  public Il2Cpp.AbilityRef AbilityRef;public bool SupporterPackItem,twitchDrop,EquipmentAbility;public string SupporterPackBackendId;public long unixTimePromoEnds;
  public int Class,EquipSlot,UniqueId;
  public Models.Abilities.AbilityVisualKey VisualKey;public List<Models.Abilities.AbilityVisualKey> AdditionalAbilityKeys=new();
 }
 public class CosmeticResources
 {
  public List<CosmeticAbility> AbilityCosmetics=new();public Dictionary<string,CosmeticAbility> AbilityLookupPairs=new();public Dictionary<string,Cosmetic> CosmeticLookupPairs=new();
  public void OnEnable(){}
 }
}
namespace Il2CppLE.Services.Visuals
{
 public class VisualDatabase<TKey,TVisual>
 {
  public class DatabaseEntry{public TKey Key;public TVisual Visual;}
  public List<DatabaseEntry> Entries=new();public Dictionary<TKey,DatabaseEntry> cachedLookup;
  public Dictionary<TKey,DatabaseEntry> BuildLookup()=>cachedLookup=Entries.ToDictionary(e=>e.Key);
 }
}
namespace Il2CppLE.Services.Visuals.Abilities
{
 public class AbilityVisual{public bool OverrideVisualObject,OverrideAlternateVisualObjects,OverrideCastingVfx,OverrideCreateVfxOnDeath,OverrideTheme,OverrideCreateAtTargetLocation;}
 public class AbilityVisualDatabase:VisualDatabase<Models.Abilities.AbilityVisualKey,AbilityVisual>{}
}
namespace Il2CppLE.AssetManagement
{
 public static class GlobalAssets
 {
  public static bool CosmeticsAvailable=true,AbilityVisualsAvailable=true;
  public static Services.Cosmetics.CosmeticResources Cosmetics=new();
  public static Services.Visuals.Abilities.AbilityVisualDatabase AbilityVisuals=new();
 }
}
namespace Il2CppLE.Networking
{
 public class UserIdentity{public string MasterAccountID;}
 public static class PlayerStore{public static UserIdentity LocalUserIdentity=new(){MasterAccountID="local"};}
}
namespace Il2CppLE.Networking.Cosmetics
{
 public class ClientCosmeticsNetworkManager:UnityEngine.Object
 {public Dictionary<string,Dictionary<long,Services.Models.Abilities.AbilityVisualKey>> CachedCharacterEquippedCosmeticSkills=new();}
}
namespace Il2CppLE.Services
{public static class ServiceProvider{public static Networking.Cosmetics.ClientCosmeticsNetworkManager CosmeticsNetworkManager=new();}}
namespace Il2CppLE
{public static class AbilitySyncService{public static void ReceiveCreateVisuals(){}}}
namespace Il2CppLE.Networking.Generated.Static
{
 public static class AbilitySyncService
 {
  public struct ActorKey{public Il2Cpp.ActorSync Actor;public Il2Cpp.ActorSync GetActorSync()=>Actor;}
  public struct AbilityIndex{public Il2Cpp.Ability Ability;public Il2Cpp.Ability GetAbility()=>Ability;}
  public struct MessageCreateVisuals{public ActorKey? ActorSyncKey;public AbilityIndex AbilityIndex;public byte VisualsId;}
 }
}
namespace LastEpochLAN.Mod.OfflineTools
{
 internal static class OfflineToolkit
 {internal static bool Enabled=true;internal static Options Settings=new();internal class Options{internal bool UnlockCosmetics=true;}}
}
