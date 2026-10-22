using System.Reflection;
namespace MelonLoader{public static class MelonLogger{public static System.Collections.Generic.List<string> Messages=new();public static void Msg(string text)=>Messages.Add(text);public static void Warning(string text)=>Messages.Add(text);}}
namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string m){}public HarmonyPatch(Type t,string m,Type[] args){}}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}
 public static class AccessTools{public static MethodInfo Method(Type type,string name)=>type.GetMethod(name);}
 public static class Priority{public const int First=1;}
}
namespace UnityEngine
{
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}}
 public static class Time{public static int frameCount;public static float unscaledTime;public static double realtimeSinceStartupAsDouble;}
 public struct Quaternion{public static Quaternion identity=>new();}
 public class Transform{public Vector3 position;public Transform Parent;public void SetParent(Transform parent,bool world){Parent=parent;}}
 public class GameObject{public bool activeSelf;public int layer=9;public Action Activated;public void SetActive(bool value){activeSelf=value;if(value)Activated?.Invoke();}}
 public class Collider:Il2Cpp.NativeObject{public bool isTrigger=true,enabled=true;}
 public class SphereCollider:Collider{public float radius=5.6f;}
}
internal static class PickupProbe{public static int Reads;}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{public new T this[int index]{get{PickupProbe.Reads++;return base[index];}set=>base[index]=value;}}}
namespace Il2Cpp
{
 public class NativeObject{static int n;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref n);public T TryCast<T>()where T:class=>this as T;}
 public class Actor:NativeObject
 {
  public object itemContainersManager=new();public UnityEngine.GameObject gameObject=new();public UnityEngine.Vector3 Position;public HealthPotion Potion=new();public UnityEngine.Vector3 position()=>Position;
  public T GetComponent<T>()where T:class=>Potion as T;
 }
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class HealthPotion{public int currentCharges,maxCharges=3;}
 public class ItemData:NativeObject{public int itemType;public bool Tutorial;public bool isTutorialItem()=>Tutorial;}
 public static class Item{public static bool isKey(int type)=>type==104;public static bool isWovenEcho(int type)=>type==107;}
 public static class ItemList
 {
  public static bool isCraftingItem(int type)=>type is >=101 and <=103;
  public static bool IsStackableItem(ItemData data)=>data.itemType is >=101 and <=104 or 107;
 }
 public class PickupGroundObjectInteraction:NativeObject
 {public Actor player;public uint id;public float index=1,delay=.2f;public bool isActiveAndEnabled=true;public UnityEngine.Transform transform=new();
  public UnityEngine.GameObject gameObject=new();public bool AwakeCompleted,canPickup;
  public UnityEngine.Collider[] Colliders={new UnityEngine.SphereCollider()};
  public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>Colliders.OfType<T>().ToArray();
  public void initialise(Actor actor,uint value){if(!AwakeCompleted)throw new NullReferenceException("Awake must bind ConditionHandler first");player=actor;id=value;LastEpochLAN.Mod.OfflineTools.OfflinePickup.Enqueue(this);}
  public void OnUpdateTick(){}}
 public class GoldPickupInteraction:PickupGroundObjectInteraction{public int amount=10;public void PickUp(){}}
 public class PotionPickupInteraction:PickupGroundObjectInteraction{public void PickUp(){}}
 public class PickupAncientBonesInteraction:PickupGroundObjectInteraction{public int amount=3;public void PickUp(){}}
 public class OtherPickupInteraction:PickupGroundObjectInteraction{}
 public class AutoPool
 {
  public class Pool<T>where T:PickupGroundObjectInteraction,new(){public T prefabT=new();}
  public class Subscriber<T>where T:PickupGroundObjectInteraction,new()
  {
   public Pool<T> Pool=new();public System.Collections.Generic.List<T> Active=new();public int Released;public bool LastInstantiateInactive;
   public T instantiate(UnityEngine.Vector3 position,UnityEngine.Quaternion rotation,UnityEngine.Transform parent,bool inactive)
   {
    LastInstantiateInactive=inactive;var item=new T();item.gameObject.Activated=()=>item.AwakeCompleted=true;item.gameObject.SetActive(!inactive);
    item.transform.position=position;item.transform.Parent=parent;
    item.Colliders=Pool.prefabT.Colliders.Select(c=>(UnityEngine.Collider)new UnityEngine.SphereCollider{radius=((UnityEngine.SphereCollider)c).radius,isTrigger=c.isTrigger}).ToArray();
    Active.Add(item);return item;
   }
   public void Release(T item){if(!Active.Remove(item))throw new Exception("Double release");Released++;item.gameObject.SetActive(false);}
  }
 }
 public class GroundItemVisuals:NativeObject
 {
  public ItemData itemData;public uint id;public UnityEngine.Transform transform=new();public UnityEngine.GameObject gameObject=new();
  public void initialise(){}public void beforePooling(){}public void OnDestroy(){}
 }
 public enum StackableItemFlags{All=255}
 public class GroundItemLabel{public ItemData Data;public ItemData getItemData()=>Data;public void ClickedItem(){}}
 public class GroundItemManager:NativeObject
 {
  public static GroundItemManager instance=new();
  public class GroundItem:NativeObject{public ItemData itemData;public UnityEngine.Vector3 location;public uint id;}
  public class GroundItemList:NativeObject{public Il2CppSystem.Collections.Generic.List<GroundItem> items=new();}
  public GroundItemList offlineItems=new();
  public AutoPool.Subscriber<GoldPickupInteraction> goldPileSubscriber=new();
  public AutoPool.Subscriber<PotionPickupInteraction> potionSubscriber=new();
  public AutoPool.Subscriber<PickupAncientBonesInteraction> ancientBoneSubscriber=new();
  public Action<uint> PoolVisual;
  public void BeforeSceneUnloaded(){}public void ChangeZone(){}public void OnDestroy(){}
  public Il2CppSystem.Collections.Generic.List<GoldPickupInteraction> activeGoldPiles=new();
  public Il2CppSystem.Collections.Generic.List<PotionPickupInteraction> activePotions=new();
  public Il2CppSystem.Collections.Generic.List<PickupAncientBonesInteraction> activeAncientBones=new();
  public int Gold,Bones,Keys,Materials,WovenEchoes,Equipment,VacuumCalls,Operations;public bool ThrowOnItem,RefuseItem;
  public bool TryGetGroundItemList(Actor a,out GroundItemList list){list=offlineItems;return true;}
  public void dropGoldForPlayer(){}public void dropItemForPlayer(){}
  public void pickupGold(Actor a,uint id,GoldPickupInteraction p){Operations++;if(!activeGoldPiles.Remove(p))return;Gold+=p.amount;p.isActiveAndEnabled=false;}
  public bool pickupPotion(Actor a,uint id,PotionPickupInteraction p){Operations++;if(a.Potion.currentCharges>=a.Potion.maxCharges)return false;if(!activePotions.Remove(p))return false;a.Potion.currentCharges++;p.isActiveAndEnabled=false;return true;}
  public void pickupAncientBone(Actor a,uint id,PickupAncientBonesInteraction p){Operations++;if(!activeAncientBones.Remove(p))return;Bones+=p.amount;p.isActiveAndEnabled=false;}
  public void pickupItem(Actor a,uint id,StackableItemFlags flags)
  {
   Operations++;if(ThrowOnItem)throw new InvalidOperationException("native test failure");
   if(RefuseItem)return;
   var entry=offlineItems.items.FirstOrDefault(x=>x.id==id);if(entry==null)return;
   // Actual native dispatch: All skips the direct inventory path for stackables.
   // The primary item itself is collected by vacuum, not before it.
   if(ItemList.IsStackableItem(entry.itemData) && flags==StackableItemFlags.All)
   {vacuumNearbyStackableItems(a,entry.location);return;}
   Collect(entry);
   if(ItemList.IsStackableItem(entry.itemData))vacuumNearbyStackableItems(a,entry.location);
  }
  private void Collect(GroundItem entry)
  {
   if(entry.itemData.itemType==104)Keys++;else if(entry.itemData.itemType is >=101 and <=103)Materials++;else if(entry.itemData.itemType==107)WovenEchoes++;else Equipment++;
   offlineItems.items.Remove(entry);PoolVisual?.Invoke(entry.id);
  }
  public void vacuumNearbyStackableItems(Actor a,UnityEngine.Vector3 origin)
  {
   VacuumCalls++;
   var gate=typeof(LastEpochLAN.Mod.OfflineTools.OfflinePickup).Assembly.GetType("LastEpochLAN.Mod.OfflineTools.OfflineAutoPickupVacuumGatePatch");
   if(gate!=null && !(bool)gate.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null))return;
   foreach(var entry in offlineItems.items.ToArray())
   {
    bool allowed=ItemList.IsStackableItem(entry.itemData);
    var filter=typeof(LastEpochLAN.Mod.OfflineTools.OfflinePickup).Assembly.GetType("LastEpochLAN.Mod.OfflineTools.OfflineAutoPickupItemFilterPatch");
    if(filter!=null){object[] args={entry.itemData,allowed};filter.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);allowed=(bool)args[1];}
    float x=entry.location.x-origin.x,z=entry.location.z-origin.z;
    if(allowed && x*x+z*z<7.5f*7.5f)Collect(entry);
   }
  }

 }
}
namespace LastEpochLAN.Mod.OfflineTools
{
 internal class Settings{public bool Keys=true,Gold=true,Potions=true,Materials=true,Bones=true,WovenEchoes=true,RangePickup=true;}
 internal static class OfflineToolkit{public static string Error="";public static bool Ready=true,Enabled=true;public static Settings Settings=new();}
 internal static class OfflineEconomy{internal static float GoldMultiplier()=>1;}
}
