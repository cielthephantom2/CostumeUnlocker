namespace HarmonyLib { [AttributeUsage(AttributeTargets.Class)] public class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string name){} } }
namespace LastEpochLAN.Mod.OfflineTools { public static class OfflineToolkit { public static bool Enabled=true; } }
namespace Il2Cpp {
 public class ItemData {
  private static int next;public IntPtr Pointer=(IntPtr)(++next);
  public int itemType,subType,forgingPotential;
  public void applyForgingPotentialCost(){} public void SetForgingPotential(int value)=>forgingPotential=value;
  public void RebuildID(){} public void CreateGameplayDuplicate(){}
 }
 public class CraftingManager { public void DuplicateItem(){} }
 public class OneItemContainer { public ItemData Item; public ItemData getItem()=>Item; }
 public class Text { public string text; }
 public class CraftingSlotManager {
  public OneItemContainer main=new(),modifier=new();
  public int minPotentialCost,maxPotentialCost;
  public Text minForgingPotentialText=new(),maxForgingPotentialText=new();
  public void UpdateForgingPotentialCostDisplay(){}
 }
}
