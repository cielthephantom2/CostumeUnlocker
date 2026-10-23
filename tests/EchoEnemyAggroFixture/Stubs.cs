namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string name){}}}
namespace Il2Cpp{
 public readonly record struct Scene(int Id,bool isLoaded=true);
 public class GameObject{public Scene scene=new(1);}
 public class StateController{static int next;public IntPtr Pointer=(IntPtr)(++next);public bool autoCombatDisabled;public float aggroRangeModifier;}
 public class Alignment{public bool Hostile;public bool IsFoe(Alignment other)=>Hostile;}
 public class ActorVisuals{public bool isActiveAndEnabled=true,isPlayer,dead,isCorpse;public Alignment alignment=new(){Hostile=true};public ActorSync actorSync;}
 public class ActorSync{public bool IsDead;public ActorVisuals actorVisuals=new();}
 public class Actor{
  static int next;public IntPtr Pointer=(IntPtr)(++next);public bool isPlayerActor;public StateController stateController=new();public GameObject gameObject=new();public ActorSync Sync=new();public Alignment Alignment=new(){Hostile=true};public bool Dead;public ActorSync GetActorSync()=>Sync;public Alignment GetAlignment()=>Alignment;public bool isDying(bool unused)=>Dead;
 }
 public class UsingAbility{public Actor Actor;public StateController controller;public bool activated;public void Activate(){}public Actor getActor()=>Actor;public void activateIfEnemyInRange(){}public static bool DisplacementWithinAggroBox(float x,float z,float multiplier)=>false;}
 public static class PlayerFinder{public static Actor Player=new(){isPlayerActor=true};public static Actor getPlayerActor()=>Player;}
 public class MonolithZoneManager{public static MonolithZoneManager instance=new();public GameObject gameObject=new();}
}
namespace LastEpochLAN.Mod.OfflineTools{
 public static class OfflineToolkit{public static bool Enabled=true;}
 public static class EnemyMapDots{public static bool Eligible(Il2Cpp.ActorVisuals v,Il2Cpp.Alignment local)=>v!=null && v.isActiveAndEnabled && !v.isPlayer && !v.dead && !v.isCorpse && v.actorSync?.IsDead!=true && v.alignment?.IsFoe(local)==true;}
}
