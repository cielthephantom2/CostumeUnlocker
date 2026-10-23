using Il2Cpp;
using System.Text.Json;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
var manager=new ZoneRestrictedCraftingManager{craftingType=ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment,maxUses=2,limitedUses=true};
ZoneRestrictedCraftingManager.all[manager.craftingType]=manager;
bool NativeGate(bool eligible,ZoneRestrictedCraftingManager altar)=>eligible && (!altar.limitedUses || altar.maxUses-altar.localTimesUsed>0);
foreach(int used in new[]{0,1,2,9,10,11,1000,int.MaxValue}){
 manager.maxUses=2;manager.localTimesUsed=used;manager.limitedUses=true;WovenEnchanter.Apply(manager);
 Check(!manager.limitedUses && manager.maxUses==2 && manager.localTimesUsed==used && NativeGate(true,manager),"native unlimited branch allows exhausted or heavily used altar: "+used);
}
Check(!NativeGate(false,manager),"unlimited use does not bypass other eligibility conditions");
manager.localTimesUsed=10;manager.limitedUses=true;WovenEnchanter.PrepareUI(manager.craftingType);Check(!manager.limitedUses && manager.localTimesUsed==10,"UI reads native unlimited flag without resetting spent uses");
for(int n=0;n<100;n++)WovenEnchanter.Apply(manager);Check(!manager.limitedUses && manager.localTimesUsed==10,"repeated queries preserve counters");
manager.maxUses=4;manager.limitedUses=true;WovenEnchanter.Apply(manager);Check(!manager.limitedUses && manager.maxUses==4,"all native altar tiers use unlimited mode without fake maxima");
var other=new ZoneRestrictedCraftingManager{craftingType=ZoneRestrictedCraftingManager.CraftingType.Other,maxUses=2,localTimesUsed=2,limitedUses=true};WovenEnchanter.Apply(other);Check(other.limitedUses && other.maxUses==2 && !NativeGate(true,other),"other crafting station still exhausts normally");
manager.limitedUses=true;OfflineToolkit.Enabled=false;WovenEnchanter.Apply(manager);WovenEnchanter.PrepareUI(manager.craftingType);Check(manager.limitedUses,"online crafting and UI stay native");OfflineToolkit.Enabled=true;
WovenEnchanter.Apply(null);ZoneRestrictedCraftingManager.all.Clear();WovenEnchanter.PrepareUI(manager.craftingType);Check(true,"missing altar registry safely ignored");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev66-enchanter-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source unlimited Enchanter checks; native gameplay pending");
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}}}
namespace Il2Cpp{public class ZoneRestrictedCraftingManager{public enum CraftingType{IdolEnchantment,Other}public CraftingType craftingType;public int maxUses,localTimesUsed;public bool limitedUses;public static Dictionary<CraftingType,ZoneRestrictedCraftingManager> all=new();public static bool CanCraft(CraftingType craftingType,Actor actorIfOnServer)=>false;public void CalculateMaxUses(){}public int UsesRemaining(Actor actorIfOnServer)=>0;public static object GetUsesValuesForUI(CraftingType craftingType)=>null;}public class Actor{}}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}
