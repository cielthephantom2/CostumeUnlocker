namespace LastEpochLAN.Core;

public static class SharedStashRules
{
    public const int ItemsPerTab=96, InitialTabCount=6, MaxTabs=24, MaximumItems=ItemsPerTab*MaxTabs;
    public static bool ValidTabIndex(int tab)=>tab>=0 && tab<MaxTabs;
    public static SharedStashTab[] InitialTabs()=>Enumerable.Range(0,InitialTabCount).Select(i=>new SharedStashTab(i,"Shared "+(i+1))).ToArray();
    public static bool ValidTabs(SharedStashTab[]? tabs)=>tabs is {Length:>0 and <=MaxTabs} &&
        tabs.Select((tab,index)=>tab!=null && tab.Id==index && tab.Valid()).All(ok=>ok) && tabs.Any(t=>!t.Deleted);
}
public sealed record SharedStashTab(int Id,string Name,int Color=0,int Icon=0)
{
    public int Category {get;init;}
    // Stable slots keep all existing item/journal IDs. Reuse increments the
    // generation so a delayed deposit cannot enter an unrelated replacement.
    public bool Deleted {get;init;}
    public int Generation {get;init;}
    public bool IgnoreFolderPriority {get;init;}
    public string Priority {get;init;}="";
    public bool Valid()=>SharedStashRules.ValidTabIndex(Id) && Name is {Length:>0 and <=32} && Name.Trim()==Name &&
        !Name.Any(c=>char.IsControl(c) || c is '<' or '>') && Color is >=0 and <=1023 && Icon is >=0 and <=1023 &&
        Category is >=0 and <24 && Generation>=0 && SharedStashPriority.ValidJson(Priority);
}
public sealed record SharedStashCategory(int Id,string Name,int Color=0,int Icon=0,string Priority="")
{
    public bool Valid()=>Id is >=0 and <24 && new SharedStashTab(Id,Name,Color,Icon){Priority=Priority}.Valid();
    public static SharedStashCategory[] Initial()=>new[]{new SharedStashCategory(0,"General")};
    public static bool ValidAll(SharedStashCategory[] categories,SharedStashTab[] tabs)=>categories is {Length:>0 and <=24} &&
        categories.All(c=>c!=null && c.Valid()) && categories.Select(c=>c.Id).Distinct().Count()==categories.Length &&
        categories.Any(c=>c.Id==0) && tabs.All(t=>categories.Any(c=>c.Id==t.Category));
}

// Native condition messages, represented as bounded managed data. No item bytes
// or native pointers are accepted as configuration. Empty string means no rule.
public sealed record SharedStashPriority
{
    public int Rarity {get;init;}
    public int Class {get;init;}
    public int Corruption {get;init;}
    public int MinLevel {get;init;}
    public int MaxLevel {get;init;}
    public int[] Potential {get;init;}=Enumerable.Repeat(255,8).ToArray();
    public int[] Types {get;init;}=Array.Empty<int>();
    public int[] Subtypes {get;init;}=Array.Empty<int>();
    public int[] Factions {get;init;}=Array.Empty<int>();
    public int[] Uniques {get;init;}=Array.Empty<int>();
    public bool Valid()=>Rarity is >=0 and <=65535 && Class is >=0 and <=65535 && Corruption is >=0 and <=255 &&
        MinLevel is >=0 and <=100 && MaxLevel is >=0 and <=100 && (MaxLevel==0 || MinLevel<=MaxLevel) &&
        Potential is {Length:8} && Potential.All(v=>v is >=0 and <=255) &&
        new[]{Types,Subtypes,Factions,Uniques}.All(a=>a is {Length:<=256} && a.All(v=>v is >=0 and <=1000000) && a.Distinct().Count()==a.Length);
    public static bool ValidJson(string json)
    {
        if(json=="")return true;
        if(json is not {Length:<=8192})return false;
        try{return System.Text.Json.JsonSerializer.Deserialize<SharedStashPriority>(json)?.Valid()==true;}catch{return false;}
    }
}
public sealed record SharedStashEdit(string Id,string Owner,string Payload,string Status,int ChangedTab)
{
    public int ChangedCategory {get;init;}=-1;
    public bool Valid(int tabs)=>Guid.TryParseExact(Id,"N",out _) && Owner is {Length:>0 and <=256} &&
        Payload is {Length:64} && Payload.All(Uri.IsHexDigit) && Status is "tab-added" or "tab-deleted" or "configured" or "sorted" or "category-added" or "category-configured" or "category-deleted" && ChangedTab>=0 && ChangedTab<tabs && ChangedCategory is >=-1 and <24;
}
