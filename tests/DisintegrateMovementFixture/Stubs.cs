using System.Reflection;
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}}public static class AccessTools{public static MethodInfo Method(Type t,string name)=>t.GetMethod(name);}}
namespace Il2Cpp{
 public class Obj{static int next;public IntPtr Pointer=(IntPtr)(++next);public T TryCast<T>()where T:class=>this as T;}
 public class Component:Obj{public Actor Owner;public T GetComponent<T>()where T:class=>Owner?.GetComponent<T>();}
 public class Actor:Obj{public Dictionary<Type,object> Components=new();public LocalTreeData localTreeData;public Agent navMeshAgent=new();public StateController stateController=new();public UnityEngine.Vector3 Position;public bool Dead;public UnityEngine.Vector3 position()=>Position;public bool isDying(bool acceptFakeDying)=>Dead;public T GetComponent<T>()where T:class=>typeof(T)==typeof(Actor)?this as T:Components.TryGetValue(typeof(T),out var value)?value as T:null;}
 public class Agent{public float speed;}
 public enum AbilityMovement{None,Spin=4}public enum AbilityID{disintegrate,fireball}
 public class Ability:Obj{public static Dictionary<AbilityID,Ability> Registry=new(){{AbilityID.disintegrate,new()},{AbilityID.fireball,new()}};public static Ability getAbility(AbilityID id)=>Registry[id];}
 public class AbilityMutator:Component{public AbilityMovement getMovementDuringUse()=>AbilityMovement.None;public bool getStopMovementOnKeyRelease()=>false;public bool getChangeDestinationWhileMoving()=>false;public bool DontStopAtDestination()=>false;public bool getCanUpdateTargetWhileChannelling()=>false;public bool getIncreasedMovementSpeedAffectsMovement()=>false;public bool getUseSpeedAffectsMovement()=>true;public float getBaseMovementDuration()=>0;public float getMovementDurationPerUnitDistance()=>0;}
 public class DisintegrateMutator:AbilityMutator{public Actor actor;public float increasedRange;public void Mutate(){}}
 public class SpeedManager:Component{public Actor actor;public int Calls;public float NormalSpeed=10;public void updateSpeed(){Calls++;actor.navMeshAgent.speed=NormalSpeed;}}
 public class MovementFromAbility:Component{public Actor actor;public bool moving=true,changeDestinationWithMousePosition=true,dontStopAtDestination;public AbilityMovement movementType=AbilityMovement.Spin;public Ability currentAbility;public UnityEngine.Vector3 Destination;public int DestinationCalls;public void UpdateDestination(UnityEngine.Vector3 newDestination){Destination=newDestination;DestinationCalls++;}public void MovementStep(float deltaTime){}public void startMoving(Ability _ability){}}
 public class UsingAbility:Component{public bool channelling;public Ability ability;public MovementFromAbility movementFromAbility;public bool TryUpdateTargetLocationIfChannelling(UnityEngine.Vector3 newTargetLocation)=>true;public void UpdateTargetLocation(UnityEngine.Vector3 newTargetLocation){}public void stopUsingAbility(bool canChangeState){}}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;public static ItemContainersManager Containers=new();public static ItemContainersManager getItemContainersManager()=>Containers;}
 public class UsingAbilityPlayer:UsingAbility{public MovementFromAbility MovementFromAbility=>movementFromAbility;public BufferedCommandHolder bufferedCommandHolder=new();public bool UseAbilityCommand(Ability _ability)=>true;}
 public class BufferedCommandHolder{public int Clears;public void clearBufferedMovement(){Clears++;}}
 public class StateController{public UsingAbilityPlayer usingAbility;public Obj currentState;public int numberOfRoots;}
 public class MovingPlayer:Component{public UsingAbilityPlayer usingAbilityPlayer;public bool MoveToPoint(UnityEngine.Vector3 point,bool dontMoveIfMouseOverUI)=>false;}
 public class MovingPlayerThumbstick:Component{public UsingAbilityPlayer usingAbilityPlayer;public UnityEngine.Vector2 input;public UnityEngine.Vector3 WorldMovementDir;public void SetInput(UnityEngine.Vector2 value,bool shouldEnterState,bool wasPressedThisFrame){}}
 public class EpochInputManager{public static EpochInputManager instance=new();public bool forceDisableInput,cinematicActive,IsInputFieldActive;}
 public static class UIMouseListener{public static bool Disabled,Over;public static bool WorldActionsDisabled()=>Disabled;public static bool MouseOverUI()=>Over;}
 public class ItemContainersManager{public Cursor cursor=new();}public class Cursor{public List<int> Content=new();public List<int> GetContent()=>Content;}

}
namespace Il2CppLocalizationSystem{public static class LocalizationAbilities{public static string GetDescription(Il2Cpp.Ability ability)=>"native";}}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}

namespace UnityEngine{
 public struct Vector2{public float x,y;public Vector2(float a,float b){x=a;y=b;}public float sqrMagnitude=>x*x+y*y;}
 public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static Vector3 zero=>new();public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);public static Vector3 operator*(Vector3 a,float f)=>new(a.x*f,a.y*f,a.z*f);}
 public static class Application{public static bool isFocused=true;}public static class Time{public static float timeScale=1;}
}
namespace Il2CppLE.UI{public static class LoadingScreen{public static bool IsVisible;}}

namespace Il2Cpp{
 public class LocalTreeData:Component{public Actor actor;public SkillTreeData Skill;public SkillTreeData getSkillTreeData(Ability ability)=>Skill;public class TreeData{public string treeID;public List<NodeData> nodes=new();}public class SkillTreeData:TreeData{}public class NodeData{public byte id,pointsAllocated;}}
 public class GlobalTreeData{public static GlobalTreeData Instance=new();public Dictionary<string,TreeData> Trees=new();public static GlobalTreeData get()=>Instance;public TreeData getTreeData(string id)=>Trees.TryGetValue(id,out var tree)?tree:null;public class TreeData{public Dictionary<byte,NodeData> Nodes=new();public NodeData getNode(byte id)=>Nodes.TryGetValue(id,out var node)?node:null;}public class NodeData{public string name;}}
 public class SkillTree:Obj{public string treeID;}public class DisintegrateTree:SkillTree{public static void updateMutator(LocalTreeData localTreeData,LocalTreeData.TreeData tree){}}
 public class SkillTreeNode{public SkillTree tree;public byte id;public class NodeTooltipStat{public string value;}}
 public class TooltipText{public string text;}public class NodeTooltipManager{public TooltipText description=new();public bool requireLayoutUpdate;public void BuildColoredValue(){}public void setInfoForNode(){}}
}
namespace Il2CppLE.UI.Tooltips{public class NodeTooltipModifierInfo{public float Value;}public class NodeTooltipInfo{public string Description;}public static class NodeTooltipContentBuilder{public static void GetModifierInfo(){}public static void GetNodeTooltipInfo(){}}}
namespace Il2CppLocalizationSystem{public static class LocalizationSkills{public static void GetNodeDescription(){}}}
