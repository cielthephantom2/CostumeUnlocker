using Il2Cpp;
using System.Text.Json;
using LastEpochLAN.Mod.OfflineTools;
var checks=new List<string>();void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
var manager=new ZoneRestrictedCraftingManager{craftingType=ZoneRestrictedCraftingManager.CraftingType.IdolEnchantment,maxUses=2,limitedUses=true};
ZoneRestrictedCraftingManager.all[manager.craftingType]=manager;
for(int used=0;used<=11;used++){
 manager.maxUses=2;manager.localTimesUsed=used;WovenEnchanter.Apply(manager);
 Check(manager.maxUses==10 && manager.localTimesUsed==used && manager.limitedUses && Math.Max(0,manager.maxUses-manager.localTimesUsed)==Math.Max(0,10-used),"native counter model keeps spent uses and exhausts at10: "+used);
}
manager.localTimesUsed=2;manager.maxUses=2;WovenEnchanter.PrepareUI(manager.craftingType);Check(manager.maxUses==10 && manager.localTimesUsed==2,"already exhausted2-use altar receives8 remaining, without counter reset");
for(int n=0;n<100;n++)WovenEnchanter.Apply(manager);Check(manager.maxUses==10 && manager.localTimesUsed==2,"repeated UI/gameplay queries cannot replenish uses");
manager.maxUses=4;WovenEnchanter.Apply(manager);Check(manager.maxUses==10,"higher native altar tier also has requested10-use cap");
var other=new ZoneRestrictedCraftingManager{craftingType=ZoneRestrictedCraftingManager.CraftingType.Other,maxUses=2,localTimesUsed=1};WovenEnchanter.Apply(other);Check(other.maxUses==2 && other.localTimesUsed==1,"other crafting stations unaffected");
manager.maxUses=2;OfflineToolkit.Enabled=false;WovenEnchanter.Apply(manager);WovenEnchanter.PrepareUI(manager.craftingType);Check(manager.maxUses==2,"online calculation and UI stay native");OfflineToolkit.Enabled=true;
WovenEnchanter.Apply(null);ZoneRestrictedCraftingManager.all.Clear();WovenEnchanter.PrepareUI(manager.craftingType);Check(true,"missing altar registry safely ignored");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev64-enchanter-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source Enchanter checks; native gameplay pending");
namespace HarmonyLib{public class HarmonyPatch:Attribute{public HarmonyPatch(params object[] args){}}}
namespace Il2Cpp{public class ZoneRestrictedCraftingManager{public enum CraftingType{IdolEnchantment,Other}public CraftingType craftingType;public int maxUses,localTimesUsed;public bool limitedUses;public static Dictionary<CraftingType,ZoneRestrictedCraftingManager> all=new();public void CalculateMaxUses(){}public int UsesRemaining(Actor actorIfOnServer)=>0;public static object GetUsesValuesForUI(CraftingType craftingType)=>null;}public class Actor{}}
namespace LastEpochLAN.Mod.OfflineTools{public static class OfflineToolkit{public static bool Enabled=true;}}
