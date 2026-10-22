using System.Numerics;
using System.Reflection;
using System.Text.Json;
using Il2Cpp;
using LastEpochLAN.Mod.OfflineTools;
using UVec=UnityEngine.Vector3;
using Time=UnityEngine.Time;
var checks=new List<string>();
void Check(bool ok,string text){if(!ok)throw new Exception("FAIL: "+text);checks.Add(text);Console.WriteLine("PASS: "+text);}
object Prefix(Type t,params object[] args)=>t.GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
GroundItemManager manager;Actor actor;
void Reset()
{
 OfflinePickup.Clear();manager=GroundItemManager.instance=new();actor=PlayerFinder.Player=new();
 OfflineToolkit.Enabled=true;OfflineToolkit.Ready=false;OfflineToolkit.Settings=new();Time.unscaledTime=0;Time.frameCount++;
}
GroundItemManager.GroundItem ItemAt(int type,float x,uint id,bool tutorial=false)=>new(){id=id,itemData=new ItemData{itemType=type,Tutorial=tutorial},location=new UVec(x,0,0)};
GroundItemVisuals Visual(GroundItemManager.GroundItem item)=>new(){id=item.id,itemData=item.itemData,transform=new(){position=item.location}};
GoldPickupInteraction Attach(GroundItemManager.GroundItem item)
{
 var visual=Visual(item);OfflinePickup.Attach(visual);
 return manager.goldPileSubscriber.Active.Last();
}
Reset();
Check(OfflinePickup.Local(actor),"inventory readiness does not depend on visual/network Ready");
Check(PickupRules.Within(Vector3.Zero,new(5,30,0)) && !PickupRules.Within(Vector3.Zero,new(5.001f,0,0)),"horizontal radius5 boundary retained");
Check(OfflinePickup.Special(new ItemData{itemType=107}),"Woven Echo eligible");
Check(!typeof(OfflinePickup).GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Any(m=>m.Name is "Tick" or "Moving"),"no frame sweep, cursor polling or movement gate");
var primary=ItemAt(104,1,900);manager.offlineItems.items.AddRange(Enumerable.Range(0,512).Select(i=>ItemAt(1,40,(uint)i)));manager.offlineItems.items.Add(primary);
var trigger=Attach(primary);
Check(trigger.AwakeCompleted && !manager.goldPileSubscriber.LastInstantiateInactive,"active unparented native trigger runs Awake before initialise");
Check(trigger.player==actor && trigger.id==primary.id && trigger.gameObject.layer==actor.gameObject.layer,"native owner/id/collision layer initialized");
Check(trigger.transform.Parent==null && trigger.gameObject.activeSelf,"loot-filtered or inactive item visuals cannot disable gameplay trigger");
Check(manager.Operations==0 && manager.activeGoldPiles.Count==0,"attachment grants nothing and never enters gold accounting list");
Check(!OfflinePickup.PickUp(trigger) && manager.Keys==1 && manager.Operations==1,"native proximity callback picks nearby key immediately despite512 earlier far equipment entries");
Check(manager.Equipment==0,"equipment remains on ground");
Check(!(bool)Prefix(typeof(OfflineNativeAutoPickupGatePatch),new GoldPickupInteraction{player=actor,transform=new(){position=new(5.01f,0,0)}}),"final native pickup gate blocks trigger overlaps beyond5");
Check((bool)Prefix(typeof(OfflineNativeAutoPickupGatePatch),new GoldPickupInteraction{player=actor,transform=new(){position=new(5,0,0)}}),"final native pickup gate permits exact boundary");

Reset();primary=ItemAt(107,1,1);manager.offlineItems.items.Add(primary);
manager.offlineItems.items.AddRange(Enumerable.Range(2,20).Select(i=>ItemAt(102,2,(uint)i)));
manager.offlineItems.items.Add(ItemAt(104,5.001f,30));manager.offlineItems.items.Add(ItemAt(1,1,31));
trigger=Attach(primary);OfflinePickup.PickUp(trigger);
Check(manager.WovenEchoes==1 && manager.Materials==20 && manager.VacuumCalls==1 && manager.Operations==1,"one native vacuum stores primary and20 surrounding materials without five-entry batch cap");
Check(manager.Keys==0 && manager.Equipment==0,"radius boundary and equipment exclusions retained");
primary=ItemAt(104,1,32);manager.offlineItems.items.Add(primary);var second=Attach(primary);OfflinePickup.PickUp(second);
Check(manager.Operations==1,"simultaneous proximity callbacks cannot send a second item action in the same frame");
Time.frameCount++;OfflinePickup.PickUp(second);Check(manager.Keys==1,"next native frame can collect a new nearby item without200ms cooldown");
Reset();primary=ItemAt(107,1,1);manager.offlineItems.items.Add(primary);actor.itemContainersManager=null;trigger=Attach(primary);
OfflinePickup.PickUp(trigger);Check(manager.WovenEchoes==0,"early native trigger waits for actual inventory storage");
actor.itemContainersManager=new();OfflineToolkit.Settings.Gold=false;
Check((bool)Prefix(typeof(OfflineNativeAutoPickupGatePatch),trigger) && !OfflinePickup.PickUp(trigger) && manager.WovenEchoes==1,"Woven trigger becomes usable when storage appears, independently of Gold toggle and visual Ready");
Reset();primary=ItemAt(104,5.001f,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);OfflinePickup.PickUp(trigger);
Check(manager.Operations==0,"physical collider overlap never admits an item outside radius5");
actor.Position=new UVec(.001f,0,0);Time.frameCount++;OfflinePickup.PickUp(trigger);Check(manager.Keys==1,"moving into exact radius5 allows pickup");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);OfflineToolkit.Settings.Keys=false;OfflinePickup.PickUp(trigger);
Check(manager.Keys==0,"disabled key cannot become gold or be picked up");
OfflineToolkit.Settings.Keys=true;OfflinePickup.PickUp(trigger);Check(manager.Keys==1,"existing native key trigger responds to toggle reenable without polling");
Reset();primary=ItemAt(107,1,1);manager.offlineItems.items.AddRange(new[]{primary,ItemAt(102,2,2),ItemAt(104,2,3),ItemAt(104,2,4,true)});trigger=Attach(primary);
OfflineToolkit.Settings.Materials=false;OfflinePickup.PickUp(trigger);
Check(manager.WovenEchoes==1 && manager.Keys==1 && manager.Materials==0 && manager.offlineItems.items.Any(x=>x.id==4),"batch respects Woven/material/key toggles and tutorial exclusion");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);OfflineToolkit.Enabled=false;OfflinePickup.PickUp(trigger);
Check(manager.Keys==0,"online mode cannot dispatch a local bridge");OfflineToolkit.Enabled=true;
var remote=new GoldPickupInteraction{player=new Actor()};Check(OfflinePickup.PickUp(remote),"unowned/nonbridge gold retains native dispatch");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);var visual=Visual(primary);OfflinePickup.Attach(visual);trigger=manager.goldPileSubscriber.Active.Single();
manager.PoolVisual=id=>OfflinePickup.Detach(visual);OfflinePickup.PickUp(trigger);
Check(manager.Keys==1 && manager.goldPileSubscriber.Active.Count==0 && manager.goldPileSubscriber.Released==1,"native visual pooling releases bridge during pickup exactly once");
OfflinePickup.Detach(visual);Check(manager.goldPileSubscriber.Released==1,"repeat detach idempotent");
OfflinePickup.Attach(visual);OfflinePickup.Attach(visual);Check(manager.goldPileSubscriber.Active.Count==1,"pooled visual reuse replaces stale trigger");
OfflinePickup.Clear();Check(manager.goldPileSubscriber.Active.Count==0,"scene cleanup returns every borrowed trigger to pool");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);manager.ThrowOnItem=true;
try{OfflinePickup.PickUp(trigger);throw new Exception("expected native error");}catch(InvalidOperationException){}
Check(AutomaticPickupScope.Depth==0 && AutomaticPickupScope.Allows(new ItemData()),"native pickup exception restores classifier scope");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);manager.RefuseItem=true;
OfflinePickup.PickUp(trigger);Time.frameCount++;OfflinePickup.PickUp(trigger);
Check(manager.Operations==1,"native rejection cannot rescan/retry every frame when storage is full");
Time.unscaledTime=.251f;Time.frameCount++;manager.RefuseItem=false;OfflinePickup.PickUp(trigger);Check(manager.Keys==1,"failed storage retries after short cooldown without movement requirement");
Reset();primary=ItemAt(104,1,1);manager.offlineItems.items.Add(primary);trigger=Attach(primary);manager.ThrowOnItem=true;
Check(!(bool)Prefix(typeof(OfflineNativeItemPickupPatch),trigger) && AutomaticPickupScope.Depth==0,"native action error is contained by bridge hook without accidental gold dispatch");
Check(MelonLoader.MelonLogger.Messages.Count==1,"native action failure emits one error instead of pickup diagnostic spam");MelonLoader.MelonLogger.Messages.Clear();
Reset();primary=ItemAt(104,1,1);var far=ItemAt(104,8,2);far.itemData=primary.itemData;manager.offlineItems.items.AddRange(new[]{primary,far});trigger=Attach(primary);OfflinePickup.PickUp(trigger);
Check(manager.Keys==0,"shared ItemData cannot admit an unapproved far ground id");
Reset();var gold=new GoldPickupInteraction{player=actor};manager.activeGoldPiles.Add(gold);OfflinePickup.Configure(gold);
Check(Math.Abs(((UnityEngine.SphereCollider)gold.Colliders[0]).radius-5.6f)<.001f && OfflinePickup.PickUp(gold),"ordinary gold keeps original native callback/radius and dispatch");
manager.potionSubscriber.Pool.prefabT.Colliders[0]=new UnityEngine.SphereCollider{radius=1};var potion=new PotionPickupInteraction{player=actor};manager.activePotions.Add(potion);OfflinePickup.Configure(potion);
Check(((UnityEngine.SphereCollider)potion.Colliders[0]).radius==5,"potion trigger reaches radius5 without a polling loop");
Check(((UnityEngine.SphereCollider)manager.potionSubscriber.Pool.prefabT.Colliders[0]).radius==1,"prefab remains unchanged");
OfflineToolkit.Settings.Potions=false;OfflinePickup.Refresh();Check(((UnityEngine.SphereCollider)potion.Colliders[0]).radius==1,"disabled enhancement restores native potion radius");
var remotePotion=new PotionPickupInteraction{player=new Actor()};OfflinePickup.Configure(remotePotion);Check(((UnityEngine.SphereCollider)remotePotion.Colliders[0]).radius==1,"remote actor gets original prefab radius");
Reset();OfflinePickup.Attach(Visual(ItemAt(1,1,1)));OfflinePickup.Attach(Visual(ItemAt(104,1,2,true)));Check(manager.goldPileSubscriber.Active.Count==0,"equipment and tutorial drops never receive native bridges");
OfflineToolkit.Enabled=false;OfflinePickup.Attach(Visual(ItemAt(104,1,3)));Check(manager.goldPileSubscriber.Active.Count==0,"online drops never receive bridges");OfflineToolkit.Enabled=true;
var equipment=ItemAt(1,20,4);manager.offlineItems.items.Add(equipment);Check(!(bool)Prefix(typeof(OfflineRangePickupPatch),new GroundItemLabel{Data=equipment.itemData}) && manager.Equipment==1,"manual Range Pickup unaffected");
Check(AutomaticPickupScope.Allows(new ItemData()) && MelonLoader.MelonLogger.Messages.Count==0,"manual classification unaffected and pickup remains silent");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev55-pickup-trigger-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,radius=5,polling=false,nativePhysicsGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} native-trigger pickup checks passed.");
