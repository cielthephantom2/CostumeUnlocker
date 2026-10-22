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
 OfflineToolkit.Enabled=true;OfflineToolkit.Ready=false;OfflineToolkit.Settings=new();Time.unscaledTime=0;PickupProbe.Reads=0;
}
int Pass(float elapsed=.21f)
{
 Time.unscaledTime+=elapsed;PickupProbe.Reads=0;int before=manager.Operations;OfflinePickup.Tick();
 Check(PickupProbe.Reads<=5,"one shared candidate budget never exceeds5 across all types");
 Check(manager.Operations-before<=1,"one pass never sends more than1 native pickup action");
 return manager.Operations-before;
}
GroundItemManager.GroundItem ItemAt(int type,float x,uint id,bool tutorial=false)=>new(){id=id,itemData=new ItemData{itemType=type,Tutorial=tutorial},location=new UVec(x,0,0)};
T Ground<T>(float x,uint id)where T:PickupGroundObjectInteraction,new()=>new(){player=actor,id=id,transform=new(){position=new UVec(x,0,0)}};
Reset();
Check(PickupRules.SweepInterval==.2f && PickupRules.MaximumVisits==5 && PickupRules.MaximumOperations==1,"5Hz/5 candidates/1 action policy");
Check(PickupRules.Within(Vector3.Zero,new(5,30,0)) && !PickupRules.Within(Vector3.Zero,new(5.001f,0,0)),"horizontal radius5 boundary unchanged");
Check(OfflinePickup.Local(actor),"gameplay inventory works without model/network Ready");
Check(OfflinePickup.Special(new ItemData{itemType=107}),"Woven Echo type107 eligible");
manager.offlineItems.items.AddRange(new[]{ItemAt(104,1,1),ItemAt(102,2,2),ItemAt(107,3,3),ItemAt(104,5.1f,4),ItemAt(1,2,5)});
Check(Pass()==1 && manager.Keys==1 && manager.Materials==1 && manager.WovenEchoes==1 && manager.VacuumCalls==1,"one native All/vacuum action collects approved surrounding key/material/Woven batch");
Check(manager.offlineItems.items.Any(x=>x.id==4) && manager.Equipment==0,"outside5 and equipment remain on ground");
manager.offlineItems.items.Add(ItemAt(104,1,6));
Check(Pass(.05f)==0 && PickupProbe.Reads==0,"no candidate reads50ms after previous pass");
Check(Pass(.149f)==0 && PickupProbe.Reads==0,"no candidate reads before200ms deadline");
Check(Pass(.002f)==1 && manager.Keys==2,"stationary pickup resumes at200ms deadline");
Check(MelonLoader.MelonLogger.Messages.Count==0,"pickup emits no diagnostic logs");
Reset();manager.activeGoldPiles.Add(Ground<GoldPickupInteraction>(1,1));manager.activePotions.Add(Ground<PotionPickupInteraction>(1,1));manager.activeAncientBones.Add(Ground<PickupAncientBonesInteraction>(1,1));manager.offlineItems.items.Add(ItemAt(107,1,1));
Pass();Check(manager.Gold==10 && manager.Bones==0 && manager.WovenEchoes==0,"first category performs only gold operation");
Pass();Check(actor.Potion.currentCharges==1,"next pass rotates to potion");
Pass();Check(manager.Bones==3,"next pass rotates to bones");
Pass();Check(manager.WovenEchoes==1,"next pass rotates to special items");
Reset();for(int i=0;i<20;i++)manager.activeGoldPiles.Add(Ground<GoldPickupInteraction>(50,(uint)i));manager.offlineItems.items.Add(ItemAt(107,1,1));
Check(Pass()==0 && PickupProbe.Reads==5,"far-gold search stops at5 entries");
Check(Pass()==1 && manager.WovenEchoes==1,"far gold cannot starve Woven pickup next pass");
Reset();OfflineToolkit.Settings.Materials=OfflineToolkit.Settings.WovenEchoes=false;
manager.offlineItems.items.AddRange(new[]{ItemAt(104,1,1),ItemAt(102,1,2),ItemAt(107,1,3),ItemAt(104,1,4,true)});
Pass();Check(manager.Keys==1 && manager.Materials==0 && manager.WovenEchoes==0 && manager.offlineItems.items.Any(x=>x.id==4),"batch respects per-type toggles and tutorial exclusions");
OfflineToolkit.Settings.WovenEchoes=true;Pass();Check(manager.WovenEchoes==1 && manager.Materials==0,"Woven can be enabled separately from materials");
Reset();manager.offlineItems.items.AddRange(Enumerable.Range(1,12).Select(i=>ItemAt(104,1,(uint)i)));
Pass();Check(manager.Keys==5 && manager.offlineItems.items.Count==7,"native batch includes only the5 approved scanner candidates");
Reset();var first=ItemAt(104,1,1);var far=ItemAt(104,8,2);far.itemData=first.itemData;manager.offlineItems.items.AddRange(new[]{first,far});Pass();Check(manager.Keys==0,"shared ItemData cannot admit an unapproved far ground id");
Reset();manager.offlineItems.items.Add(ItemAt(104,1,1));manager.ThrowOnItem=true;
try{Pass();}catch(InvalidOperationException){}
Check(AutomaticPickupScope.Depth==0 && AutomaticPickupScope.Allows(new ItemData()),"native exception restores classification scope");manager.ThrowOnItem=false;
using(var outer=new AutomaticPickupScope()){using(var inner=new AutomaticPickupScope())Check(AutomaticPickupScope.Depth==2,"nested scope retained");Check(AutomaticPickupScope.Depth==1,"nested scope restored");}
Reset();OfflineToolkit.Enabled=false;manager.offlineItems.items.Add(ItemAt(107,1,1));Check(Pass()==0 && manager.WovenEchoes==0,"online mode remains excluded");
Reset();actor.Potion.currentCharges=actor.Potion.maxCharges;manager.activePotions.Add(Ground<PotionPickupInteraction>(1,1));Pass();Check(manager.activePotions.Count==1,"full potion remains on ground");
var remote=Ground<GoldPickupInteraction>(1,2);remote.player=new Actor();manager.activeGoldPiles.Add(remote);Pass();Check(manager.activeGoldPiles.Contains(remote),"remote actor gold remains private");
Reset();var equipment=ItemAt(1,20,1);manager.offlineItems.items.Add(equipment);
Check(!(bool)Prefix(typeof(OfflineRangePickupPatch),new GroundItemLabel{Data=equipment.itemData}) && manager.Equipment==1,"manual Range Pickup unaffected");
Check(AutomaticPickupScope.Allows(new ItemData()),"manual native classification unaffected outside auto scope");
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev55-pickup-lightweight-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,scanHz=5,maxCandidates=5,maxActions=1,radius=5,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"All {checks.Count} pickup checks passed.");
