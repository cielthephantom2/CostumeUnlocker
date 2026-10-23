using HarmonyLib;
using Il2Cpp;
using System.Reflection;

namespace ItemNodeNativeQA;
public partial class Audit
{
 void ForgingPotential()
 {
  int Half(int cost)=>cost>0?Math.Max(1,cost/2):cost;
  var target=typeof(ItemData).GetMethod(nameof(ItemData.applyForgingPotentialCost));
  Check(HarmonyLib.Harmony.GetPatchInfo(target)?.Prefixes.Any(p=>p.PatchMethod.DeclaringType?.Name=="OfflineForgingPotentialCostPatch")==true,"FP charge prefix is installed on native consumer");
  Check(HarmonyLib.Harmony.GetPatchInfo(typeof(CraftingManager).GetMethod(nameof(CraftingManager.DuplicateItem)))==null,"Rune of Creation duplication remains entirely native");
  var item=ItemData.DummyData(0,0,0);
  Check(item!=null,"Disposable native item allocated without character inventory");
  foreach(var (min,max) in new[]{(1,25),(1,20),(4,8),(25,25),(1,1),(0,0)})
  {
   var losses=new List<int>();
   for(int n=0;n<128;n++)
   {
    item.SetForgingPotential(60);item.applyForgingPotentialCost(min,max,false,out int lost);
    if(lost<Half(min) || lost>Half(max) || item.forgingPotential!=60-lost)throw new Exception("Native FP charge outside reduced range: "+min+"/"+max+" -> "+lost);
    losses.Add(lost);
   }
   Check(true,"Native RNG, lucky roll and stored FP use reduced range",new{min,max,expectedMin=Half(min),expectedMax=Half(max),observedMin=losses.Min(),observedMax=losses.Max()});
  }
  foreach(var spender in Enum.GetValues<ItemData.NonAffixForgingPotentialSpender>().Where(s=>(int)s is not (0 or 7 or 8 or 11)))
  {
   int min=ItemData.getMinForgingPotentialCostForNonAffix(spender,60),max=ItemData.getMaxForgingPotentialCostForNonAffix(spender,60);
   for(int n=0;n<128;n++)
   {
    item.SetForgingPotential(60);item.applyForgingPotentialCostForNonAffix(spender,60,false,out int lost);
    if(lost<Half(min) || lost>Half(max) || item.forgingPotential!=60-lost)throw new Exception("Native rune FP charge mismatch: "+spender+" -> "+lost);
   }
   Check(true,"Native rune uses reduced FP exactly once",new{rune=spender.ToString(),min,max,reducedMin=Half(min),reducedMax=Half(max)});
  }
  for(int tier=0;tier<=4;tier++)
  {
   int min=ItemData.getMinForgingPotentialCostFromTier(tier),max=ItemData.getMaxForgingPotentialCostFromTier(tier,item.rarity);
   for(int n=0;n<128;n++)
   {
    item.SetForgingPotential(60);item.applyForgingPotentialCostFromShard(tier,false,false,out int lost);
    if(lost<Half(min) || lost>Half(max))throw new Exception("Native affix/glyph charge mismatch");
   }
   Check(true,"Native affix/glyph path reduces inlined cost range",new{tier,min,max});
  }
  item.SetForgingPotential(60);item.applyForgingPotentialCost(1,25,true,out int free);
  Check(free==0 && item.forgingPotential==60,"Native no-cost/critical/Hope preservation remains free");
  item.SetForgingPotential(3);item.applyForgingPotentialCost(25,25,false,out int capped);
  Check(capped==3 && item.forgingPotential==0,"Native cap applies after range reduction, never refunds FP");
 }
}
