using Il2Cpp;
using Il2CppLE.Factions;
using LastEpochLAN.Mod.OfflineTools;
using System.Text.Json;
using System.Reflection;
var checks=new List<string>();
void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS: "+message);}
foreach(int quantity in new[]{1,2,4,7,20})
 foreach(byte charge in new byte[]{0,1,2,3,4,10,11,19,20,29,30,99})
 {
  Notifications.Messages.Clear();
  var slot=new ProphecySlot{CompletedCharges=charge,BaseQuantity=quantity};
  bool ok=slot.TryTriggerReward(slot.Target,false,PlayerFinder.Player,out int spent);
  Check(ok==(charge>0) && spent==charge && slot.CompletedCharges==0 && slot.Items==quantity*charge && slot.EventCharges==charge,
   $"base{quantity} x{charge} charges: native per-type loot and consumed total agree, no residual charges");
  Check(!slot.TryTriggerReward(slot.Target,false,PlayerFinder.Player,out spent) && spent==0 && slot.Items==quantity*charge,
   "repeating the same target after spending all charges cannot duplicate loot");
  Check(charge==0?Notifications.Messages.Count==0:Notifications.Messages.Count==1 && Notifications.Messages[0].Charges==charge && Notifications.Messages[0].Items==quantity*charge,
   "one native notification reports the total spent charges and this reward type quantity, including above10");
 }
var doubled=new ProphecySlot{CompletedCharges=10,BaseQuantity=4};doubled.TryTriggerReward(doubled.Target,true,PlayerFinder.Player,out int used);
Check(used==10 && doubled.Items==80,"native double-reward bonus retained independently from charge consumption");
foreach(bool invalid in new[]{false,true})
{
 var slot=new ProphecySlot{CompletedCharges=10,BaseQuantity=7,Valid=!invalid,Reject=!invalid};
 Check(!slot.TryTriggerReward(slot.Target,false,PlayerFinder.Player,out used) && used==0 && slot.Items==0 && slot.CompletedCharges==10 && slot.NativeCalls==1,
  "invalid/rejected native target stops immediately without consuming charges");
}
var wrong=new ProphecySlot{CompletedCharges=10,BaseQuantity=7};
Check(!wrong.TryTriggerReward(ProphecyTargetType.Boss,false,PlayerFinder.Player,out used) && used==0 && wrong.CompletedCharges==10,"native target matching retained");
var noProgress=new ProphecySlot{CompletedCharges=10,BaseQuantity=7,NoProgress=true};noProgress.TryTriggerReward(noProgress.Target,false,PlayerFinder.Player,out used);
Check(used==0 && noProgress.NativeCalls==1 && noProgress.CompletedCharges==10,"no-progress native result never causes an unbounded loop");
var broken=new ProphecySlot{CompletedCharges=10,BaseQuantity=7,ThrowAfterSpend=true};
try{broken.TryTriggerReward(broken.Target,false,PlayerFinder.Player,out used);throw new Exception("expected failure");}catch(TargetInvocationException ex)when(ex.GetBaseException() is InvalidOperationException){}
Check(!OfflineProphecyCharges.NativeCall(broken) && broken.CompletedCharges==7 && broken.Items==0,"native exception clears reentry guard and never refunds spent charges or retries the failed spawn");
var added=new ProphecySlot{CompletedCharges=10,BaseQuantity=1};added.OnFulfilled=()=>added.CompletedCharges++;
added.TryTriggerReward(added.Target,false,PlayerFinder.Player,out used);
Check(used==3 && added.CompletedCharges==8 && added.NativeCalls==1,"external charge gains stop draining instead of consuming newly gained charges");
Notifications.Messages.Clear();
var inner=new ProphecySlot{CompletedCharges=4,BaseQuantity=2};var outer=new ProphecySlot{CompletedCharges=4,BaseQuantity=7};
outer.OnFulfilled=()=>{if(inner.CompletedCharges>0)inner.TryTriggerReward(inner.Target,false,PlayerFinder.Player,out int unused);};
outer.TryTriggerReward(outer.Target,false,PlayerFinder.Player,out used);
Check(used==4 && outer.Items==28 && inner.Items==8 && !OfflineProphecyCharges.NativeCall(inner) && !OfflineProphecyCharges.NativeCall(outer),"nested different slots retain independent native reentry guards");
Check(Notifications.Messages.Count==2 && Notifications.Messages.Any(m=>m.Slot==inner.Pointer && m.Charges==4 && m.Items==8) && Notifications.Messages.Any(m=>m.Slot==outer.Pointer && m.Charges==4 && m.Items==28),"nested slots keep their own aggregate notifications");
Notifications.Throw=true;var missingUi=new ProphecySlot{CompletedCharges=10,BaseQuantity=4};missingUi.TryTriggerReward(missingUi.Target,false,PlayerFinder.Player,out used);Notifications.Throw=false;
Check(used==10 && missingUi.Items==40 && missingUi.CompletedCharges==0 && !OfflineProphecyCharges.NativeCall(missingUi),"notification UI failure cannot repeat, undo or interrupt successful reward transactions");
var preview=new ProphecySlot{CompletedCharges=10,BaseQuantity=7};
Check(preview.GetChargesToUseWhenTriggered()==10 && preview.GetChargesToUseWhenTriggeredUIString()=="<color=#808080>Consumes 10 charges</color>","UI count and localized rich-text sentence show all charges, preserving numeric color tags");
Check(OfflineProphecyCharges.ChargeText("<size=12><color=#112233>Use 3 charges</color></size>",99)=="<size=12><color=#112233>Use 99 charges</color></size>","charge text never changes tag attributes or color codes");
Check(OfflineProphecyCharges.ChargeText("",0)=="" && OfflineProphecyCharges.ChargeText("",1)=="","zero/one-charge native UI behavior retained");
OfflineToolkit.Enabled=false;var online=new ProphecySlot{CompletedCharges=10,BaseQuantity=7};online.TryTriggerReward(online.Target,false,PlayerFinder.Player,out used);
Check(used==3 && online.Items==21 && online.CompletedCharges==7 && online.GetChargesToUseWhenTriggered()==3,"online native tiers and UI remain unchanged");OfflineToolkit.Enabled=true;
var remote=new ProphecySlot{CompletedCharges=10,BaseQuantity=7};remote.TryTriggerReward(remote.Target,false,new Actor(),out used);
Check(used==3 && remote.Items==21 && remote.CompletedCharges==7,"remote actor does not receive local full-charge dispatch");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev55-prophecy-linear-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} prophecy charge checks passed.");
