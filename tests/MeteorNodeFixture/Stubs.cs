namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute
 {public HarmonyPatch(Type type,string method){}public HarmonyPatch(Type type,string method,Type[] args){}}
}
namespace Il2Cpp
{
 public enum AilmentID{Root=20,ArcaneAscendance=57}
 public class Ailment:UnityEngine.Object{public AilmentID id;public bool roots;public float duration;}
 public class AilmentReceiver{public Actor actor;public void ApplyAilmentWithDamageStats(){}}
 public class Component
 {
  static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);
  public object Attached;public T GetComponent<T>()where T:class=>Attached as T;
  public T TryCast<T>()where T:class=>this as T;
 }
 public class Actor:Component{public BaseMana mana;public LocalTreeData localTreeData;}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;}
 public class LocalTreeData:Component
 {
  public Actor actor;public SkillTreeData Skill;public SkillTreeData getSkillTreeData(Ability ability)=>Skill;
  public class TreeData{public string treeID="Meteor";public List<NodeData> nodes=new();}
  public class SkillTreeData:TreeData{}
  public class NodeData{public byte id,pointsAllocated;}
 }
 public class ArcaneAscendanceTree:Tree{public static void updateMutator(){}}
 public enum SP{ManaRegen=18,ManaEfficiency=69}
 public class Stats{public class Stat{public SP property;public int tags,specialTag,extraTag;public float addedValue,increasedValue;public List<float> moreValues=new();}}
 public class ArcaneAscendanceMutator:Component{public Actor actor;public List<Stats.Stat> statsWhileActive=new();public void Mutate(){}}
 public class MeteorSkillTree:Tree{public static void updateMutator(){}}
 public class MeteorMutator:Component{public Actor actor;public float chanceFor100increasedArea;public int frostClawCastsOnImpact;public void Mutate(){}}
 public class MeteorAoEMutator:Component{public Actor actor;public int frostClawCastsOnImpact;public void Mutate(){}}
 public class AbilityMutator{}
 public class Ability{public string playerAbilityID;public static Ability getAbility(AbilityID id)=>new();}
 public enum AbilityID{fireball=1,meteor=7,frostClaw=708}
 public class BaseMana:Component
 {public int maxMana=100,CurrentManaRefundCastID=1;public float getManaCost()=>0;public void RefundManaFromMostRecentCast(float manaToAttemptToRefund){}}
 public class CharacterMutator:Component{public Actor actor;public BaseMana mana;public float refund20PercentCostChanceBasedOnMaxMana;public void OnAbilityUse(){}}
 public class Tree:Component{public string treeID;}
 public class SkillTreeNode
 {
  public Tree tree;public byte id;public Il2CppSystem.Collections.Generic.List<NodeTooltipStat> stats=new();
  public class NodeTooltipStat{public string value;}
 }
 public class GlobalTreeData
 {
  public static GlobalTreeData Instance=new();public static GlobalTreeData get()=>Instance;
  public Dictionary<string,TreeData> Trees=new();public TreeData getTreeData(string id)=>Trees.GetValueOrDefault(id);
  public class TreeData{public Dictionary<byte,NodeData> Nodes=new();public NodeData getNode(byte id)=>Nodes.GetValueOrDefault(id);}
  public class NodeData{public string name;}
 }
 public class NodeTooltipManager
 {
  public class Text{public string text;}public Text description=new();public bool requireLayoutUpdate;
  public static void BuildColoredValue(){}public void setInfoForNode(){}
 }
}
namespace UnityEngine
{
 public enum HideFlags{HideAndDontSave}
 public class Object{public string name;public HideFlags hideFlags;public static T Instantiate<T>(T original)where T:Object=>(T)original.MemberwiseClone();}
}
namespace Il2CppLE.UI.Tooltips
{
 public class NodeTooltipModifierInfo{public string Value;}
 public class NodeTooltipInfo{public string Description;}
 public class NodeTooltipContentBuilder{public static void GetModifierInfo(){}public static void GetNodeTooltipInfo(){}}
}
namespace LastEpochLAN.Mod.OfflineTools{internal static class OfflineToolkit{internal static bool Enabled=true;}}
namespace Il2CppLocalizationSystem{public class LocalizationAbilities{public string GetDescription(Il2Cpp.Ability ability)=>"";}}
namespace Il2CppLocalizationSystem{public class LocalizationSkills{public string GetNodeDescription(string treeID,byte nodeID,string originalValue)=>originalValue;public string GetStatName(string treeID,byte nodeID,int statIndex,string originalValue)=>originalValue;}}

namespace Il2CppSystem.Collections.Generic
{
 public class List<T>:System.Collections.Generic.List<T>
 {static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);}
}
