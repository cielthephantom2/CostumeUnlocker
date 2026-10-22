using System.Reflection;
using System.Text.Json;
using Il2CppLE.Factions;
using Il2CppLE.Core;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;

var checks=new List<string>();
void Check(bool condition,string label){if(!condition) throw new Exception(label);checks.Add(label);Console.WriteLine("PASS: "+label);}
var faction=new CircleOfFortune();
foreach(int rank in new[]{1,3,5,6,8,10,11}) OfflineFortune.ApplyRank(faction,rank,true);
Check(faction.ChanceForDoubleEnemyRewards0To1==1 && faction.ChanceForDoubleEchoRewards0To1==1,"actual adapter writes guaranteed enemy and Echo duplication fields");
Check(faction.CachedPlayerActor.generateItems.ChanceForAdditionalIdols0To1==0.5f && faction.CachedPlayerActor.generateItems.AdditionalIdols==2,
    "actual adapter writes native Idol chance and additional count");
Check(faction.ChanceToForceExaltedMultiplier==2 && faction.UniquesLPRollMultiplier==20,"actual adapter sets total native Exalted and LP multipliers");
Check(OfflineFortune.EnabledAt(faction,7),"unlocked member can receive the mage rank benefit");
faction.rankToggles[7]=false;Check(!OfflineFortune.EnabledAt(faction,7),"disabled rank toggle cannot re-enable the mage benefit");
faction.rankToggles.Clear();faction.Rank=6;Check(!OfflineFortune.EnabledAt(faction,7),"rank below seven cannot receive experimental quantity override");
faction.Rank=8;faction.IsMember=false;Check(!OfflineFortune.EnabledAt(faction,7),"leaving the faction disables custom rank eligibility");faction.IsMember=true;
OfflineToolkit.Enabled=false;faction.UniquesLPRollMultiplier=2;OfflineFortune.ApplyRank(faction,8,true);
Check(faction.UniquesLPRollMultiplier==2 && !OfflineFortune.EnabledAt(faction,7),"online/inactive toolkit keeps native faction fields and eligibility");OfflineToolkit.Enabled=true;
faction.UniquesLPRollMultiplier=1;OfflineFortune.ApplyRank(faction,8,false);Check(faction.UniquesLPRollMultiplier==1,"rank-disable callback does not overwrite native reset");
var rare=new ProphecySlotReward {itemsDropped=10};var unique=new ProphecySlotReward {itemsDropped=2};var upgrade=new ProphecySlotReward {itemsDropped=4};
rare.upgradedReward=upgrade;upgrade.upgradedReward=rare;
var catalog=new ProphecyData {rewards=new(new[]{rare,unique,rare})};
OfflineFortune.ScaleRewards(catalog);OfflineFortune.ScaleRewards(catalog);
Check(rare.itemsDropped==20 && unique.itemsDropped==4 && upgrade.itemsDropped==8,"all catalog and linked upgrade counts double once despite duplicate entries and cycles");
Check(rare.ItemsDropped==20 && rare.GetItemsDropped(3)==60,"picker count and charge-scaled gameplay use the same modified field");
rare.itemsDropped=10;OfflineFortune.ScaleRewards(catalog);
Check(rare.itemsDropped==20,"catalog reinitialization reapplies x2 to a native-reset base instead of stacking");
OfflineFortune.RestoreRewards();Check(rare.itemsDropped==10 && unique.itemsDropped==2 && upgrade.itemsDropped==4,"shutdown restores unmodified native catalog values");
OfflineFortune.ScaleRewards(catalog);rare.itemsDropped=99;OfflineFortune.RestoreRewards();Check(rare.itemsDropped==99,"restore does not overwrite a later external reward change");
var rankData=new FactionData.RankData();rankData.rankDescriptionLines.Add("Original rank text");rankData.rankDescriptionLines.Add("Unlocks a Prophecy slot.");
string Description(int rank,FactionID id)
{
    object[] values={rankData,rank,id,"<color=white>Original rank text</color>\n\nUnlocks a Prophecy slot."};
    typeof(FortuneRankDescriptionPatch).GetMethod("Postfix",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,values);return (string)values[3];
}
string text=Description(1,FactionID.CircleOfFortune);
Check(text.Contains("100%") && text.Contains("Unlocks a Prophecy slot.") && text.Contains("<color=white>"),"rank text replacement preserves native slot-unlock lines and rich-text formatting");
rankData.rankDescriptionLines[0]="Original rank text";
Check(Description(1,FactionID.MerchantsGuild).Contains("Original rank text") && Description(2,FactionID.CircleOfFortune).Contains("Original rank text"),
    "other factions and unchanged ranks retain native descriptions");
var rng=RunePrisonItemDrops.rng;var other=new RngElement();
using(var outer=new MageLootScope(rng.Pointer))
{
    outer.Observe(new Provider {Value=faction});
    Check(other.StochasticRound(2)==2 && outer.Pending,"other RNG streams cannot consume or receive the mage override");
    Check(rng.StochasticRound(2)==15 && !outer.Pending && rng.StochasticRound(2)==2,"one native mage quantity roll becomes x15 without affecting later rounding");
    using(var inner=new MageLootScope(rng.Pointer)){inner.Observe(new Provider());Check(rng.StochasticRound(2)==2,"recipient without CoF retains ordinary mage drops");}
    Check(MageLootScope.Current==outer,"nested mage scopes restore the previous recipient context");
}
Check(MageLootScope.Current==null && rng.StochasticRound(2)==2,"scope disposal clears all mage quantity changes outside the loot call");
try{using var failed=new MageLootScope(rng.Pointer);throw new InvalidOperationException();}catch(InvalidOperationException){}
Check(MageLootScope.Current==null,"exception cleanup restores mage scope without leaking into later loot");
Check(faction.IncreasedChanceToDropBossSpecificLoot==3 && faction.CachedPlayerActor.generateItems.ChanceForRareItemsToBecomeExalted0To1==.5f,"CoF Rank10 and11 native fields use300% and50%");
foreach(int rank in new[]{10,11})
{
 faction.Rank=rank-1;faction.IncreasedChanceToDropBossSpecificLoot=1.5f;faction.CachedPlayerActor.generateItems.ChanceForRareItemsToBecomeExalted0To1=.25f;
 OfflineFortune.ApplyRank(faction,rank,true);
 Check(faction.IncreasedChanceToDropBossSpecificLoot==1.5f && faction.CachedPlayerActor.generateItems.ChanceForRareItemsToBecomeExalted0To1==.25f,"CoF unearned rank is untouched: "+rank);
 faction.Rank=12;faction.rankToggles[rank]=false;OfflineFortune.ApplyRank(faction,rank,true);
 Check(faction.IncreasedChanceToDropBossSpecificLoot==1.5f && faction.CachedPlayerActor.generateItems.ChanceForRareItemsToBecomeExalted0To1==.25f,"CoF disabled rank is untouched: "+rank);faction.rankToggles.Clear();
}
var woven=new TheWeaver();
foreach(int rank in new[]{7,9})
{
 OfflineWovenRanks.ApplyRank(woven,rank,true);OfflineWovenRanks.ApplyRank(woven,rank,true);
 Check(rank==7?woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols==.3f:woven.MoreMemoryAmberDropRate==.5f,"Woven absolute native value does not stack: "+rank);
 woven.rankToggles[rank]=false;woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols=.1f;woven.MoreMemoryAmberDropRate=.1f;OfflineWovenRanks.ApplyRank(woven,rank,true);
 Check(woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols==.1f && woven.MoreMemoryAmberDropRate==.1f,"Woven disabled rank remains native: "+rank);woven.rankToggles.Clear();
 woven.Rank=rank-1;OfflineWovenRanks.ApplyRank(woven,rank,true);Check(woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols==.1f && woven.MoreMemoryAmberDropRate==.1f,"Woven unearned rank remains native: "+rank);woven.Rank=10;
}
woven.CachedPlayerActor=new Actor();OfflineWovenRanks.ApplyRank(woven,7,true);Check(woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols==.1f,"another actor is not modified");woven.CachedPlayerActor=PlayerFinder.Player;
woven.IsMember=false;OfflineWovenRanks.ApplyRank(woven,9,true);Check(woven.MoreMemoryAmberDropRate==.1f,"nonmember Woven value is not modified");woven.IsMember=true;
OfflineToolkit.Enabled=false;OfflineWovenRanks.ApplyRank(woven,7,true);Check(woven.IncreasedChanceForSecondAffixToBeWeaverWhenReforgingIdols==.1f,"online Woven retains native values");OfflineToolkit.Enabled=true;
string rank7="Reforging Weaver idols is <b>10%</b> more likely to result in 2 Weaver affixes\n+1 Weaver Tree points\nYou can purchase 5 new Woven Echoes";
string rank9="10% more Memory Amber drops in your echoes\n+2 Weaver Tree points\nYou can purchase 2 new Woven Echoes";
Check(WovenRankRules.Description(7,rank7)==rank7.Replace("10%","30%"),"Woven rank7 text preserves2affixes,1point,5echoes and markup");
Check(WovenRankRules.Description(9,rank9)==rank9.Replace("10%","50%"),"Woven rank9 text preserves2points and2echoes");
Check(WovenRankRules.Description(10,"+3 Weaver Tree points;3 new Woven Echoes")=="+3 Weaver Tree points;3 new Woven Echoes","Woven rank10 from screenshot stays unchanged");
Check(WovenRankRules.Description(7,"110% 10.5% 10 %")=="110% 10.5% 30 %","percentage edit matches exact10only and preserves spacing");
File.WriteAllText(args.Length>0?args[0]:"fortune-fixture.json",JsonSerializer.Serialize(new {passed=checks.Count,scope="source-linked Fortune adapter with recording substitutes; not native drop/render acceptance",checks},new JsonSerializerOptions {WriteIndented=true}));
Console.WriteLine($"All {checks.Count} Fortune adapter checks passed.");
