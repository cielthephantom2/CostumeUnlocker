using System.Reflection;
namespace MelonLoader{public static class MelonLogger{public static List<string> Errors=new();public static void Warning(string text)=>Errors.Add(text);}}
namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type type,string method){}}
}
namespace Il2Cpp
{
 public class Actor{static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);}
 public static class Notifications
 {
  public static List<(IntPtr Slot,int Charges,int Items)> Messages=new();public static bool Throw;
  public static void ProphecyFulfilled(Il2CppLE.Factions.ProphecySlot slot,int charges,Il2CppLE.Factions.ProphecyTargetType target)
  {
   bool run=(bool)typeof(LastEpochLAN.Mod.OfflineTools.OfflineProphecyNotificationPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{slot,charges,target});
   if(!run)return;if(Throw)throw new InvalidOperationException("UI destroyed");Messages.Add((slot.Pointer,charges,charges*slot.BaseQuantity));
  }
 }
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
}
namespace LastEpochLAN.Mod.OfflineTools{internal static class OfflineToolkit{internal static bool Enabled=true;}}
namespace Il2CppLE.Factions
{
 public enum ProphecyTargetType{Shrine,RiftBeast,Boss}
 public class ProphecySlot
 {
  static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);
  public byte CompletedCharges;public int BaseQuantity,Items,NativeCalls,Events,EventCharges;
  public bool Valid=true,Reject,NoProgress,ThrowAfterSpend;public Action OnFulfilled;public ProphecyTargetType Target=ProphecyTargetType.Shrine;
  static int Tier(int count)=>count<2?1:count<3?2:count<11?3:count<20?4:count<30?5:6;
  public bool TryTriggerReward(ProphecyTargetType target,bool doubleRewards,Il2Cpp.Actor actor,out int consumed)
  {
   object[] args={this,target,doubleRewards,actor,0,false};
   bool run=(bool)typeof(LastEpochLAN.Mod.OfflineTools.OfflineProphecyConsumeAllPatch).GetMethod("Prefix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
   if(!run){consumed=(int)args[4];return (bool)args[5];}
   NativeCalls++;consumed=0;
   if(!Valid || Reject || target!=Target || CompletedCharges==0)return false;
   if(NoProgress)return true;
   // Mimic the inlined native limits; deliberately DO NOT call patched helper.
   consumed=Tier(CompletedCharges);CompletedCharges-=(byte)consumed;
   if(ThrowAfterSpend)throw new InvalidOperationException("native spawn failure after consuming charge");
   Items+=BaseQuantity*consumed*(doubleRewards?2:1);Events++;EventCharges+=consumed;Il2Cpp.Notifications.ProphecyFulfilled(this,consumed,target);OnFulfilled?.Invoke();return true;
  }
  public int GetChargesToUseWhenTriggered()
  {
   object[] args={this,Tier(CompletedCharges)};
   typeof(LastEpochLAN.Mod.OfflineTools.OfflineProphecyChargeCountPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);return (int)args[1];
  }
  public string GetChargesToUseWhenTriggeredUIString()
  {
   object[] args={this,CompletedCharges<2?"":$"<color=#808080>Consumes {Tier(CompletedCharges)} charges</color>"};
   typeof(LastEpochLAN.Mod.OfflineTools.OfflineProphecyChargeTextPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);return (string)args[1];
  }
 }
}
