namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch : Attribute
    { public HarmonyPatch() {} public HarmonyPatch(Type type,string name) {} }
    public static class AccessTools
    { public static System.Reflection.MethodInfo Method(Type type,string name) => type.GetMethod(name); }
}
namespace Il2Cpp
{
    public class SkillTreeNode {public T TryCast<T>()where T:class=>this as T;}
    public class NodeTooltipManager
    {
        public sealed class Text {public string text;}
        public Text description=new();public bool requireLayoutUpdate;
        public static string BuildColoredValue()=>"";public void setInfoForNode(){}
    }
    public sealed class MonolithZoneManager
    {
        public IntPtr Pointer {get;set;}
        public bool enemiesCanDropPileOfSimilarItems {get;set;}
        public float totalSpawners {get;set;}
        public void DropBonusLootForAnyActor() {}
        public float ChanceForEnemiesToDropPileOfSimilarItems() => 0;
        public static float SimilarItemDropMultiplierFromCorruption() => 0;
    }
    public sealed class WovenEchoData { public float GetTotalNaturalSpawnWeighting() => 0; }
}
namespace Il2CppLE.Gameplay.Monolith
{
    public static class MonolithItemManager { public static void TryDropSimilarItemsToImprintedWeaverTreeItem() {} }
}
namespace Il2CppLE.Factions
{
    public enum WeaverTreeNodeEffect { Other, ImprintContainerCachesDropMoreItemsOfType, ImprintContainerChampionsDropMoreItemsOfType,
        ImprintContainerEnemiesDropMoreItemsOfType, ImprintContainerEchoCompletionsDropMoreItemsOfType,
        ImprintContainerSpawnMoreWovenEchoesOfType, ImprintContainerSpawnMoreWovenEchoesOfTypeSecond }
    public class WeaverTreeNode:Il2Cpp.SkillTreeNode {public WeaverTreeNodeEffect nodeEffect;}
    public sealed class TheWeaver
    {
        public float OnCacheOpenedChanceForSimilarItems {get;set;}
        public float ImprintContainerChampionsChanceToDropMoreItemsOfType {get;set;}
        public void UpdateWeaverTreeNode() {}
    }
    public static class WeaverTreeNodesExtensions
    {
        public static void OverrideEchoTypes() {}
        public static void CanSpawnSpecialEchoesNaturally() {}
        public static void IncreaseChanceForSpecificEchoTypes() {}
        public static void SetNaturalSpawnedWovenEchoIslandData() {}
    }
}
namespace LastEpochLAN.Mod.OfflineTools
{ internal static class OfflineToolkit {internal static bool Enabled {get;set;}=true;} }
namespace Il2CppLE.UI.Tooltips
{
    public class NodeTooltipModifierInfo {public string Value;}
    public class NodeTooltipInfo {public string Description;}
    public static class NodeTooltipContentBuilder {public static void GetModifierInfo(){}public static void GetNodeTooltipInfo(){}}
}
