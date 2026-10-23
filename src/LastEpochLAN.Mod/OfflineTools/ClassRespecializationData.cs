using Il2Cpp;
using Il2CppLE.Data;

namespace LastEpochLAN.Mod.OfflineTools;

internal static class ClassRespecializationData
{
    // Hold original objects for rollback; never edit their contents in place.
    internal sealed record Snapshot(int Class,byte Mastery,byte Original,SavedCharacterTree Passive,
        Il2CppSystem.Collections.Generic.List<SavedSkillTree> Skills,
        Il2CppSystem.Collections.Generic.List<string> Progress,Il2CppSystem.Collections.Generic.List<string> Bar,
        Il2CppSystem.Collections.Generic.List<int> Bear,Il2CppSystem.Collections.Generic.List<int> Spriggan,
        Il2CppSystem.Collections.Generic.List<int> Swarm);
    internal static Snapshot Capture(CharacterData data)=>new(data.CharacterClass,data.ChosenMastery,data.OriginalMastery,
        data.SavedCharacterTree,data.SavedSkillTrees,data.CharacterTreeNodeProgression,data.AbilityBar,
        data.WerebearAbilityBar,data.SprigganFormAbilityBar,data.SwarmbladeAbilityBar);
    internal static void Restore(CharacterData data,Snapshot old)
    {
        data.CharacterClass=old.Class;data.ChosenMastery=old.Mastery;data.OriginalMastery=old.Original;
        data.SavedCharacterTree=old.Passive;data.SavedSkillTrees=old.Skills;data.CharacterTreeNodeProgression=old.Progress;
        data.AbilityBar=old.Bar;data.WerebearAbilityBar=old.Bear;data.SprigganFormAbilityBar=old.Spriggan;data.SwarmbladeAbilityBar=old.Swarm;
    }
    internal static void Apply(CharacterData data,CharacterClass target,ushort version)
    {
        if(data==null || target==null || (int)target.classID is <0 or >4)throw new ArgumentException("Invalid class");
        if(data.CharacterClass==(int)target.classID)return;
        var bar=new Il2CppSystem.Collections.Generic.List<string>();
        for(int i=0;i<5;i++)bar.Add(target.defaultAbilities!=null && i<target.defaultAbilities.Count ? target.defaultAbilities[i]?.playerAbilityID??"" : "");
        int refunded=Math.Max(0,data.SavedCharacterTree?.UnspentPoints??0);
        if(data.SavedCharacterTree?.NodePoints!=null)
            foreach(var points in data.SavedCharacterTree.NodePoints)refunded=checked(refunded+points);
        var passive=new SavedCharacterTree(target.treeID,version,new(),new()){UnspentPoints=refunded};
        data.CharacterClass=(int)target.classID;data.ChosenMastery=0;data.OriginalMastery=0;
        data.SavedCharacterTree=passive;data.SavedSkillTrees=new();data.CharacterTreeNodeProgression=new();
        data.AbilityBar=bar;data.WerebearAbilityBar=new();data.SprigganFormAbilityBar=new();data.SwarmbladeAbilityBar=new();
    }
}
