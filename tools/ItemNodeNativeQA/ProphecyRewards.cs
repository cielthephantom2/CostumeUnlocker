using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppLE.Factions;
using UnityEngine;
using Lens=Il2CppSystem.Nullable<Il2CppLE.Factions.ProphecySlot.LensType>;
namespace ItemNodeNativeQA;
public partial class Audit
{
 static bool captureProphecy;
 static readonly List<byte[]> prophecyDrops=new();
 public static bool ObserveProphecyDrop(ItemData itemData)
 {
  if(!captureProphecy)return true;
  // Native copies have separate identity/duplicated metadata. Compare actual
  // rolled contents after clearing those two identity fields on a scratch copy.
  var copy=new ItemDataUnpacked(itemData.GetIDAsArray());copy.individualID=16;copy.duplicated=false;copy.RebuildID();
  prophecyDrops.Add(copy.GetIDAsArray().ToArray());return false;
 }
 static bool[] DuplicatePairs()=>Enumerable.Range(0,prophecyDrops.Count/2).Select(i=>prophecyDrops[i*2].SequenceEqual(prophecyDrops[i*2+1])).ToArray();
 ProphecySlotReward prophecyReward;
 HarmonyLib.Harmony prophecyObserver;
 int prophecyStage,prophecyFirstFrame,prophecyLastFrame,prophecyPreviousCount;
 void ProphecyStream(Actor actor)
 {
  if(prophecyStage==0)
  {
   string expected=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath),"UserData","GuestProfile"));
   Check(Il2CppLE.Core.PlatformFileManager.ApplicationSavePath==expected && actor.characterDataTracker.charData.CharacterName.StartsWith("LANQA"),"Prophecy QA uses isolated disposable native character");
   EchoAggroActor(actor);
   Check((bool)Call("OfflineToolkit","get_Ready"),"Real offline actor is ready for deferred native reward delivery");
   prophecyObserver=new HarmonyLib.Harmony("private.qa.prophecy.drops");
   prophecyObserver.Patch(typeof(GroundItemManager).GetMethod(nameof(GroundItemManager.dropItemForPlayer)),prefix:new HarmonyMethod(typeof(Audit).GetMethod(nameof(ObserveProphecyDrop))));
   captureProphecy=true;
   var source=ProphecyData.Data.rewards.First(r=>r!=null && r.itemGenerationType==ProphecySlotReward.ItemGenerationType.GenericEquipment && r.genericItem!=null);
   prophecyReward=UnityEngine.Object.Instantiate(source);prophecyReward.itemsDropped=4;
   var bypass=T("ProphecyRewards").GetField("replaying",BindingFlags.Static|BindingFlags.NonPublic);
   var global=ProphecySlotReward.rng;
   foreach(int lensId in Enumerable.Range(-1,13))
   {
    var lens=lensId<0?new Lens():new Lens((ProphecySlot.LensType)lensId);
    uint state=global.State;prophecyDrops.Clear();bypass.SetValue(null,true);
    try{prophecyReward.SpawnRewardForPlayer(actor,lens);}finally{bypass.SetValue(null,false);}
    int native=prophecyDrops.Count;
    prophecyDrops.Clear();global.State=state;
    prophecyReward.SpawnRewardForPlayer(actor,lens);
    Check(prophecyDrops.Count==0,"Native reward detour defers callback with nullable lens",new{lensId});
    Call("ProphecyRewards","Flush");
    Check(prophecyDrops.Count==native,"Stream retains native reward count for lens",new{lensId,native,streamed=prophecyDrops.Count});
    Check(ProphecySlotReward.rng.Pointer==global.Pointer,"Private lens RNG restored after replay",new{lensId});
   }
   // Force both Scales branches and Duplication outcomes using real data knobs,
   // then restore the asset immediately. No probabilities change in production.
   var data=ProphecyData.Data;float dup=data.duplicationLensChance,scales=data.celestialScalesDuplicationChance,egg=data.celestialScalesGroleEggReplacementChance;
   try
   {
    foreach(bool duplication in new[]{false,true})
    foreach(bool replacement in new[]{false,true})
    {
     data.duplicationLensChance=duplication?1:0;data.celestialScalesDuplicationChance=duplication?1:0;data.celestialScalesGroleEggReplacementChance=replacement?1:0;
     foreach(int lensId in new[]{1,9})
     {
      var lens=new Lens((ProphecySlot.LensType)lensId);prophecyDrops.Clear();bypass.SetValue(null,true);
      try{prophecyReward.SpawnRewardForPlayer(actor,lens);}finally{bypass.SetValue(null,false);}
      int native=prophecyDrops.Count;var nativePairs=DuplicatePairs();prophecyDrops.Clear();prophecyReward.SpawnRewardForPlayer(actor,lens);Call("ProphecyRewards","Flush");
      Check(prophecyDrops.Count==native && native>0,"Native forced lens outcome preserves whole-batch amount",new{lensId,duplication,replacement,native,streamed=prophecyDrops.Count});
      if(lensId==1 && duplication)Check(prophecyDrops.Count%2==0 && DuplicatePairs().SequenceEqual(nativePairs),"Duplication pair contents match native baseline after identity normalization",new{nativePairs,streamedPairs=DuplicatePairs()});
     }
    }
   }
   finally{data.duplicationLensChance=dup;data.celestialScalesDuplicationChance=scales;data.celestialScalesGroleEggReplacementChance=egg;}
   prophecyReward.itemsDropped=20;prophecyDrops.Clear();
   var target=ProphecyData.Data.targets.First(t=>t!=null && t.MatchesTarget(ProphecyTargetType.TimelineBoss));
   var slot=new ProphecySlot(ProphecyRegion.East,target,prophecyReward,new Lens(ProphecySlot.LensType.Origin),12,0);
   bool triggered=slot.TryTriggerReward(ProphecyTargetType.TimelineBoss,false,actor,out int consumed);
   Check(triggered && consumed==4 && slot.CompletedCharges==8 && prophecyDrops.Count==0,"Real native TimelineBoss trigger consumes exactly 4 of 12 charges and defers all 80 drops",new{triggered,consumed,remaining=slot.CompletedCharges,immediateDrops=prophecyDrops.Count});
   prophecyFirstFrame=prophecyLastFrame=Time.frameCount;prophecyPreviousCount=0;prophecyStage=1;return;
  }
  int count=prophecyDrops.Count;
  Check(count-prophecyPreviousCount<=1,"Native frame creates at most one Origin item",new{frame=Time.frameCount,count,previous=prophecyPreviousCount});
  prophecyPreviousCount=count;prophecyLastFrame=Time.frameCount;
  if(count<80){if(Time.frameCount-prophecyFirstFrame>200)throw new TimeoutException("80-item native stream timed out");return;}
  Check(count==80 && Time.frameCount-prophecyFirstFrame>=80,"All 80 native items delivered over at least 80 distinct frames",new{count,frames=Time.frameCount-prophecyFirstFrame});
  // Transition fallback drains pending units before native unload/cache executes.
  prophecyDrops.Clear();prophecyReward.SpawnRewardForPlayer(actor,new Lens(ProphecySlot.LensType.Origin));
  Call("ProphecyRewards","Flush");Check(prophecyDrops.Count==20,"Transition flush delivers every remaining native item exactly once");
  int after=prophecyDrops.Count;Call("ProphecyRewards","Flush");Check(prophecyDrops.Count==after,"Repeated transition flush cannot duplicate completed rewards");
  captureProphecy=false;prophecyObserver.UnpatchSelf();UnityEngine.Object.Destroy(prophecyReward);
  Finish(null);
 }
}
