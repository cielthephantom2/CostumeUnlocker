namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string m){}}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPriority:Attribute{public HarmonyPriority(int p){}}
 public static class Priority{public const int First=1;}
}
namespace UnityEngine
{
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 operator*(Vector3 v,float n)=>new(v.x*n,v.y*n,v.z*n);}
 public struct Quaternion{public static Quaternion identity=>new();}
 public class Transform{public Vector3 position;public Transform Parent;public bool KeptWorldScale;public void SetParent(Transform p,bool world){Parent=p;KeptWorldScale=world;}}
 public class GameObject{public bool Active;public int layer;public Action Activated;public void SetActive(bool value){Active=value;if(value)Activated?.Invoke();}}
 public class Collider:Il2Cpp.NativeObject{public bool isTrigger=true,enabled=true;}
 public class SphereCollider:Collider{public float radius;}
 public class CapsuleCollider:Collider{public float radius,height;}
 public class BoxCollider:Collider{public Vector3 size;}
}
namespace Il2CppSystem.Collections.Generic{public class List<T>:System.Collections.Generic.List<T>{}}
namespace Il2Cpp
{
 public class NativeObject{static int n;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref n);public T TryCast<T>()where T:class=>this as T;}
 public class Actor:NativeObject{public UnityEngine.GameObject gameObject=new(){layer=9};public UnityEngine.Vector3 Position;public UnityEngine.Vector3 position()=>Position;}
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class ItemData:NativeObject{public int itemType;public bool Tutorial;public bool isTutorialItem()=>Tutorial;}
 public static class Item{public static bool isKey(int type)=>type==100;}
 public static class ItemList{public static bool isCraftingItem(int type)=>type==101;}
 public class PickupGroundObjectInteraction:NativeObject
 {
  public bool AwakeCompleted;public Actor player;public uint id;public UnityEngine.Transform transform=new();public UnityEngine.GameObject gameObject=new();
  public UnityEngine.Collider[] Colliders={new UnityEngine.SphereCollider{radius=5.6f}};
  public T[] GetComponentsInChildren<T>(bool inactive)where T:class=>Colliders.OfType<T>().ToArray();
  public void initialise(Actor a,uint value){if(!AwakeCompleted)throw new NullReferenceException("ConditionHandler unavailable before native Awake");player=a;id=value;LastEpochLAN.Mod.OfflineTools.OfflinePickup.Configure(this);}
 }
 public class GoldPickupInteraction:PickupGroundObjectInteraction{public void PickUp(){}}
 public class PotionPickupInteraction:PickupGroundObjectInteraction{}
 public class PickupAncientBonesInteraction:PickupGroundObjectInteraction{}
 public class OtherPickupInteraction:PickupGroundObjectInteraction{}
 public class AutoPool
 {
  public class Pool<T>where T:PickupGroundObjectInteraction,new(){public T prefabT=new();}
  public class Subscriber<T>where T:PickupGroundObjectInteraction,new()
  {
   public Pool<T> Pool=new();public System.Collections.Generic.List<T> Active=new();public int Allocated,Released;public bool LastInstantiateInactive;
   public T instantiate(UnityEngine.Vector3 pos,UnityEngine.Quaternion rot,UnityEngine.Transform parent,bool inactive)
   {
    LastInstantiateInactive=inactive;var item=new T();item.gameObject.Activated=()=>item.AwakeCompleted=true;item.gameObject.SetActive(!inactive);item.transform.position=pos;item.transform.Parent=parent;
    item.Colliders=Pool.prefabT.Colliders.Select(c=>c switch{UnityEngine.SphereCollider s=>(UnityEngine.Collider)new UnityEngine.SphereCollider{radius=s.radius,isTrigger=s.isTrigger},UnityEngine.CapsuleCollider cap=>new UnityEngine.CapsuleCollider{radius=cap.radius,height=cap.height,isTrigger=cap.isTrigger},UnityEngine.BoxCollider b=>new UnityEngine.BoxCollider{size=b.size,isTrigger=b.isTrigger},_=>new UnityEngine.Collider()}).ToArray();
    Active.Add(item);Allocated++;return item;
   }
   public void Release(T item){Active.Remove(item);Released++;item.transform.Parent=null;item.gameObject.SetActive(false);}
  }
 }
 public enum StackableItemFlags{All=255}
 public class GroundItemVisuals:NativeObject
 {
  public ItemData itemData;public uint id;public UnityEngine.Transform transform=new();
  public void initialise(){}public void beforePooling(){}public void OnDestroy(){}
 }
 public class GroundItemLabel{public ItemData Data;public ItemData getItemData()=>Data;public void ClickedItem(){}}
 public class GroundItemManager
 {
  public static GroundItemManager instance=new();
  public class GroundItem{public ItemData itemData;public UnityEngine.Vector3 location;public uint id;}
  public class GroundItemList{public Il2CppSystem.Collections.Generic.List<GroundItem> items=new();}
  public GroundItemList offlineItems=new();
  public AutoPool.Subscriber<GoldPickupInteraction> goldPileSubscriber=new();
  public AutoPool.Subscriber<PotionPickupInteraction> potionSubscriber=new();
  public AutoPool.Subscriber<PickupAncientBonesInteraction> ancientBoneSubscriber=new();
  public Il2CppSystem.Collections.Generic.List<GoldPickupInteraction> activeGoldPiles=new();
  public Il2CppSystem.Collections.Generic.List<PotionPickupInteraction> activePotions=new();
  public Il2CppSystem.Collections.Generic.List<PickupAncientBonesInteraction> activeAncientBones=new();
  public int ItemCalls,VacuumCalls;public bool ThrowOnItem;public Action<uint> PoolVisual;
  public void BeforeSceneUnloaded(){}public void ChangeZone(){}public void OnDestroy(){}
  public void dropGoldForPlayer(){}
  public void pickupItem(Actor a,uint id,StackableItemFlags flags)
  {
   if(ThrowOnItem)throw new InvalidOperationException("native test failure");
   ItemCalls++;if(LastEpochLAN.Mod.OfflineTools.OfflinePickup.ItemPickupDepth==0)vacuumNearbyStackableItems();PoolVisual?.Invoke(id);
  }
  public void vacuumNearbyStackableItems(){VacuumCalls++;}
 }
}
namespace LastEpochLAN.Mod.OfflineTools
{
 internal class Settings{public bool Keys=true,Gold=true,Potions=true,Materials=true,Bones=true,RangePickup=true;}
 internal static class OfflineToolkit{public static bool Ready=true,Enabled=true;public static Settings Settings=new();}
 internal static class OfflineEconomy{internal static float GoldMultiplier()=>1;}
}
