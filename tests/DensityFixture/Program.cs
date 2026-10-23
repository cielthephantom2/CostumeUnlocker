using Il2Cpp;
using Il2CppLE.Gameplay.Monolith;
using LastEpochLAN.Mod;
using LastEpochLAN.Mod.OfflineTools;
using System.Text.Json;
var checks=new List<string>();
void Check(bool good,string label){if(!good)throw new Exception(label);checks.Add(label);}
void NativeOutside(string label){var room=new SpawnerPlacementRoom();var state=default(OfflineDensity.RollState);Check(OfflineDensity.BeforeRoll(room,ref state) && !state.Changed && room.Writes==0,label+": native roll runs with no field writes");OfflineDensity.Restore(room,state,null);Check(room.Writes==0 && room.spawnerDensityModifier==.25f && room.intendedSpawnerDensity==10,label+": finalizer preserves native fields");}
MonolithGameplayManager.ActiveRun=new(){web=new(){corruption=2000}};
MonolithZoneManager.instance=null;NativeOutside("Campaign/Dungeon with remembered high-corruption run");
HudAuthority.Session=new(){Connected=true,IsHost=false};NativeOutside("Outside Monolith on guest");HudAuthority.Session=null;
MonolithZoneManager.instance=new();MonolithZoneManager.instance.gameObject.scene.isLoaded=false;NativeOutside("Unloaded stale Monolith manager");
MonolithZoneManager.instance.gameObject.scene.isLoaded=true;
foreach(int corruption in new[]{0,100,250,500,1000}){
 MonolithGameplayManager.ActiveRun.web.corruption=corruption;var room=new SpawnerPlacementRoom();var state=default(OfflineDensity.RollState);float scale=Math.Max(1,corruption/100f);
 Check(OfflineDensity.BeforeRoll(room,ref state) && state.Changed && Math.Abs(room.spawnerDensityModifier-((1+.25f)*scale-1))<.0001f,"Monolith retains corruption scaling "+corruption);
 room.intendedSpawnerDensity=10*scale;OfflineDensity.Restore(room,state,null);
 Check(room.spawnerDensityModifier==.25f && room.intendedSpawnerDensity==10,"Monolith native placement fields restored "+corruption);
}
MonolithGameplayManager.ActiveRun.web.corruption=500;
var failed=new SpawnerPlacementRoom();var failure=default(OfflineDensity.RollState);OfflineDensity.BeforeRoll(failed,ref failure);failed.intendedSpawnerDensity=999;OfflineDensity.Restore(failed,failure,new Exception("native"));Check(failed.spawnerDensityModifier==.25f && failed.intendedSpawnerDensity==10,"Native failure restores pre-roll values");
var repeated=new SpawnerPlacementRoom();for(int i=0;i<100;i++){var state=default(OfflineDensity.RollState);OfflineDensity.BeforeRoll(repeated,ref state);repeated.intendedSpawnerDensity=10*state.Multiplier;OfflineDensity.Restore(repeated,state,null);}Check(repeated.spawnerDensityModifier==.25f && repeated.intendedSpawnerDensity==10,"100 Monolith rerolls never compound multiplier");
MonolithGameplayManager.ActiveRun=null;var missing=new SpawnerPlacementRoom();var noRun=default(OfflineDensity.RollState);Check(OfflineDensity.BeforeRoll(missing,ref noRun) && !noRun.Changed && missing.Writes==0,"Missing Monolith run preserves native placement");
OfflineToolkit.Enabled=false;var online=new SpawnerPlacementRoom();var noOnline=default(OfflineDensity.RollState);Check(OfflineDensity.BeforeRoll(online,ref noOnline) && online.Writes==0,"Online path unchanged");OfflineToolkit.Enabled=true;
MonolithGameplayManager.ActiveRun=new();HudAuthority.Session=new(){Connected=true,IsHost=false};var guest=default(OfflineDensity.RollState);Check(!OfflineDensity.BeforeRoll(new(),ref guest) && !guest.Changed,"Existing Monolith guest generation policy retained");HudAuthority.Session=null;
Directory.CreateDirectory("validation");File.WriteAllText("validation/dev58-density-fixture.json",JsonSerializer.Serialize(new{passed=checks.Count,checks,actualSource=true,nativeGameplayValidated=false},new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("PASS "+checks.Count+" actual-source density checks");
