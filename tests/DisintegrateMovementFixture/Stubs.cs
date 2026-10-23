using System.Reflection;
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}}public static class AccessTools{public static MethodInfo Method(Type t,string name)=>t.GetMethod(name);}}
namespace Il2Cpp{
 public class Obj{static int next;public IntPtr Pointer=(IntPtr)(++next);public T TryCast<T>()where T:class=>this as T;}
 public class Component:Obj{public Actor Owner;public T GetComponent<T>()where T:class=>Owner?.GetComponent<T>();}
 public class Actor:Obj{public Dictionary<Type,object> Components=new();public Agent navMeshAgent=new();public T GetComponent<T>()where T:class=>typeof(T)==typeof(Actor)?this as T:Components.TryGetValue(typeof(T),out var value)?value as T:null;}
 public class Agent{public float speed;}
 public enum AbilityMovement{None,Spin=4}public enum AbilityID{disintegrate,fireball}
 public class Ability:Obj{public static Dictionary<AbilityID,Ability> Registry=new(){{AbilityID.disintegrate,new()},{AbilityID.fireball,new()}};public static Ability getAbility(AbilityID id)=>Registry[id];}
 public class AbilityMutator:Component{public AbilityMovement getMovementDuringUse()=>AbilityMovement.None;public bool getStopMovementOnKeyRelease()=>false;public bool getChangeDestinationWhileMoving()=>false;public bool DontStopAtDestination()=>false;public bool getCanUpdateTargetWhileChannelling()=>false;public bool getIncreasedMovementSpeedAffectsMovement()=>false;public bool getUseSpeedAffectsMovement()=>true;public float getBaseMovementDuration()=>0;public float getMovementDurationPerUnitDistance()=>0;}
 public class DisintegrateMutator:AbilityMutator{public Actor actor;}
 public class SpeedManager:Component{public Actor actor;public int Calls;public float NormalSpeed=10;public void updateSpeed(){Calls++;actor.navMeshAgent.speed=NormalSpeed;}}
 public class MovementFromAbility:Component{public Actor actor;public void startMoving(Ability _ability){}}
 public class UsingAbility:Component{public bool channelling;public Ability ability;public void stopUsingAbility(bool canChangeState){}}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;}
}
namespace Il2CppLocalizationSystem{public static class LocalizationAbilities{public static string GetDescription(Il2Cpp.Ability ability)=>"native";}}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}
