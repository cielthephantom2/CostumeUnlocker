namespace HarmonyLib
{
 public class HarmonyPatch:Attribute{public HarmonyPatch(){}public HarmonyPatch(Type t,string m){}}
 public static class AccessTools{public static System.Reflection.MethodInfo Method(Type t,string m)=>t.GetMethod(m);}
}
namespace UnityEngine
{
 public enum HideFlags{HideAndDontSave}
 public class Object
 {
  public HideFlags hideFlags;public bool Destroyed;
  public static T Instantiate<T>(T source)where T:Object=>(T)typeof(object).GetMethod("MemberwiseClone",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(source,null);
  public static void Destroy(Object o){o.Destroyed=true;}
 }
 public static class Time{public static int frameCount;}
}
namespace Il2CppSystem
{
 public class Nullable<T> where T:struct{public bool HasValue;public T Value;public Nullable(){}public Nullable(T value){Value=value;HasValue=true;}}
}
namespace Il2CppLE.Core
{
 public class RngElement
 {
  public uint State;public RngElement(uint seed){State=seed;}
  public bool Roll(float chance){State=unchecked(State*1664525+1013904223);return State/(double)uint.MaxValue<chance;}
 }
}
namespace Il2Cpp
{
 public class Data{public string Id="character-a";}
 public class Tracker{public Data charData=new();}
 public class Actor:UnityEngine.Object{public IntPtr Pointer=(IntPtr)1;public Tracker characterDataTracker=new();}
 public static class PlayerFinder{public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
 public class GroundItemManager{public void BeforeSceneUnloaded(){}}
 public class UIBase{public void ExitToCharacterSelect(){}public void OnApplicationQuit(){}}
 public class ClientSceneService{public void LoadSceneAsync(){}}
}
namespace Il2CppLE.Factions
{
 public class ProphecySlot{public enum LensType{Celerity,Duplication,Charity,Tyranny,Origin,Finality,Anomaly,Curiosity,Acceleration,CelestialScales,Prowess,Quality}}
 public class ProphecyData
 {
  public static ProphecyData Data=new();
  public float duplicationLensChance=.5f,celestialScalesDuplicationChance=.5f,celestialScalesGroleEggReplacementChance=.5f;
 }
 public class ProphecySlotReward:UnityEngine.Object
 {
  public enum ItemGenerationType{GenericEquipment,SpecificEquipment,NonEquipment,BossLoot}
  public static Il2CppLE.Core.RngElement rng=new(123);
  public static readonly List<(int Count,int Lens)> Calls=new();
  public static bool ThrowNative;
  public int itemsDropped;public ItemGenerationType itemGenerationType;
  public void SpawnRewardForPlayer(Il2Cpp.Actor actor,Il2CppSystem.Nullable<ProphecySlot.LensType> lens)
  {
   if(LastEpochLAN.Mod.OfflineTools.ProphecyRewards.Defer(this,actor,lens))return;
   int count=itemsDropped;int kind=lens.HasValue?(int)lens.Value:-1;
   if(kind==1 && rng.Roll(ProphecyData.Data.duplicationLensChance))count*=2;
   if(kind==9)
   {
    if(rng.Roll(ProphecyData.Data.celestialScalesDuplicationChance))count*=2;
    if(rng.Roll(ProphecyData.Data.celestialScalesGroleEggReplacementChance))count=1;
   }
   Calls.Add((count,kind));if(ThrowNative)throw new Exception("partial native drop");
  }
 }
}
namespace MelonLoader{public static class MelonLogger{public static void Warning(string s){}public static void Error(string s){}}}
namespace LastEpochLAN.Mod.OfflineTools
{
 public static class NativeProphecyRewardSpawn{public static string Error="";public static void Deliver(Il2CppLE.Factions.ProphecySlotReward r,Il2Cpp.Actor p,Il2CppSystem.Nullable<Il2CppLE.Factions.ProphecySlot.LensType> lens)=>r.SpawnRewardForPlayer(p,lens);}
 public static class OfflineToolkit{public static bool Ready=true;}
 public static class DeathHitchTrace{public readonly struct Sample{public Sample(string s){}public void End(){}}}
}
