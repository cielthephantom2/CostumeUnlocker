using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using Il2CppLE.Core;
using Il2CppLE.Factions;
using LastEpochLAN.Mod.OfflineTools;
using UnityEngine;
using Lens=Il2CppSystem.Nullable<Il2CppLE.Factions.ProphecySlot.LensType>;
var checks=new List<string>();
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks.Add(label);}
int Drops()=>ProphecySlotReward.Calls.Sum(c=>c.Count);
int Pending()=>((System.Collections.ICollection)typeof(ProphecyRewards).GetField("pending",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Count;
var actor=PlayerFinder.Player;var reward=new ProphecySlotReward{itemsDropped=20};var origin=new Lens(ProphecySlot.LensType.Origin);
for(int i=0;i<4;i++)reward.SpawnRewardForPlayer(actor,origin);
Check(Drops()==0 && Pending()==4,"Four native reward callbacks queue without doing item generation");
ProphecyRewards.Tick();Check(Drops()==0,"No work on the death/creation frame");
reward.itemsDropped=999;
for(int i=0;i<80;i++)
{
 Time.frameCount++;ProphecyRewards.Tick();ProphecyRewards.Tick();
 Check(Drops()==i+1,"One unit per distinct frame despite repeated Tick: "+i);
}
Check(Pending()==0 && Drops()==80 && reward.itemsDropped==999,"All 80 items delivered from immutable snapshots; shared asset unchanged");
var global=ProphecySlotReward.rng;
foreach(int lensId in Enumerable.Range(-1,13))
foreach(var type in Enum.GetValues<ProphecySlotReward.ItemGenerationType>())
{
 reward.itemsDropped=20;reward.itemGenerationType=type;
 var lens=lensId<0?new Lens():new Lens((ProphecySlot.LensType)lensId);
 const uint seed=852721;global.State=seed;
 var expectedRng=new RngElement(seed);int expected=20;
 if(lensId==1 && expectedRng.Roll(ProphecyData.Data.duplicationLensChance))expected*=2;
 if(lensId==9){if(expectedRng.Roll(ProphecyData.Data.celestialScalesDuplicationChance))expected*=2;if(expectedRng.Roll(ProphecyData.Data.celestialScalesGroleEggReplacementChance))expected=1;}
 ProphecySlotReward.Calls.Clear();reward.SpawnRewardForPlayer(actor,lens);uint reserved=global.State;
 ProphecyRewards.Flush();
 Check(Drops()==expected && ProphecySlotReward.Calls.All(c=>c.Lens==lensId),"Native amount/lens retained across generation type: "+lensId+"/"+type);
 Check(ReferenceEquals(global,ProphecySlotReward.rng) && global.State==reserved,"Private lens RNG scope restored; no rerolls per chunk: "+lensId+"/"+type);
}
foreach(bool duplication in new[]{false,true})foreach(bool replacement in new[]{false,true})
{
 ProphecyData.Data.celestialScalesDuplicationChance=duplication?1:0;ProphecyData.Data.celestialScalesGroleEggReplacementChance=replacement?1:0;
 reward.itemGenerationType=ProphecySlotReward.ItemGenerationType.GenericEquipment;
 ProphecySlotReward.Calls.Clear();reward.SpawnRewardForPlayer(actor,new Lens(ProphecySlot.LensType.CelestialScales));ProphecyRewards.Flush();
 Check(Drops()==(replacement?1:duplication?40:20),"Scales rolls once for entire reward, including one egg replacement: "+duplication+"/"+replacement);
}
reward.itemsDropped=3;ProphecySlotReward.Calls.Clear();reward.SpawnRewardForPlayer(actor,origin);
actor.characterDataTracker.charData.Id="character-b";
try{ProphecyRewards.Flush();throw new Exception("Expected identity guard");}catch(InvalidOperationException){}
Check(Drops()==0 && Pending()==1,"Different character cannot consume previous character rewards");actor.characterDataTracker.charData.Id="character-a";
ProphecyRewards.Flush();Check(Drops()==3 && Pending()==0,"Identity guard retains every unit for original character");
ProphecyRewards.Flush();Check(Drops()==3,"Repeated flush does not duplicate drops");
reward.itemsDropped=2;reward.SpawnRewardForPlayer(actor,new Lens(ProphecySlot.LensType.Duplication));ProphecySlotReward.ThrowNative=true;
try{Time.frameCount++;ProphecyRewards.Tick();}catch(Exception ex)when(ex.Message=="partial native drop"){}
Check(ReferenceEquals(global,ProphecySlotReward.rng) && !(bool)typeof(ProphecyRewards).GetField("replaying",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null),"Native partial failure restores RNG and replay guard");
int before=ProphecySlotReward.Calls.Count;ProphecySlotReward.ThrowNative=false;ProphecyRewards.Flush();
Check(ProphecySlotReward.Calls.Count==before+1 && Pending()==0,"Uncertain partial drop is never automatically replayed; later units retained");
OfflineToolkit.Ready=false;Check(!ProphecyRewards.Defer(reward,actor,origin),"Online/not-ready callback stays native");OfflineToolkit.Ready=true;
Check(!ProphecyRewards.Defer(reward,new Actor{Pointer=(IntPtr)2},origin),"Other player callback stays native");
Directory.CreateDirectory("validation");File.WriteAllText("validation/prophecy-stream-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,scope="Actual production scheduler with stubbed engine generation; native ABI/count/pursuit require isolated game QA"},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source Prophecy scheduler checks");
