namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute{}
 public static class AccessTools{public static System.Reflection.MethodInfo Method(Type t,string n,Type[] parameters=null)=>parameters==null?t.GetMethod(n):t.GetMethod(n,parameters);}
}
namespace UnityEngine {public static class Time{public static int frameCount=1;}}
namespace Il2Cpp {
 public class Dying{public void onEnter(){}}
 public class DeathItemDrop{public void OnDeath(){}}
 public class ItemDrop{public void DropItem(){}}
 public class Actor{}
 public enum StackableItemFlags{All}
 public class GroundItemManager{public void dropItemForPlayer(){} public void pickupItem(Actor actor,uint id,StackableItemFlags flags){} public void pickupItem(int unrelatedOverload){}}
}
namespace LastEpochLAN.Mod.OfflineTools {public static class OfflineToolkit{public static bool Ready,Enabled=true;}}
