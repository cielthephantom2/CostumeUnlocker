using System.Reflection;
namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}
 public static class Priority{public const int First=1;}
}
namespace UnityEngine
{
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public static class Time{public static int frameCount;public static float unscaledTime;public static double realtimeSinceStartupAsDouble;}
 public class Transform{public Vector3 position;}
}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
namespace Il2Cpp
{
 public class NativeObject{static int n;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref n);public T TryCast<T>()where T:class=>this as T;}
 public class Actor:NativeObject
 {
  public UnityEngine.Vector3 Position;public HealthPotion Potion=new();public UnityEngine.Vector3 position()=>Position;
  public T GetComponent<T>()where T:class=>Potion as T;
 }
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class HealthPotion{public int currentCharges,maxCharges=3;}
 public class ItemData:NativeObject{public int itemType;public bool Tutorial;public bool isTutorialItem()=>Tutorial;}
 public static class Item{public static bool isKey(int type)=>type==100;}
 public static class ItemList{public static bool isCraftingItem(int type)=>type==101;}
 public class PickupGroundObjectInteraction:NativeObject
 {public Actor player;public uint id;public float index=1,delay=.2f;public bool isActiveAndEnabled=true;public UnityEngine.Transform transform=new();public void OnUpdateTick(){}}
 public class GoldPickupInteraction:PickupGroundObjectInteraction{public int amount=10;}
 public class PotionPickupInteraction:PickupGroundObjectInteraction{}
 public class PickupAncientBonesInteraction:PickupGroundObjectInteraction{public int amount=3;}
 public class OtherPickupInteraction:PickupGroundObjectInteraction{}
 public enum StackableItemFlags{All=255}
 public class GroundItemLabel{public ItemData Data;public ItemData getItemData()=>Data;public void ClickedItem(){}}
 public class GroundItemManager
 {
  public static GroundItemManager instance=new();
  public class GroundItem{public ItemData itemData;public UnityEngine.Vector3 location;public uint id;}
  public class GroundItemList{public Il2CppSystem.Collections.Generic.List<GroundItem> items=new();}
  public GroundItemList offlineItems=new();
  public Il2CppSystem.Collections.Generic.List<GoldPickupInteraction> activeGoldPiles=new();
  public Il2CppSystem.Collections.Generic.List<PotionPickupInteraction> activePotions=new();
  public Il2CppSystem.Collections.Generic.List<PickupAncientBonesInteraction> activeAncientBones=new();
  public int Gold,Bones,Keys,Materials,Equipment,VacuumCalls;public bool ThrowOnItem;
  public bool TryGetGroundItemList(Actor a,out GroundItemList list){list=offlineItems;return true;}
  public void dropGoldForPlayer(){}
  public void pickupGold(Actor a,uint id,GoldPickupInteraction p){Gold+=p.amount;activeGoldPiles.Remove(p);}
  public bool pickupPotion(Actor a,uint id,PotionPickupInteraction p){if(a.Potion.currentCharges>=a.Potion.maxCharges)return false;a.Potion.currentCharges++;activePotions.Remove(p);return true;}
  public void pickupAncientBone(Actor a,uint id,PickupAncientBonesInteraction p){Bones+=p.amount;activeAncientBones.Remove(p);}
  public void pickupItem(Actor a,uint id,StackableItemFlags flags)
  {
   if(ThrowOnItem)throw new InvalidOperationException("native test failure");
   var entry=offlineItems.items.FirstOrDefault(x=>x.id==id);if(entry==null)return;
   if(entry.itemData.itemType==100)Keys++;else if(entry.itemData.itemType==101)Materials++;else Equipment++;
   offlineItems.items.Remove(entry);
   var allow=(bool)typeof(LastEpochLAN.Mod.OfflineTools.OfflineAutoPickupVacuumGatePatch).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
   if(allow)vacuumNearbyStackableItems();
  }
  public void vacuumNearbyStackableItems(){VacuumCalls++;}
 }
}
namespace LastEpochLAN.Mod.OfflineTools
{
 internal class Settings{public bool Keys=true,Gold=true,Potions=true,Materials=true,Bones=true,RangePickup=true;}
 internal static class OfflineToolkit{public static bool Ready=true;public static Settings Settings=new();}
 internal static class OfflineEconomy{internal static float GoldMultiplier()=>1;}
}
