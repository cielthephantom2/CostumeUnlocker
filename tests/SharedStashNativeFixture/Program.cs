using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Core;
using LastEpochLAN.Mod;

var checks=new List<string>();
void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS: "+message);}
string Encode(SharedStashPriority data)=>JsonSerializer.Serialize(data);
var complete=new SharedStashPriority{Rarity=8,Class=4,Corruption=2,MinLevel=15,MaxLevel=99,
    Potential=new[]{1,4,2,28,3,30,4,100},Types=new[]{1,2},Subtypes=new[]{3,4},Factions=new[]{1,2},Uniques=new[]{55,56}};
var native=SharedStashPriorityCodec.Decode(Encode(complete));
var restored=JsonSerializer.Deserialize<SharedStashPriority>(SharedStashPriorityCodec.Encode(native))!;
Check(Encode(restored)==Encode(complete),"actual codec roundtrips all eight native condition messages, ranges and selections");
Check(native.PotentialStashPriority.Serialize().MinWeaversTouch==3 && native.PotentialStashPriority.Serialize().MaxForgingPotential==100,
    "WW, Weaver Touch and FP fields preserve exact native message positions");
Check(SharedStashPriorityCodec.Encode(SharedStashPriorityCodec.Decode(""))=="","empty configuration retains no priority");
bool rejected=false;try{SharedStashPriorityCodec.Decode("{bad");}catch(InvalidDataException){rejected=true;}
Check(rejected,"actual codec rejects malformed data before calling native deserializers");
var routing=new SharedStashPriorityRouting();
string match=Encode(new SharedStashPriority{Rarity=8}),other=Encode(new SharedStashPriority{Rarity=4});
var categories=new[]{new SharedStashCategory(0,"General",Priority:match),new SharedStashCategory(1,"Other")};
var tabs=new[]{new SharedStashTab(0,"A"),new SharedStashTab(1,"B"){Priority=match},new SharedStashTab(2,"C"){Priority=other}};
var item=new ItemDataUnpacked{Rarity=8};
var removed=tabs.Select(t=>t.Id==1?t with{Deleted=true}:t).ToArray();
Check(routing.Choose(removed,categories,new[]{0,0,0},0,item,75)==0,"deleted tab is never a destination even when its priority matches");
Check(routing.Choose(tabs,categories,new[]{0,0,0},2,item,75)==1,"tab-specific match takes precedence over a General category rule");
Check(StashPriority.LastLevel==75,"actual routing passes the local character level into native Match");
Check(routing.Choose(tabs,categories,new[]{0,96,0},2,item,75)==0,"full priority tab is skipped; native category rule routes to a non-full tab");
Check(routing.Choose(tabs.Select(t=>t with{IgnoreFolderPriority=true}).ToArray(),categories,new[]{0,96,0},2,item,75)==2,
    "Ignore Folder Priority suppresses inherited routing and keeps selected tab when no own rule matches");
Check(routing.Choose(tabs,categories,new[]{96,96,96},2,item,75)==2,"no eligible non-full priority destination leaves the selected-tab transaction policy in control");
Check(routing.Choose(tabs,categories,new[]{0,0,0},2,new(){Rarity=77},33)==2,"nonmatching item is not forced into a priority tab");
bool cacheStable=true;
for(int rarity=1;rarity<=105;rarity++)
{
    var varied=tabs.Select(t=>t with{Priority=t.Id==1?Encode(new SharedStashPriority{Rarity=rarity}):"",IgnoreFolderPriority=true}).ToArray();
    cacheStable &= routing.Choose(varied,categories,new[]{0,0,0},2,new(){Rarity=rarity},50)==1;
}
Check(cacheStable,"bounded native rule cache preserves routing across more than96 distinct edited configurations");
File.WriteAllText(args[0],JsonSerializer.Serialize(new{passed=checks.Count,checks,scope="actual priority codec/routing source with native-message stubs; no engine/UI acceptance"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} native priority fixture checks passed.");
