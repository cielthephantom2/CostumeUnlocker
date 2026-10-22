using Il2Cpp;
using LastEpochLAN.Core;

namespace LastEpochLAN.Mod;

internal sealed partial class SharedStashPriorityRouting
{
    private readonly Dictionary<string,StashPriority> decoded=new();
    internal int Choose(SharedStashTab[] tabs,SharedStashCategory[] categories,int[] counts,int selected,ItemDataUnpacked item,int level)
    {
        if(decoded.Count>96)decoded.Clear();
        bool Match(string rule)
        {
            if(rule=="")return false;
            if(!decoded.TryGetValue(rule,out var native)){native=SharedStashPriorityCodec.Decode(rule);decoded.Add(rule,native);}
            return native.HasValue() && native.Match(item,level);
        }
        bool Room(SharedStashTab tab)=>!tab.Deleted && (counts.Length!=tabs.Length || counts[tab.Id]<SharedStashRules.ItemsPerTab);
        // A tab-specific match takes precedence over an inherited folder rule.
        foreach(var tab in tabs.Where(Room))if(Match(tab.Priority))return tab.Id;
        foreach(var tab in tabs.Where(Room))
            if(!tab.IgnoreFolderPriority && Match(categories.Single(c=>c.Id==tab.Category).Priority))return tab.Id;
        return selected;
    }
}
