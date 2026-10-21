namespace LastEpochLAN.Core;

public static class SharedStashRules
{
    public const int ItemsPerTab=96, InitialTabCount=6, MaxTabs=24, MaximumItems=ItemsPerTab*MaxTabs;
    public static bool ValidTabIndex(int tab)=>tab>=0 && tab<MaxTabs;
    public static SharedStashTab[] InitialTabs()=>Enumerable.Range(0,InitialTabCount).Select(i=>new SharedStashTab(i,"Shared "+(i+1))).ToArray();
    public static bool ValidTabs(SharedStashTab[]? tabs)=>tabs is {Length:>0 and <=MaxTabs} &&
        tabs.Select((tab,index)=>tab!=null && tab.Id==index && tab.Valid()).All(ok=>ok);
}
public sealed record SharedStashTab(int Id,string Name,int Color=0,int Icon=0)
{
    public bool Valid()=>SharedStashRules.ValidTabIndex(Id) && Name is {Length:>0 and <=32} && Name.Trim()==Name &&
        !Name.Any(c=>char.IsControl(c) || c is '<' or '>') && Color is >=0 and <=1023 && Icon is >=0 and <=1023;
}
public sealed record SharedStashEdit(string Id,string Owner,string Payload,string Status,int ChangedTab)
{
    public bool Valid(int tabs)=>Guid.TryParseExact(Id,"N",out _) && Owner is {Length:>0 and <=256} &&
        Payload is {Length:64} && Payload.All(Uri.IsHexDigit) && Status is "tab-added" or "configured" or "sorted" && ChangedTab>=0 && ChangedTab<tabs;
}
