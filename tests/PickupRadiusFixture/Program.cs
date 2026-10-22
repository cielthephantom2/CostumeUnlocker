using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
using UVec=UnityEngine.Vector3;
using Time=UnityEngine.Time;
var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);checks.Add(text);Console.WriteLine("PASS: "+text);}
object Prefix(Type type,params object[] args)=>type.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
GroundItemManager manager;Actor actor;
void Reset()
{
 OfflinePickup.Clear();manager=GroundItemManager.instance=new();actor=PlayerFinder.Player=new();
 OfflineToolkit.Enabled=true;OfflineToolkit.Ready=false;OfflineToolkit.Settings=new();OfflineToolkit.Error="";Time.unscaledTime=0;MelonLoader.MelonLogger.Messages.Clear();
}
GroundItemManager.GroundItem Drop(int type,float x,uint id,bool tutorial=false)
{
 var data=new ItemData{itemType=type,Tutorial=tutorial};var location=new UVec(x,0,0);bool sound=true;
 var state=OfflinePickup.Prepare(manager,actor,data,ref location,ref sound);
 var item=new GroundItemManager.GroundItem{id=id,itemData=data,location=location};manager.offlineItems.items.Add(item);
 OfflinePickup.Dropped(manager,actor,data,state);return item;
}
T Interaction<T>(float x,uint id)where T:PickupGroundObjectInteraction,new()
{
 var entry=new T{player=actor,id=id,transform=new(){position=new UVec(x,0,0)}};
 if(entry is GoldPickupInteraction gold)manager.activeGoldPiles.Add(gold);
 if(entry is PotionPickupInteraction potion)manager.activePotions.Add(potion);
 if(entry is PickupAncientBonesInteraction bone)manager.activeAncientBones.Add(bone);
 OfflinePickup.Enqueue(entry);return entry;
}
void Tick(float time){Time.unscaledTime=time;OfflinePickup.Tick();}
Reset();Check(PickupRules.Delay==.5f && OfflinePickup.Local(actor),"fixed500ms delay and gameplay inventory readiness without model/network Ready");
var key=Drop(104,400,1);var material=Drop(102,400,2);var woven=Drop(107,400,3);
Check(key.location.x==actor.Position.x && manager.Operations==0,"old map-wide drop placement restored; no pickup during spawn");
Tick(.499f);Check(manager.Operations==0 && manager.offlineItems.items.Count==3,"no action before500ms");
Tick(.5f);Check(manager.Keys==1 && manager.Materials==1 && manager.WovenEchoes==1 && manager.offlineItems.items.Count==0,"key/material/Woven drops400units away collect after500ms through native All/vacuum/storage");
Check(AutomaticPickupScope.Depth==0 && AutomaticPickupScope.Allows(new ItemData()),"automatic filter scope restored");
Reset();Drop(104,100,1);Tick(.2f);Drop(104,100,2);
Tick(.5f);Check(manager.Keys==1 && manager.offlineItems.items.Count==1,"native vacuum cannot collect a newer item before its own500ms deadline");
Tick(.699f);Check(manager.Keys==1,"second item still waits until its own deadline");Tick(.7f);Check(manager.Keys==2,"second item executes at its deadline, no extra cadence delay");
Reset();Drop(104,100,1);actor.Position=new UVec(1000,0,0);Tick(.5f);
Check(manager.Keys==1,"moving far from the original pickup position does not impose radius or movement restrictions");
Reset();for(uint i=1;i<=25;i++)Drop(104,100,i);Tick(.5f);
Check(manager.Keys==25,"all ready drops execute without five-item or per-frame count caps");
Reset();var gold=Interaction<GoldPickupInteraction>(400,1);var potion=Interaction<PotionPickupInteraction>(400,1);var bone=Interaction<PickupAncientBonesInteraction>(400,1);
Check(!OfflinePickup.AllowNative(gold) && !OfflinePickup.AllowNative(potion) && !OfflinePickup.AllowNative(bone),"native automatic interactions cannot bypass pending delay");
Tick(.499f);Check(manager.Gold==0 && actor.Potion.currentCharges==0 && manager.Bones==0,"gold/potion/bones remain until deadline");
Tick(.5f);Check(manager.Gold==10 && actor.Potion.currentCharges==1 && manager.Bones==3,"native currency/potion pickup works map-wide at500ms");
Tick(1);Check(manager.Gold==10 && actor.Potion.currentCharges==1 && manager.Bones==3,"empty queue never repeats currency grants");
Reset();potion=Interaction<PotionPickupInteraction>(400,1);actor.Potion.currentCharges=actor.Potion.maxCharges;Tick(.5f);
Check(manager.activePotions.Contains(potion) && actor.Potion.currentCharges==actor.Potion.maxCharges,"full potion stays on ground, charges never exceed capacity");
Reset();gold=Interaction<GoldPickupInteraction>(400,1);OfflinePickup.Enqueue(gold);Tick(.5f);Check(manager.Gold==10,"duplicate initialization enqueues one pickup only");
Reset();gold=Interaction<GoldPickupInteraction>(400,1);manager.pickupGold(actor,gold.id,gold);Tick(.5f);
Check(manager.Gold==10,"native/manual collection before queued processing never duplicates gold");
Reset();key=Drop(104,100,1);manager.pickupItem(actor,key.id,StackableItemFlags.All);Tick(.5f);
Check(manager.Keys==1,"manually collected pending item is not granted twice");
Reset();key=Drop(104,100,1);key.id=2;Tick(.5f);Check(manager.Keys==0,"pooled ground-record ID reuse invalidates old queued request");
Reset();gold=Interaction<GoldPickupInteraction>(400,1);gold.id=2;Tick(.5f);Check(manager.Gold==0,"pooled interaction ID reuse cannot collect another drop");
Reset();Drop(104,100,1);OfflineToolkit.Settings.Keys=false;Tick(.5f);
Check(manager.Keys==0 && manager.offlineItems.items.Count==1,"toggle disabled during delay leaves item safely on ground");
Reset();OfflineToolkit.Settings.Materials=false;material=Drop(102,400,1);Drop(104,400,2);Drop(104,400,3,true);Drop(1,400,4);Tick(.5f);
Check(manager.Keys==1 && manager.Materials==0 && manager.Equipment==0 && material.location.x==400,"disabled types/equipment/tutorial loot retain native placement and are excluded from automatic vacuum");
Reset();Drop(104,100,1);actor.itemContainersManager=null;Tick(.5f);Check(manager.Keys==0,"queue waits for actual storage readiness");actor.itemContainersManager=new();Tick(.51f);Check(manager.Keys==1,"pending drop becomes usable when storage appears");
Reset();actor.itemContainersManager=null;Drop(104,100,1);Tick(.5f);actor.itemContainersManager=new();Tick(.51f);
Check(manager.Keys==1,"drops created before inventory readiness also remain queued until storage exists");
Reset();Drop(104,100,1);OfflineToolkit.Enabled=false;Tick(.5f);OfflineToolkit.Enabled=true;Tick(1);
Check(manager.Keys==0 && manager.offlineItems.items.Count==1,"online transition cancels queued actions without deleting ground loot");
Reset();Drop(104,100,1);PlayerFinder.Player=new Actor();Tick(.5f);Check(manager.Keys==0 && manager.offlineItems.items.Count==1,"character change cannot grant old character loot");
Reset();Drop(104,100,1);OfflinePickup.Clear();Tick(.5f);Check(manager.Keys==0 && manager.offlineItems.items.Count==1,"scene cleanup cancels delay while native ground record remains");
Reset();Drop(104,100,1);var old=manager;manager=GroundItemManager.instance=new();Tick(.5f);Check(old.Keys==0 && manager.Keys==0,"manager replacement invalidates queued context");
Reset();Drop(104,100,1);var previous=manager.offlineItems;manager.offlineItems=new();Tick(.5f);Check(manager.Keys==0 && previous.items.Count==1,"zone ground-list replacement invalidates queued items");
Reset();Drop(104,100,1);manager.RefuseItem=true;Tick(.5f);int calls=manager.Operations;manager.RefuseItem=false;Tick(1);
Check(manager.Keys==0 && manager.offlineItems.items.Count==1 && manager.Operations==calls,"native storage rejection leaves loot and does not add an automatic retry policy");
Reset();Drop(104,100,1);manager.ThrowOnItem=true;Tick(.5f);
Check(manager.Keys==0 && manager.offlineItems.items.Count==1 && AutomaticPickupScope.Depth==0,"native failure preserves floor item and restores classification scope");
Check(MelonLoader.MelonLogger.Messages.Count==1,"only native errors are logged, no pickup diagnostics");
Reset();var equipment=Drop(1,400,1);Check(!(bool)Prefix(typeof(OfflineRangePickupPatch),new GroundItemLabel{Data=equipment.itemData}) && manager.Equipment==1,"manual Range Pickup remains immediate and independent of auto delay");
Check(MelonLoader.MelonLogger.Messages.Count==0,"successful automatic/manual pickup emits no diagnostic logs");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev55-pickup-map-delay-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,delaySeconds=.5,mapWide=true,perFrameCountCap=false,nativeGameplayValidated=false,fpsMeasured=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} map-wide delayed pickup checks passed.");
