namespace Il2CppLE.Data
{
    public struct PotentialStashPriorityMessage{public int MinLegendaryPotential,MaxLegendaryPotential,MinWeaversWill,MaxWeaversWill,MinWeaversTouch,MaxWeaversTouch,MinForgingPotential,MaxForgingPotential;}
    public struct LevelStashPriorityMessage{public int MinLevel,MaxLevel;}
    public struct RarityStashPriorityMessage{public int Rarity;}
    public struct ClassStashPriorityMessage{public int Requirement;}
    public struct CorruptionStashPriorityMessage{public int Corruption;}
}
namespace Il2Cpp
{
    using Il2CppLE.Data;
    public class ItemDataUnpacked{public int Rarity,Level;}
    public sealed class PotentialStashPriorityCondition
    {
        private PotentialStashPriorityMessage value=new(){MinLegendaryPotential=255,MaxLegendaryPotential=255,MinWeaversWill=255,MaxWeaversWill=255,MinWeaversTouch=255,MaxWeaversTouch=255,MinForgingPotential=255,MaxForgingPotential=255};
        public PotentialStashPriorityMessage Serialize()=>value;public void Deserialize(PotentialStashPriorityMessage message)=>value=message;
    }
    public sealed class LevelStashPriorityCondition{private LevelStashPriorityMessage value;public LevelStashPriorityMessage Serialize()=>value;public void Deserialize(LevelStashPriorityMessage message)=>value=message;}
    public sealed class RarityStashPriorityCondition{private RarityStashPriorityMessage value;public RarityStashPriorityMessage Serialize()=>value;public void Deserialize(RarityStashPriorityMessage message)=>value=message;}
    public sealed class ClassStashPriorityCondition{private ClassStashPriorityMessage value;public ClassStashPriorityMessage Serialize()=>value;public void Deserialize(ClassStashPriorityMessage message)=>value=message;}
    public sealed class CorruptionStashPriorityCondition{private CorruptionStashPriorityMessage value;public CorruptionStashPriorityMessage Serialize()=>value;public void Deserialize(CorruptionStashPriorityMessage message)=>value=message;}
    public sealed class TypeStashPriorityCondition{private int[] types=[],subtypes=[];public int[] SerializeTypes()=>types;public int[] SerializeSubtypes()=>subtypes;public void Deserialize(int[] a,int[] b){types=a;subtypes=b;}}
    public sealed class FactionStashPriorityCondition{private int[] values=[];public int[] SerializeFactions()=>values;public void Deserialize(int[] data)=>values=data;}
    public sealed class UniquesStashPriorityCondition{private int[] values=[];public int[] SerializeUniqueIds()=>values;public void Deserialize(int[] data)=>values=data;}
    public sealed class StashPriority
    {
        public PotentialStashPriorityCondition PotentialStashPriority=new();public LevelStashPriorityCondition LevelStashPriority=new();
        public RarityStashPriorityCondition RarityStashPriority=new();public ClassStashPriorityCondition ClassStashPriority=new();public CorruptionStashPriorityCondition CorruptionStashPriority=new();
        public TypeStashPriorityCondition TypeStashPriority=new();public FactionStashPriorityCondition FactionStashPriority=new();public UniquesStashPriorityCondition UniquesStashPriority=new();
        public static int Calls;public static int LastLevel;
        public bool HasValue()=>RarityStashPriority.Serialize().Rarity!=0 || ClassStashPriority.Serialize().Requirement!=0 || TypeStashPriority.SerializeTypes().Length>0;
        public bool Match(ItemDataUnpacked item,int level){Calls++;LastLevel=level;return item.Rarity==RarityStashPriority.Serialize().Rarity;}
    }
}
