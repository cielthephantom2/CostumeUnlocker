namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type,string method){} } }
namespace LastEpochLAN.Mod.OfflineTools { internal static class OfflineToolkit {internal static bool Enabled=true;} }
namespace Il2CppSystem {
 public struct Nullable<T> where T:struct {public Nullable(T value){Value=value;HasValue=true;} public bool HasValue;public T Value;}
}
namespace Il2Cpp {
 public enum PlayerMinionStance {None,Protect,Assassinate}
 public sealed class CreationReferences {public Actor creator;}
 public sealed class StateController {public float aggroRangeModifier;}
 public sealed class Actor {
  public IntPtr Pointer;public StateController stateController=new(); public CreationReferences References;
  public PlayerMinionStance Stance;
  public PlayerMinionStance GetPlayerMinionStance()=>Stance;
  public CreationReferences GetCreationReferences()=>References;
 }
 public static class PlayerFinder {public static Actor Player;public static Actor getPlayerActor()=>Player;}
 public sealed class UsingAbility {public Actor Actor;public Actor getActor()=>Actor;public void activateIfEnemyInRange(){} }
 public sealed class TargetFinder {public Actor thisActor;public Il2CppSystem.Nullable<float> MaxDistanceFromSummoner;public void updateTarget(){} }
}
