namespace HarmonyLib
{
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute
 {public HarmonyPatch(Type type,string method){}public HarmonyPatch(Type type,string method,Type[] args){}}
}
namespace Il2Cpp
{
 public class Component
 {
  static int next;public IntPtr Pointer=(IntPtr)Interlocked.Increment(ref next);
  public object Attached;public T GetComponent<T>()where T:class=>Attached as T;
  public T TryCast<T>()where T:class=>this as T;
 }
 public class Actor:Component{public BaseMana mana;}
 public static class PlayerFinder{public static Actor Player;public static Actor getPlayerActor()=>Player;}
 public class LocalTreeData:Component{public Actor actor;}
 public class MeteorSkillTree:Tree{public static void updateMutator(){}}
 public class MeteorMutator:Component{public float chanceFor100increasedArea;public int frostClawCastsOnImpact;}
 public class MeteorAoEMutator:Component{public Actor actor;public int frostClawCastsOnImpact;public void Mutate(){}}
 public class AbilityMutator{}
 public enum AbilityID{fireball=1,frostClaw=708}
 public class BaseMana:Component{public float getManaCost()=>0;}
 public class Tree:Component{public string treeID;}
 public class SkillTreeNode
 {
  public Tree tree;public byte id;
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
namespace Il2CppLE.UI.Tooltips
{
 public class NodeTooltipModifierInfo{public string Value;}
 public class NodeTooltipInfo{public string Description;}
 public class NodeTooltipContentBuilder{public static void GetModifierInfo(){}public static void GetNodeTooltipInfo(){}}
}
namespace LastEpochLAN.Mod.OfflineTools{internal static class OfflineToolkit{internal static bool Enabled=true;}}
