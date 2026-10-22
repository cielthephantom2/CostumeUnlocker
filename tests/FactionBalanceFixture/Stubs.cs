namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute
    {public HarmonyPatch(Type type,string name){}public HarmonyPatch(Type type,string name,Type[] parameters){}}
}
namespace UnityEngine
{
    public class Object {private static int serial;public IntPtr Pointer {get;}=new(++serial);public static void Destroy(Object value){}}
    public class ScriptableObject:Object {public static T CreateInstance<T>() where T:new()=>new();}
    public static class Time {public static float unscaledTime;}
}
namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{public class Il2CppReferenceArray<T>:List<T>{public Il2CppReferenceArray(IEnumerable<T> values):base(values){}}}
namespace Il2Cpp
{
    public class Actor:UnityEngine.Object
    {
        public bool isPlayerActor=true;public GenerateItems generateItems=new();public Il2CppLE.Factions.IFactionInfoProvider FactionInfo;
    }
    public class GenerateItems {public float ChanceForRareItemsToBecomeExalted0To1;public float ChanceForAdditionalIdols0To1;public int AdditionalIdols;}
    public static class PlayerFinder {public static Actor Player=new();public static Actor getPlayerActor()=>Player;}
    public class RunePrisonItemDrops {public static readonly Il2CppLE.Core.RngElement rng=new();public void SpawnExperimentalItem(){}}
}
namespace Il2CppLE.Core
{
    public class RngElement:UnityEngine.Object
    {
        public int StochasticRound(float value){LastEpochLAN.Mod.OfflineTools.MageLootScope.Current?.Adjust(Pointer,ref value);return (int)MathF.Round(value);}
    }
}
namespace Il2CppLE.Factions
{
    public enum FactionID {CircleOfFortune,MerchantsGuild,TheWeaver}
    public class Faction {public T TryCast<T>() where T:class=>this as T;}
    public class CircleOfFortune:Faction
    {
        public bool IsMember=true;public int Rank=12;public Dictionary<int,bool> rankToggles=new();public Il2Cpp.Actor CachedPlayerActor=Il2Cpp.PlayerFinder.Player;
        public float ChanceForDoubleEnemyRewards0To1,ChanceForDoubleEchoRewards0To1,ChanceToForceExaltedMultiplier,UniquesLPRollMultiplier,IncreasedChanceToDropBossSpecificLoot;
        public bool ExiledMagesDropExperimentalItemsTwice=true;
        public void ToggleRank(int rank,bool enable,bool rankChangedViaGameplay){}
    }
    public class TheWeaver:Faction
    {
        public bool IsMember=true;public int Rank=10;public Il2Cpp.Actor CachedPlayerActor=Il2Cpp.PlayerFinder.Player;public Dictionary<int,bool> rankToggles=new();
        public float IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols,MoreMemoryAmberDropRate;public void ToggleRank(){}
    }
    public interface IFactionInfoProvider {bool TryGetFaction(FactionID id,out Faction faction);}
    public class Provider:IFactionInfoProvider
    {
        public Faction Value;public bool TryGetFaction(FactionID id,out Faction faction){faction=Value;return Value!=null;}
    }
    public class ProphecySlotReward:UnityEngine.ScriptableObject
    {
        public int itemsDropped;public ProphecySlotReward upgradedReward;
        public int ItemsDropped=>itemsDropped;public int GetItemsDropped(int charges)=>charges*itemsDropped;
    }
    public class ProphecyData:UnityEngine.ScriptableObject
    {
        public static ProphecyData Data;public Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ProphecySlotReward> rewards;
        public void InitializeInstance(){}
    }
    public class FactionData {public class RankData {public List<string> rankDescriptionLines=new();public string GetRankDescription(int rank,FactionID id,bool lessLineBreaks)=>"";}}
}
namespace LastEpochLAN.Mod.OfflineTools
{
    internal static class OfflineToolkit {internal static bool Enabled=true,Ready=true;}
}
