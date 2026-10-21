using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Data;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal static class SharedStashPriorityCodec
{
    internal static string Encode(StashPriority priority)
    {
        if(priority==null || !priority.HasValue())return "";
        var p=priority.PotentialStashPriority.Serialize();var l=priority.LevelStashPriority.Serialize();
        var value=new SharedStashPriority{
            Rarity=priority.RarityStashPriority.Serialize().Rarity,Class=priority.ClassStashPriority.Serialize().Requirement,
            Corruption=priority.CorruptionStashPriority.Serialize().Corruption,MinLevel=l.MinLevel,MaxLevel=l.MaxLevel,
            Potential=new[]{p.MinLegendaryPotential,p.MaxLegendaryPotential,p.MinWeaversWill,p.MaxWeaversWill,
                p.MinWeaversTouch,p.MaxWeaversTouch,p.MinForgingPotential,p.MaxForgingPotential},
            Types=priority.TypeStashPriority.SerializeTypes()?.ToArray()??Array.Empty<int>(),
            Subtypes=priority.TypeStashPriority.SerializeSubtypes()?.ToArray()??Array.Empty<int>(),
            Factions=priority.FactionStashPriority.SerializeFactions()?.ToArray()??Array.Empty<int>(),
            Uniques=priority.UniquesStashPriority.SerializeUniqueIds()?.ToArray()??Array.Empty<int>()};
        string json=JsonSerializer.Serialize(value);
        if(!SharedStashPriority.ValidJson(json))throw new InvalidDataException("Priority configuration exceeds shared limits");
        return json;
    }
    internal static StashPriority Decode(string json)
    {
        var result=new StashPriority();if(json=="")return result;
        if(!SharedStashPriority.ValidJson(json))throw new InvalidDataException("Invalid shared priority configuration");
        var value=JsonSerializer.Deserialize<SharedStashPriority>(json)!;var p=value.Potential;
        result.PotentialStashPriority.Deserialize(new PotentialStashPriorityMessage{
            MinLegendaryPotential=p[0],MaxLegendaryPotential=p[1],MinWeaversWill=p[2],MaxWeaversWill=p[3],
            MinWeaversTouch=p[4],MaxWeaversTouch=p[5],MinForgingPotential=p[6],MaxForgingPotential=p[7]});
        result.LevelStashPriority.Deserialize(new LevelStashPriorityMessage{MinLevel=value.MinLevel,MaxLevel=value.MaxLevel});
        result.RarityStashPriority.Deserialize(new RarityStashPriorityMessage{Rarity=value.Rarity});
        result.ClassStashPriority.Deserialize(new ClassStashPriorityMessage{Requirement=value.Class});
        result.CorruptionStashPriority.Deserialize(new CorruptionStashPriorityMessage{Corruption=value.Corruption});
        result.TypeStashPriority.Deserialize(value.Types,value.Subtypes);
        result.FactionStashPriority.Deserialize(value.Factions);result.UniquesStashPriority.Deserialize(value.Uniques);
        return result;
    }
}
