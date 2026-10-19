using UnityEngine;

namespace LastEpochLAN.Mod;

// Private QA only: call native reward methods with no encounter/network hooks.
internal sealed class NativeEncounterIsolationSmoke:IDisposable
{
 private readonly Il2Cpp.Actor actor=Il2Cpp.PlayerFinder.getPlayerActor();
 private readonly List<object> observations=new();
 private readonly HashSet<uint> before=new();
 private int stage;
 private float waitUntil;
 internal bool Complete{get;private set;}
 internal object[] Observations=>observations.ToArray();
 internal NativeEncounterIsolationSmoke()
 {
  if(actor==null || !Environment.GetCommandLineArgs().Contains("--lelan-private-campaign-smoke"))
   throw new InvalidOperationException("Private native encounter isolation QA only.");
 }
 internal void Tick()
 {
  if(Complete || Time.unscaledTime<waitUntil)return;
  var items=Il2Cpp.GroundItemManager.instance.offlineItems.items;
  if(stage>0)
  {
   int created=0,corrupted=0;
   foreach(var item in items)if(!before.Contains(item.id))
   {created++;if(item.itemData?.IsCorrupted()==true)corrupted++;}
   observations.Add(new{path=stage==1 ? "native-item-rewards" : "native-full-rewards",created,corrupted});
   if(created<1 || corrupted<1)throw new InvalidOperationException("Native Omen did not place Corrupted loot on the ground in isolation: "+System.Text.Json.JsonSerializer.Serialize(Observations));
   if(stage==2){Complete=true;return;}
  }
  before.Clear();foreach(var item in items)before.Add(item.id);
  var root=new GameObject("Private QA untouched native Omen reward");root.SetActive(false);
  UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,actor.gameObject.scene);
  var position=actor.transform.position+new Vector3(4,0,4);root.transform.position=position;
  try
  {
   var data=Il2Cpp.ObserverWindowData.Instance;
   var window=root.AddComponent<Il2Cpp.ObserverWindowLogic>();window.encounterData=data;window.windowType=Il2Cpp.ObserverWindowData.WindowType.Regular;
   window.windowTier=1;window.windowSettings=data.GetWindowTypeSettings(window.windowType);window.radius=15;window.baseSize=15;window.maxRingSize=15;
   window.championLootStorage=new();
   if(stage==0)window.DropItemRewards(ref position,ref position);else window.DropAllRewards();
  }
  finally{UnityEngine.Object.Destroy(root);}
  stage++;waitUntil=Time.unscaledTime+6;
 }
 public void Dispose(){}
}
